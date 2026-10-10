using Risk.Server.Models;

namespace Risk.Server.Services;

/// <summary>
/// Easy AI strategy — consolidated from old Tiers 1–4.
/// Plays like a competent beginner: attacks when strong, defends borders, no ML or mission awareness.
/// Designed for new players learning the game.
/// </summary>
public class AiEasyStrategy : IAiStrategy
{
    public float TimingMultiplier => 1.0f;

    public (Territory Source, Territory Target)? ChooseAttack(GameState state, int myIndex)
    {
        var sources = state.Territories
            .Where(t => t.OwnerId == myIndex && t.Armies > 1
                && t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
            .ToList();

        if (state.HouseRules.LockedAttackFront && state.AttackFrontIds.Count > 0)
            sources = sources.Where(t => state.AttackFrontIds.Contains(t.Id)).ToList();

        if (sources.Count == 0) return null;

        // Strongest source first
        var source = sources.OrderByDescending(t => t.Armies).First();
        if (source.Armies <= 2) return null; // not worth attacking with 2

        // Weakest adjacent enemy
        var targets = source.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId != myIndex)
            .OrderBy(t => t.Armies)
            .ToList();

        if (targets.Count == 0) return null;

        return (source, targets[0]);
    }

    public bool ShouldBlitz(Territory source, Territory target)
    {
        return source.Armies >= 5;
    }

    public bool ShouldStopAttacking(GameState state, Player player, int attackCount)
    {
        // Stop after 4–6 attacks (randomised)
        int max = 4 + Random.Shared.Next(3); // 4, 5, or 6
        return attackCount >= max;
    }

    public int CalculateMoveTroops(Territory source, Territory target, bool capturedViaBlitz)
    {
        // Move all available troops in
        return source.Armies - 1;
    }

    public Territory ChooseReinforceTarget(GameState state, List<Territory> owned, int myIndex)
    {
        // Front-line territories with most enemy neighbours, then lowest armies
        var frontLine = owned
            .Where(t => t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
            .OrderByDescending(t => t.Adjacent.Count(a => state.Territories[a].OwnerId != myIndex))
            .ThenBy(t => t.Armies)
            .ToList();

        return frontLine.Count > 0 ? frontLine[0] : owned[Random.Shared.Next(owned.Count)];
    }

    public (Territory Source, Territory Target, int Armies)? ChooseFortify(GameState state, int myIndex)
    {
        // Move armies from inland (safe) territories to the front line
        var inland = state.Territories
            .Where(t => t.OwnerId == myIndex && t.Armies > 1
                && t.Adjacent.All(a => state.Territories[a].OwnerId == myIndex))
            .OrderByDescending(t => t.Armies)
            .FirstOrDefault();

        if (inland is null) return null;

        // Find adjacent front-line territory with fewest armies
        var frontTarget = inland.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId == myIndex
                && t.Adjacent.Any(a => state.Territories[a].OwnerId != myIndex))
            .OrderBy(t => t.Armies)
            .FirstOrDefault();

        // Fallback: any adjacent owned territory
        frontTarget ??= inland.Adjacent
            .Select(a => state.Territories[a])
            .Where(t => t.OwnerId == myIndex)
            .OrderBy(t => t.Armies)
            .FirstOrDefault();

        if (frontTarget is null) return null;

        return (inland, frontTarget, inland.Armies - 1);
    }

    public bool ShouldTradeCards(Player player, GameState state)
    {
        // Trade when holding 4+ cards
        return player.Cards.Count >= 4;
    }
}
