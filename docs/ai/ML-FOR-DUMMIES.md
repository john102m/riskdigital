# ML.NET in Risk Digital — How It Works

## The Problem

The AI needs to decide: "Should I blitz territory X from territory Y?" 

Blitzing is all-or-nothing — you can't stop. Bad call = your army wiped out. Good call = easy capture. The question is: **what are the actual odds?**

## The Solution

Instead of writing `if (attackers > defenders * 2) blitz()`, we simulate thousands of battles and let ML.NET learn the probability curve from real outcomes.

---

## Our Code — 4 Files, Each Does One Job

### `Training/BlitzSimulator.cs` — Generates Training Data

Runs 58,000 simulated blitz battles using the exact same dice rules as `GameService.Blitz()`. For every combination of 2–30 attackers vs 1–20 defenders, it simulates 100 battles and records:

```csv
AttackerArmies,DefenderArmies,Captured,AttackerLosses,DefenderLosses
8,3,1,2,3
4,6,0,3,4
...
```

This CSV is the **training data** — examples the model learns from.

Called by: `/admin/train` endpoint (once).

---

### `Training/ModelTrainer.cs` — Trains the Model

Takes the CSV and runs the ML.NET pipeline:

```csharp
// 1. Load CSV into ML.NET's table format
var data = mlContext.Data.LoadFromTextFile<BlitzRow>(csvPath);

// 2. Tell ML.NET which columns are inputs ("Features") 
mlContext.Transforms.Concatenate("Features", "AttackerArmies", "DefenderArmies")

// 3. Train using FastTree (100 decision trees that vote together)
mlContext.Regression.Trainers.FastTree(numberOfTrees: 100)
```

**What FastTree does:** Builds 100 flowcharts. Each one says things like "if attackers > 12 and defenders < 5, predict 0.91." The final prediction = average of all 100 trees. More nuanced than any single rule we could write.

**Output:** `Data/models/blitz-model.zip` — the trained "brain." Contains the 100 trees serialized to disk.

**`BlitzRow` class** defines the CSV schema:
```csharp
public class BlitzRow
{
    [LoadColumn(0)] public float AttackerArmies { get; set; }  // Feature
    [LoadColumn(1)] public float DefenderArmies { get; set; }  // Feature
    [LoadColumn(2)] public float Label { get; set; }           // What we're predicting (0=lost, 1=captured)
}
```

ML.NET conventions: inputs must be combined into a column called "Features". The thing you're predicting must be called "Label". The prediction output is called "Score".

Called by: `/admin/train` endpoint (once).

---

### `Services/MlModels.cs` — Runtime Predictions

Loads the .zip at startup and exposes a fast prediction method:

```csharp
public float PredictBlitz(int attackerArmies, int defenderArmies)
```

Returns 0.0–1.0 (probability of capturing the territory).

Uses `PredictionEngine<BlitzInput, BlitzOutput>` — ML.NET's optimised single-prediction class. Sub-millisecond per call.

**`BlitzInput`** — what we give the model:
```csharp
public class BlitzInput
{
    public float AttackerArmies { get; set; }
    public float DefenderArmies { get; set; }
}
```

**`BlitzOutput`** — what comes back:
```csharp
public class BlitzOutput
{
    [ColumnName("Score")]       // ML.NET puts its prediction here
    public float CaptureChance { get; set; }  // 0.0 to 1.0
}
```

**Fallback:** If the .zip doesn't exist (first run, model not yet trained), it returns a simple ratio: `attackers / (attackers + defenders)`. Functional but less accurate.

Registered as: singleton in DI (`Program.cs`).

---

### `Services/AiService.cs` — Uses the Prediction

In `RunStrategicAttack()` (Tier 3), the AI evaluates every possible attack:

```csharp
foreach (source in mySources)
    foreach (target in adjacentEnemies)
        float score = ml.PredictBlitz(source.Armies, target.Armies);
        // Track the best scoring attack
```

Then decides:
- **score > 0.7** → Blitz (confident — high chance of success)
- **score 0.4–0.7** → Single attack (probe — worth trying but don't commit)
- **score < 0.4** → Skip (bad odds — don't waste armies)

This is the fundamental difference from Tier 2, which just attacks the weakest neighbour regardless of odds.

---

## The Full Flow

```
TRAINING (once, /admin/train):
  BlitzSimulator.GenerateData()     → blitz-data.csv (58K rows)
  ModelTrainer.Train()              → blitz-model.zip (trained trees)
  MlModels.Load()                   → model ready in memory

RUNTIME (every Tier-3 AI turn):
  AiService.RunStrategicAttack()
    → ml.PredictBlitz(8, 3)         → 0.83
    → ml.PredictBlitz(4, 6)         → 0.31
    → picks 8v3 attack (best odds)
    → score > 0.7 → blitzes it
```

---

## Evaluation Metrics (from our training)

```
R² = 0.6440    — model explains 64% of outcomes
MAE = 0.1589   — predictions off by ~16% on average
```

Why not 100%? **Dice are random.** Even with 10 vs 1, there's a tiny chance you lose. The model captures the trend perfectly but can't predict individual dice rolls — nor should it. It predicts the *probability*, which is all the AI needs for decision-making.

---

## Key ML.NET Concepts As Used Here

| Concept | Where | What it does |
|---------|-------|-------------|
| `MLContext` | ModelTrainer.cs | Entry point for all ML operations — like `new DbContext()` |
| `IDataView` | ModelTrainer.cs | ML.NET's in-memory table (loaded from CSV) |
| `Pipeline` | ModelTrainer.cs | Chain of: combine features → train algorithm |
| `FastTree` | ModelTrainer.cs | Gradient boosted trees — 100 decision trees voting together |
| `.Fit(data)` | ModelTrainer.cs | The actual "learning" — builds trees from data |
| `.Save()` | ModelTrainer.cs | Serializes trained model to .zip |
| `.Load()` | MlModels.cs | Deserializes .zip back into a usable model |
| `PredictionEngine` | MlModels.cs | Fast single-prediction wrapper |
| `[LoadColumn(n)]` | ModelTrainer.cs | Maps CSV column index to class property |
| `[ColumnName("Score")]` | MlModels.cs | Maps ML.NET's output column to our property |

---

## Why This Approach Works for Risk

1. **Dice outcomes are statistical** — perfect for regression (predicting probabilities)
2. **Training data is free** — we simulate battles instantly, no need to play real games
3. **Inference is instant** — sub-ms predictions, no lag during AI turns
4. **Self-contained** — one NuGet package, no Python, no GPUs, no cloud services
5. **Extendable** — add more features (board context, continent progress) for smarter models later

---

## What's Next

| Phase | Model | Learns from | Improves |
|-------|-------|-------------|----------|
| 1 ✅ | Blitz Probability | Simulated dice | "Should I blitz?" |
| 2 | Attack Advisor | Simulated full games | "Which attack is best?" |
| 3 | Reinforce Advisor | Simulated full games | "Where should armies go?" |

Each phase adds a new model. Phase 2–3 require a `GameSimulator` that plays thousands of headless games between existing AI tiers and logs every decision + outcome. The winning players' decisions become the training signal.

## How ML + Heuristics Work Together (Tier 3)

The ML model answers: **"Can I win this fight?"** (probability)

The heuristics answer: **"Should I pick this fight?"** (strategy)

Neither alone is enough:
- ML without heuristics = wins fights but doesn't know *why* to fight (no continent plan)
- Heuristics without ML = knows *why* but guesses *whether* (hardcoded 5+ army threshold vs learned probability)

### The Combined Score (`ScoreAttack` in AiService.cs)

```csharp
float mlScore = ml.PredictBlitz(source.Armies, target.Armies);  // 0.0–1.0
float continentBonus = ...;  // big bonus if this capture completes a continent

return mlScore + (continentBonus / 20f);  // combined
```

Example decisions:
| Situation | ML says | Heuristic says | Combined | Decision |
|-----------|---------|----------------|----------|----------|
| 8v3, random territory | 0.83 | +0.0 | 0.83 | Blitz (>0.7) |
| 4v5, random territory | 0.31 | +0.0 | 0.31 | Skip (<0.4) |
| 4v3, completes Australia | 0.55 | +0.5 (bonus×5/20) | 1.05 | Blitz! (continent worth the risk) |
| 3v4, completes Asia | 0.25 | +1.75 (7×5/20) | 2.0 | Blitz! (Asia bonus so valuable, commit) |

The heuristic can override caution when the strategic payoff is high enough. That's the "smart" in Tier 3 — it takes calculated risks for continent control.

### Other Heuristics (no ML involved)

| Heuristic | Where | What it does |
|-----------|-------|-------------|
| `ScoreReinforceTarget()` | AiService.cs | Scores territories for reinforcement: continent gaps > borders > threat |
| Attack restraint | RunStrategicAttack() | Stops attacking after earning a card (preserve armies) |
| `HasTerritoryBonusSet()` | RunReinforce() | Holds cards until territory bonus available or 4+ held |
| `FindStrategicFortify()` | RunStrategicFortify() | Moves armies to weakest border of owned continents |

These are regular C# logic — no training data, no models, no .zip files. They encode *Risk strategy knowledge* that humans know (protect your continent borders, don't overextend, earn a card and stop).

### The Layer Cake

```
┌──────────────────────────────────┐
│  Strategic Heuristics             │  "What should I do?" (goals)
│  - Complete continents            │
│  - Protect borders                │
│  - Earn a card then stop          │
├──────────────────────────────────┤
│  ML Model (blitz-model.zip)       │  "Can I do it?" (probability)
│  - PredictBlitz(atk, def) → 0–1  │
├──────────────────────────────────┤
│  Game Rules (GameService)         │  "Is it legal?" (validation)
│  - Adjacency, army counts, dice   │
└──────────────────────────────────┘
```

Each layer handles a different question. Together they produce an AI that plays like a thoughtful human — knows the rules, calculates the odds, and picks fights for strategic reasons.

---

*Updated: 2026-06-23*
