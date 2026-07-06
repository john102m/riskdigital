using System.Collections.Concurrent;

namespace Risk.Server.Services;

/// <summary>
/// Manages multiple concurrent game instances. Singleton.
/// Tracks which connections belong to which games for routing.
/// </summary>
public class GameManager
{
    private readonly ConcurrentDictionary<string, GameService> _games = new();
    private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
    private readonly DiceAuditLogger? _diceAudit;

    public GameManager(DiceAuditLogger? diceAudit = null)
    {
        _diceAudit = diceAudit;
    }

    /// <summary>Creates a new game and returns (gameCode, gameService).</summary>
    public (string GameCode, GameService Game) CreateGame()
    {
        var game = new GameService(_diceAudit);
        string code;
        do { code = GenerateCode(); }
        while (!_games.TryAdd(code, game));
        return (code, game);
    }

    public GameService? GetGame(string gameCode)
    {
        _games.TryGetValue(gameCode, out var game);
        return game;
    }

    /// <summary>Gets the game associated with a connection ID.</summary>
    public GameService? GetGameByConnection(string connectionId)
    {
        if (_connectionToGame.TryGetValue(connectionId, out var code))
            return GetGame(code);
        return null;
    }

    /// <summary>Gets the game code for a connection ID.</summary>
    public string? GetGameCode(string connectionId)
    {
        _connectionToGame.TryGetValue(connectionId, out var code);
        return code;
    }

    /// <summary>Associates a connection with a game.</summary>
    public void TrackConnection(string connectionId, string gameCode)
    {
        _connectionToGame[connectionId] = gameCode;
    }

    /// <summary>Removes a connection from tracking. Returns the game code it was in.</summary>
    public string? UntrackConnection(string connectionId)
    {
        _connectionToGame.TryRemove(connectionId, out var code);
        return code;
    }

    public void RemoveGame(string gameCode)
    {
        _games.TryRemove(gameCode, out _);
        // Clean up connection mappings for this game
        var toRemove = _connectionToGame.Where(kv => kv.Value == gameCode).Select(kv => kv.Key).ToList();
        foreach (var connId in toRemove)
            _connectionToGame.TryRemove(connId, out _);
    }

    /// <summary>Returns all active games (code → game).</summary>
    public IReadOnlyDictionary<string, GameService> GetAllGames() => _games;

    /// <summary>Resets a specific game.</summary>
    public void ResetGame(string gameCode)
    {
        if (_games.TryGetValue(gameCode, out var game))
        {
            game.Reset();
            RemoveGame(gameCode);
        }
    }

    /// <summary>Resets all games.</summary>
    public void ResetAll()
    {
        foreach (var kv in _games)
            kv.Value.Reset();
        _games.Clear();
        _connectionToGame.Clear();
    }

    private static string GenerateCode() => Random.Shared.Next(1000, 9999).ToString();
}
