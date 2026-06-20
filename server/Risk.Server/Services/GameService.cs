using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using System.Text.Json;

namespace Risk.Server.Services;

public class GameService
{
    private static readonly string[] PlayerColours = ["#E53E3E", "#3182CE", "#38A169", "#D69E2E", "#805AD5", "#DD6B20"];
    private static readonly int[] StartingArmies = [0, 0, 40, 35, 30, 25, 20]; // index = player count

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
            return;
        }

        do
        {
            _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Count;
        }
        while (_state.Players[_state.CurrentPlayerIndex].ReinforcementsRemaining <= 0);
    }

    private static string GenerateCode()
    {
        return Random.Shared.Next(1000, 9999).ToString();
    }
}

file record TerritoryData(List<TerritoryDef> Territories, List<ContinentDef> Continents);
file record TerritoryDef(int Id, string Name, string Continent, List<int> Adjacent);
file record ContinentDef(string Name, int Bonus, List<int> Territories);
