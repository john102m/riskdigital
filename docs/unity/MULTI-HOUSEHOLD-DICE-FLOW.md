# Multi-Household Dice Flow

## Scenario 1 — Human (England) attacks Bot (Scotland)

Player 0 on England TV attacks Bot 2 on Scotland TV.

```mermaid
sequenceDiagram
    participant H as Handset (Player 0)
    participant S as Server
    participant E as England TV (Z440)
    participant SC as Scotland TV (Laptop)

    H->>S: Attack(source, target, 3)
    S->>S: Create PendingCombat
    S->>E: SpawnDice("attacker", 3, src, tgt)
    Note over E: Red dice tumble (physics)
    E->>E: WaitForSettle()
    E->>S: SubmitRolledDice("attacker", [5,4,2])
    S->>E: AttackerDiceResult([5,4,2])
    S->>SC: AttackerDiceResult([5,4,2])
    Note over SC: Place red dice statically
    S->>SC: SpawnDice("defender", 2, src, tgt)
    Note over SC: Blue dice tumble (physics)
    SC->>SC: WaitForSettle()
    SC->>S: SubmitRolledDice("defender", [6,3])
    S->>E: DefenderDiceResult([6,3])
    S->>SC: DefenderDiceResult([6,3])
    Note over E: Place blue dice statically
    S->>S: TryComplete → ResolveCombat
    S->>E: CombatResult
    S->>SC: CombatResult
    Note over E: Both sets visible, result shown
    Note over SC: Both sets visible, result shown
```

## Scenario 2 — Bot (Scotland) attacks Human (England)

Bot 2 on Scotland TV attacks Player 0 on England TV.

```mermaid
sequenceDiagram
    participant S as Server (AI turn)
    participant E as England TV (Z440)
    participant SC as Scotland TV (Laptop)

    S->>S: AI chooses attack, Create PendingCombat
    S->>SC: SpawnDice("attacker", 3, src, tgt)
    Note over SC: Red dice tumble (physics)
    SC->>SC: WaitForSettle()
    SC->>S: SubmitRolledDice("attacker", [6,4,1])
    S->>E: AttackerDiceResult([6,4,1])
    S->>SC: AttackerDiceResult([6,4,1])
    Note over E: Place red dice statically
    S->>E: SpawnDice("defender", 2, src, tgt)
    Note over E: Blue dice tumble (physics)
    E->>E: WaitForSettle()
    E->>S: SubmitRolledDice("defender", [5,3])
    S->>E: DefenderDiceResult([5,3])
    S->>SC: DefenderDiceResult([5,3])
    Note over SC: Place blue dice statically
    S->>S: TryComplete → ResolveCombat
    S->>E: CombatResult
    S->>SC: CombatResult
```

## Scenario 3 — Same-household combat (Player 0 attacks Bot 1, both England)

Both players belong to England TV. Scotland TV is spectator.

```mermaid
sequenceDiagram
    participant H as Handset (Player 0)
    participant S as Server
    participant E as England TV (Z440)
    participant SC as Scotland TV (Laptop)

    H->>S: Attack(source, target, 3)
    S->>S: Create PendingCombat
    S->>E: SpawnDice("attacker", 3, src, tgt)
    Note over E: Red dice tumble
    S->>E: SpawnDice("defender", 2, src, tgt)
    Note over E: Blue dice tumble alongside red
    E->>E: WaitForSettle() (all 5 dice)
    E->>S: SubmitDiceResult([5,4,2], [6,3])
    S->>E: AttackerDiceResult([5,4,2])
    S->>SC: AttackerDiceResult([5,4,2])
    S->>E: DefenderDiceResult([6,3])
    S->>SC: DefenderDiceResult([6,3])
    Note over SC: Place all dice statically (spectator)
    S->>S: TryComplete → ResolveCombat
    S->>E: CombatResult
    S->>SC: CombatResult
```

## Scenario 4 — Human (England) attacks Human (Scotland)

Player 0 attacks Player 3. Defender must tap Roll on handset.

```mermaid
sequenceDiagram
    participant H0 as Handset (Player 0)
    participant H3 as Handset (Player 3)
    participant S as Server
    participant E as England TV (Z440)
    participant SC as Scotland TV (Laptop)

    H0->>S: Attack(source, target, 3)
    S->>S: Create PendingCombat
    S->>E: SpawnDice("attacker", 3, src, tgt)
    Note over E: Red dice tumble
    S->>H3: RollPrompt("defender", 2, src, tgt)
    E->>E: WaitForSettle()
    E->>S: SubmitRolledDice("attacker", [5,4,2])
    S->>E: AttackerDiceResult([5,4,2])
    S->>SC: AttackerDiceResult([5,4,2])
    Note over SC: Place red dice statically
    H3->>S: RollDice(2)
    S->>SC: SpawnDice("defender", 2, src, tgt)
    Note over SC: Blue dice tumble
    SC->>SC: WaitForSettle()
    SC->>S: SubmitRolledDice("defender", [6,3])
    S->>E: DefenderDiceResult([6,3])
    S->>SC: DefenderDiceResult([6,3])
    Note over E: Place blue dice statically
    S->>S: TryComplete → ResolveCombat
    S->>E: CombatResult
    S->>SC: CombatResult
```

## Key Principle

**Defender SpawnDice must wait for attacker to submit.** The server should NOT send `SpawnDice("defender")` until `SubmitRolledDice("attacker")` has arrived. This ensures:

1. Attacker dice are visible (statically) on defender's TV before defender rolls
2. No race condition where both TVs submit simultaneously and timeout fires
3. Matches the real board game feel — attacker rolls, reveals, THEN defender rolls

## Current Bug

The server sends both `SpawnDice` calls immediately in `PlayerRoll` + `AutoRollBotOpponent`. Both TVs start physics simultaneously. If defender settles before attacker submits, the `PendingCombat` may not have attacker dice yet — or worse, the arena display is confused.

## Required Server Change

`AutoRollBotOpponent("defender")` should NOT fire immediately. Instead, defender spawn should be triggered AFTER `SubmitRolledDice("attacker")` arrives:

```
AttackWithDice:
  1. Create PendingCombat
  2. PlayerRoll(attacker) → SpawnDice("attacker") to attacker TV
  3. Wait for SubmitRolledDice("attacker") ← NEW WAIT POINT
  4. THEN SpawnDice("defender") to defender TV (or RollPrompt if human)
  5. Wait for SubmitRolledDice("defender")
  6. TryComplete → ResolveCombat
```

---

*Created: 2026-07-07*
