# AI Personalities + Mission Awareness — Implementation Plan

## Goal

Wire personality weight profiles into the Tier 3 engine. Add own-mission pursuit and basic threat detection. Single "🧠 Personality" button assigns a random character (hidden from players until game end).

## Prerequisites (done)

- ✅ Tier 3 base: ML blitz model + ScoreAttack + ScoreReinforceTarget + FindStrategicFortify
- ✅ AiTier field on Player
- ✅ Tier chooser in lobby

---

## Changes

### 1. AiPersonality Record + Profiles (`Services/AiService.cs`)

```csharp
private record AiPersonality(
    string Name,
    string Emoji,
    float AttackThreshold,     // min combined score to attack (0.3–0.8)
    float ContinentWeight,     // multiplier on continent bonus in ScoreAttack (0.5–5.0)
    float WeakPlayerWeight,    // bonus for targeting player with fewest territories (0–5)
    float ArmyPreservation,    // 1.0 = stop after card, 0.0 = never stop
    int CardHoarding           // min cards before trading (3=immediately, 5=hold max)
);

private static readonly Dictionary<string, AiPersonality> Personalities = new()
{
    ["Carl"]  = new("Carl",  "🐢", 0.8f, 1.0f, 0.0f, 1.0f, 5),
    ["Alice"] = new("Alice", "💥", 0.3f, 0.5f, 0.5f, 0.0f, 3),
    ["Chris"] = new("Chris", "🗺️", 0.5f, 5.0f, 0.0f, 0.5f, 4),
    ["Ollie"] = new("Ollie", "🦊", 0.4f, 0.5f, 5.0f, 0.3f, 4),
};
```

### 2. Player Model (`Models/GameState.cs`)

```csharp
public string? AiPersonality { get; set; }  // "Carl", "Alice", "Chris", "Ollie" (null for Tier 1-2)
```

### 3. AddAiPlayer — Assign Random Personality for Tier 3

When tier=3: pick random personality not already in use. Store on player.

### 4. Thread Weights Through Tier 3 Methods

Get personality at start of each method:
```csharp
var p = Personalities.GetValueOrDefault(player.AiPersonality ?? "", Personalities["Carl"]);
```

**ScoreAttack():**
- Replace hardcoded `continentBonus / 20f` → `continentBonus * p.ContinentWeight / 20f`
- Add weak player bonus: `if target owner is weakest player → score += p.WeakPlayerWeight * 0.1f`
- Use `p.AttackThreshold` instead of hardcoded 0.4f

**RunStrategicAttack():**
- Card restraint: `if (player.EarnedCardThisTurn && p.ArmyPreservation > Random.Shared.NextSingle()) break`
- Gives probabilistic stop — Carl always stops, Alice never stops, others sometimes

**RunReinforce() card timing:**
- Replace `player.Cards.Count >= 4` → `player.Cards.Count >= p.CardHoarding`

**ScoreReinforceTarget():**
- Weight continent scores by `p.ContinentWeight`

### 5. Own Mission Pursuit

In `ScoreAttack()` and `ScoreReinforceTarget()`, check `player.Mission`:

```csharp
// If attack target is in a required continent for our mission → bonus
if (player.Mission?.Type == MissionType.ContinentConquest
    && player.Mission.RequiredContinents != null)
{
    var targetContinent = state.Territories[target.Id].Continent;
    if (player.Mission.RequiredContinents.Contains(targetContinent))
        score += 0.3f;  // mission alignment bonus
}

// If mission is elimination → huge bonus for attacking that player
if (player.Mission?.Type == MissionType.Elimination
    && target.OwnerId == player.Mission.TargetPlayerIndex)
    score += 0.5f;

// If mission is territory count → prefer expanding to new territories over stacking
if (player.Mission?.Type == MissionType.TerritoryCount)
    score += 0.1f;  // any capture helps
```

### 6. Basic Threat Detection

New helper: `FindThreats(GameState state, int myIndex)` → list of (playerIndex, threatType, urgency)

```csharp
foreach opponent:
  foreach continent:
    if opponent owns >= 80% → flag CONTINENT_THREAT (high)
  if opponent territory count >= 16 → flag TERRITORY_THREAT (medium)
```

In `ScoreAttack()`:
- If target territory would BLOCK a high-threat opponent's continent → big bonus
- Only active for personalities with awareness (Chris for continents, Ollie for all threats)

### 7. Lobby UI Update

Replace three tier buttons with:
```
[🤖 Easy] [⚔️ Aggressive] [🧠 Personality]
```

"Personality" calls `AddAI(3)` — server assigns random personality.
Player list shows: "Bot Alice 🧠" (no character revealed).

### 8. Game-End Reveal

On GameOver broadcast, include personality for each AI:
- TV shows: "Bot Alice was 🐢 Carl (Cautious)" in the winner overlay or post-game summary

---

## Files to Modify

| File | Change |
|------|--------|
| `Models/GameState.cs` | Add `AiPersonality` to Player |
| `Services/AiService.cs` | Personality record, profiles dict, thread weights through all Tier 3 methods, mission pursuit, threat detection |
| `Services/GameService.cs` | Assign random personality in AddAiPlayer for tier 3 |
| `Hubs/GameHub.cs` | No change (tier param already exists) |
| `handset/src/components/LobbyScreen.tsx` | Replace Tier-3 button with "🧠 Personality" |
| `handset/src/types/game.ts` | Add `aiPersonality` to Player |
| `server/wwwroot/tv.html` | Game-end reveal (optional) |

---

## Test Checklist

- [ ] Carl: barely attacks, builds massive stacks, hoards 5 cards
- [ ] Alice: attacks everything, trades immediately, spreads thin
- [ ] Chris: only attacks continent targets, ignores easy kills elsewhere
- [ ] Ollie: hunts weakest player, blocks anyone close to winning
- [ ] All pursue their own mission (reinforce/attack toward mission goals)
- [ ] Continent threat detected and blocked (at least by Chris/Ollie)
- [ ] Personality hidden in lobby, revealed at game end
- [ ] Different personalities feel noticeably different to play against

---

## Branch

`feature/ai-personalities`

---

*Created: 2026-06-23*
