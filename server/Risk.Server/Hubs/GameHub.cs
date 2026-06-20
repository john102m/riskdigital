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

    public async Task EndAttack()
    {
        var state = _game.EndAttack(Context.ConnectionId);
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
        var state = _game.MoveAfterCapture(Context.ConnectionId, sourceId, targetId, armies);
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
            await Clients.Caller.SendAsync("GameStateUpdated", _game.State);
    }

    public async Task GetState()
    {
        if (_game.State is not null)
            await Clients.Caller.SendAsync("GameStateUpdated", _game.State);
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
