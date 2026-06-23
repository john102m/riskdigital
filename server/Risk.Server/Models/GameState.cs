using System.Text.Json.Serialization;

namespace Risk.Server.Models;

public enum GamePhase
{
    Lobby,
    InitialPlacement,
    Playing,
    GameOver
}

public enum TurnPhase
{
    Reinforce,
    Attack,
    Fortify
}

public class GameState
{
    public string GameCode { get; set; } = "";
    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public TurnPhase TurnPhase { get; set; } = TurnPhase.Reinforce;
    public List<Player> Players { get; set; } = [];
    public List<Territory> Territories { get; set; } = [];
    public int CurrentPlayerIndex { get; set; }
    public int? AttackFrontId { get; set; }
    public List<int> AttackFrontIds { get; set; } = [];
    public int LastDiceCount { get; set; }
    public int? PendingMoveSource { get; set; }
    public int? PendingMoveTarget { get; set; }
    [JsonIgnore]
    public List<Card> Deck { get; set; } = [];
    public int CardTradeCount { get; set; }
    public HouseRules HouseRules { get; set; } = new();
}

public class HouseRules
{
    public bool LockedAttackFront { get; set; } = true;
    public bool UseMissions { get; set; } = true;
    public bool FixedCardValues { get; set; } = true;
}

public enum MissionType { ContinentConquest, TerritoryCount, Elimination }

public class Mission
{
    public MissionType Type { get; set; }
    public string Description { get; set; } = "";
    public List<string>? RequiredContinents { get; set; }
    public int? TerritoryCount { get; set; }
    public int? MinArmiesPerTerritory { get; set; }
    public int? TargetPlayerIndex { get; set; }
    public bool FallenBackToWorldDomination { get; set; }
}

public class Player
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Colour { get; set; } = "";
    public int AvatarIndex { get; set; }
    public bool IsHost { get; set; }
    public int ReinforcementsRemaining { get; set; }
    [JsonIgnore]
    public List<Card> Cards { get; set; } = [];
    public int CardCount => Cards.Count;
    public bool EarnedCardThisTurn { get; set; }
    public bool IsEliminated { get; set; }
    public bool IsAI { get; set; }
    public int AiTier { get; set; } = 1;
    [JsonIgnore]
    public Mission? Mission { get; set; }
}

public class Territory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Continent { get; set; } = "";
    public int OwnerId { get; set; } = -1;
    public int Armies { get; set; }
    public List<int> Adjacent { get; set; } = [];
}

public class Card
{
    public int? TerritoryId { get; set; }
    public CardType Type { get; set; }
}

public enum CardType
{
    Infantry,
    Cavalry,
    Artillery,
    Wild
}
