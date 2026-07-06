# Proposal — AiService Refactoring

## Problem

`AiService.cs` is 997 lines with significant repetition. The same attack/fortify/broadcast sequences are copy-pasted across 5 tiers with minor variations in scoring logic. This makes it:
- Hard to find the tier you want (scroll past 4 others)
- Easy to fix a bug in one tier and miss the same bug in another
- Difficult to add a new tier or change the attack flow

## Repeated Patterns

### Attack Execution (copied 6 times)
```csharp
// This 20-line block appears in: DoRandomAttack, RunAggressiveAttack, 
// RunStrategicAttack, RunTier4Attack (blitz path + single path each)
await Group.SendAsync("AttackSelection", source.Id, (int?)null);
await Delay(1000, 1500);
await Group.SendAsync("AttackSelection", source.Id, target.Id);
await Delay(1500, 2000);

// Then either blitz or single:
var (_, blitzResult) = game.Blitz(connId, source.Id, target.Id);
await Group.SendAsync("BlitzResult", blitzResult);
await Broadcast();
if (blitzResult.Captured) {
    await Delay(1500, 2000);
    int max = source.Armies - 1;
    if (max > 0) {
        var (_, _, _, missionWon, _) = game.MoveAfterCapture(...);
        if (missionWon) await Group.SendAsync("MissionComplete", ...);
        await Broadcast();
    }
    if (state.Phase == GamePhase.GameOver) return;
}
await Delay(2500, 3500);
```

~120 lines repeated across blitz/single × 4 tiers = **~480 lines** that could be **~50 lines** (one shared method + calls).

### Fortify Execution (copied 4 times)
```csharp
game.Fortify(connId, source.Id, target.Id, armies);
await Group.SendAsync("FortifyMoved", ...);
await Broadcast();
// ... then EndTurn + TurnStarted + TriggerIfAi
```

~15 lines × 4 = **~60 lines** → **~15 lines** (one shared method).

### Source Filtering (copied 5 times)
```csharp
var sources = state.Territories
    .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
        && t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
    .ToList();
if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
    sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();
```

~6 lines × 5 = **~30 lines** → **~6 lines** (one helper).

## Proposed Structure

```
Services/
  AiService.cs              — Entry point: TriggerIfAi, RunTurnAsync, error recovery
  AiActions.cs              — Shared execution: DoBlitz, DoSingleAttack, DoFortify, 
                              DoEndTurn, ShowSelection, GetValidSources
  AiTier1Random.cs          — Tier 1: random target selection
  AiTier2Aggressive.cs      — Tier 2: weakest neighbour, strongest source
  AiTier3Strategic.cs       — Tier 3: ML-scored pairs, continent completion
  AiTier4Personality.cs     — Tier 4/5: personality-weighted scoring, mission pursuit
  AiScoring.cs              — Shared scoring: ScoreAttack, ScoreContinentDenial, 
                              IsChokepoint, FindWeakestPlayer, FindStrategicFortify
  PersonalityWeights.cs     — Already exists (keep as-is)
```

## How It Works

Each tier implements a simple interface:

```csharp
interface IAiStrategy
{
    (Territory Source, Territory Target)? ChooseAttack(GameState state, int myIndex);
    (Territory Source, Territory Target, int Armies)? ChooseFortify(GameState state, int myIndex);
    Territory ChooseReinforceTarget(GameState state, List<Territory> owned, int myIndex);
    bool ShouldBlitz(Territory source, Territory target);
    bool ShouldStopAttacking(GameState state, Player player, int attackCount);
    float TimingMultiplier { get; }
}
```

`AiService.RunTurnAsync` becomes:

```csharp
var strategy = GetStrategy(player.AiTier, player.Personality);
await RunReinforce(strategy, state, player, connId);
await RunAttack(strategy, state, player, connId);
await RunFortify(strategy, state, player, connId);
```

And `RunAttack` becomes:

```csharp
while (attackCount < maxAttacks)
{
    var choice = strategy.ChooseAttack(state, myIndex);
    if (choice is null) break;
    if (strategy.ShouldStopAttacking(state, player, attackCount)) break;
    
    await actions.ShowSelection(choice.Source, choice.Target, strategy.TimingMultiplier);
    
    if (strategy.ShouldBlitz(choice.Source, choice.Target))
        await actions.DoBlitz(connId, choice.Source, choice.Target);
    else
        await actions.DoSingleAttack(connId, choice.Source, choice.Target);
    
    if (state.Phase == GamePhase.GameOver) return;
    attackCount++;
}
```

## Estimated Line Counts

| File | Lines | Notes |
|------|-------|-------|
| `AiService.cs` | ~80 | Entry, routing, error recovery |
| `AiActions.cs` | ~120 | Shared execution (blitz, single, fortify, broadcast) |
| `AiTier1Random.cs` | ~40 | Random selection |
| `AiTier2Aggressive.cs` | ~60 | Simple scoring |
| `AiTier3Strategic.cs` | ~100 | ML scoring + continent logic |
| `AiTier4Personality.cs` | ~150 | Personality weights, mission pursuit, elimination |
| `AiScoring.cs` | ~120 | All shared scoring helpers |
| `PersonalityWeights.cs` | ~50 | Already exists |
| **Total** | **~720** | |

**Current:** 997 lines in one file
**After:** ~720 lines across 8 files

**Reduction: ~280 lines (28%)** — but more importantly, the duplication drops from ~480 lines of repeated patterns to ~50 lines of shared methods. Each file is under 150 lines and has one clear purpose.

## Migration Risk

Low. The refactoring is purely structural — no logic changes, no new behaviour, no SignalR contract changes. Each tier produces identical game outcomes. Testable by playing games before/after and confirming bots behave the same.

## When

Not urgent — the current code works. Best done when:
- Multi-game and multi-household features are stable
- No active feature branches touching AiService
- A quiet session with time to verify all 5 tiers still play correctly

---

*Created: 2026-07-06*
