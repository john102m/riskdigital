using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using Risk.Server.Services;

namespace Risk.Server.Hubs;

public class GameHub : Hub
{
    private readonly GameManager _manager;
    private readonly AiService _ai;
    private readonly ActionLogger _log;
    private readonly MlModels _ml;
    private readonly ILogger<GameHub> _logger;
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(GameManager manager, AiService ai, ActionLogger log, MlModels ml, ILogger<GameHub> logger, IHubContext<GameHub> hubContext)
    {
        _manager = manager;
        _ai = ai;
        _log = log;
        _ml = ml;
        _logger = logger;
        _hubContext = hubContext;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private (GameService Game, string GameCode) GetCallerGame()
    {
        var code = _manager.GetGameCode(Context.ConnectionId);
        if (code is null) throw new HubException("Not in a game.");
        var game = _manager.GetGame(code);
        if (game is null) throw new HubException("Game not found.");
        return (game, code);
    }

    private IClientProxy GameGroup(string gameCode) => Clients.Group(gameCode);

    private async Task BroadcastState(GameState state, string gameCode)
    {
        _logger.LogInformation("BroadcastState: code={Code} phase={Phase}", gameCode, state.Phase);
        await GameGroup(gameCode).SendAsync("GameStateUpdated", state);
        if (state.Phase == GamePhase.GameOver)
        {
            _logger.LogInformation("GameOver broadcast to group {Code}", gameCode);
            var missions = state.Players.Select(p => new { p.Name, p.Colour, Mission = p.Mission?.Description ?? "World domination" }).ToArray();
            await GameGroup(gameCode).SendAsync("AllMissionsRevealed", missions);
            _ = Task.Run(() => RetrainModels());
        }
    }

    private async Task BroadcastLobbyStatus(string gameCode, GameService game)
    {
        var status = game.GetLobbyStatus();
        await GameGroup(gameCode).SendAsync("LobbyStatus", status);
    }

    // ─── Lobby ───────────────────────────────────────────────────────────────

    public async Task GetLobbyStatus()
    {
        // If caller is in a game, return that game's status
        var code = _manager.GetGameCode(Context.ConnectionId);
        if (code is not null)
        {
            var game = _manager.GetGame(code);
            if (game is not null)
            {
                await Clients.Caller.SendAsync("LobbyStatus", game.GetLobbyStatus());
                return;
            }
        }
        // Not in a game — return no-game status
        await Clients.Caller.SendAsync("LobbyStatus", new { gameExists = false });
    }

    public async Task CreateGame(string playerName, int colourIndex = 0, int avatarIndex = 0)
    {
        var (gameCode, game) = _manager.CreateGame();
        var state = game.CreateGame(playerName, Context.ConnectionId, colourIndex, avatarIndex, gameCode);

        // Track connection and join SignalR group
        _manager.TrackConnection(Context.ConnectionId, gameCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);

        await BroadcastState(state, gameCode);
        await BroadcastLobbyStatus(gameCode, game);
        var createdPlayer = state.Players[0];
        await GameGroup(gameCode).SendAsync("PlayerJoined", createdPlayer.Name, createdPlayer.Colour);
    }

    public async Task JoinGame(string gameCode, string playerName, int colourIndex = 0, int avatarIndex = 0)
    {
        var game = _manager.GetGame(gameCode) ?? throw new HubException("Game not found.");
        var state = game.JoinGame(gameCode, playerName, Context.ConnectionId, colourIndex, avatarIndex);

        _manager.TrackConnection(Context.ConnectionId, gameCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);

        await BroadcastState(state, gameCode);
        await BroadcastLobbyStatus(gameCode, game);
        var joinedPlayer = state.Players[^1];
        await GameGroup(gameCode).SendAsync("PlayerJoined", joinedPlayer.Name, joinedPlayer.Colour);
    }

    public async Task AddAI(int tier = 2, string? personality = null)
    {
        var (game, gameCode) = GetCallerGame();
        var state = game.AddAiPlayer(Context.ConnectionId, tier, personality);
        await BroadcastState(state, gameCode);
        await BroadcastLobbyStatus(gameCode, game);
        var aiPlayer = state.Players[^1];
        await GameGroup(gameCode).SendAsync("PlayerJoined", aiPlayer.Name, aiPlayer.Colour);
    }

    public async Task RemoveAI(int playerIndex)
    {
        var (game, gameCode) = GetCallerGame();
        var state = game.RemoveAI(Context.ConnectionId, playerIndex);
        await BroadcastState(state, gameCode);
        await BroadcastLobbyStatus(gameCode, game);
    }

    public async Task StartGame(string placementMode = "Auto")
    {
        var (game, gameCode) = GetCallerGame();
        var mode = placementMode switch
        {
            "FreeForAll" or "Free" => PlacementMode.FreeForAll,
            "Manual" => PlacementMode.Manual,
            _ => PlacementMode.Auto
        };
        var state = game.StartGame(Context.ConnectionId, mode);
        await BroadcastState(state, gameCode);
        if (state.HouseRules.UseMissions)
        {
            foreach (var p in state.Players.Where(p => p.Mission is not null && !p.IsAI))
                await Clients.Client(p.ConnectionId).SendAsync("MissionUpdated", p.Mission);
        }
        _ai.TriggerIfAi(game, gameCode);
    }

    // ─── Placement ───────────────────────────────────────────────────────────

    public async Task PlaceArmy(int territoryId, int count = 1)
    {
        var (game, gameCode) = GetCallerGame();
        var playerIndex = game.State!.CurrentPlayerIndex;
        var (state, placed) = game.PlaceArmy(Context.ConnectionId, territoryId, count);
        await GameGroup(gameCode).SendAsync("ArmiesPlaced", playerIndex, territoryId, placed);
        await BroadcastState(state, gameCode);
        _ai.TriggerIfAi(game, gameCode);
    }

    // ─── Reinforce ───────────────────────────────────────────────────────────

    public async Task Reinforce(int territoryId, int count = 1)
    {
        var (game, gameCode) = GetCallerGame();
        var (state, placed) = game.Reinforce(Context.ConnectionId, territoryId, count);
        _log.LogReinforce(state, state.CurrentPlayerIndex, territoryId);
        await GameGroup(gameCode).SendAsync("ArmiesPlaced", state.CurrentPlayerIndex, territoryId, placed);
        if (state.HouseRules.UseMissions && game.CheckMissionComplete(state.CurrentPlayerIndex))
        {
            state.Phase = GamePhase.GameOver;
            await GameGroup(gameCode).SendAsync("MissionComplete", state.CurrentPlayerIndex, state.Players[state.CurrentPlayerIndex].Mission?.Description);
        }
        await BroadcastState(state, gameCode);
    }

    public async Task EndReinforce()
    {
        var (game, gameCode) = GetCallerGame();
        var state = game.EndReinforce(Context.ConnectionId);
        await BroadcastState(state, gameCode);
    }

    public async Task TradeCards(int[] cardIndices)
    {
        var (game, gameCode) = GetCallerGame();
        var (state, armies, bonusIds) = game.TradeCards(Context.ConnectionId, cardIndices);
        await GameGroup(gameCode).SendAsync("CardTraded", state.CurrentPlayerIndex, armies);
        await Clients.Caller.SendAsync("CardsUpdated", state.Players.First(p => p.ConnectionId == Context.ConnectionId).Cards);
        await BroadcastState(state, gameCode);
    }

    // ─── Attack ──────────────────────────────────────────────────────────────

    public async Task Attack(int sourceId, int targetId, int diceCount)
    {
        var (game, gameCode) = GetCallerGame();
        _log.LogAttack(game.State!, game.State!.CurrentPlayerIndex, sourceId, targetId, false);

        _logger.LogInformation("DICE: Attack started src={Src} tgt={Tgt} dice={Dice}", sourceId, targetId, diceCount);
        await GameGroup(gameCode).SendAsync("CombatStarted");
        var (state, result) = await game.AttackWithDice(_hubContext, gameCode, Context.ConnectionId, sourceId, targetId, diceCount);

        _logger.LogInformation("DICE: AttackWithDice returned, broadcasting CombatResult");
        await GameGroup(gameCode).SendAsync("CombatResult", result);
        await BroadcastState(state, gameCode);
        await GameGroup(gameCode).SendAsync("CombatResolved");
    }

    public async Task RollDice(int diceCount)
    {
        var (game, gameCode) = GetCallerGame();
        await game.PlayerRoll(_hubContext, gameCode, Context.ConnectionId, diceCount);
    }

    public async Task Blitz(int sourceId, int targetId)
    {
        var (game, gameCode) = GetCallerGame();
        _log.LogAttack(game.State!, game.State!.CurrentPlayerIndex, sourceId, targetId, true);
        await GameGroup(gameCode).SendAsync("CombatStarted");
        var (state, result) = game.Blitz(Context.ConnectionId, sourceId, targetId);
        await GameGroup(gameCode).SendAsync("BlitzResult", result);
        await BroadcastState(state, gameCode);
        await GameGroup(gameCode).SendAsync("CombatResolved");
    }

    public async Task EndAttack()
    {
        var (game, gameCode) = GetCallerGame();
        var state = game.EndAttack(Context.ConnectionId);
        var player = state.Players.First(p => p.ConnectionId == Context.ConnectionId);
        await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
        await BroadcastState(state, gameCode);
    }

    public async Task MoveAfterCapture(int sourceId, int targetId, int armies)
    {
        var (game, gameCode) = GetCallerGame();
        var (state, forcedTrade, eliminatedIndex, missionWon, fallbackPlayers) = game.MoveAfterCapture(Context.ConnectionId, sourceId, targetId, armies);
        await GameGroup(gameCode).SendAsync("TroopsMovedIn", state.CurrentPlayerIndex, sourceId, targetId, armies);
        if (eliminatedIndex >= 0)
            await GameGroup(gameCode).SendAsync("PlayerEliminated", eliminatedIndex, state.CurrentPlayerIndex);
        if (missionWon)
            await GameGroup(gameCode).SendAsync("MissionComplete", state.CurrentPlayerIndex, state.Players[state.CurrentPlayerIndex].Mission?.Description);
        if (forcedTrade)
            await Clients.Caller.SendAsync("ForcedTradeRequired", state.Players.First(p => p.ConnectionId == Context.ConnectionId).Cards);

        foreach (var pi in fallbackPlayers)
        {
            var p = state.Players[pi];
            if (!p.IsAI && p.ConnectionId is not null)
                await Clients.Client(p.ConnectionId).SendAsync("MissionUpdated", p.Mission);
        }

        await BroadcastState(state, gameCode);
    }

    // ─── Fortify ─────────────────────────────────────────────────────────────

    public async Task Fortify(int sourceId, int targetId, int armies)
    {
        var (game, gameCode) = GetCallerGame();
        var state = game.Fortify(Context.ConnectionId, sourceId, targetId, armies);
        _log.LogFortify(state, state.CurrentPlayerIndex, sourceId, targetId, armies);
        await GameGroup(gameCode).SendAsync("FortifyMoved", state.CurrentPlayerIndex, sourceId, targetId, armies);
        if (state.HouseRules.UseMissions && game.CheckMissionComplete(state.CurrentPlayerIndex))
        {
            state.Phase = GamePhase.GameOver;
            await GameGroup(gameCode).SendAsync("MissionComplete", state.CurrentPlayerIndex, state.Players[state.CurrentPlayerIndex].Mission?.Description);
        }
        await BroadcastState(state, gameCode);
    }

    public async Task EndTurn()
    {
        var (game, gameCode) = GetCallerGame();
        if (game.State?.TurnPhase == TurnPhase.Fortify)
            _log.LogFortifySkip(game.State, game.State.CurrentPlayerIndex);
        var state = game.EndTurn(Context.ConnectionId);
        await Task.Delay(1000); // breathing room for fortify animation to clear
        await GameGroup(gameCode).SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await BroadcastState(state, gameCode);
        _ai.TriggerIfAi(game, gameCode);
    }

    // ─── Reconnect / State ───────────────────────────────────────────────────

    public async Task Rejoin(string playerName)
    {
        // Find which game this player was in by scanning all games
        foreach (var (code, game) in _manager.GetAllGames())
        {
            var player = game.State?.Players.FirstOrDefault(p => p.Name == playerName);
            if (player is not null)
            {
                game.Rejoin(playerName, Context.ConnectionId);
                _manager.TrackConnection(Context.ConnectionId, code);
                await Groups.AddToGroupAsync(Context.ConnectionId, code);

                await Clients.Caller.SendAsync("GameStateUpdated", game.State);
                if (player.Cards is not null)
                    await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
                if (player.Mission is not null)
                    await Clients.Caller.SendAsync("MissionUpdated", player.Mission);

                // Re-send RollPrompt if this player is the pending defender
                var pending = game.GetPending();
                if (pending != null && !pending.DefenderRoll.Task.IsCompleted)
                {
                    var playerIndex = game.State!.Players.IndexOf(player);
                    if (playerIndex == pending.DefenderPlayerIndex)
                        await Clients.Caller.SendAsync("RollPrompt",
                            new RollPrompt("defender", pending.DefenderDiceCount, pending.DefenderDiceCount, pending.SourceId, pending.TargetId, player.Name));
                }

                // Re-send ForcedTradeRequired if player has 5+ cards
                if (game.State!.Phase == GamePhase.Playing
                    && (game.State.TurnPhase == TurnPhase.Reinforce || game.State.TurnPhase == TurnPhase.Attack)
                    && game.State.Players[game.State.CurrentPlayerIndex] == player
                    && player.Cards.Count >= 5)
                {
                    await Clients.Caller.SendAsync("ForcedTradeRequired", player.Cards);
                }
                return;
            }
        }
    }

    public async Task GetState()
    {
        var code = _manager.GetGameCode(Context.ConnectionId);
        if (code is null) return;
        var game = _manager.GetGame(code);
        if (game?.State is null) return;

        await Clients.Caller.SendAsync("GameStateUpdated", game.State);
        var player = game.State.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player is not null)
        {
            await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
            if (player.Mission is not null)
                await Clients.Caller.SendAsync("MissionUpdated", player.Mission);
        }
    }

    public async Task GetMission()
    {
        var (game, _) = GetCallerGame();
        var player = game.State?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player?.Mission is not null)
            await Clients.Caller.SendAsync("MissionUpdated", player.Mission);
    }

    // ─── TV Registration ─────────────────────────────────────────────────────

    public async Task RegisterAsTV(string gameCode)
    {
        await RegisterAsTVInternal(gameCode, null, null);
    }

    // Called by Unity with no args (backward compat) — auto-detect game
    public async Task RegisterTV()
    {
        await RegisterAsTVInternal("", null, null);
    }

    /// <summary>Register as TV with household assignment (multi-household mode).</summary>
    public async Task RegisterAsTVWithHousehold(string gameCode, string householdId, int[] playerIndices)
    {
        await RegisterAsTVInternal(gameCode, householdId, playerIndices);
    }

    private async Task RegisterAsTVInternal(string gameCode, string? householdId, int[]? playerIndices)
    {
        try
        {
            string? code = null;
            if (!string.IsNullOrEmpty(gameCode))
            {
                var game = _manager.GetGame(gameCode) ?? throw new HubException("Game not found.");
                code = gameCode;
            }
            else
            {
                var games = _manager.GetAllGames();
                _logger.LogInformation("RegisterAsTV: {Count} games active", games.Count);
                if (games.Count == 1)
                    code = games.Keys.First();
                else if (games.Count == 0)
                    return; // No games yet — will pick up on next poll
                else
                    throw new HubException("Multiple games active — specify a game code.");
            }

            var g = _manager.GetGame(code!)!;
            g.RegisterAsTV(Context.ConnectionId, householdId, playerIndices);
            _manager.TrackConnection(Context.ConnectionId, code!);
            await Groups.AddToGroupAsync(Context.ConnectionId, code!);
            _logger.LogInformation("RegisterAsTV: success, code={Code}, household={Household}", code, householdId ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegisterAsTV failed");
            throw;
        }
    }

    /// <summary>Legacy: TV submits both attacker and defender dice (single TV mode).</summary>
    public Task SubmitDiceResult(int[] attackerDice, int[] defenderDice)
    {
        var game = _manager.GetGameByConnection(Context.ConnectionId);
        if (game == null) return Task.CompletedTask;

        game.SubmitDiceResult(attackerDice, defenderDice);
        return Task.CompletedTask;
        // Broadcasts (AttackerDiceResult / DefenderDiceResult) are handled by AttackWithDice
        // after both dice sets are known — correct shape, correct timing, serves all TVs.
    }

    /// <summary>Multi-household: TV submits only its own dice (attacker OR defender).</summary>
    public Task SubmitRolledDice(string role, int[] dice)
    {
        var game = _manager.GetGameByConnection(Context.ConnectionId);
        var gameCode = _manager.GetGameCode(Context.ConnectionId);
        if (game == null || gameCode == null) { _logger.LogInformation("DICE: SubmitRolledDice({Role}) game/code null!", role); return Task.CompletedTask; }

        var pending = game.GetPending();
        if (pending == null) { _logger.LogInformation("DICE: SubmitRolledDice({Role}) pending is null (timed out?)", role); return Task.CompletedTask; }

        _logger.LogInformation("DICE: SubmitRolledDice({Role}): [{Values}]", role, string.Join(",", dice));

        if (role == "attacker")
            pending.SubmitAttackerDice(dice);
        else if (role == "defender")
            pending.SubmitDefenderDice(dice);

        return Task.CompletedTask;
    }

    public async Task GetActiveGames()
    {
        var games = _manager.GetAllGames().Select(kv => new
        {
            code = kv.Key,
            phase = kv.Value.State?.Phase.ToString() ?? "Lobby",
            playerCount = kv.Value.State?.Players.Count ?? 0
        }).ToArray();
        await Clients.Caller.SendAsync("ActiveGames", games);
    }

    public async Task SelectAttack(int? sourceId, int? targetId)
    {
        var code = _manager.GetGameCode(Context.ConnectionId);
        if (code is not null)
            await Clients.OthersInGroup(code).SendAsync("AttackSelection", sourceId, targetId);
    }

    // ─── Disconnect ──────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var code = _manager.GetGameCode(Context.ConnectionId);
        if (code is not null)
        {
            var game = _manager.GetGame(code);
            game?.UnregisterTV(Context.ConnectionId);
        }
        _manager.UntrackConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ─── ML Retrain ──────────────────────────────────────────────────────────

    private void RetrainModels()
    {
        try
        {
            var logsDir = _log.LogDir;
            var modelsDir = Path.Combine(Path.GetDirectoryName(logsDir)!, "risk-models");
            Directory.CreateDirectory(modelsDir);

            var reinforceCsv = Path.Combine(logsDir, "reinforce-log.csv");
            if (File.Exists(reinforceCsv))
                _logger.LogInformation("Retrain: {Result}", Training.BehaviourTrainer.TrainReinforce(reinforceCsv, Path.Combine(modelsDir, "reinforce-behaviour.zip")));

            var attackCsv = Path.Combine(logsDir, "attack-log.csv");
            if (File.Exists(attackCsv))
                _logger.LogInformation("Retrain: {Result}", Training.BehaviourTrainer.TrainAttack(attackCsv, Path.Combine(modelsDir, "attack-behaviour.zip")));

            var fortifyCsv = Path.Combine(logsDir, "fortify-log.csv");
            if (File.Exists(fortifyCsv))
                _logger.LogInformation("Retrain: {Result}", Training.BehaviourTrainer.TrainFortify(fortifyCsv, Path.Combine(modelsDir, "fortify-behaviour.zip")));

            _ml.LoadBehaviourModels(modelsDir);
            _logger.LogInformation("Retrain: models reloaded");
        }
        catch (Exception ex) { _logger.LogError(ex, "Retrain failed"); }
    }
}
