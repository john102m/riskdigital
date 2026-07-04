namespace Risk.Server.Services;

/// <summary>
/// Logs every individual dice roll for long-term fairness analysis.
/// Compares server PRNG vs Unity physics dice outcomes.
/// </summary>
public class DiceAuditLogger
{
    private readonly string _logPath;
    private static readonly object _lock = new();

    public string LogPath => _logPath;

    public DiceAuditLogger(ActionLogger actionLogger)
    {
        _logPath = Path.Combine(actionLogger.LogDir, "dice-audit.csv");
    }

    public void LogRolls(string source, string role, int[] values)
    {
        foreach (var value in values)
            Log(source, role, value);
    }

    private void Log(string source, string role, int value)
    {
        try
        {
            lock (_lock)
            {
                if (!File.Exists(_logPath))
                    File.WriteAllText(_logPath, "Timestamp,Source,Role,Value\n");
                File.AppendAllText(_logPath, $"{DateTime.UtcNow:o},{source},{role},{value}\n");
            }
        }
        catch { }
    }
}
