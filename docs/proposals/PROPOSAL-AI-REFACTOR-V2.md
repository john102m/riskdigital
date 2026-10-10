# Proposal — AiService Refactoring (V2)

## Problem

`AiService.cs` is 997 lines with significant repetition. The same attack/fortify/broadcast sequences are copy-pasted across 5 tiers with minor variations in scoring logic. This makes it:
- Hard to find the tier you want (scroll past 4 others)
- Easy to fix a bug in one tier and miss the same bug in another
- Difficult to add a new tier or change the attack flow

Additionally, Tiers 1–4 are never used in real gameplay — only Tier 5 (ML + personality) sees action. Four unused tiers are dead weight to maintain.

## What Changes

1. **Structural refactoring** — extract strategy pattern, shared actions, eliminate duplication.
2. **Tier consolidation** — collapse Tiers 1–4 into a single "Easy" bot. Keep Tier 5 (ML + personality) as the competitive bot.
3. **Lobby simplification** — replace 5 tier buttons with 2 clear options: Easy and ML (with personality picker).

No SignalR contract changes. The `AddAI` hub method signature stays the same (tier int + optional personality). The server just maps tier 1 → Easy strategy internally.

## The "Easy" Bot — What It Takes From Each Old Tier

The Easy bot is a single coherent strategy aimed at new players learning the game. It plays competently enough to be a challenge but predictably enough to learn from. Here's what it cherry-picks:

| Behaviour | Source | Why |
|-----------|--------|-----|
| **Attack selection:** strongest source → weakest adjacent target | Tier 2 | Simple, understandable aggression. New players can see *why* it attacked there. |
| **Blitz when ≥ 5 armies** on source | Tier 2 | Clear rule. Doesn't require ML to decide. |
| **Reinforce front-line territories**, prioritising those with most enemy neighbours | Tier 2 | Teaches new players that borders matter. |
| **Fortify inland → front-line** | Tier 2 | Demonstrates good army positioning without complex continent logic. |
| **Stop attacking after 4–6 attacks** (randomised) | Tier 2/3 | Doesn't steamroll but doesn't quit after one fight. |
| **Trade cards when holding 4+** (forced at 5) | Tier 3 | Slightly smarter than "trade immediately" — shows card value to new players. |
| **No ML, no personality weights, no mission pursuit** | — | Keeps it readable, debuggable, and fast. |
| **TimingMultiplier: 1.0** (standard human-like pace) | Tier 2 | Not too fast, not too slow. |

What it does **NOT** do (reserved for ML bot):
- ML-predicted blitz probability scoring
- Continent completion awareness
- Continent denial (blocking opponents)
- Elimination hunting / targeting weakest player
- Mission-directed play
- Personality-weighted thresholds
- Human behaviour blending (Tier 5)

**In short:** Easy plays like a competent beginner — attacks when strong, defends borders, doesn't do anything clever. The ML bot plays like an experienced human with a game plan.

## Proposed Structure

```
Services/
  AiService.cs              — Entry point: TriggerIfAi, RunTurnAsync, error recovery (~100 lines)
  IAiStrategy.cs            — Interface definition (~15 lines)
  AiActions.cs              — Shared execution: DoBlitz, DoSingleAttack, DoFortify,
                              DoEndTurn, ShowSelection, GetValidSources (~130 lines)
  AiEasyStrategy.cs         — Easy bot: simple heuristics, no ML (~80 lines)
  AiMlStrategy.cs           — ML bot: personality-weighted scoring, mission pursuit,
                              elimination hunting, human-behaviour blend (~200 lines)
```

**Total: ~525 lines across 5 files** (down from 997 in one file).

## Interface

```csharp
interface IAiStrategy
{
    (Territory Source, Territory Target)? ChooseAttack(GameState state, int myIndex);
    (Territory Source, Territory Target, int Armies)? ChooseFortify(GameState state, int myIndex);
    Territory ChooseReinforceTarget(GameState state, List<Territory> owned, int myIndex);
    bool ShouldBlitz(Territory source, Territory target);
    bool ShouldStopAttacking(GameState state, Player player, int attackCount);
    int CalculateMoveTroops(Territory source, Territory target, bool capturedViaBlitz);
    bool ShouldTradeCards(Player player, GameState state);
    float TimingMultiplier { get; }
}
```

### Why `CalculateMoveTroops` is on the interface

Easy moves all troops in (source - 1) after capture. ML uses a personality-weighted fraction (expansion speed). This decision belongs to the strategy, not the shared action layer.

### Why `ShouldTradeCards` is on the interface

Easy trades at 4+ cards. ML uses personality `CardHoarding` weight. Keeps card logic out of AiService.

## AiActions (stateless shared execution)

`AiActions` receives `timingMultiplier` as a parameter on each call rather than holding a reference to the strategy. Stateless and simple.

```csharp
class AiActions(IHubContext<GameHub> hub, GameService game, string gameCode)
{
    async Task ShowSelection(int sourceId, int? targetId, float timingMultiplier) { ... }
    async Task DoBlitz(string connId, Territory source, Territory target, int moveTroops) { ... }
    async Task DoSingleAttack(string connId, Territory source, Territory target, int moveTroops) { ... }
    async Task DoFortify(string connId, int sourceId, int targetId, int armies) { ... }
    async Task DoEndTurn(string connId) { ... }
    async Task Broadcast() { ... }
    List<Territory> GetValidSources(GameState state, int myIndex) { ... }
}
```

## AiService.RunTurnAsync (simplified)

```csharp
var strategy = GetStrategy(player.AiTier, player.Personality);
var actions = new AiActions(hub, game, gameCode);

await RunReinforce(strategy, actions, state, player, connId);
if (state.Phase == GamePhase.GameOver) return;

await RunAttack(strategy, actions, state, player, connId);
if (state.Phase == GamePhase.GameOver) return;

await RunFortify(strategy, actions, state, player, connId);
```

Strategy routing:
```csharp
private IAiStrategy GetStrategy(int tier, AiPersonality? personality)
{
    if (tier >= 5) return new AiMlStrategy(ml, game, personality ?? AiPersonality.Opportunist);
    return new AiEasyStrategy();
}
```

Any tier value < 5 maps to Easy. This means existing saved games or test calls with tier 1–4 still work — they just all get the same Easy behaviour now.

## RunAttack loop (shared, both strategies)

```csharp
private async Task RunAttack(IAiStrategy strategy, AiActions actions, GameState state, Player player, string connId)
{
    int attackCount = 0;
    while (true)
    {
        if (strategy.ShouldStopAttacking(state, player, attackCount)) break;

        var choice = strategy.ChooseAttack(state, state.CurrentPlayerIndex);
        if (choice is null) break;

        var (source, target) = choice.Value;
        await actions.ShowSelection(source.Id, null, strategy.TimingMultiplier);
        await actions.ShowSelection(source.Id, target.Id, strategy.TimingMultiplier);

        if (strategy.ShouldBlitz(source, target))
        {
            int moveTroops = strategy.CalculateMoveTroops(source, target, true);
            await actions.DoBlitz(connId, source, target, moveTroops);
        }
        else
        {
            int moveTroops = strategy.CalculateMoveTroops(source, target, false);
            await actions.DoSingleAttack(connId, source, target, moveTroops);
        }

        if (state.Phase == GamePhase.GameOver) return;
        attackCount++;
    }

    await actions.ClearSelection();
    await actions.DoEndAttack(connId);
}
```

## Handset Lobby Changes

### Current (5 buttons + personality expand)
```
[🤖 T1] [⚔️ T2] [🧠 T3] [🦊 T4] [🧬 T5]
         ↓ expands personality picker
```

### After (2 buttons + personality expand)
```
[🤖 Easy] [🧠 ML]
            ↓ tap expands personality picker:
[🦊 Opportunist] [🛡️ Cautious] [🔥 Aggressive] [🗺️ Continental] [🎲 Mystery]
```

- **Easy** calls `AddAI(1)` — server maps to Easy strategy
- **ML + personality** calls `AddAI(5, "Opportunist")` etc.
- **Mystery** calls `AddAI(5)` (null personality → server picks random)

Player list display:
- Easy bot: `🤖` badge with no tier number
- ML bot: personality emoji (`🦊`, `🛡️`, `🔥`, `🗺️`, or `🎲` for mystery)

### TypeScript change

`LobbyScreen.tsx` — replace the 5-button row + T5 expand with 2-button row + ML expand. Same `addAI(tier, personality?)` function, just different arguments.

## Design Decisions

### No separate AiScoring.cs
Scoring methods (`ScoreContinentDenial`, `IsChokepoint`, `FindWeakestPlayer`, etc.) are only consumed by `AiMlStrategy`. They live as private methods in that file. If a second consumer appears, extract then.

### TimingMultiplier passed as parameter
`AiActions` stays stateless — no reference to the active strategy. Each action method receives the timing multiplier it needs.

### Placement stays in AiService.cs
`RunPlacement` doesn't vary enough to justify routing through the strategy interface. Easy and ML both place on front-line territories (the current Tier 2+ logic). A simple inline `if` is fine.

### Error recovery stays in AiService.cs
The `try/catch` in `RunTurnAsync` that force-advances phases on failure is infrastructure, not strategy.

### Backward compatibility
`AddAI(tier, personality)` hub signature unchanged. Tiers 1–4 all route to Easy. Tier 5 routes to ML. No client-side breakage for web TV board or Unity.

## Strategy Comparison

| Aspect | Easy | ML |
|--------|------|-----|
| **Attack selection** | Strongest source → weakest adjacent | Personality-weighted scoring across all pairs |
| **Attack threshold** | Always attacks if source > 2 armies | Dynamic: `0.3 + (preservation * 0.3)` |
| **Stop attacking** | After 4–6 random attacks | After earning card (if preservation high), or max `8 * expansionSpeed` |
| **Blitz decision** | Source ≥ 5 armies | ML captureChance > 0.6 and ≥ 4 armies |
| **Reinforce** | Front-line, most enemy neighbours | Weighted: elimination, continent, denial, chokepoint, mission |
| **Fortify** | Inland → front-line (biggest stack to weakest border) | Elimination-directed or continent-border protection |
| **Card trading** | Trade at 4+ cards | Personality `CardHoarding` weight |
| **Move after capture** | All (source - 1) | Personality `ExpansionSpeed` fraction |
| **Mission awareness** | None | Pursues own mission, targets elimination missions |
| **ML models** | None | `PredictBlitz`, `PredictHumanAttack` (Tier 5 blend) |
| **Timing** | 1.0× (standard) | Personality `TimingMultiplier` |

## Migration Verification

1. Add an Easy bot and an ML bot — confirm both complete turns correctly
2. Confirm card trading fires at correct thresholds for each
3. Confirm mission win detection still works for ML bot
4. Confirm error recovery still advances stuck turns
5. Confirm lobby UI shows correct buttons and player list badges
6. Run `npx tsc --noEmit` on handset to verify TypeScript compiles

## Scope Summary

| Area | Change |
|------|--------|
| `server/Risk.Server/Services/AiService.cs` | Gut and rebuild — entry point + shared loops only |
| `server/Risk.Server/Services/IAiStrategy.cs` | New file — interface |
| `server/Risk.Server/Services/AiActions.cs` | New file — shared execution |
| `server/Risk.Server/Services/AiEasyStrategy.cs` | New file — Easy bot |
| `server/Risk.Server/Services/AiMlStrategy.cs` | New file — ML bot (absorbs all Tier 4/5 logic) |
| `handset/src/components/LobbyScreen.tsx` | Simplify AI buttons: 5 → 2 with personality picker |
| `server/Risk.Server/Hubs/GameHub.cs` | No change (AddAI signature unchanged) |
| `server/Risk.Server/Services/GameService.cs` | No change (AddAiPlayer unchanged) |
| SignalR contract | No change |

---

*Updated: 2026-07-12 (V2 — consolidates Tiers 1–4 into Easy, simplifies lobby)*
