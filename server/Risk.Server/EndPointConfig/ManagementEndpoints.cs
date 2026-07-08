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

        admin.MapGet("/games", (GameManager manager) =>
        {
            var games = manager.GetAllGames().Select(kv => new
            {
                Code = kv.Key,
                Phase = kv.Value.State?.Phase.ToString() ?? "None",
                Players = kv.Value.State?.Players.Select(p => new { p.Name, p.Colour, p.IsAI }).ToArray()
            });
            return Results.Ok(games);
        });

        admin.MapGet("/reset/{gameCode}", (string gameCode, GameManager manager, IHubContext<GameHub> hub) =>
        {
            var game = manager.GetGame(gameCode);
            if (game is null) return Results.NotFound("Game not found.");
            manager.ResetGame(gameCode);
            hub.Clients.Group(gameCode).SendAsync("GameStateUpdated", (object?)null);
            return Results.Ok($"Reset game {gameCode}");
        });

        admin.MapGet("/reset", (GameManager manager, IHubContext<GameHub> hub, bool? debug) =>
        {
            manager.ResetAll();
            hub.Clients.All.SendAsync("GameStateUpdated", (object?)null);
            return Results.Ok("Reset all games");
        });

        admin.MapGet("/testdice", async (IHubContext<GameHub> hub, string? gameCode, int? a, int? d, GameManager manager) =>
        {
            // Send to specific game group or all
            if (gameCode is not null)
                await hub.Clients.Group(gameCode).SendAsync("CombatRollRequest", new Risk.Server.Models.CombatRollRequest(0, 1, a ?? 3, d ?? 2));
            else
                await hub.Clients.All.SendAsync("CombatRollRequest", new Risk.Server.Models.CombatRollRequest(0, 1, a ?? 3, d ?? 2));
            return Results.Ok($"Sent {a ?? 3}a {d ?? 2}d");
        });

        admin.MapGet("/gameover", async (GameManager manager, IHubContext<GameHub> hub, string? gameCode) =>
        {
            // Find the game — by code or the only active game
            GameService? game = null;
            string? code = gameCode;
            if (code is not null)
                game = manager.GetGame(code);
            else
            {
                var games = manager.GetAllGames();
                if (games.Count == 1) { code = games.Keys.First(); game = games.Values.First(); }
            }
            if (game?.State is null) return Results.BadRequest("No game found");

            game.State.Phase = Risk.Server.Models.GamePhase.GameOver;
            var winnerIndex = game.State.CurrentPlayerIndex;
            var winner = game.State.Players[winnerIndex];

            await hub.Clients.Group(code!).SendAsync("MissionComplete", winnerIndex, winner.Mission?.Description ?? "Debug game over");

            var missions = game.State.Players.Select(p => new { p.Name, p.Colour, Mission = p.Mission?.Description ?? "World domination" }).ToArray();
            await hub.Clients.Group(code!).SendAsync("AllMissionsRevealed", missions);

            await hub.Clients.Group(code!).SendAsync("GameStateUpdated", game.State);
            return Results.Ok($"Game over — winner: {winner.Name}");
        });

        admin.MapGet("/missions", (GameManager manager, string? gameCode) =>
        {
            GameService? game = null;
            if (gameCode is not null)
                game = manager.GetGame(gameCode);
            else
            {
                var games = manager.GetAllGames();
                if (games.Count == 1) game = games.Values.First();
            }
            if (game?.State is null) return Results.BadRequest("No game found");
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

        admin.MapGet("/testcombat", async (int attacker, int defender, int? dice, string? gameCode, bool? human,
            GameManager manager, IHubContext<GameHub> hub, ILogger<GameManager> logger) =>
        {
            // Find game
            GameService? game = null;
            string? code = gameCode;
            if (code is not null)
                game = manager.GetGame(code);
            else
            {
                var games = manager.GetAllGames();
                if (games.Count == 1) { code = games.Keys.First(); game = games.Values.First(); }
            }
            if (game?.State is null) return Results.BadRequest("No game found");
            var state = game.State;

            if (attacker < 0 || attacker >= state.Players.Count || defender < 0 || defender >= state.Players.Count)
                return Results.BadRequest($"Player index out of range (0–{state.Players.Count - 1})");
            if (attacker == defender)
                return Results.BadRequest("Attacker and defender must be different");

            // Find any adjacent territory pair between these players (ignore army counts)
            int sourceId = -1, targetId = -1;
            foreach (var t in state.Territories.Where(t => t.OwnerId == attacker))
            {
                var adjId = t.Adjacent.FirstOrDefault(a => state.Territories.First(x => x.Id == a).OwnerId == defender);
                if (adjId != default || t.Adjacent.Any(a => state.Territories.First(x => x.Id == a).OwnerId == defender))
                {
                    sourceId = t.Id;
                    targetId = t.Adjacent.First(a => state.Territories.First(x => x.Id == a).OwnerId == defender);
                    break;
                }
            }
            if (sourceId == -1)
                return Results.BadRequest($"No adjacent territories between player {attacker} and player {defender}");

            int diceCount = dice ?? 3;
            var source = state.Territories.First(t => t.Id == sourceId);
            var target = state.Territories.First(t => t.Id == targetId);
            var attackerPlayer = state.Players[attacker];
            var connId = attackerPlayer.ConnectionId ?? "test";

            // ─── Snapshot state to restore after test ─────────────────────────
            var prevPhase = state.TurnPhase;
            var prevPlayerIndex = state.CurrentPlayerIndex;
            var prevSourceArmies = source.Armies;
            var prevTargetArmies = target.Armies;
            var prevSourceOwner = source.OwnerId;
            var prevTargetOwner = target.OwnerId;
            var prevPendingMoveSource = state.PendingMoveSource;
            var prevPendingMoveTarget = state.PendingMoveTarget;
            var prevEarnedCard = attackerPlayer.EarnedCardThisTurn;
            var prevAttackFront = state.AttackFrontIds.ToList();
            var defenderPlayer = state.Players[defender];
            var prevDefenderIsAI = defenderPlayer.IsAI;

            // ─── Force valid state for combat ─────────────────────────────────
            state.TurnPhase = Risk.Server.Models.TurnPhase.Attack;
            state.CurrentPlayerIndex = attacker;
            if (source.Armies <= diceCount)
                source.Armies = diceCount + 1; // ensure enough to attack
            if (target.Armies < 1)
                target.Armies = 2; // ensure there's something to defend
            defenderPlayer.IsAI = human == true ? prevDefenderIsAI : true; // force auto-roll unless &human=true

            logger.LogInformation("TESTCOMBAT: player {Attacker} ({AName}) → player {Defender} ({DName}), src={Src} tgt={Tgt} dice={Dice}",
                attacker, attackerPlayer.Name, defender, state.Players[defender].Name, sourceId, targetId, diceCount);

            try
            {
                // Let TVs know which territories are fighting before dice spawn
                await hub.Clients.Group(code!).SendAsync("AttackSelection", sourceId, targetId);
                await Task.Delay(300);

                object result;
                if (game.IsUnityTVConnected)
                {
                    var r = await game.AttackWithDice(hub, code!, connId, sourceId, targetId, diceCount);
                    await Task.Delay(5000); // Let TVs finish dice display before CombatResult resets arena
                    await hub.Clients.Group(code!).SendAsync("CombatResult", r.Result);
                    result = new { status = "resolved", r.Result.Captured, r.Result.AttackerDice, r.Result.DefenderDice, sourceId, targetId };
                }
                else
                {
                    var r = game.Attack(connId, sourceId, targetId, diceCount);
                    await hub.Clients.Group(code!).SendAsync("CombatResult", r.Result);
                    result = new { status = "resolved (server roll)", r.Result.Captured, r.Result.AttackerDice, r.Result.DefenderDice, sourceId, targetId };
                }

                // ─── Restore state (non-destructive) ──────────────────────────
                state.TurnPhase = prevPhase;
                state.CurrentPlayerIndex = prevPlayerIndex;
                source.Armies = prevSourceArmies;
                source.OwnerId = prevSourceOwner;
                target.Armies = prevTargetArmies;
                target.OwnerId = prevTargetOwner;
                state.PendingMoveSource = prevPendingMoveSource;
                state.PendingMoveTarget = prevPendingMoveTarget;
                attackerPlayer.EarnedCardThisTurn = prevEarnedCard;
                state.AttackFrontIds.Clear();
                state.AttackFrontIds.AddRange(prevAttackFront);
                defenderPlayer.IsAI = prevDefenderIsAI;

                // Broadcast restored state so handsets/TVs don't show stale data
                await hub.Clients.Group(code!).SendAsync("GameStateUpdated", state);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                // Restore on failure too
                game.ClearPending();
                state.TurnPhase = prevPhase;
                state.CurrentPlayerIndex = prevPlayerIndex;
                source.Armies = prevSourceArmies;
                source.OwnerId = prevSourceOwner;
                target.Armies = prevTargetArmies;
                target.OwnerId = prevTargetOwner;
                state.PendingMoveSource = prevPendingMoveSource;
                state.PendingMoveTarget = prevPendingMoveTarget;
                attackerPlayer.EarnedCardThisTurn = prevEarnedCard;
                state.AttackFrontIds.Clear();
                state.AttackFrontIds.AddRange(prevAttackFront);
                defenderPlayer.IsAI = prevDefenderIsAI;
                await hub.Clients.Group(code!).SendAsync("GameStateUpdated", state);

                logger.LogError(ex, "TESTCOMBAT failed");
                return Results.Ok(new { status = "error", message = ex.Message, stack = ex.StackTrace });
            }
        });

        admin.MapGet("/testblitz", async (int attacker, int defender, string? gameCode,
            GameManager manager, IHubContext<GameHub> hub, ILogger<GameManager> logger) =>
        {
            // Find game
            GameService? game = null;
            string? code = gameCode;
            if (code is not null)
                game = manager.GetGame(code);
            else
            {
                var games = manager.GetAllGames();
                if (games.Count == 1) { code = games.Keys.First(); game = games.Values.First(); }
            }
            if (game?.State is null) return Results.BadRequest("No game found");
            var state = game.State;

            if (attacker < 0 || attacker >= state.Players.Count || defender < 0 || defender >= state.Players.Count)
                return Results.BadRequest($"Player index out of range (0–{state.Players.Count - 1})");
            if (attacker == defender)
                return Results.BadRequest("Attacker and defender must be different");

            // Find any adjacent territory pair between these players
            int sourceId = -1, targetId = -1;
            foreach (var t in state.Territories.Where(t => t.OwnerId == attacker))
            {
                var adjEnemy = t.Adjacent.FirstOrDefault(a => state.Territories.First(x => x.Id == a).OwnerId == defender);
                if (adjEnemy != default)
                {
                    sourceId = t.Id;
                    targetId = adjEnemy;
                    break;
                }
            }
            if (sourceId == -1)
                return Results.BadRequest($"No adjacent territories between player {attacker} and player {defender}");

            var source = state.Territories.First(t => t.Id == sourceId);
            var target = state.Territories.First(t => t.Id == targetId);
            var attackerPlayer = state.Players[attacker];
            var connId = attackerPlayer.ConnectionId ?? "test";

            // ─── Snapshot state ───────────────────────────────────────────────
            var prevPhase = state.TurnPhase;
            var prevPlayerIndex = state.CurrentPlayerIndex;
            var prevSourceArmies = source.Armies;
            var prevTargetArmies = target.Armies;
            var prevSourceOwner = source.OwnerId;
            var prevTargetOwner = target.OwnerId;
            var prevPendingMoveSource = state.PendingMoveSource;
            var prevPendingMoveTarget = state.PendingMoveTarget;
            var prevEarnedCard = attackerPlayer.EarnedCardThisTurn;
            var prevAttackFront = state.AttackFrontIds.ToList();

            // ─── Force valid state for blitz ──────────────────────────────────
            state.TurnPhase = Risk.Server.Models.TurnPhase.Attack;
            state.CurrentPlayerIndex = attacker;
            if (source.Armies < 5) source.Armies = 10; // ensure enough armies for a meaningful blitz
            if (target.Armies < 3) target.Armies = 5;
            state.AttackFrontIds.Clear(); // clear locked front so blitz isn't rejected

            logger.LogInformation("TESTBLITZ: player {Attacker} ({AName}) → player {Defender} ({DName}), src={Src} tgt={Tgt}",
                attacker, attackerPlayer.Name, defender, state.Players[defender].Name, sourceId, targetId);

            try
            {
                // Let TVs know which territories are fighting (triggers camera zoom + sets source/target for blitz display)
                await hub.Clients.Group(code!).SendAsync("AttackSelection", sourceId, targetId);
                await Task.Delay(300); // brief pause for TVs to process before BlitzResult arrives

                var (_, result) = game.Blitz(connId, sourceId, targetId);
                await hub.Clients.Group(code!).SendAsync("BlitzResult", result);

                // Wait for TVs to finish the blitz display before restoring state
                // (OnStateChanged kills the arena if turnPhase leaves Attack)
                await Task.Delay(5000);

                // ─── Restore state ────────────────────────────────────────────
                state.TurnPhase = prevPhase;
                state.CurrentPlayerIndex = prevPlayerIndex;
                source.Armies = prevSourceArmies;
                source.OwnerId = prevSourceOwner;
                target.Armies = prevTargetArmies;
                target.OwnerId = prevTargetOwner;
                state.PendingMoveSource = prevPendingMoveSource;
                state.PendingMoveTarget = prevPendingMoveTarget;
                attackerPlayer.EarnedCardThisTurn = prevEarnedCard;
                state.AttackFrontIds.Clear();
                state.AttackFrontIds.AddRange(prevAttackFront);

                await hub.Clients.Group(code!).SendAsync("GameStateUpdated", state);

                return Results.Ok(new { status = "resolved", result.Captured, result.Rounds,
                    result.TotalAttackerLosses, result.TotalDefenderLosses,
                    result.FinalAttackerDice, result.FinalDefenderDice, sourceId, targetId });
            }
            catch (Exception ex)
            {
                state.TurnPhase = prevPhase;
                state.CurrentPlayerIndex = prevPlayerIndex;
                source.Armies = prevSourceArmies;
                source.OwnerId = prevSourceOwner;
                target.Armies = prevTargetArmies;
                target.OwnerId = prevTargetOwner;
                state.PendingMoveSource = prevPendingMoveSource;
                state.PendingMoveTarget = prevPendingMoveTarget;
                attackerPlayer.EarnedCardThisTurn = prevEarnedCard;
                state.AttackFrontIds.Clear();
                state.AttackFrontIds.AddRange(prevAttackFront);
                await hub.Clients.Group(code!).SendAsync("GameStateUpdated", state);

                logger.LogError(ex, "TESTBLITZ failed");
                return Results.Ok(new { status = "error", message = ex.Message, stack = ex.StackTrace });
            }
        });
    }
}
