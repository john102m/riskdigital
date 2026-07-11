# TV Join & Household Picker Flow

## Scenario 1: Single TV (one room)

```mermaid
sequenceDiagram
    participant H as Handsets
    participant S as Server
    participant TV as TV (single)

    H->>S: JoinGame (players join lobby)
    TV->>S: RegisterAsTV(code)
    S->>TV: GameStateUpdated (Lobby, player list)
    TV->>TV: Join screen shows game, waiting

    H->>S: StartGame
    S->>TV: GameStateUpdated (Playing, final player list)
    TV->>TV: Picker appears (all players ticked)

    TV->>TV: User taps "Everyone" + "Confirm"
    Note over TV: householdId = ""<br/>playerIndices = []<br/>(single-TV mode)
    TV->>TV: Panel hides → Board shows
    Note over TV: All dice roll on this TV
```

## Scenario 2: Multi-TV (two rooms)

```mermaid
sequenceDiagram
    participant H as Handsets
    participant S as Server
    participant TV1 as England TV
    participant TV2 as Scotland TV

    H->>S: JoinGame (players join lobby)
    TV1->>S: RegisterAsTV(code)
    TV2->>S: RegisterAsTV(code)
    S->>TV1: GameStateUpdated (Lobby)
    S->>TV2: GameStateUpdated (Lobby)

    H->>S: StartGame
    S->>TV1: GameStateUpdated (Playing, players: John, Alice, Bob, Dave)
    S->>TV2: GameStateUpdated (Playing, players: John, Alice, Bob, Dave)

    TV1->>TV1: Picker appears
    TV2->>TV2: Picker appears

    TV1->>TV1: Tick John + Alice → Confirm
    TV1->>S: RegisterAsTVWithHousehold("tv-a3f1", [0,1])
    Note over TV1: Board shows<br/>Owns players 0,1

    TV2->>TV2: Tick Bob + Dave → Confirm
    TV2->>S: RegisterAsTVWithHousehold("tv-7c2e", [2,3])
    Note over TV2: Board shows<br/>Owns players 2,3

    Note over TV1,TV2: Combat: attacker dice on attacker's TV<br/>defender dice on defender's TV<br/>ghost rolls on the other
```

## Key Points

- Picker shows on transition OUT of Lobby (player list is final)
- Single-TV: "Everyone" → no household config → all dice here
- Multi-TV: each TV picks its subset → re-registers with household → split dice routing
- Board only shows after Confirm (not before)
- Both TVs confirm independently — no coordination needed
