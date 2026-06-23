using Microsoft.ML;
using Microsoft.ML.Data;

namespace Risk.Server.Training;

/// <summary>
/// Trains an ML.NET regression model to predict blitz capture probability.
/// 
/// ML.NET CONCEPTS:
/// - MLContext: the entry point for all ML.NET operations (like DbContext for EF)
/// - IDataView: ML.NET's tabular data format (like a DataFrame)
/// - Pipeline: a chain of transforms + a trainer algorithm
/// - Trainer: the algorithm that learns patterns (FastTree = gradient boosted trees)
/// - Model: the trained result — can predict on new inputs
/// 
/// We train a regression model:
///   Input:  AttackerArmies (float), DefenderArmies (float)
///   Output: Captured (float, 0 or 1) — model learns the probability
/// </summary>
public static class ModelTrainer
{
    // Schema for reading CSV rows
    public class BlitzRow
    {
        [LoadColumn(0)] public float AttackerArmies { get; set; }
        [LoadColumn(1)] public float DefenderArmies { get; set; }
        [LoadColumn(2)] public float Label { get; set; }  // "Captured" column → named Label directly
        [LoadColumn(3)] public float AttackerLosses { get; set; }
        [LoadColumn(4)] public float DefenderLosses { get; set; }
    }

    public static void Train(string csvPath, string modelOutputPath)
    {
        var mlContext = new MLContext(seed: 42);

        var data = mlContext.Data.LoadFromTextFile<BlitzRow>(
            csvPath, hasHeader: true, separatorChar: ',');

        var pipeline = mlContext.Transforms
            .Concatenate("Features",
                nameof(BlitzRow.AttackerArmies),
                nameof(BlitzRow.DefenderArmies))
            .Append(mlContext.Regression.Trainers.FastTree(
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 10));

        // 4. Train the model — this is where the learning happens
        //    FastTree builds 100 decision trees that together predict capture probability
        Console.WriteLine("Training blitz probability model...");
        var model = pipeline.Fit(data);

        // 5. Evaluate accuracy (optional but good practice)
        var predictions = model.Transform(data);
        var metrics = mlContext.Regression.Evaluate(predictions);
        Console.WriteLine($"  R² = {metrics.RSquared:F4} (1.0 = perfect)");
        Console.WriteLine($"  MAE = {metrics.MeanAbsoluteError:F4} (lower = better)");

        // 6. Save the trained model as a .zip file
        //    This .zip contains the learned tree structure — load it later for predictions
        mlContext.Model.Save(model, data.Schema, modelOutputPath);
        Console.WriteLine($"  Model saved to: {modelOutputPath}");
    }
}
