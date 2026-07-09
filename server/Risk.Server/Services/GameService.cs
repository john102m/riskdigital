using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Risk.Server.Hubs;
using Risk.Server.Models;
using System.Text.Json;

namespace Risk.Server.Services;

/// <summary>
/// Game state singleton — the single source of truth for all game logic.
/// Split across partial files:
///   GameService.cs         — Fields, constructor, lobby/setup, private helpers
///   GameService.Combat.cs  — Attack, Blitz, ResolveCombat, MoveAfterCapture, Unity dice delegation
///   GameService.Turn.cs    — TradeCards, Reinforce, EndReinforce, EndTurn, Fortify
/// </summary>
public partial class GameService
{
    private static readonly string[] PlayerColours = ["#E53E3E", "#3182CE", "#38A169", "#D69E2E", "#805AD5", "#DD6B20"];
    private static readonly int[] StartingArmies = [0, 0, 40, 35, 30, 25, 20];
    private static readonly int[] DebugArmies =   [0, 0, 23, 16, 15, 12, 10];
    private static readonly string[] AiNames = ["Bot Alice", "Bot Bob", "Bot Carol", "Bot Dave", "Bot Eve"];
    private static readonly int[] FemaleAvatars = [0, 1, 2, 3, 4, 5];
    private static readonly int[] MaleAvatars = [6, 7, 8];

    public bool DebugMode { get; set; }

    private readonly TerritoryData _territoryData;
    private readonly DiceAuditLogger? _diceAudit;
    private readonly ILogger<GameService> _logger;
    private GameState? _state;
    private readonly List<TVRegistration> _registeredTVs = new();
    private PendingCombat? _pending;

    public bool IsUnityTVConnected => _registeredTVs.Count > 0;
    public PendingCombat? GetPending() => _pending;
    public void ClearPending() => _pending = null;
    public GameState? State => _state;
    public TerritoryData MapData => _territoryData;

    public GameService(ILogger<GameService> logger, DiceAuditLogger? diceAudit = null)
    {
        _logger = logger;
        _diceAudit = diceAudit;
        var json = File.ReadAllText("Data/territories.json");
        _territoryData = JsonSerializer.Deserialize<TerritoryData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    #region Unity TV

    /// <summary>TV registration with optional household assignment.</summary>
    public record TVRegistration(string ConnectionId, string? HouseholdId, int[]? PlayerIndices);

    /// <summary>Register a TV. If no householdId/playerIndices, it's a single-TV setup (backward compat).</summary>
    public void RegisterAsTV(string connectionId, string? householdId = null, int[]? playerIndices = null)
    {
        // Remove any existing registration for this connection
        _registeredTVs.RemoveAll(t => t.ConnectionId == connectionId);
        _registeredTVs.Add(new TVRegistration(connectionId, householdId, playerIndices));
    }

    /// <summary>Get the TV connection that owns a given player (for dice routing).</summary>
    public string? GetTVForPlayer(int playerIndex)
    {
        _logger.LogInformation("[TV] GetTVForPlayer({PlayerIndex}): count={Count}, TVs=[{TVs}]", playerIndex, _registeredTVs.Count, string.Join("; ", _registeredTVs.Select(t => $"{t.HouseholdId ?? "none"}:[{(t.PlayerIndices != null ? string.Join(",", t.PlayerIndices) : "null")}]")));

        // If only one TV registered (or no households configured), it gets everything
        if (_registeredTVs.Count == 1) return _registeredTVs[0].ConnectionId;
        if (_registeredTVs.Count == 0) return null;

        // Look for a TV that claims this player
        var match = _registeredTVs.FirstOrDefault(t => t.PlayerIndices?.Contains(playerIndex) == true);
        if (match != null)
        {
            _logger.LogInformation("[TV] → matched {Household}", match.HouseholdId);
            return match.ConnectionId;
        }

        // No match — fall back to first TV (shouldn't happen if configured correctly)
        _logger.LogInformation("[TV] → NO MATCH, falling back to first TV ({Household})", _registeredTVs[0].HouseholdId);
        return _registeredTVs[0].ConnectionId;
    }

    /// <summary>Get all registered TV connection IDs.</summary>
    public IReadOnlyList<TVRegistration> GetRegisteredTVs() => _registeredTVs;

    public void UnregisterTV(string connectionId)
    {
        var removed = _registeredTVs.RemoveAll(t => t.ConnectionId == connectionId);
        if (removed > 0)
        {
            // Force-fail any pending dice result so server falls back immediately
            _pending?.DiceResult.TrySetCanceled();
        }
    }

    public void SubmitDiceResult(int[] attackerDice, int[] defenderDice)
    {
        _diceAudit?.LogRolls("unity", "attacker", attackerDice);
        _diceAudit?.LogRolls("unity", "defender", defenderDice);
        _pending?.SubmitDiceResult(attackerDice, defenderDice);
    }

    #endregion

    #region Lobby & Setup

    public object GetLobbyStatus()
    {
        if (_state is null || _state.Phase == GamePhase.GameOver)
            return new { gameExists = false };
        return new { gameExists = true, gameCode = _state.GameCode, phase = _state.Phase.ToString(), playerCount = _state.Players.Count };
    }

    public GameState CreateGame(string playerName, string connectionId, int colourIndex = 0, int avatarIndex = 0, string? gameCode = null)
    {
        if (_state is not null && _state.Phase != GamePhase.GameOver)
            throw new HubException("A game is already in progress.");

        _state = new GameState
        {
            GameCode = gameCode ?? GenerateCode(),
            Phase = GamePhase.Lobby
        };

        _state.Players.Add(new Player
        {
            ConnectionId = connectionId,
            Name = playerName,
            Colour = PlayerColours[Math.Clamp(colourIndex, 0, 5)],
            AvatarIndex = Math.Clamp(avatarIndex, 0, 8),
            IsHost = true
        });

        return _state;
    }

    public GameState JoinGame(string gameCode, string playerName, string connectionId, int colourIndex = 0, int avatarIndex = 0)
    {
        if (_state is null || _state.GameCode != gameCode)
            throw new HubException("Game not found.");
        if (_state.Phase != GamePhase.Lobby)
            throw new HubException("Game already started.");
        if (_state.Players.Count >= 6)
            throw new HubException("Game is full.");
        if (_state.Players.Any(p => p.Name == playerName))
            throw new HubException("Name already taken.");

        var colour = PlayerColours[Math.Clamp(colourIndex, 0, 5)];
        if (_state.Players.Any(p => p.Colour == colour))
            throw new HubException("Colour already taken. Pick another.");

        _state.Players.Add(new Player
        {
            ConnectionId = connectionId,
            Name = playerName,
            Colour = colour,
            AvatarIndex = Math.Clamp(avatarIndex, 0, 8)
        });

        return _state;
    }

    public GameState AddAiPlayer(string connectionId, int tier = 2, string? personality = null)
    {
        if (_state is null || _state.Phase != GamePhase.Lobby)
            throw new HubException("Not in lobby.");

        var caller = _state.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (caller is null || !caller.IsHost)
            throw new HubException("Only the host can add AI players.");
        if (_state.Players.Count >= 6)
            throw new HubException("Game is full.");

        var usedNames = _state.Players.Select(p => p.Name).ToHashSet();
        var name = AiNames.FirstOrDefault(n => !usedNames.Contains(n)) ?? $"Bot {_state.Players.Count}";

        var usedColours = _state.Players.Select(p => p.Colour).ToHashSet();
        var colour = PlayerColours.First(c => !usedColours.Contains(c));

        var usedAvatars = _state.Players.Select(p => p.AvatarIndex).ToHashSet();
        var isFemale = name.Contains("Alice") || name.Contains("Carol") || name.Contains("Eve");
        var genderPool = isFemale ? FemaleAvatars : MaleAvatars;
        var avatar = genderPool.FirstOrDefault(a => !usedAvatars.Contains(a));
        if (usedAvatars.Contains(avatar)) avatar = Enumerable.Range(0, 9).First(a => !usedAvatars.Contains(a));

        AiPersonality? parsedPersonality = tier >= 5 && Enum.TryParse<AiPersonality>(personality, true, out var p) ? p
            : (tier >= 5 ? (AiPersonality)Random.Shared.Next(4)
            : (tier >= 4 ? AiPersonality.Opportunist : null));

        _state.Players.Add(new Player
        {
            ConnectionId = $"ai-{Guid.NewGuid():N}",
            Name = name,
            Colour = colour,
            AvatarIndex = avatar,
            IsAI = true,
            AiTier = Math.Clamp(tier, 1, 5),
            Personality = parsedPersonality
        });

        return _state;
    }

    public GameState RemoveAI(string connectionId, int playerIndex)
    {
        if (_state is null || _state.Phase != GamePhase.Lobby)
            throw new HubException("Not in lobby.");
        var caller = _state.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (caller is null || !caller.IsHost)
            throw new HubException("Only the host can remove AI players.");
        if (playerIndex < 0 || playerIndex >= _state.Players.Count)
            throw new HubException("Invalid player index.");
        if (!_state.Players[playerIndex].IsAI)
            throw new HubException("Can only remove AI players.");

        _state.Players.RemoveAt(playerIndex);
        return _state;
    }

    public GameState StartGame(string connectionId, PlacementMode mode = PlacementMode.Auto)
    {
        if (_state is null)
            throw new HubException("No game exists.");
        var caller = _state.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (caller is null || !caller.IsHost)
            throw new HubException("Only the host can start the game.");
        if (_state.Players.Count < 2)
            throw new HubException("Need at least 2 players.");

        _state.HouseRules.PlacementMode = mode;
        DealTerritories();
        SetStartingArmies();
        GenerateDeck();
        if (_state.HouseRules.UseMissions) DealMissions();

        if (mode == PlacementMode.Auto)
        {
            // Auto-place all armies and skip straight to Playing
            for (int i = 0; i < _state.Players.Count; i++)
                AutoPlaceArmies(i);
            _state.Phase = GamePhase.Playing;
            _state.CurrentPlayerIndex = Random.Shared.Next(_state.Players.Count);
            CalculateReinforcements();
        }
        else
        {
            _state.Phase = GamePhase.InitialPlacement;
            _state.CurrentPlayerIndex = Random.Shared.Next(_state.Players.Count);
        }

        return _state;
    }

    private void AutoPlaceArmies(int playerIndex)
    {
        var player = _state!.Players[playerIndex];
        var myTerritories = _state.Territories.Where(t => t.OwnerId == playerIndex).ToList();

        while (player.ReinforcementsRemaining > 0)
        {
            // Score each territory
            var best = myTerritories
                .Select(t => new
                {
                    Territory = t,
                    Score = ScorePlacementTarget(t, playerIndex) * (0.8f + Random.Shared.NextDouble() * 0.4) // ±20% jitter
                })
                .OrderByDescending(x => x.Score)
                .First().Territory;

            best.Armies++;
            player.ReinforcementsRemaining--;
        }
    }

    private double ScorePlacementTarget(Territory t, int playerIndex)
    {
        double score = 0;
        var adjacent = t.Adjacent.Select(a => _state!.Territories[a]).ToList();
        bool isBorder = adjacent.Any(a => a.OwnerId != playerIndex);

        if (isBorder) score += 3;

        // Threat: adjacent enemy armies
        int enemyThreat = adjacent.Where(a => a.OwnerId != playerIndex).Sum(a => a.Armies);
        if (enemyThreat > 0) score += Math.Min(2, enemyThreat * 0.5);

        // Continent progress
        var contTerritories = _state!.Territories.Where(x => x.Continent == t.Continent).ToList();
        int owned = contTerritories.Count(x => x.OwnerId == playerIndex);
        if (owned > contTerritories.Count / 2) score += 1;

        // Weakest border gets priority
        if (isBorder && t.Armies <= 2) score += 1;

        return score;
    }

    public (GameState State, int Placed) PlaceArmy(string connectionId, int territoryId, int count = 1)
    {
        if (_state is null || _state.Phase != GamePhase.InitialPlacement)
            throw new HubException("Not in placement phase.");

        int callerIndex;
        Player player;

        if (_state.HouseRules.PlacementMode == PlacementMode.FreeForAll)
        {
            // Any player can place anytime
            callerIndex = _state.Players.FindIndex(p => p.ConnectionId == connectionId);
            if (callerIndex < 0) throw new HubException("Not in this game.");
            player = _state.Players[callerIndex];
        }
        else
        {
            // Manual: strict turn order
            player = _state.Players[_state.CurrentPlayerIndex];
            callerIndex = _state.CurrentPlayerIndex;
            if (player.ConnectionId != connectionId)
                throw new HubException("Not your turn.");
        }

        if (player.ReinforcementsRemaining <= 0)
            throw new HubException("No armies remaining.");

        var territory = _state.Territories.FirstOrDefault(t => t.Id == territoryId);
        if (territory is null || territory.OwnerId != callerIndex)
            throw new HubException("You don't own that territory.");

        var actual = Math.Min(Math.Max(1, count), player.ReinforcementsRemaining);
        territory.Armies += actual;
        player.ReinforcementsRemaining -= actual;

        // Check if all players done (both modes use this)
        if (_state.Players.All(p => p.ReinforcementsRemaining <= 0))
        {
            _state.Phase = GamePhase.Playing;
            _state.CurrentPlayerIndex = Random.Shared.Next(_state.Players.Count);
            _state.TurnPhase = TurnPhase.Reinforce;
            CalculateReinforcements();
        }
        else if (_state.HouseRules.PlacementMode == PlacementMode.Manual)
        {
            AdvancePlacementTurn();
        }

        return (_state, actual);
    }

    public void Rejoin(string playerName, string connectionId)
    {
        var player = _state?.Players.FirstOrDefault(p => p.Name == playerName);
        if (player is not null) player.ConnectionId = connectionId;
    }

    public void Reset() => _state = null;

    #endregion

    #region Private Helpers

    private int[] RollDice(int count, string role = "attacker")
    {
        var dice = new int[count];
        for (int i = 0; i < count; i++)
            dice[i] = Random.Shared.Next(1, 7);
        _diceAudit?.LogRolls("server", role, dice);
        return dice;
    }

    private void CalculateReinforcements()
    {
        var player = _state!.Players[_state.CurrentPlayerIndex];
        int playerIndex = _state.CurrentPlayerIndex;
        int territoryCount = _state.Territories.Count(t => t.OwnerId == playerIndex);
        int armies = Math.Max(3, territoryCount / 3);

        foreach (var continent in _territoryData.Continents)
        {
            if (continent.Territories.All(id => _state.Territories[id].OwnerId == playerIndex))
                armies += continent.Bonus;
        }

        player.ReinforcementsRemaining = armies;
    }

    private static string GenerateCode() => Random.Shared.Next(1000, 9999).ToString();

    private void ShuffleDeck()
    {
        var deck = _state!.Deck;
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    private void DealTerritories()
    {
        var indices = Enumerable.Range(0, 42).OrderBy(_ => Random.Shared.Next()).ToList();
        _state!.Territories = _territoryData.Territories.Select(t => new Territory
        {
            Id = t.Id, Name = t.Name, Continent = t.Continent, Adjacent = t.Adjacent
        }).ToList();

        for (int i = 0; i < indices.Count; i++)
        {
            var territory = _state.Territories[indices[i]];
            territory.OwnerId = i % _state.Players.Count;
            territory.Armies = 1;
        }
    }

    private void SetStartingArmies()
    {
        int total = (DebugMode ? DebugArmies : StartingArmies)[_state!.Players.Count];
        for (int i = 0; i < _state.Players.Count; i++)
        {
            int owned = _state.Territories.Count(t => t.OwnerId == i);
            _state.Players[i].ReinforcementsRemaining = total - owned;
        }
    }

    private void GenerateDeck()
    {
        var types = new[] { CardType.Infantry, CardType.Cavalry, CardType.Artillery };
        _state!.Deck = _state.Territories
            .Select((t, i) => new Card { TerritoryId = t.Id, Type = types[i % 3] })
            .ToList();
        _state.Deck.Add(new Card { TerritoryId = null, Type = CardType.Wild });
        _state.Deck.Add(new Card { TerritoryId = null, Type = CardType.Wild });
        ShuffleDeck();
    }

    private void DealMissions()
    {
        var missions = new List<Mission>
        {
            new() { Type = MissionType.ContinentConquest, Description = "Control North America and Africa", RequiredContinents = ["North America", "Africa"] },
            new() { Type = MissionType.ContinentConquest, Description = "Control North America and Australia", RequiredContinents = ["North America", "Australia"] },
            new() { Type = MissionType.ContinentConquest, Description = "Control Asia and South America", RequiredContinents = ["Asia", "South America"] },
            new() { Type = MissionType.ContinentConquest, Description = "Control Asia and Africa", RequiredContinents = ["Asia", "Africa"] },
            new() { Type = MissionType.ContinentConquest, Description = "Control Europe, South America, and a third continent", RequiredContinents = ["Europe", "South America"] },
            new() { Type = MissionType.ContinentConquest, Description = "Control Europe, Australia, and a third continent", RequiredContinents = ["Europe", "Australia"] },
            new() { Type = MissionType.TerritoryCount, Description = "Control 18 territories with at least 2 armies each", TerritoryCount = 18, MinArmiesPerTerritory = 2 },
            new() { Type = MissionType.TerritoryCount, Description = "Control 24 territories", TerritoryCount = 24, MinArmiesPerTerritory = 1 },
        };

        for (int i = 0; i < _state!.Players.Count; i++)
        {
            var colourName = _state.Players[i].Colour switch
            {
                "#E53E3E" => "Red", "#3182CE" => "Blue", "#38A169" => "Green",
                "#D69E2E" => "Gold", "#805AD5" => "Purple", "#DD6B20" => "Orange",
                _ => $"Player {i + 1}"
            };
            missions.Add(new Mission { Type = MissionType.Elimination, Description = $"Eliminate {colourName}", TargetPlayerIndex = i });
        }

        for (int i = missions.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (missions[i], missions[j]) = (missions[j], missions[i]);
        }

        int deckIdx = 0;
        for (int i = 0; i < _state.Players.Count; i++)
        {
            while (deckIdx < missions.Count
                && missions[deckIdx].Type == MissionType.Elimination
                && missions[deckIdx].TargetPlayerIndex == i)
                deckIdx++;

            _state.Players[i].Mission = deckIdx < missions.Count
                ? missions[deckIdx++]
                : new Mission { Type = MissionType.TerritoryCount, Description = "Control 24 territories", TerritoryCount = 24, MinArmiesPerTerritory = 1 };
        }
    }

    public bool CheckMissionComplete(int playerIndex)
    {
        var mission = _state!.Players[playerIndex].Mission;
        if (mission is null || mission.FallenBackToWorldDomination)
            return _state.Territories.All(t => t.OwnerId == playerIndex);

        return mission.Type switch
        {
            MissionType.ContinentConquest => CheckContinentMission(playerIndex, mission),
            MissionType.TerritoryCount => _state.Territories.Count(t => t.OwnerId == playerIndex && t.Armies >= (mission.MinArmiesPerTerritory ?? 1)) >= mission.TerritoryCount,
            MissionType.Elimination => mission.TargetPlayerIndex is int target && _state.Players[target].IsEliminated
                && _state.Territories.Any(t => t.OwnerId == playerIndex),
            _ => false
        };
    }

    private bool CheckContinentMission(int playerIndex, Mission mission)
    {
        foreach (var name in mission.RequiredContinents!)
        {
            if (!_territoryData.Continents.Any(c => c.Name == name && c.Territories.All(id => _state!.Territories[id].OwnerId == playerIndex)))
                return false;
        }
        if (mission.Description.Contains("third continent"))
            return _territoryData.Continents.Any(c => !mission.RequiredContinents.Contains(c.Name)
                && c.Territories.All(id => _state!.Territories[id].OwnerId == playerIndex));
        return true;
    }

    private void AdvancePlacementTurn()
    {
        if (_state!.Players.All(p => p.ReinforcementsRemaining <= 0))
        {
            _state.Phase = GamePhase.Playing;
            _state.CurrentPlayerIndex = 0;
            _state.TurnPhase = TurnPhase.Reinforce;
            CalculateReinforcements();
            return;
        }
        do { _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Count; }
        while (_state.Players[_state.CurrentPlayerIndex].ReinforcementsRemaining <= 0);
    }

    #endregion
}

