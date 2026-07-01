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
            try
            {
                var state = game.State;
                if (state is null || state.Phase != GamePhase.Playing) return;
                var player = state.Players[state.CurrentPlayerIndex];
                if (!player.IsAI) return; // safety — don't advance a human's turn

                var connId = player.ConnectionId;

                // Force through remaining phases so game doesn't freeze
                if (state.TurnPhase == TurnPhase.Reinforce)
                {
                    // Dump remaining armies on random owned territory
                    var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
                    while (player.ReinforcementsRemaining > 0 && owned.Count > 0)
                        game.Reinforce(connId, owned[Random.Shared.Next(owned.Count)].Id);
                    game.EndReinforce(connId);
                }
                if (state.TurnPhase == TurnPhase.Attack)
                    game.EndAttack(connId);
                if (state.TurnPhase == TurnPhase.Fortify)
                    game.EndTurn(connId);

                await Broadcast();
                await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
                TriggerIfAi();
            }
            catch { /* last resort — at least we tried */ }
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

        if (player.AiTier >= 4)
        {
            var w = PersonalityWeights.For(player.Personality ?? AiPersonality.Opportunist);
            // Trade based on CardHoarding weight
            var set = FindValidSet(player.Cards);
            if (set is not null && (w.CardHoarding < 0.3f || (w.CardHoarding < 0.7f && player.Cards.Count >= 4) || HasTerritoryBonusSet(state, player)))
            {
                await Delay((int)(2000 * w.TimingMultiplier), (int)(2500 * w.TimingMultiplier));
                game.TradeCards(connId, set);
                await hub.Clients.All.SendAsync("CardTraded", state.CurrentPlayerIndex, 0);
                await Broadcast();
            }
        }
        else if (player.AiTier >= 2)
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
            var w = player.AiTier >= 4 ? PersonalityWeights.For(player.Personality ?? AiPersonality.Opportunist) : null;
            await Delay(w is not null ? (int)(1500 * w.TimingMultiplier) : 1500, w is not null ? (int)(2000 * w.TimingMultiplier) : 2000);
            var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
            Territory target;
            if (player.AiTier >= 4)
            {
                target = ScoreTier4ReinforceTarget(state, owned, w!);
            }
            else if (player.AiTier >= 3)
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
        if (player.AiTier >= 4)
        {
            await RunTier4Attack(state, player, connId);
            return;
        }
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
                var (_, result) = await game.AttackWithDice(hub, connId, bestSource.Id, bestTarget.Id, dice);
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
                var (_, result) = await game.AttackWithDice(hub, connId, source.Id, target.Id, dice);
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
        var (_, result) = await game.AttackWithDice(hub, connId, source.Id, target.Id, dice);
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
        if (player.AiTier >= 4)
        {
            await RunTier4Fortify(state, player, connId);
            return;
        }
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

    // --- Tier 4: Enhanced Heuristics + Personality ---

    private async Task RunTier4Attack(GameState state, Player player, string connId)
    {
        var w = PersonalityWeights.For(player.Personality ?? AiPersonality.Opportunist);
        int myIndex = state.CurrentPlayerIndex;
        int maxAttacks = (int)(8 * w.ExpansionSpeed);

        for (int i = 0; i < maxAttacks; i++)
        {
            // Stop after earning a card if preservation is high
            if (player.EarnedCardThisTurn && i > 0 && w.ArmyPreservation > 0.5f) break;

            var sources = state.Territories
                .Where(t => t.OwnerId == myIndex && t.Armies > 1
                    && t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
                .ToList();

            if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
                sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

            if (sources.Count == 0) break;

            Territory? bestSource = null;
            Territory? bestTarget = null;
            float bestScore = 0;

            foreach (var src in sources)
            {
                var targets = src.Adjacent
                    .Select(a => state.Territories[a])
                    .Where(t => t.OwnerId != myIndex)
                    .ToList();

                foreach (var tgt in targets)
                {
                    float score = ScoreTier4Attack(state, src, tgt, w, myIndex, player.AiTier);
                    if (score > bestScore) { bestScore = score; bestSource = src; bestTarget = tgt; }
                }
            }

            // Threshold based on army preservation
            float threshold = 0.3f + (w.ArmyPreservation * 0.3f);
            if (bestSource is null || bestTarget is null || bestScore < threshold) break;

            // Show selection
            await hub.Clients.All.SendAsync("AttackSelection", bestSource.Id, (int?)null);
            await Delay((int)(1000 * w.TimingMultiplier), (int)(1500 * w.TimingMultiplier));
            await hub.Clients.All.SendAsync("AttackSelection", bestSource.Id, bestTarget.Id);
            await Delay((int)(1500 * w.TimingMultiplier), (int)(2000 * w.TimingMultiplier));

            float captureChance = ml.PredictBlitz(bestSource.Armies, bestTarget.Armies);
            if (captureChance > 0.6f && bestSource.Armies >= 4)
            {
                var (_, blitzResult) = game.Blitz(connId, bestSource.Id, bestTarget.Id);
                await hub.Clients.All.SendAsync("BlitzResult", blitzResult);
                await Broadcast();

                if (blitzResult.Captured)
                {
                    await Delay((int)(1500 * w.TimingMultiplier), (int)(2000 * w.TimingMultiplier));
                    int moveArmies = (int)((bestSource.Armies - 1) * w.ExpansionSpeed);
                    moveArmies = Math.Max(moveArmies, Math.Min(3, bestSource.Armies - 1));
                    moveArmies = Math.Min(moveArmies, bestSource.Armies - 1);
                    if (moveArmies > 0)
                    {
                        game.MoveAfterCapture(connId, bestSource.Id, bestTarget.Id, moveArmies);
                        await Broadcast();
                    }
                    if (state.Phase == GamePhase.GameOver) return;
                }
            }
            else
            {
                int dice = Math.Min(3, bestSource.Armies - 1);
                var (_, result) = await game.AttackWithDice(hub, connId, bestSource.Id, bestTarget.Id, dice);
                await hub.Clients.All.SendAsync("CombatResult", result);
                await Broadcast();

                if (result.Captured)
                {
                    await Delay((int)(1500 * w.TimingMultiplier), (int)(2000 * w.TimingMultiplier));
                    int max = bestSource.Armies - 1;
                    if (max > 0)
                    {
                        game.MoveAfterCapture(connId, bestSource.Id, bestTarget.Id, max);
                        await Broadcast();
                    }
                    if (state.Phase == GamePhase.GameOver) return;
                }
            }

            await Delay((int)(2000 * w.TimingMultiplier), (int)(3000 * w.TimingMultiplier));
        }

        await hub.Clients.All.SendAsync("AttackSelection", null, null);
        await EndAttack(state, player, connId);
    }

    private async Task RunTier4Fortify(GameState state, Player player, string connId)
    {
        var w = PersonalityWeights.For(player.Personality ?? AiPersonality.Opportunist);
        int myIndex = state.CurrentPlayerIndex;
        await Delay((int)(1500 * w.TimingMultiplier), (int)(2500 * w.TimingMultiplier));

        // Opportunist: fortify toward weakest player's territories
        if (w.EliminationHunting > 0.5f)
        {
            int weakest = FindWeakestPlayer(state, myIndex);
            var weakTerritories = state.Territories.Where(t => t.OwnerId == weakest).Select(t => t.Id).ToHashSet();

            // Find owned territory adjacent to weakest player with lowest armies
            var frontVsWeak = state.Territories
                .Where(t => t.OwnerId == myIndex && t.Adjacent.Any(a => weakTerritories.Contains(a)))
                .OrderBy(t => t.Armies)
                .FirstOrDefault();

            if (frontVsWeak is not null)
            {
                // Find adjacent owned territory with surplus to donate
                var donor = frontVsWeak.Adjacent
                    .Select(a => state.Territories[a])
                    .Where(t => t.OwnerId == myIndex && t.Armies > 2 && t.Id != frontVsWeak.Id)
                    .OrderByDescending(t => t.Armies)
                    .FirstOrDefault();

                if (donor is not null)
                {
                    int armies = donor.Armies - 1;
                    game.Fortify(connId, donor.Id, frontVsWeak.Id, armies);
                    await hub.Clients.All.SendAsync("FortifyMoved", myIndex, donor.Id, frontVsWeak.Id, armies);
                    await Broadcast();
                    await Delay(1000, 1500);
                    game.EndTurn(connId);
                    await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
                    await Broadcast();
                    TriggerIfAi();
                    return;
                }
            }
        }

        // Fallback: strategic fortify (protect continent borders)
        var (source, target) = FindStrategicFortify(state);
        if (source is not null && target is not null)
        {
            int armies = source.Armies - 1;
            game.Fortify(connId, source.Id, target.Id, armies);
            await hub.Clients.All.SendAsync("FortifyMoved", myIndex, source.Id, target.Id, armies);
            await Broadcast();
        }
        else
        {
            await RunAggressiveFortifyLogic(state, connId);
        }

        await Delay(1000, 1500);
        game.EndTurn(connId);
        await hub.Clients.All.SendAsync("TurnStarted", state.CurrentPlayerIndex);
        await Broadcast();
        TriggerIfAi();
    }

    private Territory ScoreTier4ReinforceTarget(GameState state, List<Territory> owned, PersonalityWeights w)
    {
        int myIndex = state.CurrentPlayerIndex;
        int weakest = FindWeakestPlayer(state, myIndex);
        var weakTerritories = state.Territories.Where(t => t.OwnerId == weakest).Select(t => t.Id).ToHashSet();

        Territory? best = null;
        float bestScore = -1;

        foreach (var t in owned)
        {
            bool isBorder = t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex);
            if (!isBorder) continue;

            float score = 0;

            // Adjacent to weakest player (elimination hunting)
            if (t.Adjacent.Any(a => weakTerritories.Contains(a)))
                score += 10f * w.EliminationHunting;

            // Continent completion (gateway to finishing a continent)
            foreach (var continent in game.MapData.Continents)
            {
                int ownedInCont = continent.Territories.Count(id => state.Territories[id].OwnerId == myIndex);
                int total = continent.Territories.Count;
                float progress = (float)ownedInCont / total;

                if (progress >= 0.6f && t.Adjacent.Any(a => continent.Territories.Contains(a) && state.Territories[a].OwnerId != myIndex))
                    score += continent.Bonus * 3f * w.ContinentPriority;

                if (ownedInCont == total && t.Adjacent.Any(a => !continent.Territories.Contains(a) && state.Territories[a].OwnerId != myIndex))
                    score += continent.Bonus * 2f * w.ContinentPriority;
            }

            // Continent denial (block opponent near completion)
            score += ScoreContinentDenial(state, t, myIndex) * w.ContinentDenial;

            // Chokepoint value
            if (IsChokepoint(t))
                score += 5f;

            // Shore up weak points
            int enemyThreat = t.Adjacent.Where(a => state.Territories[a].OwnerId != myIndex).Sum(a => state.Territories[a].Armies);
            score += Math.Max(0, enemyThreat - t.Armies) * w.ArmyPreservation;

            // Mission pursuit
            var mission = state.Players[myIndex].Mission;
            if (mission is not null)
            {
                if (mission.Type == MissionType.ContinentConquest && mission.RequiredContinents is not null
                    && mission.RequiredContinents.Contains(t.Continent))
                    score += 5f;
                else if (mission.Type == MissionType.Elimination
                    && t.Adjacent.Any(a => state.Territories[a].OwnerId == mission.TargetPlayerIndex))
                    score += 8f;
                else if (mission.Type == MissionType.TerritoryCount && mission.MinArmiesPerTerritory >= 2
                    && t.Armies < 2)
                    score += 3f;
            }

            if (score > bestScore) { bestScore = score; best = t; }
        }

        return best ?? owned[Random.Shared.Next(owned.Count)];
    }

    private float ScoreTier4Attack(GameState state, Territory source, Territory target, PersonalityWeights w, int myIndex, int tier = 4)
    {
        float mlScore = ml.PredictBlitz(source.Armies, target.Armies);

        // Base: must meet ratio threshold
        float ratio = (float)source.Armies / Math.Max(1, target.Armies);
        if (ratio < w.AttackRatioThreshold && mlScore < 0.5f) return 0;

        float score = mlScore * (1f - w.ArmyPreservation * 0.5f);

        // Elimination hunting: bonus for attacking nearly-dead players
        int targetOwner = target.OwnerId;
        int targetTerritoryCount = state.Territories.Count(t => t.OwnerId == targetOwner);
        if (targetTerritoryCount <= 2)
            score += (3f - targetTerritoryCount) * w.EliminationHunting;
        else if (targetTerritoryCount <= 4)
            score += 0.5f * w.EliminationHunting;

        // Card escalation: eliminations worth more as trade count rises
        if (targetTerritoryCount == 1)
            score += Math.Min(state.CardTradeCount * 0.1f, 1.0f) * w.EliminationHunting;

        // Continent completion bonus
        foreach (var continent in game.MapData.Continents)
        {
            if (!continent.Territories.Contains(target.Id)) continue;
            int ownedInCont = continent.Territories.Count(id => state.Territories[id].OwnerId == myIndex);
            int total = continent.Territories.Count;
            if (ownedInCont == total - 1)
                score += continent.Bonus * 0.5f * w.ContinentPriority;
            else if ((float)ownedInCont / total >= 0.6f)
                score += continent.Bonus * 0.2f * w.ContinentPriority;
        }

        // Continent denial: block opponent from completing
        score += ScoreContinentDenialAttack(state, target, myIndex) * w.ContinentDenial;

        // Chokepoint value
        if (IsChokepoint(target))
            score += 0.3f;

        // Mission pursuit
        var mission = state.Players[myIndex].Mission;
        if (mission is not null)
        {
            if (mission.Type == MissionType.ContinentConquest && mission.RequiredContinents is not null
                && mission.RequiredContinents.Contains(target.Continent))
                score += 0.4f;
            else if (mission.Type == MissionType.Elimination && target.OwnerId == mission.TargetPlayerIndex)
                score += 0.5f;
            else if (mission.Type == MissionType.TerritoryCount)
                score += 0.1f;
        }

        // Tier 5: blend learned human behaviour
        if (tier >= 5)
        {
            float humanScore = ml.PredictHumanAttack(source.Armies, target.Armies, targetTerritoryCount, 1f, 0f);
            score = score * 0.7f + humanScore * 0.3f;
        }

        return score;
    }

    // --- Tier 4 Heuristic Helpers ---

    private int FindWeakestPlayer(GameState state, int myIndex)
    {
        return state.Players
            .Select((p, i) => (p, i))
            .Where(x => !x.p.IsEliminated && x.i != myIndex)
            .OrderBy(x => state.Territories.Count(t => t.OwnerId == x.i))
            .First().i;
    }

    private float ScoreContinentDenial(GameState state, Territory t, int myIndex)
    {
        float score = 0;
        foreach (var continent in game.MapData.Continents)
        {
            if (!continent.Territories.Contains(t.Id)) continue;
            // Check if any opponent is close to owning this continent
            for (int p = 0; p < state.Players.Count; p++)
            {
                if (p == myIndex || state.Players[p].IsEliminated) continue;
                int theirCount = continent.Territories.Count(id => state.Territories[id].OwnerId == p);
                int total = continent.Territories.Count;
                if (theirCount >= total - 1)
                    score += continent.Bonus * 3f; // they're 1 away — critical block
                else if (theirCount >= total - 2)
                    score += continent.Bonus * 1f;
            }
        }
        return score;
    }

    private float ScoreContinentDenialAttack(GameState state, Territory target, int myIndex)
    {
        float score = 0;
        int targetOwner = target.OwnerId;
        foreach (var continent in game.MapData.Continents)
        {
            if (!continent.Territories.Contains(target.Id)) continue;
            int theirCount = continent.Territories.Count(id => state.Territories[id].OwnerId == targetOwner);
            int total = continent.Territories.Count;
            // Taking this territory blocks them from completing
            if (theirCount >= total - 1)
                score += continent.Bonus * 2f;
            else if ((float)theirCount / total >= 0.7f)
                score += continent.Bonus * 0.5f;
        }
        return score;
    }

    private static bool IsChokepoint(Territory t)
    {
        // Territories with high adjacency that gate multiple continents
        return t.Adjacent.Count >= 5 || t.Name is "Ukraine" or "Middle East" or "North Africa"
            or "Siam" or "Central America" or "East Africa";
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
        float score = mlScore + (continentBonus / 20f);

        // Mission pursuit
        var mission = state.Players[playerIndex].Mission;
        if (mission is not null)
        {
            if (mission.Type == MissionType.ContinentConquest && mission.RequiredContinents is not null
                && mission.RequiredContinents.Contains(target.Continent))
                score += 0.3f;
            else if (mission.Type == MissionType.Elimination && target.OwnerId == mission.TargetPlayerIndex)
                score += 0.4f;
            else if (mission.Type == MissionType.TerritoryCount)
                score += 0.1f;
        }

        return score;
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
