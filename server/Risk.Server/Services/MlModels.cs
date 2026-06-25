using Microsoft.ML;
using Microsoft.ML.Data;

namespace Risk.Server.Services;

/// <summary>
/// Loads trained ML.NET models at startup and exposes prediction methods.
/// 
/// ML.NET CONCEPTS:
/// - PredictionEngine: makes single predictions from a loaded model
///   (thread-safe via pooling — ML.NET handles this internally)
/// - Input/Output classes: define the shape of data going in and coming out
/// - The model .zip contains the trained decision trees — no retraining at runtime
/// </summary>
public class MlModels
{
    private PredictionEngine<BlitzInput, BlitzOutput>? _blitzEngine;

    /// <summary>
    /// Input features for blitz prediction.
    /// Must match the column names used during training.
    /// </summary>
    public class BlitzInput
    {
        public float AttackerArmies { get; set; }
        public float DefenderArmies { get; set; }
    }

    /// <summary>
    /// Output from the model.
    /// "Score" is ML.NET's default output column name for regression.
    /// It predicts the probability of capture (0.0 to 1.0).
    /// </summary>
    public class BlitzOutput
    {
        [ColumnName("Score")]
        public float CaptureChance { get; set; }
    }

    /// <summary>
    /// Load the blitz model from disk. Call once at startup.
    /// If the model file doesn't exist, predictions return a simple heuristic fallback.
    /// </summary>
    public void Load(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"ML model not found at {modelPath} — using heuristic fallback");
            return;
        }

        var mlContext = new MLContext();
        var model = mlContext.Model.Load(modelPath, out _);
        _blitzEngine = mlContext.Model.CreatePredictionEngine<BlitzInput, BlitzOutput>(model);
        Console.WriteLine($"ML blitz model loaded from {modelPath}");
    }

    /// <summary>
    /// Predict the probability of capturing a territory via blitz.
    /// Returns 0.0–1.0 (0 = will lose, 1 = guaranteed capture).
    /// Falls back to a simple ratio heuristic if model isn't loaded.
    /// </summary>
    public float PredictBlitz(int attackerArmies, int defenderArmies)
    {
        if (_blitzEngine is null)
        {
            // Heuristic fallback: simple ratio-based estimate
            return Math.Clamp((float)attackerArmies / (attackerArmies + defenderArmies), 0f, 1f);
        }

        var prediction = _blitzEngine.Predict(new BlitzInput
        {
            AttackerArmies = attackerArmies,
            DefenderArmies = defenderArmies
        });

        return Math.Clamp(prediction.CaptureChance, 0f, 1f);
    }

    public bool IsLoaded => _blitzEngine is not null;

    // --- Behaviour models (learned from human players) ---

    private PredictionEngine<ReinforceInput, ScoreOutput>? _reinforceEngine;
    private PredictionEngine<AttackInput, ScoreOutput>? _attackEngine;

    public class ReinforceInput
    {
        public float TerritoryArmies { get; set; }
        public float IsBorder { get; set; }
        public float EnemyThreat { get; set; }
        public float ContinentProgress { get; set; }
        public float ContinentBonus { get; set; }
    }

    public class AttackInput
    {
        public float SourceArmies { get; set; }
        public float TargetArmies { get; set; }
        public float TargetOwnerTerritoryCount { get; set; }
        public string TargetContinentProgress { get; set; } = "";
        public string MyContinentProgress { get; set; } = "";
        public float UsedBlitz { get; set; }
        public float WouldCompleteCont { get; set; }
        public float TurnNumber { get; set; }
        public float DidAttack { get; set; }
    }

    public class ScoreOutput
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }

    public void LoadBehaviourModels(string modelsDir)
    {
        if (!Directory.Exists(modelsDir)) return;
        var mlContext = new MLContext();

        var reinforcePath = Path.Combine(modelsDir, "reinforce-behaviour.zip");
        if (File.Exists(reinforcePath))
        {
            var model = mlContext.Model.Load(reinforcePath, out _);
            _reinforceEngine = mlContext.Model.CreatePredictionEngine<ReinforceInput, ScoreOutput>(model);
            Console.WriteLine("Behaviour model loaded: reinforce");
        }

        var attackPath = Path.Combine(modelsDir, "attack-behaviour.zip");
        if (File.Exists(attackPath))
        {
            var model = mlContext.Model.Load(attackPath, out _);
            _attackEngine = mlContext.Model.CreatePredictionEngine<AttackInput, ScoreOutput>(model);
            Console.WriteLine("Behaviour model loaded: attack");
        }
    }

    public float PredictHumanReinforce(float armies, float isBorder, float enemyThreat, float continentProgress, float continentBonus)
    {
        if (_reinforceEngine is null) return 0.5f;
        var pred = _reinforceEngine.Predict(new ReinforceInput
        {
            TerritoryArmies = armies, IsBorder = isBorder, EnemyThreat = enemyThreat,
            ContinentProgress = continentProgress, ContinentBonus = continentBonus
        });
        return Math.Clamp(pred.Score, 0f, 1f);
    }

    public float PredictHumanAttack(float sourceArmies, float targetArmies, float targetOwnerTerritories, float usedBlitz, float wouldComplete)
    {
        if (_attackEngine is null) return 0.5f;
        var pred = _attackEngine.Predict(new AttackInput
        {
            SourceArmies = sourceArmies, TargetArmies = targetArmies,
            TargetOwnerTerritoryCount = targetOwnerTerritories, UsedBlitz = usedBlitz, WouldCompleteCont = wouldComplete,
            TargetContinentProgress = "", MyContinentProgress = "", TurnNumber = 0, DidAttack = 0
        });
        return Math.Clamp(pred.Score, 0f, 1f);
    }

    public bool BehaviourModelsLoaded => _reinforceEngine is not null || _attackEngine is not null;
}
