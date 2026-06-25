# ML.NET in Risk Digital — From Data Science Basics to Learning AI

A targeted tutorial explaining how Risk Digital uses machine learning, from fundamental concepts through to an AI that learns from human players. References the actual project code throughout.

---

## Part 1: What is Machine Learning?

Machine learning = software that improves by seeing examples, not by being explicitly programmed.

Instead of writing:
```csharp
if (attackers > defenders * 2) blitz();  // rigid, often wrong
```

You show the computer thousands of examples:
```
8 attackers vs 3 defenders → captured (1)
4 attackers vs 6 defenders → lost (0)
...58,000 more rows...
```

It finds the patterns itself. The result? A function that predicts the right answer for combinations it's never seen before.

---

## Part 2: Core Concepts (with Risk examples)

### Training Data
Examples the model learns from. Each row = one observation.

**Risk example** (`Data/models/blitz-data.csv`):
```csv
AttackerArmies,DefenderArmies,Captured
8,3,1
4,6,0
12,5,1
```

### Features
The inputs — what the model looks at to make a prediction.

**Risk:** `AttackerArmies` and `DefenderArmies` are features.

### Label
The answer — what you're trying to predict.

**Risk:** `Captured` (1 = won, 0 = lost) is the label.

### Model
The trained "brain" — a mathematical function that maps features → prediction. In ML.NET, saved as a `.zip` file.

**Risk:** `Data/models/blitz-model.zip`

### Training
The process of feeding data into an algorithm and producing a model.

### Inference / Prediction
Using the trained model to predict on new data.

**Risk:** `ml.PredictBlitz(7, 2)` → `0.89` (89% chance of capture)

### Regression vs Classification
- **Regression:** Predict a number (0.0–1.0 probability) ← what we use
- **Classification:** Predict a category (yes/no, cat/dog)

---

## Part 3: ML.NET — The Framework

ML.NET is Microsoft's machine learning library for .NET. No Python, no cloud, no GPUs needed. One NuGet package:

```xml
<PackageReference Include="Microsoft.ML" Version="3.*" />
<PackageReference Include="Microsoft.ML.FastTree" Version="3.*" />
```

### Key ML.NET Classes

| Class | Purpose | Risk file |
|-------|---------|-----------|
| `MLContext` | Entry point for all ML ops (like `DbContext`) | `ModelTrainer.cs` |
| `IDataView` | In-memory table loaded from CSV | `ModelTrainer.cs` |
| `Pipeline` | Chain of transforms + training algorithm | `ModelTrainer.cs` |
| `PredictionEngine<TIn, TOut>` | Fast single-prediction wrapper | `MlModels.cs` |

### ML.NET Conventions
- Input features must be combined into a column called `"Features"`
- The prediction target must be called `"Label"`
- Output predictions land in a column called `"Score"`
- Models are saved/loaded as `.zip` files (no extraction needed — ML.NET reads them directly)

---

## Part 4: The Blitz Model (Tier 3 AI)

### Problem
"Should the AI blitz this territory?" Needs to know the probability of capturing.

### Solution
Simulate 58,000 battles using real dice rules, train a model on the outcomes.

### The 4 Files

#### `Training/BlitzSimulator.cs` — Generates data
Runs dice combat for every combo of 2–30 attackers vs 1–20 defenders (100 simulations each):
```csharp
// Uses the same dice resolution as GameService.Blitz()
// Outputs: AttackerArmies, DefenderArmies, Captured (0 or 1)
```

#### `Training/ModelTrainer.cs` — Trains the model
```csharp
var pipeline = mlContext.Transforms
    .Concatenate("Features", "AttackerArmies", "DefenderArmies")
    .Append(mlContext.Regression.Trainers.FastTree(numberOfTrees: 100));

var model = pipeline.Fit(data);
mlContext.Model.Save(model, data.Schema, modelPath);
```

**FastTree** = 100 decision trees that each vote. More nuanced than any single `if` statement.

#### `Services/MlModels.cs` — Runtime predictions
```csharp
public float PredictBlitz(int attackerArmies, int defenderArmies)
// Returns 0.0–1.0 (probability of capture)
// Sub-millisecond per call
```

#### `Services/AiService.cs` — Uses predictions
```csharp
float score = ml.PredictBlitz(source.Armies, target.Armies);
if (score > 0.7f) // blitz (confident)
else if (score > 0.4f) // single attack (probe)
else // skip (bad odds)
```

### Training is one-time
Hit `/admin/train` → generates CSV → trains → loads. Done forever (dice rules don't change).

### Accuracy
```
R² = 0.64  — explains 64% of variance (dice are inherently random)
MAE = 0.16 — predictions off by ~16% average
```

Perfect accuracy is impossible (dice are random). The model predicts the *probability curve* which is all the AI needs.

---

## Part 5: The Behaviour Models (Tier 5 AI — Learning from Humans)

### Problem
The AI plays well using heuristics, but can it learn *how your family plays* and adapt?

### Solution
Log every human decision with board context → train models → blend predictions with heuristics.

### How It's Different from Blitz Model

| | Blitz Model | Behaviour Models |
|--|-------------|-----------------|
| Data source | Simulated (instant, unlimited) | Real games (slow, limited) |
| What it learns | Physics (dice probability) | Psychology (human choices) |
| Retraining | Never (rules don't change) | After every few games (new data) |
| Accuracy | High (deterministic-ish) | Lower initially, improves over time |

### The Pipeline

```
DURING GAMES:
  Human plays → ActionLogger.cs → CSV files (Data/logs/)

WHEN YOU RETRAIN (/admin/train-behaviour):
  BehaviourTrainer.cs reads CSVs → trains models → saves .zip files
  MlModels.cs reloads models → ready for predictions immediately

AT RUNTIME (Tier 5 AI turn):
  Heuristic score (0.7 weight) + human behaviour prediction (0.3 weight) = blended decision
```

### The 3 Action Logs

#### `Data/logs/reinforce-log.csv`
Every time a human places an army:
```csv
GameId,PlayerIndex,TerritoryId,TerritoryArmies,IsBorder,EnemyThreat,ContinentProgress,ContinentBonus,TotalReinforcements,TurnNumber
6700,0,9,5,1,1,1.00,2,6,0
```
**What it captures:** "Given this board situation, where did the human choose to place?"

#### `Data/logs/attack-log.csv`
Every human attack:
```csv
GameId,PlayerIndex,SourceArmies,TargetArmies,TargetOwnerTerritoryCount,TargetContinentProgress,MyContinentProgress,UsedBlitz,WouldCompleteCont,TurnNumber,DidAttack
6700,0,7,1,14,2/4,2/4,1,0,0,1
```
**What it captures:** "Given these odds and this board, did the human attack? Did they blitz?"

#### `Data/logs/fortify-log.csv`
Every fortify decision (including skips):
```csv
GameId,PlayerIndex,SourceId,TargetId,ArmiesMoved,TargetIsBorder,TargetEnemyThreat,Skipped
6700,0,11,10,3,1,1,0
6700,0,-1,-1,0,0,0,1
```
**What it captures:** "Did they fortify or skip? If fortified, where?"

### ActionLogger.cs — The Collector
```csharp
public class ActionLogger
{
    // Only logs human players (skips AI)
    public void LogReinforce(GameState state, int playerIndex, int territoryId)
    public void LogAttack(GameState state, int playerIndex, int sourceId, int targetId, bool usedBlitz)
    public void LogFortify(GameState state, int playerIndex, int sourceId, int targetId, int armies)
    public void LogFortifySkip(GameState state, int playerIndex)
}
```
Wired into `GameHub.cs` — fires after each successful hub method call. Lightweight (just string append to file).

### BehaviourTrainer.cs — The Trainer
```csharp
public static string TrainReinforce(string csvPath, string modelPath)
public static string TrainAttack(string csvPath, string modelPath)
```
Same pattern as blitz trainer: load CSV → define features → FastTree → save .zip.

### MlModels.cs — The Predictor
```csharp
public float PredictHumanReinforce(armies, isBorder, enemyThreat, continentProgress, continentBonus)
// "How likely would a human place here?" → 0.0–1.0

public float PredictHumanAttack(sourceArmies, targetArmies, targetOwnerTerritories, usedBlitz, wouldComplete)
// "Would a human attack this?" → 0.0–1.0
```

Falls back to `0.5` (neutral) if models aren't trained yet.

### Data Volume & Growth
- ~25 KB per game (tiny)
- 10 games = ~250 KB of logs
- 100 games = ~2.5 MB
- Models retrain in seconds regardless of size
- WHUK disk space: completely negligible

### The Blend (Tier 5 scoring)
```csharp
float heuristicScore = ScoreTier4Attack(state, source, target, weights, myIndex); // strategic
float humanScore = ml.PredictHumanAttack(features);                              // learned
float blendedScore = heuristicScore * 0.7f + humanScore * 0.3f;                  // combined
```

Why 70/30? The heuristics encode proven Risk strategy. The human model adds flavour — "your family tends to attack here" — but shouldn't override fundamentals. As more data accumulates and model accuracy improves, the blend could shift toward 60/40 or 50/50.

---

## Part 6: The Layer Cake — How All 5 Tiers Build On Each Other

```
┌───────────────────────────────────────────┐
│  Tier 5: Learned Behaviour                 │  "What would a human do?"
│  - reinforce-behaviour.zip                 │  Blended 30% with heuristics
│  - attack-behaviour.zip                    │  Gets smarter with every game
├───────────────────────────────────────────┤
│  Tier 4: Enhanced Heuristics + Personality │  "What should I prioritise?"
│  - Elimination hunting                     │  Weight profiles per character
│  - Continent denial                        │  (Ollie, Carl, Alice, Chris)
│  - Chokepoint recognition                  │
├───────────────────────────────────────────┤
│  Tier 3: ML Blitz + Strategic Heuristics   │  "Can I win? Is it worth it?"
│  - blitz-model.zip                         │  Continent completion scoring
│  - ScoreAttack() combines ML + strategy    │  Card timing, attack restraint
├───────────────────────────────────────────┤
│  Tier 2: Aggressive                        │  "Attack weakest, blitz 5+"
│  - Simple strongest→weakest targeting      │  No probability calculation
│  - Front-line reinforcement                │
├───────────────────────────────────────────┤
│  Tier 1: Random                            │  "Roll dice, see what happens"
│  - 50% chance to skip                      │  Random target selection
│  - 1–3 attacks per turn                    │
├───────────────────────────────────────────┤
│  Game Engine (GameService.cs)              │  "Is this legal?"
│  - Adjacency, army counts, dice rules      │  Source of truth
└───────────────────────────────────────────┘
```

Each tier inherits everything below it. Tier 5 has all the capabilities of Tiers 1–4 plus learned human patterns.

---

## Part 7: How to Operate (on WHUK)

### First-time setup
1. Deploy server to WHUK
2. Hit `/admin/train` to generate blitz model (one-time, simulated data)
3. Play games normally — logs accumulate automatically

### After 5–10 games
1. Browse to `http://your-domain:5000/admin/train-behaviour`
2. See: "Reinforce model trained (X rows), Attack model trained (Y rows)"
3. Models hot-loaded — AI immediately uses them (no restart)

### Ongoing
- Play more games → richer data → retrain → smarter AI
- Each retrain takes seconds (FastTree is fast even on budget hosting)
- Logs are tiny (~25 KB/game) — never a disk concern
- Models are small (~100–500 KB .zip)

### Debugging
- Check logs exist: browse `Data/logs/` directory
- Check models exist: browse `Data/models/` directory
- Blitz model is permanent (dice rules don't change)
- Behaviour models improve over time — early versions may be crude

---

## Part 8: What the AI Learns That Heuristics Can't

| Pattern | How a human knows | How the model learns |
|---------|-------------------|---------------------|
| "Always take Australia first" | Experience | Sees early-game reinforcement bias toward continent progress = 0.75+ with bonus = 2 |
| "Blitz anything under 3 armies" | Intuition | Sees high `UsedBlitz` rate when `TargetArmies ≤ 3` |
| "Fortify forward after big attacks" | Aggression | Sees `TargetIsBorder=1` and high `ArmiesMoved` in fortify logs |
| "Don't bother attacking 10-army stacks" | Risk aversion | Sees `DidAttack=0` when `TargetArmies ≥ 8` regardless of source |
| "Pile everything on one border" | Focus | Sees repeated reinforcement of same territory across turns |

The model doesn't "understand" these strategies — it just recognises the statistical signature of human choices and reproduces them.

---

## Part 9: Limitations & Fairness

### What it can't learn
- Other players' missions (private information)
- Card hands (private)
- Multi-turn plans (single-decision model, no memory)
- Bluffing or diplomacy (no communication channel)

### Why it's still beatable
- **Dice are random** — even perfect predictions lose to bad rolls
- **Single-turn horizon** — humans can set 2–3 turn traps
- **Personality blind spots** — each Tier 4 character has deliberate weaknesses
- **Small data** — early models are crude approximations
- **No adaptation mid-game** — model is static until retrained

### Privacy
- All data stays on your server (WHUK)
- No cloud services, no external calls
- Only your family/friends' games are observed
- `/admin/clear-logs` (future) to reset if needed

---

*Created: 2026-06-24*
