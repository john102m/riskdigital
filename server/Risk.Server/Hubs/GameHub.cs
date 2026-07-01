using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using Risk.Server.Services;

namespace Risk.Server.Hubs;

public class GameHub : Hub
{
    private readonly GameService _game;
    private readonly AiService _ai;
    private readonly ActionLogger _log;
    private readonly MlModels _ml;
    private readonly ILogger<GameHub> _logger;
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(GameService game, AiService ai, ActionLogger log, MlModels ml, ILogger<GameHub> logger, IHubContext<GameHub> hubContext)
    {
        _game = game;
        _ai = ai;
        _log = log;
        _ml = ml;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task GetLobbyStatus()
    {
        var status = _game.GetLobbyStatus();
        await Clients.Caller.SendAsync("LobbyStatus", status);
    }

    public async Task CreateGame(string playerName, int colourIndex = 0, int avatarIndex = 0)
    {
        var state = _game.CreateGame(playerName, Context.ConnectionId, colourIndex, avatarIndex);
        await BroadcastState(state);
        await BroadcastLobbyStatus();
    }

    public async Task JoinGame(string gameCode, string playerName, int colourIndex = 0, int avatarIndex = 0)
    {
        var state = _game.JoinGame(gameCode, playerName, Context.ConnectionId, colourIndex, avatarIndex);
        await BroadcastState(state);
        await BroadcastLobbyStatus();
    }

    public async Task AddAI(int tier = 2, string? personality = null)
    {
        var state = _game.AddAiPlayer(Context.ConnectionId, tier, personality);
        await BroadcastState(state);
        await BroadcastLobbyStatus();
    }

    public async Task RemoveAI(int playerIndex)
    {
        var state = _game.RemoveAI(Context.ConnectionId, playerIndex);
        await BroadcastState(state);
        await BroadcastLobbyStatus();
    }

    public async Task StartGame()
    {
        var state = _game.StartGame(Context.ConnectionId);
        await BroadcastState(state);
        if (state.HouseRules.UseMissions)
        {
            foreach (var p in state.Players.Where(p => p.Mission is not null && !p.IsAI))
                await Clients.Client(p.ConnectionId).SendAsync("MissionUpdated", p.Mission);
        }
        _ai.TriggerIfAi();
    }

    public async Task PlaceArmy(int territoryId, int count = 1)
    {
        var playerIndex = _game.State!.CurrentPlayerIndex;
        var (state, placed) = _game.PlaceArmy(Context.ConnectionId, territoryId, count);
        await Clients.All.SendAsync("ArmiesPlaced", playerIndex, territoryId, placed);
        await BroadcastState(state);
        _ai.TriggerIfAi();
    }

    public async Task Reinforce(int territoryId, int count = 1)
    {
        var (state, placed) = _game.Reinforce(Context.ConnectionId, territoryId, count);
        _log.LogReinforce(state, state.CurrentPlayerIndex, territoryId);
        await Clients.All.SendAsync("ArmiesPlaced", state.CurrentPlayerIndex, territoryId, placed);
        if (state.HouseRules.UseMissions && _game.CheckMissionComplete(state.CurrentPlayerIndex))
        {
            state.Phase = GamePhase.GameOver;
            await Clients.All.SendAsync("MissionComplete", state.CurrentPlayerIndex, state.Players[state.CurrentPlayerIndex].Mission?.Description);
        }
        await BroadcastState(state);
    }

    public async Task EndReinforce()
    {
        var state = _game.EndReinforce(Context.ConnectionId);
        await BroadcastState(state);
    }

    public async Task TradeCards(int[] cardIndices)
    {
        var (state, armies, bonusIds) = _game.TradeCards(Context.ConnectionId, cardIndices);
        await Clients.All.SendAsync("CardTraded", state.CurrentPlayerIndex, armies);
        await Clients.Caller.SendAsync("CardsUpdated", state.Players.First(p => p.ConnectionId == Context.ConnectionId).Cards);
        await BroadcastState(state);
    }

    public async Task EndAttack()
    {
        var state = _game.EndAttack(Context.ConnectionId);
        var player = state.Players.First(p => p.ConnectionId == Context.ConnectionId);
        await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
        await BroadcastState(state);
    }

    public async Task Attack(int sourceId, int targetId, int diceCount)
    {
        _log.LogAttack(_game.State!, _game.State!.CurrentPlayerIndex, sourceId, targetId, false);

        var (state, result) = await _game.AttackWithDice(_hubContext, Context.ConnectionId, sourceId, targetId, diceCount);

        await Clients.All.SendAsync("CombatResult", result);
        await BroadcastState(state);
    }

    public async Task RollDice(int diceCount)
    {
        await _game.PlayerRoll(_hubContext, Context.ConnectionId, diceCount);
    }

    public async Task Blitz(int sourceId, int targetId)
    {
        _log.LogAttack(_game.State!, _game.State!.CurrentPlayerIndex, sourceId, targetId, true);
        var (state, result) = _game.Blitz(Context.ConnectionId, sourceId, targetId);
        await Clients.All.SendAsync("BlitzResult", result);
        await BroadcastState(state);
    }

    public async Task MoveAfterCapture(int sourceId, int targetId, int armies)
    {
        var (state, forcedTrade, eliminatedIndex, missionWon) = _game.MoveAfterCapture(Context.ConnectionId, sourceId, targetId, armies);
        await Clients.All.SendAsync("TroopsMovedIn", state.CurrentPlayerIndex, sourceId, targetId, armies);
        if (eliminatedIndex >= 0)
            await Clients.All.SendAsync("PlayerEliminated", eliminatedIndex, state.CurrentPlayerIndex);
        if (missionWon)
            await Clients.All.SendAsync("MissionComplete", state.CurrentPlayerIndex, state.Players[state.CurrentPlayerIndex].Mission?.Description);
        if (forcedTrade)
            await Clients.Caller.SendAsync("ForcedTradeRequired", state.Players.First(p => p.ConnectionId == Context.ConnectionId).Cards);
        await BroadcastState(state);
    }

    public async Task EndTurn()
    {
        if (_game.State?.TurnPhase == TurnPhase.Fortify)
            _log.LogFortifySkip(_game.State, _game.State.CurrentPlayerIndex);
        var state = _game.EndTurn(Context.ConnectionId);
        await Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await BroadcastState(state);
        _ai.TriggerIfAi();
    }

    public async Task Fortify(int sourceId, int targetId, int armies)
    {
        var state = _game.Fortify(Context.ConnectionId, sourceId, targetId, armies);
        _log.LogFortify(state, state.CurrentPlayerIndex, sourceId, targetId, armies);
        await Clients.All.SendAsync("FortifyMoved", state.CurrentPlayerIndex, sourceId, targetId, armies);
        if (state.HouseRules.UseMissions && _game.CheckMissionComplete(state.CurrentPlayerIndex))
        {
            state.Phase = GamePhase.GameOver;
            await Clients.All.SendAsync("MissionComplete", state.CurrentPlayerIndex, state.Players[state.CurrentPlayerIndex].Mission?.Description);
        }
        await BroadcastState(state);
    }

    public async Task Rejoin(string playerName)
    {
        _game.Rejoin(playerName, Context.ConnectionId);
        if (_game.State is not null)
        {
            await Clients.Caller.SendAsync("GameStateUpdated", _game.State);
            var player = _game.State.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player is not null)
            {
                await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
                if (player.Mission is not null)
                    await Clients.Caller.SendAsync("MissionUpdated", player.Mission);

                // Re-send RollPrompt if this player is the pending defender
                var pending = _game.GetPending();
                if (pending != null && !pending.DefenderRoll.Task.IsCompleted)
                {
                    var playerIndex = _game.State.Players.IndexOf(player);
                    if (playerIndex == pending.DefenderPlayerIndex)
                        await Clients.Caller.SendAsync("RollPrompt",
                            new RollPrompt("defender", pending.DefenderDiceCount, pending.DefenderDiceCount, pending.SourceId, pending.TargetId, player.Name));
                }

                // Re-send ForcedTradeRequired if player has 5+ cards and it's their reinforce turn
                if (_game.State.Phase == GamePhase.Playing
                    && (_game.State.TurnPhase == TurnPhase.Reinforce || _game.State.TurnPhase == TurnPhase.Attack)
                    && _game.State.Players[_game.State.CurrentPlayerIndex] == player
                    && player.Cards.Count >= 5)
                {
                    await Clients.Caller.SendAsync("ForcedTradeRequired", player.Cards);
                }
            }
        }
    }

    public async Task GetState()
    {
        if (_game.State is not null)
        {
            await Clients.Caller.SendAsync("GameStateUpdated", _game.State);
            var player = _game.State.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player is not null)
            {
                await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
                if (player.Mission is not null)
                    await Clients.Caller.SendAsync("MissionUpdated", player.Mission);
            }
        }
    }

    public async Task GetMission()
    {
        var player = _game.State?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (player?.Mission is not null)
            await Clients.Caller.SendAsync("MissionUpdated", player.Mission);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _game.UnregisterTV(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SelectAttack(int? sourceId, int? targetId)
    {
        await Clients.Others.SendAsync("AttackSelection", sourceId, targetId);
    }

    public Task RegisterAsTV()
    {
        _game.RegisterAsTV(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public void SubmitDiceResult(int[] attackerDice, int[] defenderDice)
    {
        _game.SubmitDiceResult(attackerDice, defenderDice);
    }

    private async Task BroadcastState(object state)
    {
        await Clients.All.SendAsync("GameStateUpdated", state);
        if (state is GameState gs && gs.Phase == GamePhase.GameOver)
            _ = Task.Run(() => RetrainModels());
    }

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

    private async Task BroadcastLobbyStatus()
    {
        var status = _game.GetLobbyStatus();
        await Clients.All.SendAsync("LobbyStatus", status);
    }
}
