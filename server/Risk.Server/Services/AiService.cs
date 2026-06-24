using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Models;

namespace Risk.Server.Services;

public class AiService(GameService game, IHubContext<GameHub> hub, MlModels ml)
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
            await Delay(2500, 3000); // let turn popup display on TV

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
            await Delay(1500, 2000);
            var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
            Territory target;
            if (player.AiTier >= 2)
            {
                // Tier 2: place on front-line territory with fewest armies
                var frontLine = owned.Where(t => t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex)).ToList();
                target = (frontLine.Count > 0 ? frontLine : owned).OrderBy(t => t.Armies).First();
            }
            else
            {
                target = owned[Random.Shared.Next(owned.Count)];
            }
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

        if (player.AiTier >= 2)
        {
            // Tier 2: trade immediately if able
            // Tier 3: trade if have territory bonus card OR 4+ cards (save for strategic moment)
            var set = FindValidSet(player.Cards);
            if (set is not null && (player.AiTier < 3 || player.Cards.Count >= 4 || HasTerritoryBonusSet(state, player)))
            {
                await Delay(2000, 2500);
                game.TradeCards(connId, set);
                await hub.Clients.All.SendAsync("CardTraded", state.CurrentPlayerIndex, 0);
                await Broadcast();
            }
        }

        // Place armies
        while (player.ReinforcementsRemaining > 0)
        {
            await Delay(1500, 2000);
            var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
            Territory target;
            if (player.AiTier >= 3)
            {
                // Tier 3: strategic reinforce — continent gap territories get priority
                target = ScoreReinforceTarget(state, owned);
            }
            else if (player.AiTier >= 2)
            {
                // Tier 2: concentrate on front-line, prioritise most adjacent enemies then lowest armies
                var frontLine = owned
                    .Where(t => t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
                    .OrderByDescending(t => t.Adjacent.Count(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
                    .ThenBy(t => t.Armies)
                    .ToList();
                target = frontLine.Count > 0 ? frontLine[0] : owned[Random.Shared.Next(owned.Count)];
            }
            else
            {
                target = owned[Random.Shared.Next(owned.Count)];
            }
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
        if (player.AiTier >= 3)
        {
            await RunStrategicAttack(state, player, connId);
            return;
        }
        if (player.AiTier >= 2)
        {
            await RunAggressiveAttack(state, player, connId);
            return;
        }

        // Tier 1: 50% chance to skip, 1-3 random attacks
        if (Random.Shared.Next(2) == 0)
        {
            await EndAttack(state, player, connId);
            return;
        }

        int attacks = Random.Shared.Next(1, 4);
        for (int i = 0; i < attacks; i++)
        {
            if (!await DoRandomAttack(state, player, connId)) break;
            await Delay(2500, 3500);
        }

        await hub.Clients.All.SendAsync("AttackSelection", null, null);
        await EndAttack(state, player, connId);
    }

    private async Task RunStrategicAttack(GameState state, Player player, string connId)
    {
        int maxAttacks = 6;
        for (int i = 0; i < maxAttacks; i++)
        {
            // Attack restraint: stop if we already earned a card this turn (preserve armies)
            if (player.EarnedCardThisTurn && i > 0) break;

            var sources = state.Territories
                .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
                    && t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
                .ToList();

            if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
                sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

            if (sources.Count == 0) break;

            // Evaluate all possible (source, target) pairs using strategic scoring
            Territory? bestSource = null;
            Territory? bestTarget = null;
            float bestScore = 0;

            foreach (var src in sources)
            {
                var targets = src.Adjacent
                    .Select(a => state.Territories[a])
                    .Where(t => t.OwnerId != state.CurrentPlayerIndex)
                    .ToList();

                foreach (var tgt in targets)
                {
                    float score = ScoreAttack(state, src, tgt);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSource = src;
                        bestTarget = tgt;
                    }
                }
            }

            // Only attack if combined score > 0.4
            if (bestSource is null || bestTarget is null || bestScore < 0.4f) break;

            // Show selection
            await hub.Clients.All.SendAsync("AttackSelection", bestSource.Id, (int?)null);
            await Delay(1000, 1500);
            await hub.Clients.All.SendAsync("AttackSelection", bestSource.Id, bestTarget.Id);
            await Delay(1500, 2000);

            float captureChance = ml.PredictBlitz(bestSource.Armies, bestTarget.Armies);
            if (captureChance > 0.7f && bestSource.Armies >= 4)
            {
                // High confidence — blitz
                var (_, blitzResult) = game.Blitz(connId, bestSource.Id, bestTarget.Id);
                await hub.Clients.All.SendAsync("BlitzResult", blitzResult);
                await Broadcast();

                if (blitzResult.Captured)
                {
                    await Delay(1500, 2000);
                    int max = bestSource.Armies - 1;
                    if (max > 0)
                    {
                        game.MoveAfterCapture(connId, bestSource.Id, bestTarget.Id, max);
                        await Broadcast();
                    }
                    if (state.Phase == GamePhase.GameOver) return;
                }
            }
            else
            {
                // Medium confidence — single attack
                int dice = Math.Min(3, bestSource.Armies - 1);
                var (_, result) = game.Attack(connId, bestSource.Id, bestTarget.Id, dice);
                await hub.Clients.All.SendAsync("CombatResult", result);
                await Broadcast();

                if (result.Captured)
                {
                    await Delay(1500, 2000);
                    int max = bestSource.Armies - 1;
                    if (max > 0)
                    {
                        game.MoveAfterCapture(connId, bestSource.Id, bestTarget.Id, max);
                        await Broadcast();
                    }
                    if (state.Phase == GamePhase.GameOver) return;
                }
            }

            await Delay(2000, 3000);
        }

        await hub.Clients.All.SendAsync("AttackSelection", null, null);
        await EndAttack(state, player, connId);
    }

    private async Task RunAggressiveAttack(GameState state, Player player, string connId)
    {
        int maxAttacks = 6;
        for (int i = 0; i < maxAttacks; i++)
        {
            var sources = state.Territories
                .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
                    && t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
                .ToList();

            if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
                sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

            if (sources.Count == 0) break;

            // Pick strongest source
            var source = sources.OrderByDescending(t => t.Armies).First();
            if (source.Armies <= 2) break; // not worth attacking with 2

            // Pick weakest adjacent enemy
            var targets = source.Adjacent
                .Select(a => state.Territories[a])
                .Where(t => t.OwnerId != state.CurrentPlayerIndex)
                .OrderBy(t => t.Armies)
                .ToList();

            if (targets.Count == 0) break;
            var target = targets[0];

            // Show selection
            await hub.Clients.All.SendAsync("AttackSelection", source.Id, (int?)null);
            await Delay(1000, 1500);
            await hub.Clients.All.SendAsync("AttackSelection", source.Id, target.Id);
            await Delay(1500, 2000);

            // Blitz if 5+ armies, otherwise single attack
            if (source.Armies >= 5)
            {
                var (_, blitzResult) = game.Blitz(connId, source.Id, target.Id);
                await hub.Clients.All.SendAsync("BlitzResult", blitzResult);
                await Broadcast();

                if (blitzResult.Captured)
                {
                    await Delay(1500, 2000);
                    int max = source.Armies - 1;
                    if (max > 0)
                    {
                        game.MoveAfterCapture(connId, source.Id, target.Id, max);
                        await Broadcast();
                    }
                    if (state.Phase == GamePhase.GameOver) return;
                }
            }
            else
            {
                int dice = Math.Min(3, source.Armies - 1);
                var (_, result) = game.Attack(connId, source.Id, target.Id, dice);
                await hub.Clients.All.SendAsync("CombatResult", result);
                await Broadcast();

                if (result.Captured)
                {
                    await Delay(1500, 2000);
                    int max = source.Armies - 1;
                    if (max > 0)
                    {
                        game.MoveAfterCapture(connId, source.Id, target.Id, max);
                        await Broadcast();
                    }
                    if (state.Phase == GamePhase.GameOver) return;
                }
            }

            await Delay(2000, 3000);
        }

        await hub.Clients.All.SendAsync("AttackSelection", null, null);
        await EndAttack(state, player, connId);
    }

    private async Task<bool> DoRandomAttack(GameState state, Player player, string connId)
    {
        var sources = state.Territories
            .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
                && t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
            .ToList();

        if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
            sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

        if (sources.Count == 0) return false;

        var source = sources[Random.Shared.Next(sources.Count)];
        var targets = source.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId != state.CurrentPlayerIndex)
            .ToList();

        if (targets.Count == 0) return false;
        var target = targets[Random.Shared.Next(targets.Count)];

        await hub.Clients.All.SendAsync("AttackSelection", source.Id, (int?)null);
        await Delay(1000, 1500);
        await hub.Clients.All.SendAsync("AttackSelection", source.Id, target.Id);
        await Delay(1500, 2000);

        int dice = Math.Min(3, source.Armies - 1);
        var (_, result) = game.Attack(connId, source.Id, target.Id, dice);
        await hub.Clients.All.SendAsync("CombatResult", result);
        await Broadcast();

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
            if (state.Phase == GamePhase.GameOver) return false;
        }

        return true;
    }

    private async Task RunFortify(GameState state, Player player, string connId)
    {
        if (player.AiTier >= 3)
        {
            await RunStrategicFortify(state, player, connId);
            return;
        }
        if (player.AiTier >= 2)
        {
            await RunAggressiveFortify(state, player, connId);
            return;
        }

        // Tier 1: 50% skip
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

    private async Task RunStrategicFortify(GameState state, Player player, string connId)
    {
        await Delay(1500, 2500);

        var (source, target) = FindStrategicFortify(state);
        if (source is not null && target is not null)
        {
            int armies = source.Armies - 1;
            game.Fortify(connId, source.Id, target.Id, armies);
            await hub.Clients.All.SendAsync("FortifyMoved", state.CurrentPlayerIndex, source.Id, target.Id, armies);
            await Broadcast();
        }
        else
        {
            // Fall back to aggressive fortify (inland → front)
            await RunAggressiveFortifyLogic(state, connId);
        }

        await Delay(1000, 1500);
        game.EndTurn(connId);
        await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await Broadcast();
        TriggerIfAi();
    }

    private async Task RunAggressiveFortifyLogic(GameState state, string connId)
    {
        var inland = state.Territories
            .Where(t => t.OwnerId == state.CurrentPlayerIndex && t.Armies > 1
                && t.Adjacent.All(a => state.Territories[a].OwnerId == state.CurrentPlayerIndex))
            .OrderByDescending(t => t.Armies)
            .FirstOrDefault();

        if (inland is not null)
        {
            var frontTarget = inland.Adjacent
                .Select(a => state.Territories[a])
                .Where(t => t.OwnerId == state.CurrentPlayerIndex
                    && t.Adjacent.Any(a => state.Territories[a].OwnerId != state.CurrentPlayerIndex))
                .OrderBy(t => t.Armies)
                .FirstOrDefault();

            frontTarget ??= inland.Adjacent
                .Select(a => state.Territories[a])
                .Where(t => t.OwnerId == state.CurrentPlayerIndex)
                .FirstOrDefault();

            if (frontTarget is not null)
            {
                int armies = inland.Armies - 1;
                game.Fortify(connId, inland.Id, frontTarget.Id, armies);
                await hub.Clients.All.SendAsync("FortifyMoved", state.CurrentPlayerIndex, inland.Id, frontTarget.Id, armies);
                await Broadcast();
            }
        }
    }

    private async Task EndAttack(GameState state, Player player, string connId)
    {
        await Delay(1000, 1500);
        game.EndAttack(connId);
        await Broadcast();
    }

    private async Task RunAggressiveFortify(GameState state, Player player, string connId)
    {
        await Delay(1500, 2500);
        await RunAggressiveFortifyLogic(state, connId);
        await Delay(1000, 1500);
        game.EndTurn(connId);
        await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await Broadcast();
        TriggerIfAi();
    }

    // --- Strategic Helpers (Tier 3) ---

    /// <summary>
    /// Scores each owned territory for reinforcement priority.
    /// Weighs: continent completion proximity, border threat, chokepoint value.
    /// </summary>
    private Territory ScoreReinforceTarget(GameState state, List<Territory> owned)
    {
        var playerIndex = state.CurrentPlayerIndex;
        Territory? best = null;
        float bestScore = -1;

        foreach (var t in owned)
        {
            bool isBorder = t.Adjacent.Any(a => state.Territories[a].OwnerId != playerIndex);
            if (!isBorder) continue; // never reinforce inland

            float score = 0;

            // Threat: enemy armies adjacent
            int enemyThreat = t.Adjacent
                .Where(a => state.Territories[a].OwnerId != playerIndex)
                .Sum(a => state.Territories[a].Armies);
            score += enemyThreat * 0.5f;

            // Continent gap bonus: if this territory can attack into a continent we nearly own
            foreach (var continent in game.MapData.Continents)
            {
                int ownedInContinent = continent.Territories.Count(id => state.Territories[id].OwnerId == playerIndex);
                int total = continent.Territories.Count;
                float progress = (float)ownedInContinent / total;

                // Territory is the gateway to completing this continent
                if (progress >= 0.6f && t.Adjacent.Any(a => continent.Territories.Contains(a) && state.Territories[a].OwnerId != playerIndex))
                    score += continent.Bonus * 3f;

                // Territory is a border of a continent we fully own (protect it)
                if (ownedInContinent == total && t.Adjacent.Any(a => !continent.Territories.Contains(a) && state.Territories[a].OwnerId != playerIndex))
                    score += continent.Bonus * 2f;
            }

            // Prefer territories with fewer armies (shore up weak points)
            score += Math.Max(0, 10 - t.Armies);

            if (score > bestScore) { bestScore = score; best = t; }
        }

        return best ?? owned[Random.Shared.Next(owned.Count)];
    }

    /// <summary>
    /// Scores an attack for Tier 3 considering continent completion.
    /// Combines ML blitz probability with strategic value.
    /// </summary>
    private float ScoreAttack(GameState state, Territory source, Territory target)
    {
        var playerIndex = state.CurrentPlayerIndex;
        float mlScore = ml.PredictBlitz(source.Armies, target.Armies);

        float continentBonus = 0;
        foreach (var continent in game.MapData.Continents)
        {
            if (!continent.Territories.Contains(target.Id)) continue;
            int ownedInContinent = continent.Territories.Count(id => state.Territories[id].OwnerId == playerIndex);
            int total = continent.Territories.Count;

            // Capturing this would complete the continent
            if (ownedInContinent == total - 1)
                continentBonus = continent.Bonus * 5f;
            // Close to completing (>60%)
            else if ((float)ownedInContinent / total >= 0.6f)
                continentBonus = continent.Bonus * 2f;
        }

        // Combined score: ML probability + strategic value (normalised)
        return mlScore + (continentBonus / 20f);
    }

    /// <summary>
    /// Finds the best fortify move for Tier 3: protect continent chokepoints.
    /// </summary>
    private (Territory? Source, Territory? Target) FindStrategicFortify(GameState state)
    {
        var playerIndex = state.CurrentPlayerIndex;

        // Find continent borders we own that are under-defended
        Territory? weakestBorder = null;
        int lowestArmies = int.MaxValue;

        foreach (var continent in game.MapData.Continents)
        {
            int ownedInContinent = continent.Territories.Count(id => state.Territories[id].OwnerId == playerIndex);
            if (ownedInContinent < continent.Territories.Count) continue; // don't own this continent

            // Find border territories of this owned continent
            foreach (var tId in continent.Territories)
            {
                var t = state.Territories[tId];
                if (t.Adjacent.Any(a => !continent.Territories.Contains(a) && state.Territories[a].OwnerId != playerIndex))
                {
                    if (t.Armies < lowestArmies) { lowestArmies = t.Armies; weakestBorder = t; }
                }
            }
        }

        if (weakestBorder is null)
        {
            // No owned continents — fall back to Tier 2 logic (inland to front)
            return (null, null);
        }

        // Find adjacent owned territory with surplus armies to donate
        var donor = weakestBorder.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId == playerIndex && t.Armies > 2
                && t.Adjacent.All(a => state.Territories[a].OwnerId == playerIndex)) // donor is safe inland
            .OrderByDescending(t => t.Armies)
            .FirstOrDefault();

        return donor is not null ? (donor, weakestBorder) : (null, null);
    }

    /// <summary>
    /// Checks if player has a card matching a territory they own (gives +2 bonus on trade).
    /// </summary>
    private static bool HasTerritoryBonusSet(GameState state, Player player)
    {
        var ownedIds = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).Select(t => t.Id).ToHashSet();
        return player.Cards.Any(c => c.TerritoryId.HasValue && ownedIds.Contains(c.TerritoryId.Value));
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
