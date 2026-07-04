using Risk.Server.Models;

namespace Risk.Server.Services;

/// <summary>
/// Logs human player actions with board context for ML training.
/// Only logs non-AI players. Appends to CSV files in Data/logs/.
/// </summary>
public class ActionLogger
{
    private readonly string _logDir;
    private readonly ILogger<ActionLogger> _logger;
    private static readonly object _lock = new();
    private bool _errorLogged;

    public string LogDir => _logDir;

    public ActionLogger(IWebHostEnvironment env, ILogger<ActionLogger> logger)
    {
        _logger = logger;
        // Try app-local first, then vhost tmp, then system temp
        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, "Data", "logs"),
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "tmp", "risk-logs")),
            Path.Combine(Path.GetTempPath(), "risk-logs")
        };
        _logDir = candidates.Last(); // fallback
        foreach (var dir in candidates)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var test = Path.Combine(dir, ".write-test");
                File.WriteAllText(test, "ok");
                File.Delete(test);
                _logDir = dir;
                break;
            }
            catch { }
        }
        _logger.LogInformation("ActionLogger writing to: {Path}", _logDir);
    }

    public void LogReinforce(GameState state, int playerIndex, int territoryId)
    {
        var player = state.Players[playerIndex];
        if (player.IsAI) return;

        var t = state.Territories[territoryId];
        bool isBorder = t.Adjacent.Any(a => state.Territories[a].OwnerId != playerIndex);
        int enemyThreat = t.Adjacent.Where(a => state.Territories[a].OwnerId != playerIndex).Sum(a => state.Territories[a].Armies);
        var continent = GetContinentFor(state, territoryId);
        float continentProgress = continent.total > 0 ? (float)continent.owned / continent.total : 0;

        var line = $"{state.GameCode},{playerIndex},{territoryId},{t.Armies},{(isBorder ? 1 : 0)},{enemyThreat},{continentProgress:F2},{continent.bonus},{player.ReinforcementsRemaining},{state.TurnNumber}";
        Append("reinforce-log.csv", line, "GameId,PlayerIndex,TerritoryId,TerritoryArmies,IsBorder,EnemyThreat,ContinentProgress,ContinentBonus,TotalReinforcements,TurnNumber");
    }

    public void LogAttack(GameState state, int playerIndex, int sourceId, int targetId, bool usedBlitz)
    {
        var player = state.Players[playerIndex];
        if (player.IsAI) return;

        var source = state.Territories[sourceId];
        var target = state.Territories[targetId];
        int targetOwnerTerritoryCount = state.Territories.Count(t => t.OwnerId == target.OwnerId);
        var myCont = GetContinentFor(state, targetId, playerIndex);
        var theirCont = GetContinentFor(state, targetId, target.OwnerId);
        bool wouldComplete = myCont.owned == myCont.total - 1;

        var line = $"{state.GameCode},{playerIndex},{source.Armies},{target.Armies},{targetOwnerTerritoryCount},{theirCont.owned}/{theirCont.total},{myCont.owned}/{myCont.total},{(usedBlitz ? 1 : 0)},{(wouldComplete ? 1 : 0)},{state.TurnNumber},1";
        Append("attack-log.csv", line, "GameId,PlayerIndex,SourceArmies,TargetArmies,TargetOwnerTerritoryCount,TargetContinentProgress,MyContinentProgress,UsedBlitz,WouldCompleteCont,TurnNumber,DidAttack");
    }

    public void LogFortify(GameState state, int playerIndex, int sourceId, int targetId, int armies)
    {
        var player = state.Players[playerIndex];
        if (player.IsAI) return;

        var target = state.Territories[targetId];
        bool isBorder = target.Adjacent.Any(a => state.Territories[a].OwnerId != playerIndex);
        int enemyThreat = target.Adjacent.Where(a => state.Territories[a].OwnerId != playerIndex).Sum(a => state.Territories[a].Armies);

        var line = $"{state.GameCode},{playerIndex},{sourceId},{targetId},{armies},{(isBorder ? 1 : 0)},{enemyThreat},0";
        Append("fortify-log.csv", line, "GameId,PlayerIndex,SourceId,TargetId,ArmiesMoved,TargetIsBorder,TargetEnemyThreat,Skipped");
    }

    public void LogFortifySkip(GameState state, int playerIndex)
    {
        var player = state.Players[playerIndex];
        if (player.IsAI) return;

        var line = $"{state.GameCode},{playerIndex},-1,-1,0,0,0,1";
        Append("fortify-log.csv", line, "GameId,PlayerIndex,SourceId,TargetId,ArmiesMoved,TargetIsBorder,TargetEnemyThreat,Skipped");
    }

    private (int owned, int total, int bonus) GetContinentFor(GameState state, int territoryId, int? ownerOverride = null)
    {
        var t = state.Territories[territoryId];
        var contName = t.Continent;
        var contTerritories = state.Territories.Where(x => x.Continent == contName).ToList();
        int owner = ownerOverride ?? state.CurrentPlayerIndex;
        int owned = contTerritories.Count(x => x.OwnerId == owner);
        int bonus = contName switch
        {
            "North America" => 5, "South America" => 2, "Europe" => 5,
            "Africa" => 3, "Asia" => 7, "Australia" => 2, _ => 0
        };
        return (owned, contTerritories.Count, bonus);
    }

    private void Append(string filename, string line, string header)
    {
        try
        {
            lock (_lock)
            {
                var path = Path.Combine(_logDir, filename);
                if (!File.Exists(path))
                    File.WriteAllText(path, header + "\n");
                File.AppendAllText(path, line + "\n");
            }
        }
        catch (Exception ex)
        {
            if (!_errorLogged)
            {
                _logger.LogError(ex, "ActionLogger write failed: {Path}", _logDir);
                _errorLogged = true;
            }
        }
    }
}
