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
    public HouseRules HouseRules { get; set; } = new();
}

public class HouseRules
{
    public bool LockedAttackFront { get; set; } = true;
}

public class Player
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Colour { get; set; } = "";
    public bool IsHost { get; set; }
    public int ReinforcementsRemaining { get; set; }
    public List<Card> Cards { get; set; } = [];
    public bool IsEliminated { get; set; }
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
    public int TerritoryId { get; set; }
    public CardType Type { get; set; }
}

public enum CardType
{
    Infantry,
    Cavalry,
    Artillery,
    Wild
}
