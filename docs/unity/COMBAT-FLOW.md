# Combat Flow — Visual Reference

Companion to [PROPOSAL-COMBAT-STATE-MACHINE.md](PROPOSAL-COMBAT-STATE-MACHINE.md) and [PROPOSAL-PLAYER-ROLLED-DICE.md](PROPOSAL-PLAYER-ROLLED-DICE.md).

---

## 1. Unity CombatTheatre — State Diagram

```mermaid
stateDiagram-v2
    [*] --> Idle

    Idle --> WaitingForDice : SpawnDice(attacker)
    Idle --> ShowingBlitz : BlitzResult

    WaitingForDice --> Settling : SpawnDice(defender)

    Settling --> ShowingResult : dice settled (physics)

    ShowingResult --> Idle : 3s elapsed
    ShowingResult --> Hiding : CombatResult(captured=true)

    Hiding --> Idle : 4s elapsed
    Hiding --> WaitingForDice : SpawnDice(attacker) [cancels hide]

    ShowingBlitz --> Idle : 6s elapsed
    ShowingBlitz --> WaitingForDice : SpawnDice(attacker) [guards bail out]

    note right of WaitingForDice
        Panel visible
        Camera flying (first time only)
        Attacker dice in arena
    end note

    note right of Settling
        Both dice sets spawned
        Physics running
        Reads faces on settle
        Sends SubmitDiceResult to server
    end note

    note right of Hiding
        4s hold so players see killing blow
        CancellationToken checked
        Camera flag resets on dismiss
    end note

    note left of ShowingBlitz
        State guards after each await
        Bails out if state != ShowingBlitz
        (fixes bug #9)
    end note
```

### Event Guards

| Event | Ignored when state is... | Reason |
|-------|--------------------------|--------|
| `CombatResult` | `WaitingForDice` | Stale result from previous combat |
| `OnStateChanged` (phase != Attack) | `WaitingForDice`, `Settling`, `Hiding` | Only cleans up in `Idle` or `ShowingResult` |
| `SpawnDice("attacker")` | *(never ignored)* | Always forces `WaitingForDice` — starts new combat |

---

## 2. Player-Rolled Dice — Sequence Diagrams

### Human attacks Bot

```mermaid
sequenceDiagram
    participant AH as Attacker Handset
    participant S as Server
    participant U as Unity TV

    AH->>S: Attack(source, target, dice)
    S->>S: Create PendingCombat
    S->>S: PlayerRoll(attacker) — immediate
    S-->>U: SpawnDice("attacker", count)
    S->>S: AutoRollBotOpponent("defender")
    S-->>U: SpawnDice("defender", count)

    Note over U: Physics simulate...<br/>Both sets settle

    U->>S: SubmitDiceResult(atkValues, defValues)
    S->>S: ResolveCombat()
    S-->>AH: CombatResult
    S-->>U: CombatResult
```

### Bot attacks Human

```mermaid
sequenceDiagram
    participant S as Server
    participant DH as Defender Handset
    participant U as Unity TV

    S->>S: AiService calls AttackWithDice
    S->>S: Create PendingCombat
    S->>S: PlayerRoll(attacker) — immediate (bot)
    S-->>U: SpawnDice("attacker", count)
    S-->>DH: RollPrompt("defender", diceCount, name)

    Note over DH: Phone vibrates<br/>"Defend!" overlay shown

    DH->>S: RollDice(diceCount)
    S->>S: PlayerRoll(defender)
    S-->>U: SpawnDice("defender", count)

    Note over U: Physics simulate...<br/>Both sets settle

    U->>S: SubmitDiceResult(atkValues, defValues)
    S->>S: ResolveCombat()
    S-->>DH: CombatResult
    S-->>U: CombatResult
```

### Human attacks Human

```mermaid
sequenceDiagram
    participant AH as Attacker Handset
    participant S as Server
    participant DH as Defender Handset
    participant U as Unity TV

    AH->>S: Attack(source, target, dice)
    S->>S: Create PendingCombat
    S->>S: PlayerRoll(attacker) — immediate
    S-->>U: SpawnDice("attacker", count)
    S-->>DH: RollPrompt("defender", diceCount, name)

    Note over DH: Phone vibrates<br/>"Defend!" overlay shown

    DH->>S: RollDice(diceCount)
    S->>S: PlayerRoll(defender)
    S-->>U: SpawnDice("defender", count)

    Note over U: Physics simulate...<br/>Both sets settle

    U->>S: SubmitDiceResult(atkValues, defValues)
    S->>S: ResolveCombat()
    S-->>AH: CombatResult
    S-->>DH: CombatResult
    S-->>U: CombatResult
```

### Bot attacks Bot

```mermaid
sequenceDiagram
    participant S as Server
    participant U as Unity TV

    S->>S: AiService calls AttackWithDice
    S->>S: Create PendingCombat

    Note over S: Task.Run with 1s delay

    S->>S: PlayerRoll(attacker)
    S-->>U: SpawnDice("attacker", count)
    S->>S: PlayerRoll(defender)
    S-->>U: SpawnDice("defender", count)

    Note over U: Physics simulate...<br/>Both sets settle

    U->>S: SubmitDiceResult(atkValues, defValues)
    S->>S: ResolveCombat()
    S-->>U: CombatResult
```

### Reconnect Recovery (Bug #8 fix)

```mermaid
sequenceDiagram
    participant S as Server
    participant DH as Defender Handset
    participant U as Unity TV

    S-->>DH: RollPrompt("defender", ...)
    Note over DH: Phone screen off<br/>SignalR disconnects

    DH->>S: Rejoin(playerName)
    S->>S: Update player.ConnectionId
    S->>S: Check _pending != null<br/>Check defender not yet rolled
    S-->>DH: RollPrompt (re-sent)

    DH->>S: RollDice(diceCount)
    Note over S: PlayerRoll matches by<br/>player INDEX (not stale connId)
    S-->>U: SpawnDice("defender", count)
```

---

## 3. AttackWithDice — Decision Flowchart

```mermaid
flowchart TD
    A[AttackWithDice called] --> B{Unity TV connected?}
    B -->|No| C[Server-side roll<br/>Attack method<br/>Instant result]
    B -->|Yes| D[Create PendingCombat]

    D --> E{Attacker is bot<br/>AND defender is bot?}
    E -->|Yes| F[Task.Run: 1s delay<br/>then roll both]
    E -->|No| G{Defender is bot?}

    G -->|Yes| H[Roll attacker immediately<br/>AutoRollBotOpponent defender]
    G -->|No| I[Roll attacker immediately<br/>Send RollPrompt to defender]

    F --> J[await both TCS tasks]
    H --> J
    I --> J

    J --> K[await DiceResult from Unity<br/>vs 10s timeout]

    K --> L{DiceResult received?}
    L -->|Yes| M[ResolveCombat with<br/>physics dice values]
    L -->|No / Timeout| N[Fallback: server-side roll<br/>Attack method]

    M --> O[Broadcast CombatResult<br/>null _pending]
    N --> O
```

---

## Key Invariants

- **`_pending` is either null or represents ONE active combat.** Never two in flight.
- **Player indices are stable** — connection IDs change on reconnect, indices don't.
- **SpawnDice("attacker") always wins** — any async sequence (blitz display, hide countdown) must yield if a new combat starts.
- **No timeout on human defender roll** — humans roll when ready. Bots roll immediately.
- **Blitz stays server-side** — too many rounds for physics. Only final dice displayed statically on Unity.
- **10s timeout on Unity physics** — if SubmitDiceResult never arrives, server falls back to random roll.
