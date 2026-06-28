using System.IO.Compression;
using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Services;

namespace Risk.Server.EndPointConfig;

public static class ManagementEndpoints
{
    public static void MapManagementEndpoints(this WebApplication app)
    {

        app.MapGet("/board", (IWebHostEnvironment env) =>
            Results.File(Path.Combine(env.WebRootPath, "tv.html"), "text/html"));

        app.MapGet("/guide", (IWebHostEnvironment env) =>
            Results.File(Path.Combine(env.WebRootPath, "guide.html"), "text/html"));

        var admin = app.MapGroup("/admin");
        admin.MapGet("/reset", (GameService game, IHubContext<GameHub> hub, bool? debug) =>
        {
            game.DebugMode = debug ?? false;
            game.Reset();
            hub.Clients.All.SendAsync("GameStateUpdated", (object?)null);
            return Results.Ok(game.DebugMode ? "Reset (debug mode — reduced armies)" : "Reset");
        });

        admin.MapGet("/gameover", (GameService game, IHubContext<GameHub> hub) =>
        {
            if (game.State is null) return Results.BadRequest("No game");
            game.State.Phase = Risk.Server.Models.GamePhase.GameOver;
            hub.Clients.All.SendAsync("GameStateUpdated", game.State);
            return Results.Ok($"Game over — winner: {game.State.Players[game.State.CurrentPlayerIndex].Name}");
        });

        admin.MapGet("/missions", (GameService game) =>
        {
            if (game.State is null) return Results.BadRequest("No game");
            var missions = game.State.Players.Select((p, i) => new { Player = p.Name, Colour = p.Colour, Mission = p.Mission?.Description ?? "none", Fallback = p.Mission?.FallenBackToWorldDomination ?? false });
            return Results.Ok(missions);
        });

        admin.MapGet("/train", (IWebHostEnvironment env, MlModels ml, ActionLogger actionLogger) =>
        {
            try
            {
                var modelsDir = Path.Combine(Path.GetDirectoryName(actionLogger.LogDir)!, "risk-models");
                Directory.CreateDirectory(modelsDir);
                var results = new List<string>();

                // Blitz model (simulated data)
                try
                {
                    var csvPath = Path.Combine(modelsDir, "blitz-data.csv");
                    var modelPath = Path.Combine(modelsDir, "blitz-model.zip");
                    Risk.Server.Training.BlitzSimulator.GenerateData(csvPath);
                    Risk.Server.Training.ModelTrainer.Train(csvPath, modelPath);
                    ml.Load(modelPath);
                    results.Add($"Blitz model trained and loaded");
                }
                catch (Exception ex) { results.Add($"Blitz training error: {ex.Message}"); }

                // Behaviour models (from player logs)
                var logsDir = actionLogger.LogDir;
                if (!Directory.Exists(logsDir) || Directory.GetFiles(logsDir, "*.csv").Length == 0)
                {
                    results.Add("No game data yet — play some games first for behaviour models");
                }
                else
                {
                    try
                    {
                        var reinforceCsv = Path.Combine(logsDir, "reinforce-log.csv");
                        if (File.Exists(reinforceCsv))
                            results.Add(Risk.Server.Training.BehaviourTrainer.TrainReinforce(reinforceCsv, Path.Combine(modelsDir, "reinforce-behaviour.zip")));
                        else
                            results.Add("No reinforce log found");
                    }
                    catch (Exception ex) { results.Add($"Reinforce training error: {ex.Message}"); }

                    try
                    {
                        var attackCsv = Path.Combine(logsDir, "attack-log.csv");
                        if (File.Exists(attackCsv))
                            results.Add(Risk.Server.Training.BehaviourTrainer.TrainAttack(attackCsv, Path.Combine(modelsDir, "attack-behaviour.zip")));
                        else
                            results.Add("No attack log found");
                    }
                    catch (Exception ex) { results.Add($"Attack training error: {ex.Message}"); }

                    try
                    {
                        var fortifyCsv = Path.Combine(logsDir, "fortify-log.csv");
                        if (File.Exists(fortifyCsv))
                            results.Add(Risk.Server.Training.BehaviourTrainer.TrainFortify(fortifyCsv, Path.Combine(modelsDir, "fortify-behaviour.zip")));
                        else
                            results.Add("No fortify log found");
                    }
                    catch (Exception ex) { results.Add($"Fortify training error: {ex.Message}"); }

                    try { ml.LoadBehaviourModels(modelsDir); }
                    catch (Exception ex) { results.Add($"Model load error: {ex.Message}"); }
                }

                return Results.Ok(string.Join("\n", results));
            }
            catch (Exception ex)
            {
                return Results.Ok($"Unexpected error: {ex}");
            }
        });

        admin.MapGet("/logs-status", (IWebHostEnvironment env, ActionLogger actionLogger) =>
        {
            var activeDir = actionLogger.LogDir;
            var exists = Directory.Exists(activeDir);
            var files = exists ? Directory.GetFiles(activeDir).Select(f => new { Name = Path.GetFileName(f), Size = new FileInfo(f).Length }) : [];
            bool writable = false;
            string? error = null;
            try
            {
                Directory.CreateDirectory(activeDir);
                var test = Path.Combine(activeDir, ".write-test");
                File.WriteAllText(test, "ok");
                File.Delete(test);
                writable = true;
            }
            catch (Exception ex) { error = ex.Message; }

            // Test alternative paths
            var altPaths = new[] {
                Path.Combine(env.ContentRootPath, "Data", "logs"),
                Path.Combine(env.ContentRootPath, "..", "tmp", "risk-logs"),
                @"D:\Inetpub\vhosts\spooch.co.uk\tmp\risk-logs"
            };
            var altResults = altPaths.Select(p => {
                try { Directory.CreateDirectory(p); File.WriteAllText(Path.Combine(p, ".test"), "ok"); File.Delete(Path.Combine(p, ".test")); return new { path = p, writable = true, error = (string?)null }; }
                catch (Exception ex) { return new { path = p, writable = false, error = ex.Message }; }
            });

            return Results.Ok(new { logDir = activeDir, exists, writable, error, files, altResults });
        });



        admin.MapGet("/logs-download", (ActionLogger actionLogger) =>
        {
            var dir = actionLogger.LogDir;
            if (!Directory.Exists(dir)) return Results.NotFound("No logs directory");
            var csvs = Directory.GetFiles(dir, "*.csv");
            if (csvs.Length == 0) return Results.NotFound("No log files");
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var csv in csvs)
                    zip.CreateEntryFromFile(csv, Path.GetFileName(csv));
            }
            ms.Position = 0;
            return Results.File(ms, "application/zip", "risk-logs.zip");
        });

        admin.MapGet("/ml-status", (MlModels ml) =>
        {
            return Results.Ok(new
            {
                blitzModel = ml.IsLoaded,
                behaviourModels = ml.BehaviourModelsLoaded,
                
                sampleBlitz = $"8v3 = {ml.PredictBlitz(8, 3):F2}, 4v6 = {ml.PredictBlitz(4, 6):F2}",
                sampleReinforce = $"border+threat = {ml.PredictHumanReinforce(3, 1, 12, 0.75f, 5):F2}, interior = {ml.PredictHumanReinforce(1, 0, 0, 0.25f, 2):F2}",
                sampleAttack = $"8v3 = {ml.PredictHumanAttack(8, 3, 10, 0, 0):F2}, 3v8 = {ml.PredictHumanAttack(3, 8, 10, 0, 0):F2}"
            });
        });

       admin.MapPost("/logs-upload", async (HttpRequest request, ActionLogger actionLogger) =>
        {
            var dir = actionLogger.LogDir;
            Directory.CreateDirectory(dir);
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            int count = 0;
            foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".csv")))
            {
                var target = Path.Combine(dir, entry.Name);
                // Append to existing rather than overwrite
                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                var content = await reader.ReadToEndAsync();
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                // Skip header if file already exists
                bool exists = File.Exists(target);
                var toWrite = exists ? lines.Skip(1) : lines;
                File.AppendAllLines(target, toWrite);
                count++;
            }
            return Results.Ok($"Uploaded {count} CSV files to {dir}");
        });

        admin.MapGet("/app-log", (Risk.Server.Services.RingBufferLogger ring) =>
        {
            return Results.Text(string.Join("\n", ring.GetLines()), "text/plain");
        });
    }
}
