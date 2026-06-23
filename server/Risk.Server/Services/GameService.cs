using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;
using System.Text.Json;

namespace Risk.Server.Services;

public class GameService
{
    private static readonly string[] PlayerColours = ["#E53E3E", "#3182CE", "#38A169", "#D69E2E", "#805AD5", "#DD6B20"];
    private static readonly int[] StartingArmies = [0, 0, 40, 35, 30, 25, 20]; // index = player count
    private static readonly int[] DebugArmies =   [0, 0, 23, 16, 15, 12, 10];
    public bool DebugMode { get; set; }

    private readonly TerritoryData _territoryData;
    private GameState? _state;

    public GameState? State => _state;

    public GameService()
    {
        var json = File.ReadAllText("Data/territories.json");
        _territoryData = JsonSerializer.Deserialize<TerritoryData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public object GetLobbyStatus()
    {
        if (_state is null || _state.Phase == GamePhase.GameOver)
            return new { gameExists = false };
        return new { gameExists = true, gameCode = _state.GameCode, phase = _state.Phase.ToString(), playerCount = _state.Players.Count };
    }

    public GameState CreateGame(string playerName, string connectionId, int colourIndex = 0, int avatarIndex = 0)
    {
        if (_state is not null && _state.Phase != GamePhase.GameOver)
            throw new HubException("A game is already in progress.");

        _state = new GameState
        {
            GameCode = GenerateCode(),
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


    private static readonly string[] AiNames = ["Bot Alice", "Bot Bob", "Bot Carol", "Bot Dave", "Bot Eve"];
    private static readonly string[] AvatarFiles = ["female-1", "female-2", "female-3", "female-4", "female-5", "female-6", "male-1", "male-2", "male-3"];
    private static readonly int[] FemaleAvatars = [0, 1, 2, 3, 4, 5];
    private static readonly int[] MaleAvatars = [6, 7, 8];

    public GameState AddAiPlayer(string connectionId, int tier = 2)
    {
        if (_state is null || _state.Phase != GamePhase.Lobby)
            throw new HubException("Not in lobby.");

        var caller = _state.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (caller is null || !caller.IsHost)
            throw new HubException("Only the host can add AI players.");

        if (_state.Players.Count >= 6)
            throw new HubException("Game is full.");

        var usedNames = _state.Players.Select(p => p.Name).ToHashSet();
        var name = AiNames.FirstOrDefault(n => !usedNames.Contains(n))
            ?? $"Bot {_state.Players.Count}";

        var usedColours = _state.Players.Select(p => p.Colour).ToHashSet();
        var colour = PlayerColours.First(c => !usedColours.Contains(c));

        var usedAvatars = _state.Players.Select(p => p.AvatarIndex).ToHashSet();
        var isFemale = name.Contains("Alice") || name.Contains("Carol") || name.Contains("Eve");
        var genderPool = isFemale ? FemaleAvatars : MaleAvatars;
        var avatar = genderPool.FirstOrDefault(a => !usedAvatars.Contains(a));
        if (usedAvatars.Contains(avatar)) avatar = Enumerable.Range(0, 9).First(a => !usedAvatars.Contains(a));

        _state.Players.Add(new Player
        {
            ConnectionId = $"ai-{Guid.NewGuid():N}",
            Name = name,
            Colour = colour,
            AvatarIndex = avatar,
            IsAI = true,
            AiTier = Math.Clamp(tier, 1, 3)
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

        var target = _state.Players[playerIndex];
        if (!target.IsAI)
            throw new HubException("Can only remove AI players.");

        _state.Players.RemoveAt(playerIndex);

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
        GenerateDeck();
        if (_state.HouseRules.UseMissions)
            DealMissions();
        _state.Phase = GamePhase.InitialPlacement;
        _state.CurrentPlayerIndex = Random.Shared.Next(_state.Players.Count);

        return _state;
    }

    public (GameState State, int Placed) PlaceArmy(string connectionId, int territoryId, int count = 1)
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

        var actual = Math.Min(Math.Max(1, count), player.ReinforcementsRemaining);
        territory.Armies += actual;
        player.ReinforcementsRemaining -= actual;

        AdvancePlacementTurn();

        return (_state, actual);
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

    private void ShuffleDeck()
    {
        var deck = _state!.Deck;
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
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

        // Add elimination missions only for colours in the game
        for (int i = 0; i < _state!.Players.Count; i++)
        {
            var colourName = _state.Players[i].Colour switch
            {
                "#E53E3E" => "Red", "#3182CE" => "Blue", "#38A169" => "Green",
                "#D69E2E" => "Yellow", "#805AD5" => "Purple", "#DD6B20" => "Orange",
                _ => $"Player {i + 1}"
            };
            missions.Add(new Mission { Type = MissionType.Elimination, Description = $"Eliminate {colourName}", TargetPlayerIndex = i });
        }

        // Shuffle
        for (int i = missions.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (missions[i], missions[j]) = (missions[j], missions[i]);
        }

        // Deal — if player draws their own elimination, swap with next in deck
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
            return _state.Territories.All(t => t.OwnerId == playerIndex); // world domination

        return mission.Type switch
        {
            MissionType.ContinentConquest => CheckContinentMission(playerIndex, mission),
            MissionType.TerritoryCount => _state.Territories.Count(t => t.OwnerId == playerIndex && t.Armies >= (mission.MinArmiesPerTerritory ?? 1)) >= mission.TerritoryCount,
            MissionType.Elimination => mission.TargetPlayerIndex is int target && _state.Players[target].IsEliminated
                && _state.Territories.Any(t => t.OwnerId == playerIndex), // attacker must still be alive
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

        // "Third continent of your choice" — check if description mentions it
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

        do
        {
            _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Count;
        }
        while (_state.Players[_state.CurrentPlayerIndex].ReinforcementsRemaining <= 0);
    }

    public (GameState State, int ArmiesGranted, List<int> TerritoryBonusIds) TradeCards(string connectionId, int[] cardIndices)
    {
        if (_state is null || _state.Phase != GamePhase.Playing)
            throw new HubException("Not in playing phase.");

        if (_state.TurnPhase != TurnPhase.Reinforce && _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Cannot trade cards in this phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (cardIndices.Length != 3 || cardIndices.Distinct().Count() != 3)
            throw new HubException("Must trade exactly 3 distinct cards.");

        if (cardIndices.Any(i => i < 0 || i >= player.Cards.Count))
            throw new HubException("Invalid card index.");

        var cards = cardIndices.Select(i => player.Cards[i]).ToArray();

        if (!IsValidSet(cards))
            throw new HubException("Invalid card set.");

        // Calculate armies
        int armies;
        if (_state.HouseRules.FixedCardValues)
        {
            var types = cards.Select(c => c.Type).ToArray();
            int wilds = types.Count(t => t == CardType.Wild);
            var nonWild = types.Where(t => t != CardType.Wild).ToArray();

            // Determine effective set type
            bool isOneOfEach = nonWild.Distinct().Count() + wilds >= 3 && nonWild.Distinct().Count() > 1;

            if (isOneOfEach)
                armies = 10;
            else
            {
                // All same (or wilds filling in) — use the non-wild type
                var effectiveType = nonWild.Length > 0 ? nonWild[0] : CardType.Infantry;
                armies = effectiveType switch
                {
                    CardType.Artillery => 8,
                    CardType.Cavalry => 6,
                    _ => 4
                };
            }
        }
        else
        {
            _state.CardTradeCount++;
            armies = _state.CardTradeCount switch
            {
                1 => 4, 2 => 6, 3 => 8, 4 => 10, 5 => 12, 6 => 15,
                _ => 15 + (_state.CardTradeCount - 6) * 5
            };
        }

        // Territory bonus: +2 for each traded card matching an owned territory
        var bonusIds = new List<int>();
        foreach (var card in cards)
        {
            if (card.TerritoryId is int tid && _state.Territories[tid].OwnerId == _state.CurrentPlayerIndex)
            {
                _state.Territories[tid].Armies += 2;
                bonusIds.Add(tid);
            }
        }

        player.ReinforcementsRemaining += armies;

        // Remove cards (highest index first to preserve indices)
        foreach (var i in cardIndices.OrderByDescending(x => x))
            player.Cards.RemoveAt(i);

        // Return cards to deck and shuffle
        _state.Deck.AddRange(cards);
        ShuffleDeck();

        return (_state, armies, bonusIds);
    }

    private static bool IsValidSet(Card[] cards)
    {
        var types = cards.Select(c => c.Type).ToArray();
        int wilds = types.Count(t => t == CardType.Wild);

        if (wilds >= 2) return true; // 2 wilds + anything
        if (wilds == 1) return true; // 1 wild + any 2

        // No wilds: all same or all different
        var nonWild = types.Where(t => t != CardType.Wild).ToArray();
        return nonWild.Distinct().Count() == 1 || nonWild.Distinct().Count() == 3;
    }

    public (GameState State, int Placed) Reinforce(string connectionId, int territoryId, int count = 1)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Reinforce)
            throw new HubException("Not in reinforce phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.Cards.Count >= 5)
            throw new HubException("You must trade cards first (5+ cards).");

        if (player.ReinforcementsRemaining <= 0)
            throw new HubException("No reinforcements remaining.");

        var territory = _state.Territories.FirstOrDefault(t => t.Id == territoryId);
        if (territory is null || territory.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own that territory.");

        var actual = Math.Min(Math.Max(1, count), player.ReinforcementsRemaining);
        territory.Armies += actual;
        player.ReinforcementsRemaining -= actual;

        return (_state, actual);
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

        if (player.EarnedCardThisTurn && _state.Deck.Count > 0)
        {
            player.Cards.Add(_state.Deck[^1]);
            _state.Deck.RemoveAt(_state.Deck.Count - 1);
        }

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
            _state.PendingMoveSource = sourceId;
            _state.PendingMoveTarget = targetId;
            if (_state.HouseRules.LockedAttackFront)
                _state.AttackFrontIds.Add(targetId);
            if (!player.EarnedCardThisTurn)
                player.EarnedCardThisTurn = true;
        }

        var result = new CombatResult(
            attackerDice, defenderDice,
            attackerLosses, defenderLosses,
            captured, sourceId, targetId,
            source.Armies, target.Armies
        );

        return (_state, result);
    }

    public (GameState State, BlitzResult Result) Blitz(string connectionId, int sourceId, int targetId)
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
        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count > 0
            && !_state.AttackFrontIds.Contains(sourceId))
            throw new HubException("You must continue attacking from your current front.");
        if (source.Armies <= 1)
            throw new HubException("Not enough armies to attack.");

        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count == 0)
            _state.AttackFrontIds.Add(sourceId);

        int startSourceArmies = source.Armies;
        int startTargetArmies = target.Armies;
        int rounds = 0;

        int lastDice = 0;
        while (source.Armies > 1 && target.Armies > 0)
        {
            lastDice = Math.Min(3, source.Armies - 1);
            var attackerDice = RollDice(lastDice).OrderByDescending(d => d).ToArray();
            int defDice = target.Armies >= 2 ? 2 : 1;
            var defenderDice = RollDice(defDice).OrderByDescending(d => d).ToArray();

            int comparisons = Math.Min(attackerDice.Length, defenderDice.Length);
            for (int i = 0; i < comparisons; i++)
            {
                if (attackerDice[i] > defenderDice[i])
                    target.Armies--;
                else
                    source.Armies--;
            }
            rounds++;
        }

        bool captured = target.Armies <= 0;
        // Min move-in = dice used on the final (capturing) round
        _state.LastDiceCount = Math.Min(lastDice, source.Armies - 1);

        if (captured)
        {
            target.OwnerId = _state.CurrentPlayerIndex;
            target.Armies = 0;
            _state.PendingMoveSource = sourceId;
            _state.PendingMoveTarget = targetId;
            if (_state.HouseRules.LockedAttackFront)
                _state.AttackFrontIds.Add(targetId);
            if (!player.EarnedCardThisTurn)
                player.EarnedCardThisTurn = true;
        }

        var result = new BlitzResult(
            rounds,
            startSourceArmies - source.Armies,
            startTargetArmies - target.Armies,
            captured, sourceId, targetId,
            source.Armies, target.Armies
        );

        return (_state, result);
    }

    public (GameState State, bool ForcedTradeRequired, int EliminatedPlayerIndex, bool MissionWon) MoveAfterCapture(string connectionId, int sourceId, int targetId, int armies)
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

        int minMove = Math.Min(_state.LastDiceCount, source.Armies - 1);
        if (armies < minMove || armies >= source.Armies)
            throw new HubException($"Must move between {minMove} and {source.Armies - 1} armies.");

        source.Armies -= armies;
        target.Armies += armies;
        _state.PendingMoveSource = null;
        _state.PendingMoveTarget = null;

        // Check elimination
        int defenderId = -1;
        for (int i = 0; i < _state.Players.Count; i++)
        {
            if (i != _state.CurrentPlayerIndex && !_state.Players[i].IsEliminated
                && !_state.Territories.Any(t => t.OwnerId == i))
            {
                _state.Players[i].IsEliminated = true;
                defenderId = i;
                // Transfer cards to attacker
                player.Cards.AddRange(_state.Players[i].Cards);
                _state.Players[i].Cards.Clear();

                // Fallback: any player whose elimination target was killed by someone else
                if (_state.HouseRules.UseMissions)
                {
                    for (int p = 0; p < _state.Players.Count; p++)
                    {
                        if (p == _state.CurrentPlayerIndex) continue;
                        var m = _state.Players[p].Mission;
                        if (m is { Type: MissionType.Elimination } && m.TargetPlayerIndex == i)
                            m.FallenBackToWorldDomination = true;
                    }
                }
            }
        }

        // Check win — mission or world domination
        bool missionWon = false;
        if (_state.HouseRules.UseMissions && CheckMissionComplete(_state.CurrentPlayerIndex))
        {
            _state.Phase = GamePhase.GameOver;
            missionWon = true;
        }
        else if (_state.Territories.All(t => t.OwnerId == _state.CurrentPlayerIndex))
        {
            _state.Phase = GamePhase.GameOver;
        }

        bool forcedTrade = player.Cards.Count >= 5;
        return (_state, forcedTrade, defenderId, missionWon);
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

        player.EarnedCardThisTurn = false;

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


