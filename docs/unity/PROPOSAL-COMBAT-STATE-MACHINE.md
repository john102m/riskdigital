# Proposal: Combat State Machine Refactor

## Problem

`CombatTheatre.cs` and `GameService.AttackWithDice` have accumulated flags and pending state that interact in fragile ways. Bugs keep appearing from timing issues between:
- Panel show/hide
- Camera fly state
- Spawn counting
- Delayed hide timers
- CombatResult resets

Current flags on CombatTheatre: `isPlaying`, `panelVisible`, `spawnCount`, `cameraFlownThisTurn`, `awaitingSecondRoll`, `hideCts`

Current pending state on GameService: `_pendingAttackerRoll`, `_pendingDefenderRoll`, `_pendingAttackerDiceCount`, `_pendingDefenderDiceCount`, `_pendingSourceId`, `_pendingTargetId`, `_pendingAttackerConnId`, `_pendingDefenderConnId`

## Proposed Solution

### Unity: CombatTheatre → State Machine

Replace flags with an explicit state enum:

```csharp
enum CombatState
{
    Idle,                  // No panel, no dice
    AwaitingFirstSpawn,    // Panel shown, camera flying, waiting for first SpawnDice
    AwaitingSecondSpawn,   // First dice in arena, waiting for second SpawnDice
    DiceSettling,          // Both spawned, physics running, reading faces
    ShowingResult,         // Dice settled, holding for player to read
    HidingAfterCapture,    // Delayed hide in progress
    ShowingBlitz           // Blitz result display
}
```

Each state has clear entry/exit conditions. Events check current state before acting — invalid transitions are ignored instead of causing bugs.

### Server: PendingCombat class

Extract pending roll state into a small class:

```csharp
class PendingCombat
{
    public int SourceId, TargetId;
    public int AttackerDiceCount, DefenderDiceCount;
    public string AttackerConnId, DefenderConnId;
    public TaskCompletionSource<int> AttackerRoll = new();
    public TaskCompletionSource<int> DefenderRoll = new();
}
```

`GameService` holds `PendingCombat? _pending` — null when no combat in progress. All methods check for null. Clean lifecycle.

## Benefits

- No more flag conflicts (only one state active at a time)
- Easier to reason about transitions
- New features (sound effects, animations) slot in as state-specific behaviours
- Server pending state has clear lifecycle (create on attack, dispose on resolve)

## Risk

- Medium refactor — touches working code
- State machine needs thorough testing of all transitions
- Should do AFTER current feature stabilises (not during debugging)

## When

After player-rolled dice is stable and merged. Not now.
