# AI Personalities — Implementation (as built)

## Overview

Personality-based AI lives on **Tier 5** (not Tier 3/4 as originally planned). Each personality uses the same Tier 4 strategic engine (chokepoints, elimination hunting, continent denial, ML blitz probability) but with different weight profiles that shift priorities. Tier 5 additionally blends ML behaviour models trained from human play data.

## Personalities

| Personality | Emoji | Style |
|---|---|---|
| Opportunist | 🦊 | Hunts eliminations for card steals, values chokepoints, fast tempo |
| Cautious | 🛡️ | Only attacks at 4:1 ratio, hoards cards, turtles, preserves armies |
| Aggressive | 🔥 | Attacks at 1.5:1, max expansion, doesn't preserve armies, fastest |
| Continental | 🗺️ | Prioritises continent completion, blocks opponents, ignores eliminations |

## Weight Profiles (`Models/GameState.cs`)

```csharp
public record PersonalityWeights(
    float AttackRatioThreshold,  // min army ratio to attack
    float ContinentPriority,    // weight on continent scoring
    float EliminationHunting,   // bonus for targeting weak players
    float ContinentDenial,      // bonus for blocking opponent continents
    float ArmyPreservation,     // tendency to stop attacking after card
    float ExpansionSpeed,       // general aggression multiplier
    float CardHoarding,         // 0=trade immediately, 1=hold until forced
    float TimingMultiplier      // action delay multiplier (lower=faster)
);
```

Values per personality:
- **Opportunist:** `(2.0, 0.1, 1.0, 0.3, 0.4, 0.7, 0.5, 0.9)`
- **Cautious:** `(4.0, 0.3, 0.1, 0.5, 1.0, 0.2, 1.0, 1.3)`
- **Aggressive:** `(1.5, 0.2, 0.3, 0.2, 0.2, 1.0, 0.0, 0.6)`
- **Continental:** `(2.5, 1.0, 0.0, 0.8, 0.5, 0.5, 0.3, 1.0)`

## Lobby UX

- Tap **🧬 Tier-5** to expand a 2×2 grid + mystery button
- Selecting a personality calls `AddAI(5, "Opportunist")` etc.
- **🎲 Mystery** picks a random personality — players don't know which until they observe behaviour
- Tier 4 is always Opportunist (no picker)

## ML Integration (Tier 5 only)

Tier 5 blends human behaviour model predictions at 30% weight into the attack scoring:

```csharp
if (tier >= 5)
{
    float humanScore = ml.PredictHumanAttack(source.Armies, target.Armies, ...);
    score = score * 0.7f + humanScore * 0.3f;
}
```

Three behaviour models trained from player logs:
- **Reinforce** — predict which territories humans reinforce (border, threat, continent progress)
- **Attack** — predict which attacks humans take (army ratio, continent completion, blitz usage)
- **Fortify** — predict fortify behaviour (border targeting, threat response, skip rate)

## Auto-Retrain

Models retrain in background (fire-and-forget) after every game-over. More games = smarter Tier 5.

## Key Files

| File | Role |
|------|------|
| `Models/GameState.cs` | `AiPersonality` enum, `PersonalityWeights` record with `.For()` |
| `Services/AiService.cs` | Tier 4/5 decision logic, weights threaded through all methods |
| `Services/MlModels.cs` | Model loading + prediction (blitz, reinforce, attack, fortify) |
| `Services/ActionLogger.cs` | Logs human decisions to CSV for training |
| `Training/BehaviourTrainer.cs` | Trains reinforce/attack/fortify models from CSVs |
| `Hubs/GameHub.cs` | Background retrain trigger on game-over |

---

*Updated: 2026-06-25*
