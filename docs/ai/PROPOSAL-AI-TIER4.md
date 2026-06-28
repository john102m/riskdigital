# Proposal: AI Tier 4 — Enhanced Heuristics + Personality Weights

## What

Tier 4 adds **deeper Risk domain knowledge** (heuristics Tier 3 doesn't have), then uses personality weights to select which heuristics each character prioritises. The result is a strong but beatable bot that feels human.

**Phase 1:** Opportunist Ollie only (proves the architecture). Single `🦊 Tier-4` lobby button.

## Enhanced Heuristics (the new intelligence)

| Heuristic | What it does |
|-----------|--------------|
| Elimination hunting | Detect players 1–2 territories from death, chain attacks to wipe them for cards |
| Continent denial | Block opponents 1 territory from completing a continent, even at bad odds |
| Card escalation awareness | Value eliminations higher as trade count rises (later trades = more armies) |
| Chokepoint recognition | Value strategic territories (Siam, Ukraine, N. Africa) that gate continents |
| Snowball detection | Identify the leader and shift attack priority toward them |
| Weakest player targeting | Focus attacks on most vulnerable opponent for quick elimination |

## Personality Weights (the selector)

```csharp
public record PersonalityWeights(
    float AttackRatioThreshold,    // min ratio to consider attacking
    float ContinentPriority,       // continent completion value
    float EliminationHunting,      // targeting weakest for card steal
    float ContinentDenial,         // blocking opponent continent completion
    float ArmyPreservation,        // reluctance to lose armies
    float ExpansionSpeed,          // eagerness to attack broadly
    float CardHoarding,            // 0=trade ASAP, 1=hold until forced
    float TimingMultiplier         // delay speed (0.6=fast, 1.3=slow)
);
```

### Opportunist Ollie 🦊 (Phase 1)
- `AttackRatioThreshold = 2.0` — attacks at decent odds
- `ContinentPriority = 0.1` — doesn't care about continents
- `EliminationHunting = 1.0` — all about the kill
- `ContinentDenial = 0.3` — mild blocking
- `ArmyPreservation = 0.4` — willing to take risks
- `ExpansionSpeed = 0.7` — fairly aggressive
- `CardHoarding = 0.5` — strategic trading (holds for territory bonus)
- `TimingMultiplier = 0.9` — slightly fast, faster when smelling blood

### Future personalities (Phase 2)
- Cautious Carl 🐢: high preservation, high threshold, slow
- Aggressive Alice ⚔️: low threshold, max expansion, very fast
- Continental Chris 🗺️: max continent priority + denial, methodical

## Where

| File | Change |
|------|--------|
| `Models/GameState.cs` | Add `AiPersonality` enum, `PersonalityWeights` record, `AiPersonality` field on Player |
| `Services/GameService.cs` | Raise clamp to 4, assign `Opportunist` personality on Tier 4 |
| `Services/AiService.cs` | New Tier 4 methods: `RunTier4Reinforce`, `RunTier4Attack`, `RunTier4Fortify` with heuristic helpers |
| `handset/.../LobbyScreen.tsx` | Add `🦊 Tier-4` button |

## Key Implementation Details

### Attack scoring (replaces Tier 3's `ScoreAttack` for Tier 4)

```
score = mlBlitzProb * (1 - ArmyPreservation)
      + continentCompletionValue * ContinentPriority
      + continentDenialValue * ContinentDenial  
      + eliminationProximity * EliminationHunting
      + chokePointValue * 0.3
```

### Elimination detection
```csharp
// Can we kill this player in one turn?
var targetTerritories = state.Territories.Where(t => t.OwnerId == victimIndex).ToList();
bool canEliminate = targetTerritories.All(t => 
    t.Adjacent.Any(a => state.Territories[a].OwnerId == myIndex && state.Territories[a].Armies > t.Armies));
```

### Why still beatable
- Dice randomness (even 10v1 can lose)
- No knowledge of other players' missions/cards  
- Single-turn horizon (humans can set multi-turn traps)
- Personality blind spots (Ollie ignores his own continent progress)
- Can't negotiate or form alliances

## Scope

Phase 1 deliverables:
1. PersonalityWeights system
2. Enhanced heuristics (elimination hunting, continent denial, chokepoints)
3. Opportunist Ollie wired up
4. Lobby button
5. Timing multiplier on delays

---

*Proposed: 2026-06-24 (updated)*
