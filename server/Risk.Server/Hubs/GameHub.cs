using Microsoft.AspNetCore.SignalR;
using Risk.Server.Services;

namespace Risk.Server.Hubs;

public class GameHub : Hub
{
    private readonly GameService _game;

    public GameHub(GameService game)
    {
        _game = game;
    }

    public async Task CreateGame(string playerName)
    {
        var state = _game.CreateGame(playerName, Context.ConnectionId);
        await BroadcastState(state);
    }

    public async Task JoinGame(string gameCode, string playerName)
    {
        var state = _game.JoinGame(gameCode, playerName, Context.ConnectionId);
        await BroadcastState(state);
    }

    public async Task StartGame()
    {
        var state = _game.StartGame(Context.ConnectionId);
        await BroadcastState(state);
        if (state.HouseRules.UseMissions)
        {
            foreach (var p in state.Players.Where(p => p.Mission is not null))
                await Clients.Client(p.ConnectionId).SendAsync("MissionUpdated", p.Mission);
        }
    }

    public async Task PlaceArmy(int territoryId)
    {
        var state = _game.PlaceArmy(Context.ConnectionId, territoryId);
        await BroadcastState(state);
    }

    public async Task Reinforce(int territoryId)
    {
        var state = _game.Reinforce(Context.ConnectionId, territoryId);
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
        var (state, result) = _game.Attack(Context.ConnectionId, sourceId, targetId, diceCount);
        await Clients.All.SendAsync("CombatResult", result);
        await BroadcastState(state);
    }

    public async Task MoveAfterCapture(int sourceId, int targetId, int armies)
    {
        var (state, forcedTrade, eliminatedIndex, missionWon) = _game.MoveAfterCapture(Context.ConnectionId, sourceId, targetId, armies);
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
        var state = _game.EndTurn(Context.ConnectionId);
        await BroadcastState(state);
    }

    public async Task Fortify(int sourceId, int targetId, int armies)
    {
        var state = _game.Fortify(Context.ConnectionId, sourceId, targetId, armies);
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
        // TODO: Handle player disconnect (timeout/reconnect window)
        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastState(object state)
    {
        await Clients.All.SendAsync("GameStateUpdated", state);
    }
}
