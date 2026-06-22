using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Models;

namespace Risk.Server.Services;

public class AiService(GameService game, IHubContext<GameHub> hub)
{
    public void TriggerIfAi()
    {
        if (game.State is null) return;
        var player = game.State.Players[game.State.CurrentPlayerIndex];
        if (!player.IsAI || player.IsEliminated) return;
        _ = RunTurnAsync();
    }

    private async Task RunTurnAsync()
    {
        try
        {
            var state = game.State!;
            var player = state.Players[state.CurrentPlayerIndex];
            var connId = player.ConnectionId;

            if (state.Phase == GamePhase.InitialPlacement)
            {
                await RunPlacement(state, player, connId);
                return;
            }

            if (state.Phase == GamePhase.Playing)
            {
                await RunReinforce(state, player, connId);
                if (state.Phase == GamePhase.GameOver) return;

                await RunAttack(state, player, connId);
                if (state.Phase == GamePhase.GameOver) return;

                await RunFortify(state, player, connId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AI error: {ex.Message}");
        }
    }

    private async Task RunPlacement(GameState state, Player player, string connId)
    {
        while (player.ReinforcementsRemaining > 0 && state.Players[state.CurrentPlayerIndex].ConnectionId == connId)
        {
            await Delay(2000, 2500);
            var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
            var target = owned[Random.Shared.Next(owned.Count)];
            var idx = state.CurrentPlayerIndex;
            game.PlaceArmy(connId, target.Id);
            await hub.Clients.All.SendAsync("ArmiesPlaced", idx, target.Id, 1);
            await Broadcast();
        }

        await Delay(500);
        TriggerIfAi();
    }

    private async Task RunReinforce(GameState state, Player player, string connId)
    {
        // Trade cards if forced (5+)
        while (player.Cards.Count >= 5)
        {
            await Delay(2000, 3000);
            var set = FindValidSet(player.Cards);
            if (set is null) break;
            game.TradeCards(connId, set);
            await hub.Clients.All.SendAsync("CardTraded", state.CurrentPlayerIndex, 0);
            await Broadcast();
        }

        // Place armies one at a time
        while (player.ReinforcementsRemaining > 0)
        {
            await Delay(2000, 2500);
            var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
            var target = owned[Random.Shared.Next(owned.Count)];
            game.Reinforce(connId, target.Id);
            await hub.Clients.All.SendAsync("ArmiesPlaced", state.CurrentPlayerIndex, target.Id, 1);
            await Broadcast();
        }

        await Delay(1000, 1500);
        game.EndReinforce(connId);
        await Broadcast();
    }

    private async Task RunAttack(GameState state, Player player, string connId)
    {
        // 50% chance to not attack at all
        if (Random.Shared.Next(2) == 0)
        {
            await EndAttack(state, player, connId);
            return;
        }

        int attacks = Random.Shared.Next(1, 4); // 1-3 attacks
        for (int i = 0; i < attacks; i++)
        {
            // Find valid attack sources
            var sources = state.Territories
                .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
                    && t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
                .ToList();

            // Respect locked attack front
            if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
                sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

            if (sources.Count == 0) break;

            var source = sources[Random.Shared.Next(sources.Count)];
            var targets = source.Adjacent
                .Select(a => state.Territories[a])
                .Where(t => t.OwnerId != state.CurrentPlayerIndex)
                .ToList();

            if (targets.Count == 0) break;

            var target = targets[Random.Shared.Next(targets.Count)];

            // Show selection glow on TV
            await hub.Clients.All.SendAsync("AttackSelection", source.Id, target.Id);
            await Delay(2500, 3500);

            int dice = Math.Min(3, source.Armies - 1);
            var (_, result) = game.Attack(connId, source.Id, target.Id, dice);
            await hub.Clients.All.SendAsync("CombatResult", result);
            await Broadcast();

            // Move in after capture
            if (result.Captured)
            {
                await Delay(2000, 2500);
                int min = Math.Min(state.LastDiceCount, source.Armies - 1);
                int max = source.Armies - 1;
                if (min > 0 && max > 0)
                {
                    game.MoveAfterCapture(connId, source.Id, target.Id, min);
                    await Broadcast();
                }

                if (state.Phase == GamePhase.GameOver) return;
            }

            // Delay between attacks
            await Delay(2500, 3500);
        }

        // Clear glow
        await hub.Clients.All.SendAsync("AttackSelection", null, null);
        await EndAttack(state, player, connId);
    }

    private async Task EndAttack(GameState state, Player player, string connId)
    {
        await Delay(1000, 1500);
        game.EndAttack(connId);
        await Broadcast();
    }

    private async Task RunFortify(GameState state, Player player, string connId)
    {
        // 50% chance to skip
        if (Random.Shared.Next(2) == 0)
        {
            await Delay(1000, 1500);
            game.EndTurn(connId);
            await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
            await Broadcast();
            TriggerIfAi();
            return;
        }

        await Delay(2000, 3000);

        // Find territory with >1 army that has adjacent owned territory
        var sources = state.Territories
            .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
                && t.Adjacent.Any(a => state.Territories[a].OwnerId == state.CurrentPlayerIndex))
            .ToList();

        if (sources.Count > 0)
        {
            var source = sources[Random.Shared.Next(sources.Count)];
            var targets = source.Adjacent
                .Select(a => state.Territories[a])
                .Where(t => t.OwnerId == state.CurrentPlayerIndex)
                .ToList();

            var target = targets[Random.Shared.Next(targets.Count)];
            int armies = Random.Shared.Next(1, source.Armies);
            game.Fortify(connId, source.Id, target.Id, armies);
            await hub.Clients.All.SendAsync("FortifyMoved", state.CurrentPlayerIndex, source.Id, target.Id, armies);
            await Broadcast();
        }

        await Delay(1000, 1500);
        game.EndTurn(connId);
        await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await Broadcast();
        TriggerIfAi();
    }

    private static int[]? FindValidSet(List<Card> cards)
    {
        for (int i = 0; i < cards.Count - 2; i++)
            for (int j = i + 1; j < cards.Count - 1; j++)
                for (int k = j + 1; k < cards.Count; k++)
                {
                    var types = new[] { cards[i].Type, cards[j].Type, cards[k].Type };
                    int wilds = types.Count(t => t == CardType.Wild);
                    if (wilds >= 2) return [i, j, k];
                    if (wilds == 1) return [i, j, k];
                    var nonWild = types.Where(t => t != CardType.Wild).ToArray();
                    if (nonWild.Distinct().Count() == 1 || nonWild.Distinct().Count() == 3)
                        return [i, j, k];
                }
        return null;
    }

    private async Task Broadcast()
    {
        await hub.Clients.All.SendAsync("GameStateUpdated", game.State);
    }

    private static async Task Delay(int minMs, int maxMs)
    {
        await Task.Delay(Random.Shared.Next(minMs, maxMs));
    }

    private static async Task Delay(int ms)
    {
        await Task.Delay(ms);
    }
}
