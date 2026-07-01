# Combat State Machine Refactor ✅ IMPLEMENTED (2026-06-29)

## Problem

`CombatTheatre.cs` and `GameService.AttackWithDice` accumulated flags and pending state that interacted in fragile ways. Bugs appeared from timing between panel show/hide, camera fly state, spawn counting, delayed hide timers, and CombatResult resets.

### Before (flags)
```
CombatTheatre: isPlaying, panelVisible, spawnCount, cameraFlownThisTurn, awaitingSecondRoll, hideCts, combatGeneration
GameService: _pendingAttackerRoll, _pendingDefenderRoll, _pendingAttackerDiceCount, _pendingDefenderDiceCount, _pendingSourceId, _pendingTargetId, _pendingAttackerConnId, _pendingDefenderConnId, _pendingDiceResult
```

### After (structured state)
```
CombatTheatre: CombatState enum + cameraFlownThisTurn + hideCts (2 support fields)
GameService: PendingCombat? _pending (single nullable object)
```

## Unity: CombatTheatre State Machine

### States

| State | Panel | Camera | What's happening |
|-------|-------|--------|-----------------|
| `Idle` | Hidden | — | No combat in progress, waiting for next event |
| `WaitingForDice` | Visible | Flying (first time) | Attacker dice spawned, waiting for defender dice |
| `Settling` | Visible | At rest | Both sets in arena, physics running |
| `ShowingResult` | Visible | At rest | Dice settled, holding 3s for players to read |
| `Hiding` | Visible → Hidden | — | 4s countdown after capture before panel hides |
| `ShowingBlitz` | Visible | Sweeping | Blitz final dice on display for 6s |

### State Transitions

```
Idle ──SpawnDice(attacker)──→ WaitingForDice
WaitingForDice ──SpawnDice(defender)──→ Settling
Settling ──dice settled──→ ShowingResult
ShowingResult ──3s elapsed──→ Idle
ShowingResult ──CombatResult(captured)──→ Hiding
Hiding ──4s elapsed──→ Idle
Hiding ──SpawnDice(attacker)──→ WaitingForDice (cancels hide)
Idle ──BlitzResult──→ ShowingBlitz
ShowingBlitz ──6s elapsed──→ Idle
Any ──phase != Attack──→ Idle (cleanup)
```

### Event Guards
- `OnCombatResult`: ignored if state is `WaitingForDice` or `Settling` (stale result from previous combat)
- `OnSpawnDice("attacker")`: always forces transition to `WaitingForDice` (cancels any pending hide)
- `OnStateChanged`: only hides panel if in `Idle` or `ShowingResult`

### Entry Methods
- `EnterWaitingForDice()` — cancel hide, clear dice, show panel, start camera fly (if first time this turn)
- `EnterSettling()` — trigger `WaitSettleAndSend()`
- `EnterHiding()` — start 4s cancellable countdown

### Debug
All transitions logged: `[Combat] Idle → WaitingForDice`

## Server: PendingCombat Class

### Structure
```csharp
public class PendingCombat
{
    public int SourceId { get; init; }
    public int TargetId { get; init; }
    public int AttackerDiceCount { get; init; }
    public int DefenderDiceCount { get; set; }
    public string AttackerConnId { get; init; }
    public string DefenderConnId { get; init; }
    public TaskCompletionSource<int> AttackerRoll { get; } = new();
    public TaskCompletionSource<int> DefenderRoll { get; } = new();
    public TaskCompletionSource<(int[], int[])> DiceResult { get; } = new();
}
```

### Lifecycle
1. **Created** in `AttackWithDice` when Unity is connected
2. **Populated** by `PlayerRoll` (attacker/defender tap Roll) and `SubmitDiceResult` (Unity returns faces)
3. **Consumed** when `DiceResult.Task` completes — values passed to `ResolveCombat`
4. **Nulled** after resolve or timeout — clean slate for next combat

### Guards
- `SubmitDiceResult`: no-ops if `_pending` is null
- `PlayerRoll`: returns immediately if `_pending` is null
- `AutoRollBotOpponent`: returns immediately if `_pending` is null

## Files Changed

| File | Before | After |
|------|--------|-------|
| `CombatTheatre.cs` | 230 lines, 6 flags, unclear interactions | 230 lines, enum state machine, explicit transitions |
| `GameService.cs` | 8 pending fields scattered in class | `PendingCombat? _pending` + clean class at bottom |

## Benefits Achieved

- **Readable**: state enum tells you exactly what's happening at a glance
- **Debuggable**: transition logs show exact flow without breakpoints
- **Safe**: events check state before acting — stale events can't corrupt
- **Maintainable**: adding new behaviour means adding a state + transitions, not another flag
