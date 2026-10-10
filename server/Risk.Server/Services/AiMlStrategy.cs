using Risk.Server.Models;

namespace Risk.Server.Services;

/// <summary>
/// ML-driven AI strategy with personality weights, mission pursuit, continent denial,
/// elimination hunting, and human-behaviour blending (Tier 5).
/// </summary>
public class AiMlStrategy(MlModels ml, GameService game, AiPersonality personality, int tier) : IAiStrategy
{
    private readonly PersonalityWeights _w = PersonalityWeights.For(personality);

    public float TimingMultiplier => _w.TimingMultiplier;

    #region Attack

    public (Territory Source, Territory Target)? ChooseAttack(GameState state, int myIndex)
    {
        var sources = state.Territories
            .Where(t => t.OwnerId == myIndex && t.Armies > 1
                && t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
            .ToList();

        if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
            sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

        if (sources.Count == 0) return null;

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
                float score = ScoreAttack(state, src, tgt, myIndex);
                if (score > bestScore) { bestScore = score; bestSource = src; bestTarget = tgt; }
            }
        }

        // Dynamic threshold based on army preservation
        float threshold = 0.3f + (_w.ArmyPreservation * 0.3f);
        if (bestSource is null || bestTarget is null || bestScore < threshold) return null;

        return (bestSource, bestTarget);
    }

    public bool ShouldBlitz(Territory source, Territory target)
    {
        float captureChance = ml.PredictBlitz(source.Armies, target.Armies);
        return captureChance > 0.6f && source.Armies >= 4;
    }

    public bool ShouldStopAttacking(GameState state, Player player, int attackCount)
    {
        int maxAttacks = (int)(8 * _w.ExpansionSpeed);
        if (attackCount >= maxAttacks) return true;

        // Stop after earning a card if preservation is high
        if (player.EarnedCardThisTurn && attackCount > 0 && _w.ArmyPreservation > 0.5f) return true;

        return false;
    }

    public int CalculateMoveTroops(Territory source, Territory target, bool capturedViaBlitz)
    {
        if (capturedViaBlitz)
        {
            int moveArmies = (int)((source.Armies - 1) * _w.ExpansionSpeed);
            moveArmies = Math.Max(moveArmies, Math.Min(3, source.Armies - 1));
            return Math.Min(moveArmies, source.Armies - 1);
        }
        // Single attack: move all
        return source.Armies - 1;
    }

    #endregion

    #region Reinforce

    public Territory ChooseReinforceTarget(GameState state, List<Territory> owned, int myIndex)
    {
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
                score += 10f * _w.EliminationHunting;

            // Continent completion
            foreach (var continent in game.MapData.Continents)
            {
                int ownedInCont = continent.Territories.Count(id => state.Territories[id].OwnerId == myIndex);
                int total = continent.Territories.Count;
                float progress = (float)ownedInCont / total;

                if (progress >= 0.6f && t.Adjacent.Any(a => continent.Territories.Contains(a) && state.Territories[a].OwnerId != myIndex))
                    score += continent.Bonus * 3f * _w.ContinentPriority;

                if (ownedInCont == total && t.Adjacent.Any(a => !continent.Territories.Contains(a) && state.Territories[a].OwnerId != myIndex))
                    score += continent.Bonus * 2f * _w.ContinentPriority;
            }

            // Continent denial
            score += ScoreContinentDenial(state, t, myIndex) * _w.ContinentDenial;

            // Chokepoint value
            if (IsChokepoint(t))
                score += 5f;

            // Shore up weak points
            int enemyThreat = t.Adjacent.Where(a => state.Territories[a].OwnerId != myIndex).Sum(a => state.Territories[a].Armies);
            score += Math.Max(0, enemyThreat - t.Armies) * _w.ArmyPreservation;

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

    #endregion

    #region Fortify

    public (Territory Source, Territory Target, int Armies)? ChooseFortify(GameState state, int myIndex)
    {
        // Elimination hunting: fortify toward weakest player
        if (_w.EliminationHunting > 0.5f)
        {
            int weakest = FindWeakestPlayer(state, myIndex);
            var weakTerritories = state.Territories.Where(t => t.OwnerId == weakest).Select(t => t.Id).ToHashSet();

            var frontVsWeak = state.Territories
                .Where(t => t.OwnerId == myIndex && t.Adjacent.Any(a => weakTerritories.Contains(a)))
                .OrderBy(t => t.Armies)
                .FirstOrDefault();

            if (frontVsWeak is not null)
            {
                var donor = frontVsWeak.Adjacent
                    .Select(a => state.Territories[a])
                    .Where(t => t.OwnerId == myIndex && t.Armies > 2 && t.Id != frontVsWeak.Id)
                    .OrderByDescending(t => t.Armies)
                    .FirstOrDefault();

                if (donor is not null)
                    return (donor, frontVsWeak, donor.Armies - 1);
            }
        }

        // Fallback: protect continent borders
        var (source, target) = FindStrategicFortify(state, myIndex);
        if (source is not null && target is not null)
            return (source, target, source.Armies - 1);

        // Last resort: inland → front-line
        return FindAggressiveFortify(state, myIndex);
    }

    public bool ShouldTradeCards(Player player, GameState state)
    {
        if (_w.CardHoarding < 0.3f) return true; // always trade
        if (_w.CardHoarding < 0.7f && player.Cards.Count >= 4) return true;
        if (HasTerritoryBonusSet(state, player)) return true;
        return false;
    }

    #endregion

    #region Scoring

    private float ScoreAttack(GameState state, Territory source, Territory target, int myIndex)
    {
        float mlScore = ml.PredictBlitz(source.Armies, target.Armies);

        // Base: must meet ratio threshold
        float ratio = (float)source.Armies / Math.Max(1, target.Armies);
        if (ratio < _w.AttackRatioThreshold && mlScore < 0.5f) return 0;

        float score = mlScore * (1f - _w.ArmyPreservation * 0.5f);

        // Elimination hunting
        int targetOwner = target.OwnerId;
        int targetTerritoryCount = state.Territories.Count(t => t.OwnerId == targetOwner);
        if (targetTerritoryCount <= 2)
            score += (3f - targetTerritoryCount) * _w.EliminationHunting;
        else if (targetTerritoryCount <= 4)
            score += 0.5f * _w.EliminationHunting;

        // Card escalation: eliminations worth more as trade count rises
        if (targetTerritoryCount == 1)
            score += Math.Min(state.CardTradeCount * 0.1f, 1.0f) * _w.EliminationHunting;

        // Continent completion bonus
        foreach (var continent in game.MapData.Continents)
        {
            if (!continent.Territories.Contains(target.Id)) continue;
            int ownedInCont = continent.Territories.Count(id => state.Territories[id].OwnerId == myIndex);
            int total = continent.Territories.Count;
            if (ownedInCont == total - 1)
                score += continent.Bonus * 0.5f * _w.ContinentPriority;
            else if ((float)ownedInCont / total >= 0.6f)
                score += continent.Bonus * 0.2f * _w.ContinentPriority;
        }

        // Continent denial
        score += ScoreContinentDenialAttack(state, target, myIndex) * _w.ContinentDenial;

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

    private float ScoreContinentDenial(GameState state, Territory t, int myIndex)
    {
        float score = 0;
        foreach (var continent in game.MapData.Continents)
        {
            if (!continent.Territories.Contains(t.Id)) continue;
            for (int p = 0; p < state.Players.Count; p++)
            {
                if (p == myIndex || state.Players[p].IsEliminated) continue;
                int theirCount = continent.Territories.Count(id => state.Territories[id].OwnerId == p);
                int total = continent.Territories.Count;
                if (theirCount >= total - 1)
                    score += continent.Bonus * 3f;
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
            if (theirCount >= total - 1)
                score += continent.Bonus * 2f;
            else if ((float)theirCount / total >= 0.7f)
                score += continent.Bonus * 0.5f;
        }
        return score;
    }

    private static bool IsChokepoint(Territory t)
    {
        return t.Adjacent.Count >= 5 || t.Name is "Ukraine" or "Middle East" or "North Africa"
            or "Siam" or "Central America" or "East Africa";
    }

    #endregion

    #region Helpers

    private int FindWeakestPlayer(GameState state, int myIndex)
    {
        return state.Players
            .Select((p, i) => (p, i))
            .Where(x => !x.p.IsEliminated && x.i != myIndex)
            .OrderBy(x => state.Territories.Count(t => t.OwnerId == x.i))
            .First().i;
    }

    private (Territory? Source, Territory? Target) FindStrategicFortify(GameState state, int myIndex)
    {
        Territory? weakestBorder = null;
        int lowestArmies = int.MaxValue;

        foreach (var continent in game.MapData.Continents)
        {
            int ownedInContinent = continent.Territories.Count(id => state.Territories[id].OwnerId == myIndex);
            if (ownedInContinent < continent.Territories.Count) continue;

            foreach (var tId in continent.Territories)
            {
                var t = state.Territories[tId];
                if (t.Adjacent.Any(a => !continent.Territories.Contains(a) && state.Territories[a].OwnerId != myIndex))
                {
                    if (t.Armies < lowestArmies) { lowestArmies = t.Armies; weakestBorder = t; }
                }
            }
        }

        if (weakestBorder is null) return (null, null);

        var donor = weakestBorder.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId == myIndex && t.Armies > 2
                && t.Adjacent.All(a => state.Territories[a].OwnerId == myIndex))
            .OrderByDescending(t => t.Armies)
            .FirstOrDefault();

        return donor is not null ? (donor, weakestBorder) : (null, null);
    }

    private (Territory Source, Territory Target, int Armies)? FindAggressiveFortify(GameState state, int myIndex)
    {
        var inland = state.Territories
            .Where(t => t.OwnerId == myIndex && t.Armies > 1
                && t.Adjacent.All(a => state.Territories[a].OwnerId == myIndex))
            .OrderByDescending(t => t.Armies)
            .FirstOrDefault();

        if (inland is null) return null;

        var frontTarget = inland.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId == myIndex
                && t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
            .OrderBy(t => t.Armies)
            .FirstOrDefault();

        frontTarget ??= inland.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId == myIndex)
            .OrderBy(t => t.Armies)
            .FirstOrDefault();

        if (frontTarget is null) return null;

        return (inland, frontTarget, inland.Armies - 1);
    }

    private static bool HasTerritoryBonusSet(GameState state, Player player)
    {
        var ownedIds = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).Select(t => t.Id).ToHashSet();
        return player.Cards.Any(c => c.TerritoryId.HasValue && ownedIds.Contains(c.TerritoryId.Value));
    }

    #endregion
}
