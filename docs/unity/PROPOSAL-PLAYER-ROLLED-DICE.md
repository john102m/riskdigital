# Proposal: Player-Rolled Dice

## Summary

Let the attacker and defender each trigger their own dice roll from their handset, rather than the server auto-rolling on attack. The Unity TV still does the physics — but each player gets to "throw" their dice with a tap.

## Current Flow

```
Attacker taps Attack → Server broadcasts CombatRollRequest → Unity rolls ALL dice → sends result back
```

## Proposed Flow

```
1. Attacker taps Attack → Server enters "awaiting rolls" state
2. Server sends RollPrompt to attacker ("Tap to roll 3 dice")
3. Server sends RollPrompt to defender ("Tap to roll 2 dice")
4. Attacker taps Roll → Server notifies Unity to spawn attacker dice
5. Defender taps Roll → Server notifies Unity to spawn defender dice
6. Unity reads all faces once both sets have settled → sends result back
7. Server resolves combat
```

## Key Design Decisions

### Ordering
- Either player can roll first — no enforced order
- Dice spawn independently as each player rolls
- Result only resolves once both have settled

### Timeout
- If a player doesn't roll within 8 seconds, auto-roll for them (same as current)
- Prevents stalling the game

### Bot Handling
- Bots auto-roll after a randomised delay (1–3 seconds) to simulate thinking
- No handset prompt needed — server triggers their roll directly

### Defender Choice: Dice Count
- Defender with 2+ armies can choose 1 or 2 dice (standard Risk rule)
- RollPrompt includes max dice available, defender picks before rolling
- Default to max if timeout expires

## Server Changes

| File | Change |
|------|--------|
| Models/ | New `RollPrompt` DTO, new `PlayerRoll` DTO |
| GameService | New state: `AwaitingRolls` with two TCS (attacker + defender) |
| GameHub | `Attack()` sends prompts instead of immediate CombatRollRequest |
| GameHub | New `RollDice()` hub method — player confirms their roll |
| AiService | Auto-rolls after delay when it's their dice |

## Handset Changes

| File | Change |
|------|--------|
| useSignalR | Listen for `RollPrompt` event |
| AttackPhase | Show "Roll!" button when prompted (replaces immediate attack) |
| DefendPrompt | New component — defender sees dice count choice + roll button |

## Unity Changes

| File | Change |
|------|--------|
| DiceRoller | Split `RollAndRead` into `SpawnAttackerDice` + `SpawnDefenderDice` + `ReadAll` |
| CombatTheatre | Handle two-phase spawn (attacker dice land, then defender dice land) |
| SignalRClient | New events: `SpawnAttackerDice`, `SpawnDefenderDice` |

## UX Feel

- Attacker sees: "Roll!" button appears → tap → dice fly on TV → satisfying
- Defender sees: "Defend! (1 or 2 dice)" → chooses → tap → their dice join the arena
- Spectators see: staggered dice appearing, building tension
- Bots: dice just appear after a beat, no human input needed

## Open Questions

1. Should both sets of dice be in the arena simultaneously, or attacker first then defender?
2. Do we animate a "shake" on the handset before releasing? (haptic feedback?)
3. Should the defender dice count choice be a separate step or combined with the roll tap?

## Complexity: Medium

Main risk is timing coordination — two async waits instead of one. The timeout fallback keeps it robust.

## Not in Scope

- Gyro/accelerometer shake-to-roll (future polish)
- Individual die selection (always roll max unless defender chooses fewer)
