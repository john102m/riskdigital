using Microsoft.ML;

namespace Risk.Server.Training;

/// <summary>
/// Trains behaviour models from human player action logs.
/// Reinforce: regression (predict placement likelihood per territory features).
/// Attack: binary classification (predict whether human would attack given source/target).
/// </summary>
public static class BehaviourTrainer
{
    public static string TrainReinforce(string csvPath, string modelPath)
    {
        var mlContext = new MLContext(seed: 42);

        var data = mlContext.Data.LoadFromTextFile<ReinforceRow>(csvPath, separatorChar: ',', hasHeader: true);
        var count = mlContext.Data.CreateEnumerable<ReinforceRow>(data, reuseRowObject: false).Count();

        if (count < 10) return $"Not enough data ({count} rows, need 10+)";

        var pipeline = mlContext.Transforms.CopyColumns("Label", nameof(ReinforceRow.IsBorder))
            .Append(mlContext.Transforms.Concatenate("Features",
                nameof(ReinforceRow.TerritoryArmies), nameof(ReinforceRow.EnemyThreat),
                nameof(ReinforceRow.ContinentProgress), nameof(ReinforceRow.ContinentBonus)))
            .Append(mlContext.Regression.Trainers.FastTree(numberOfTrees: 50, numberOfLeaves: 10, minimumExampleCountPerLeaf: 2));

        var model = pipeline.Fit(data);
        mlContext.Model.Save(model, data.Schema, modelPath);
        return $"Reinforce model trained ({count} rows) → {modelPath}";
    }

    public static string TrainAttack(string csvPath, string modelPath)
    {
        var mlContext = new MLContext(seed: 42);

        var data = mlContext.Data.LoadFromTextFile<AttackRow>(csvPath, separatorChar: ',', hasHeader: true, allowQuoting: true);
        var count = mlContext.Data.CreateEnumerable<AttackRow>(data, reuseRowObject: false).Count();

        if (count < 10) return $"Not enough data ({count} rows, need 10+)";

        var pipeline = mlContext.Transforms.CopyColumns("Label", nameof(AttackRow.DidAttack))
            .Append(mlContext.Transforms.Concatenate("Features",
                nameof(AttackRow.SourceArmies), nameof(AttackRow.TargetArmies),
                nameof(AttackRow.TargetOwnerTerritoryCount), nameof(AttackRow.UsedBlitz),
                nameof(AttackRow.WouldCompleteCont)))
            .Append(mlContext.Regression.Trainers.FastTree(numberOfTrees: 50, numberOfLeaves: 10, minimumExampleCountPerLeaf: 2));

        var model = pipeline.Fit(data);
        mlContext.Model.Save(model, data.Schema, modelPath);
        return $"Attack model trained ({count} rows) → {modelPath}";
    }

    // Row schemas for CSV loading
    public class ReinforceRow
    {
        [Microsoft.ML.Data.LoadColumn(3)] public float TerritoryArmies { get; set; }
        [Microsoft.ML.Data.LoadColumn(4)] public float IsBorder { get; set; }
        [Microsoft.ML.Data.LoadColumn(5)] public float EnemyThreat { get; set; }
        [Microsoft.ML.Data.LoadColumn(6)] public float ContinentProgress { get; set; }
        [Microsoft.ML.Data.LoadColumn(7)] public float ContinentBonus { get; set; }
    }

    public class AttackRow
    {
        [Microsoft.ML.Data.LoadColumn(2)] public float SourceArmies { get; set; }
        [Microsoft.ML.Data.LoadColumn(3)] public float TargetArmies { get; set; }
        [Microsoft.ML.Data.LoadColumn(4)] public float TargetOwnerTerritoryCount { get; set; }
        [Microsoft.ML.Data.LoadColumn(5)] public string TargetContinentProgress { get; set; } = "";
        [Microsoft.ML.Data.LoadColumn(6)] public string MyContinentProgress { get; set; } = "";
        [Microsoft.ML.Data.LoadColumn(7)] public float UsedBlitz { get; set; }
        [Microsoft.ML.Data.LoadColumn(8)] public float WouldCompleteCont { get; set; }
        [Microsoft.ML.Data.LoadColumn(9)] public float TurnNumber { get; set; }
        [Microsoft.ML.Data.LoadColumn(10)] public float DidAttack { get; set; }
    }
}
