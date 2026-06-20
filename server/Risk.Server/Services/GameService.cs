using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using System.Text.Json;

namespace Risk.Server.Services;

public class GameService
{
    private static readonly string[] PlayerColours = ["#E53E3E", "#3182CE", "#38A169", "#D69E2E", "#805AD5", "#DD6B20"];
    private static readonly int[] StartingArmies = [0, 0, 23, 35, 30, 25, 20]; // index = player count (2p reduced for dev)

    private readonly TerritoryData _territoryData;
    private GameState? _state;

    public GameState? State => _state;

    public GameService()
    {
        var json = File.ReadAllText("Data/territories.json");
        _territoryData = JsonSerializer.Deserialize<TerritoryData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public GameState CreateGame(string playerName, string connectionId)
    {
        _state = new GameState
        {
            GameCode = GenerateCode(),
            Phase = GamePhase.Lobby
        };

        _state.Players.Add(new Player
        {
            ConnectionId = connectionId,
            Name = playerName,
            Colour = PlayerColours[0],
            IsHost = true
        });

        return _state;
    }

    public GameState JoinGame(string gameCode, string playerName, string connectionId)
    {
        if (_state is null || _state.GameCode != gameCode)
            throw new HubException("Game not found.");

        if (_state.Phase != GamePhase.Lobby)
            throw new HubException("Game already started.");

        if (_state.Players.Count >= 6)
            throw new HubException("Game is full.");

        if (_state.Players.Any(p => p.Name == playerName))
            throw new HubException("Name already taken.");

        _state.Players.Add(new Player
        {
            ConnectionId = connectionId,
            Name = playerName,
            Colour = PlayerColours[_state.Players.Count]
        });

        return _state;
    }

    public GameState StartGame(string connectionId)
    {
        if (_state is null)
            throw new HubException("No game exists.");

        var caller = _state.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (caller is null || !caller.IsHost)
            throw new HubException("Only the host can start the game.");

        if (_state.Players.Count < 2)
            throw new HubException("Need at least 2 players.");

        DealTerritories();
        SetStartingArmies();
        _state.Phase = GamePhase.InitialPlacement;
        _state.CurrentPlayerIndex = 0;

        return _state;
    }

    public GameState PlaceArmy(string connectionId, int territoryId)
    {
        if (_state is null || _state.Phase != GamePhase.InitialPlacement)
            throw new HubException("Not in placement phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.ReinforcementsRemaining <= 0)
            throw new HubException("No armies remaining.");

        var territory = _state.Territories.FirstOrDefault(t => t.Id == territoryId);
        if (territory is null || territory.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own that territory.");

        territory.Armies++;
        player.ReinforcementsRemaining--;

        AdvancePlacementTurn();

        return _state;
    }

    public void Rejoin(string playerName, string connectionId)
    {
        var player = _state?.Players.FirstOrDefault(p => p.Name == playerName);
        if (player is not null)
            player.ConnectionId = connectionId;
    }

    public void Reset()
    {
        _state = null;
    }

    private void DealTerritories()
    {
        var indices = Enumerable.Range(0, 42).OrderBy(_ => Random.Shared.Next()).ToList();
        _state!.Territories = _territoryData.Territories.Select(t => new Territory
        {
            Id = t.Id,
            Name = t.Name,
            Continent = t.Continent,
            Adjacent = t.Adjacent
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
        int total = StartingArmies[_state!.Players.Count];
        for (int i = 0; i < _state.Players.Count; i++)
        {
            int owned = _state.Territories.Count(t => t.OwnerId == i);
            _state.Players[i].ReinforcementsRemaining = total - owned;
        }
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

        do
        {
            _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Count;
        }
        while (_state.Players[_state.CurrentPlayerIndex].ReinforcementsRemaining <= 0);
    }

    public GameState Reinforce(string connectionId, int territoryId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Reinforce)
            throw new HubException("Not in reinforce phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.ReinforcementsRemaining <= 0)
            throw new HubException("No reinforcements remaining.");

        var territory = _state.Territories.FirstOrDefault(t => t.Id == territoryId);
        if (territory is null || territory.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own that territory.");

        territory.Armies++;
        player.ReinforcementsRemaining--;

        return _state;
    }

    public GameState EndReinforce(string connectionId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Reinforce)
            throw new HubException("Not in reinforce phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.ReinforcementsRemaining > 0)
            throw new HubException("Place all reinforcements first.");

        _state.TurnPhase = TurnPhase.Attack;
        _state.AttackFrontId = null;
        _state.AttackFrontIds = [];
        return _state;
    }

    public GameState EndAttack(string connectionId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        _state.TurnPhase = TurnPhase.Fortify;
        return _state;
    }

    public (GameState State, CombatResult Result) Attack(string connectionId, int sourceId, int targetId, int diceCount)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.FirstOrDefault(t => t.Id == sourceId);
        var target = _state.Territories.FirstOrDefault(t => t.Id == targetId);

        if (source is null || source.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own the source territory.");

        if (target is null || target.OwnerId == _state.CurrentPlayerIndex)
            throw new HubException("Target must be an enemy territory.");

        if (!source.Adjacent.Contains(targetId))
            throw new HubException("Target is not adjacent to source.");

        // House rule: locked attack front
        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count > 0
            && !_state.AttackFrontIds.Contains(sourceId))
            throw new HubException("You must continue attacking from your current front.");

        if (diceCount < 1 || diceCount > 3)
            throw new HubException("Dice count must be 1-3.");

        if (source.Armies <= diceCount)
            throw new HubException("Not enough armies to attack with that many dice.");

        // Roll dice
        _state.LastDiceCount = diceCount;
        var attackerDice = RollDice(diceCount).OrderByDescending(d => d).ToArray();
        int defenderDiceCount = target.Armies >= 2 ? 2 : 1;
        var defenderDice = RollDice(defenderDiceCount).OrderByDescending(d => d).ToArray();

        // Compare pairs — defender wins ties
        int attackerLosses = 0, defenderLosses = 0;
        int comparisons = Math.Min(attackerDice.Length, defenderDice.Length);
        for (int i = 0; i < comparisons; i++)
        {
            if (attackerDice[i] > defenderDice[i])
                defenderLosses++;
            else
                attackerLosses++;
        }

        source.Armies -= attackerLosses;
        target.Armies -= defenderLosses;

        bool captured = target.Armies <= 0;

        // Set initial front on first attack (must be before adding captured territory)
        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count == 0)
            _state.AttackFrontIds.Add(sourceId);

        if (captured)
        {
            target.OwnerId = _state.CurrentPlayerIndex;
            target.Armies = 0; // Will be filled by MoveAfterCapture
            if (_state.HouseRules.LockedAttackFront)
                _state.AttackFrontIds.Add(targetId);
        }

        var result = new CombatResult(
            attackerDice, defenderDice,
            attackerLosses, defenderLosses,
            captured, sourceId, targetId,
            source.Armies, target.Armies
        );

        return (_state, result);
    }

    public GameState MoveAfterCapture(string connectionId, int sourceId, int targetId, int armies)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.First(t => t.Id == sourceId);
        var target = _state.Territories.First(t => t.Id == targetId);

        if (source.OwnerId != _state.CurrentPlayerIndex || target.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You must own both territories.");

        int minMove = _state.LastDiceCount;
        if (armies < minMove || armies >= source.Armies)
            throw new HubException($"Must move between {minMove} and {source.Armies - 1} armies.");

        source.Armies -= armies;
        target.Armies += armies;

        // Check win condition
        if (_state.Territories.All(t => t.OwnerId == _state.CurrentPlayerIndex))
        {
            _state.Phase = GamePhase.GameOver;
        }

        return _state;
    }

    private static int[] RollDice(int count)
    {
        var dice = new int[count];
        for (int i = 0; i < count; i++)
            dice[i] = Random.Shared.Next(1, 7);
        return dice;
    }

    public GameState EndTurn(string connectionId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing)
            throw new HubException("Not in playing phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        // Advance to next non-eliminated player
        do
        {
            _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Count;
        }
        while (_state.Players[_state.CurrentPlayerIndex].IsEliminated);

        _state.TurnPhase = TurnPhase.Reinforce;
        CalculateReinforcements();

        return _state;
    }

    public GameState Fortify(string connectionId, int sourceId, int targetId, int armies)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Fortify)
            throw new HubException("Not in fortify phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.First(t => t.Id == sourceId);
        var target = _state.Territories.First(t => t.Id == targetId);

        if (source.OwnerId != _state.CurrentPlayerIndex || target.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You must own both territories.");

        if (!source.Adjacent.Contains(targetId))
            throw new HubException("Territories must be adjacent.");

        if (armies < 1 || armies >= source.Armies)
            throw new HubException($"Must move between 1 and {source.Armies - 1} armies.");

        source.Armies -= armies;
        target.Armies += armies;

        return _state;
    }

    private void CalculateReinforcements()
    {
        var player = _state!.Players[_state.CurrentPlayerIndex];
        int playerIndex = _state.CurrentPlayerIndex;

        // Territories / 3, minimum 3
        int territoryCount = _state.Territories.Count(t => t.OwnerId == playerIndex);
        int armies = Math.Max(3, territoryCount / 3);

        // Continent bonuses
        foreach (var continent in _territoryData.Continents)
        {
            if (continent.Territories.All(id => _state.Territories[id].OwnerId == playerIndex))
                armies += continent.Bonus;
        }

        player.ReinforcementsRemaining = armies;
    }

    private static string GenerateCode()
    {
        return Random.Shared.Next(1000, 9999).ToString();
    }
}


