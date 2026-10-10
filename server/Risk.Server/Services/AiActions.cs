using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Models;

namespace Risk.Server.Services;

/// <summary>
/// Stateless shared execution methods for AI turns.
/// Handles SignalR broadcasts, blitz/single attack execution, fortify, and end-turn sequencing.
/// </summary>
public class AiActions(IHubContext<GameHub> hub, GameService game, string gameCode)
{
    private IClientProxy Group => hub.Clients.Group(gameCode);

    public async Task ShowSelection(int? sourceId, int? targetId, float timingMultiplier)
    {
        await Group.SendAsync("AttackSelection", sourceId, targetId);
        int baseDelay = targetId is null ? 1000 : 1500;
        int maxDelay = targetId is null ? 1500 : 2000;
        await Delay((int)(baseDelay * timingMultiplier), (int)(maxDelay * timingMultiplier));
    }

    public async Task ClearSelection()
    {
        await Group.SendAsync("AttackSelection", null, null);
    }

    public async Task<bool> DoBlitz(string connId, Territory source, Territory target, int moveTroops, float timingMultiplier)
    {
        var state = game.State!;
        var player = state.Players[state.CurrentPlayerIndex];

        var (_, blitzResult) = game.Blitz(connId, source.Id, target.Id);
        await Group.SendAsync("BlitzResult", blitzResult);
        await Broadcast();

        if (blitzResult.Captured)
        {
            await Delay((int)(1500 * timingMultiplier), (int)(2000 * timingMultiplier));
            int armies = Math.Min(moveTroops, source.Armies - 1);
            if (armies > 0)
            {
                var (_, _, _, missionWon, _) = game.MoveAfterCapture(connId, source.Id, target.Id, armies);
                if (missionWon)
                    await Group.SendAsync("MissionComplete", state.CurrentPlayerIndex, player.Mission?.Description);
                await Broadcast();
            }
            if (state.Phase == GamePhase.GameOver) return true;
        }

        await Delay(2500, 3500); // let blitz display clear on Unity board
        return state.Phase == GamePhase.GameOver;
    }

    public async Task<bool> DoSingleAttack(string connId, Territory source, Territory target, int moveTroops, float timingMultiplier)
    {
        var state = game.State!;
        var player = state.Players[state.CurrentPlayerIndex];

        int dice = Math.Min(3, source.Armies - 1);
        var (_, result) = await game.AttackWithDice(hub, gameCode, connId, source.Id, target.Id, dice);
        await Group.SendAsync("CombatResult", result);
        await Broadcast();

        if (result.Captured)
        {
            await Delay((int)(1500 * timingMultiplier), (int)(2000 * timingMultiplier));
            int armies = Math.Min(moveTroops, source.Armies - 1);
            if (armies > 0)
            {
                var (_, _, _, missionWon, _) = game.MoveAfterCapture(connId, source.Id, target.Id, armies);
                if (missionWon)
                    await Group.SendAsync("MissionComplete", state.CurrentPlayerIndex, player.Mission?.Description);
                await Broadcast();
            }
            if (state.Phase == GamePhase.GameOver) return true;
        }

        return state.Phase == GamePhase.GameOver;
    }

    public async Task DoFortify(string connId, int sourceId, int targetId, int armies, int myIndex)
    {
        game.Fortify(connId, sourceId, targetId, armies);
        await Group.SendAsync("FortifyMoved", myIndex, sourceId, targetId, armies);
        await Broadcast();
    }

    public async Task DoEndAttack(string connId, float timingMultiplier)
    {
        await Delay((int)(1000 * timingMultiplier), (int)(1500 * timingMultiplier));
        game.EndAttack(connId);
        await Broadcast();
    }

    public async Task DoEndTurn(string connId, float timingMultiplier)
    {
        await Delay((int)(1000 * timingMultiplier), (int)(1500 * timingMultiplier));
        game.EndTurn(connId);
        var state = game.State!;
        await Group.SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await Broadcast();
    }

    public async Task Broadcast()
    {
        await Group.SendAsync("GameStateUpdated", game.State);
    }

    public List<Territory> GetValidSources(GameState state, int myIndex)
    {
        var sources = state.Territories
            .Where(t => t.OwnerId == myIndex && t.Armies > 1
                && t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
            .ToList();

        if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
            sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

        return sources;
    }

    public static async Task Delay(int minMs, int maxMs)
    {
        await Task.Delay(Random.Shared.Next(minMs, maxMs));
    }

    public static async Task Delay(int ms)
    {
        await Task.Delay(ms);
    }
}
