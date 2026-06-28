# AI Tier 3 — ML.NET Implementation Plan

## What is Tier 3?

Strategic AI with ML.NET-informed decisions. Combines learned tactical predictions with handcrafted strategic logic.

| Tier | Label | Approach | Behaviour |
|------|-------|----------|-----------|
| 1 | Easy | Random | Random everything, 50% skip |
| 2 | Aggressive | Heuristic | Attack weakest, reinforce front, always push |
| 3 | Strategic | ML.NET + Heuristics | Learned attack/reinforce scoring + continent awareness |
| 4 | Personality | Weighted ML | Tier 3 base with personality weight overrides |

## What ML.NET Does in Tier 3

Three models, built progressively:

### Model 1: Blitz Probability (Phase 1 — start here)
- **Input:** attacker armies, defender armies
- **Output:** P(capture), expected losses
- **Use:** AI decides blitz vs single attack vs skip
- **Training:** Simulate 100K blitz battles, log outcomes

### Model 2: Attack Advisor (Phase 2)
- **Input:** source armies, target armies, army ratio, continent progress, border flag
- **Output:** attack desirability score (0–1)
- **Use:** AI scores all possible attacks, picks best
- **Training:** Simulate 10K games between Tier 1/2 bots, label attacks by win correlation

### Model 3: Reinforce Advisor (Phase 3)
- **Input:** territory features (threat, continent progress, army count, is-border)
- **Output:** reinforce priority score
- **Use:** AI places armies on highest-scoring territory
- **Training:** Same simulated games — where did winners reinforce?

## What Heuristics Do in Tier 3

ML handles **tactical** decisions (where to attack, where to reinforce). Heuristics handle **strategic** goals:

- **Continent targeting:** prioritise completing small continents (Australia, S. America)
- **Continent denial:** hold 1 territory in opponent's nearly-complete continent
- **Card timing:** hold cards until territory bonus available or 4+ held
- **Mission awareness:** weight decisions toward mission objectives
- **Threat assessment:** avoid weakening borders facing strongest player
- **Attack restraint:** don't attack if model says < 0.4 desirability (unlike Tier 2 which always attacks)

## Implementation Steps

### Phase 1: Blitz Model (ML.NET learning exercise)
1. Add `Microsoft.ML` NuGet package
2. Create `Training/BlitzSimulator.cs` — runs simulated blitz battles, outputs CSV
3. Create `Training/BlitzTrainer.cs` — trains FastTree regression, saves `blitz-model.zip`
4. Create `Services/MlModels.cs` — singleton, loads model at startup, exposes `PredictBlitz(atk, def)`
5. Tier 3 attack logic uses prediction: blitz if P > 0.7, single if 0.4–0.7, skip if < 0.4
6. Register `MlModels` in DI

### Phase 2: Attack Advisor
1. Create `Training/GameSimulator.cs` — runs headless games (GameService only, no SignalR)
2. Run 10K+ games, log every attack decision + game outcome to CSV
3. Train binary classifier → attack desirability
4. Tier 3 evaluates all possible (source, target) pairs, picks top-scoring

### Phase 3: Reinforce Advisor
1. Use same game simulation data from Phase 2
2. Extract territory features at each reinforce decision
3. Train regression → reinforce priority
4. Tier 3 places armies on highest-scoring territory

### Phase 4: Wire into AiService
- `RunTier3Attack()` — uses attack model + blitz model
- `RunTier3Reinforce()` — uses reinforce model
- `RunTier3Fortify()` — heuristic (move toward weak continent border)
- Add Tier 3 button to lobby chooser

## File Structure

```
server/Risk.Server/
├── Services/
│   ├── AiService.cs           — Tier 3 branches using MlModels
│   └── MlModels.cs            — loads .zip models, Predict methods
├── Training/                   — offline tools (console app or #if DEBUG)
│   ├── BlitzSimulator.cs      — generates blitz-data.csv
│   ├── GameSimulator.cs       — runs headless games → game-data.csv
│   └── ModelTrainer.cs        — trains models from CSVs → .zip files
├── Data/
│   ├── territories.json
│   └── models/                — trained model files
│       ├── blitz-model.zip
│       ├── attack-model.zip
│       └── reinforce-model.zip
└── Risk.Server.csproj         — Microsoft.ML package reference
```

## Training vs Runtime

| | Training (offline) | Runtime (in-game) |
|---|---|---|
| When | Before deployment, or on demand | Every AI turn |
| Speed | Minutes (simulating thousands of games) | Sub-millisecond (single prediction) |
| Where | Dev machine, maybe a console command | In AiService during normal play |
| Output | .zip model files (committed to repo) | Decision (attack/skip/blitz) |

## Open Questions

- Should training be a separate console project or `#if DEBUG` code in the server?
- How many simulation games are enough? Start with 10K, increase if model accuracy is poor.
- Should Tier 3 fall back to Tier 2 heuristics if models aren't loaded? (Yes — graceful degradation)
- Retrain after rule changes? (Yes — but models are small, retraining takes seconds)

## Branch

`feature/ai-tier3-ml`

Start with Phase 1 (Blitz model) — it's self-contained, teaches the full ML.NET pipeline, and provides immediate value to the AI.

---

*Created: 2026-06-23*
