# ML.NET Integration — Proposal

## Opportunity

Use ML.NET to make AI decisions data-driven rather than hardcoded heuristics. Risk has enough measurable outcomes (attack success, game wins) to generate training data via simulation.

## What Changes in the AI Plan

### Current Plan (Heuristic Tiers)
```
Tier 1: Random (no logic)
Tier 2: Aggressive (hardcoded: attack weakest, reinforce front)
Tier 3: Strategic (hardcoded: continent scoring, threat weights)
Tier 4: Personality (weighted heuristics)
```

### Revised Plan (ML-Enhanced)
```
Tier 1: Random (unchanged — baseline, testing)
Tier 2: ML-Informed (learned attack/reinforce decisions from simulated data)
Tier 3: ML + Heuristics (ML for tactics, heuristics for strategy)
Tier 4: Personality (weight overrides on ML model outputs)
```

The key insight: **Tier 2 doesn't need handcrafted "attack weakest neighbour" rules if a model can learn what good players do.** Instead of us deciding the heuristics, we simulate thousands of games and let the model learn patterns.

## ML.NET Models (3 focused models, not one mega-model)

### Model 1: Attack Advisor
**Question:** "Should I attack territory Y from territory X?"

**Input features:**
- Source armies
- Target armies
- Source is on continent border (bool)
- Target completes a continent (bool)
- Army ratio (source / target)
- Total player armies vs total opponent armies

**Output:** Score 0.0–1.0 (attack desirability)

**Training data:** Simulate 50,000 games between heuristic players. Label each attack decision with whether that player eventually won the game.

### Model 2: Reinforce Advisor
**Question:** "Which territory should receive my next reinforcement?"

**Input features (per territory):**
- Adjacent enemy armies (threat)
- Adjacent enemy count
- Continent progress (owned / total in continent)
- Is border territory (bool)
- Current armies

**Output:** Score per territory (pick highest)

**Training data:** Same simulated games — what did winning players reinforce?

### Model 3: Blitz Probability
**Question:** "What's my chance of capturing with a blitz?"

**Input features:**
- Attacker armies
- Defender armies

**Output:** P(capture), expected attacker losses

**Training data:** Simulate 100,000 blitz battles at various army combinations. Pure statistics — no game context needed.

This one is the **simplest to start with** (no game context, just math).

## Implementation Plan

### Phase 1: Blitz Probability Model (Learning Exercise)
1. Add `Microsoft.ML` NuGet package
2. Create `Tools/BlitzSimulator.cs` — runs N blitz simulations, outputs CSV
3. Create `Tools/BlitzTrainer.cs` — trains regression model, saves to `Models/blitz-model.zip`
4. Load model in `AiService` at startup
5. AI uses it: `if (predictedCaptureChance > 0.7) Blitz() else SingleAttack()`

**Effort:** 1-2 hours. Simple regression. Good ML.NET introduction.

### Phase 2: Attack Advisor
1. Build game simulator (headless — no SignalR, just GameService called in loops)
2. Run 10,000+ games between Tier 1 bots, log all decisions + outcomes
3. Train binary classifier: attack(features) → won_game
4. Replace "attack weakest" heuristic with model scoring
5. AI evaluates all possible attacks, picks highest-scoring

**Effort:** Half day. More complex features, needs game simulation infrastructure.

### Phase 3: Reinforce Advisor
1. Same training data as Phase 2 (already have it)
2. Train: territory_features → reinforcement_score
3. AI scores all owned territories, places armies on highest-scoring

**Effort:** Few hours (training data already exists from Phase 2).

## Architecture

```
server/Risk.Server/
├── Services/
│   ├── AiService.cs          — uses models for decisions
│   └── MlModels.cs           — loads trained models, exposes Predict methods
├── Training/                  — offline tools (not deployed)
│   ├── BlitzSimulator.cs     — generates blitz training data
│   ├── GameSimulator.cs      — runs headless games for training data
│   └── ModelTrainer.cs       — trains models from CSV data
├── Models/
│   ├── blitz-model.zip       — trained blitz probability model
│   ├── attack-model.zip      — trained attack advisor model
│   └── reinforce-model.zip   — trained reinforce advisor model
└── Data/
    └── training/             — CSV files (gitignored, large)
```

**Key principle:** Training is offline (run once, produces .zip files). Inference is fast and happens in `AiService` during normal gameplay. No training at runtime.

## What ML.NET Handles Well Here

- **Tabular data** — army counts, territory features, boolean flags → perfect for ML.NET
- **Binary classification** — "attack yes/no", "won/lost"
- **Regression** — "probability of capture", "desirability score"
- **Fast inference** — sub-millisecond per prediction, no GPU needed
- **Self-contained** — no Python, no external services, just a NuGet package

## What It Won't Do

- **Multi-turn planning** — "I should save armies for 3 turns then push" (that's reinforcement learning or search trees)
- **Opponent modelling** — "Bob always attacks me after I take Africa" (needs memory/history)
- **Bluffing/deception** — mission concealment (still heuristic)

These remain as handcrafted logic in Tier 3+ on top of ML predictions.

## Does This Change Entry-Level AI?

**Tier 1 stays the same** — random, no ML. It's the testing bot.

**Tier 2 becomes ML-powered instead of brute-force heuristics.** The Aggressive design in `AI-TIER2-PLAN.md` (always attack weakest, reinforce front) becomes the *fallback* if ML models aren't loaded. With models:
- "Always attack" → "Attack when model says > 0.6 desirability"
- "Target weakest" → "Target highest-scoring attack per model"
- "Reinforce front" → "Reinforce highest model score"

The behaviour may end up similar (ML might learn that attacking weak neighbours is good!) but it's *learned* not hardcoded. And it might discover non-obvious patterns we wouldn't program.

**Tier 3 is ML + strategic heuristics** — model handles tactics, continent/mission logic handles strategy.

## Quick Win First

Start with **Phase 1 (Blitz Probability)** — it's:
- Self-contained (no game simulation needed)
- Pure math (attacker/defender armies → outcome)
- Great ML.NET intro (load data, train, predict)
- Immediately useful (AI makes smarter blitz decisions)
- ~50 lines of training code, ~10 lines of inference code

## Dependencies

```xml
<PackageReference Include="Microsoft.ML" Version="3.0.1" />
```

No other dependencies. Ships as part of the server binary.

---

*Created: 2026-06-23*
