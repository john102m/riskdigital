# Proposal: Split CombatTheatre into Partial Classes

## Problem

`CombatTheatre.cs` is 663 lines handling 5 distinct concerns in one file. Hard to navigate, easy to introduce bugs when editing one concern and accidentally affecting another.

## Solution

Split into partial classes (same pattern as server's `GameService.cs` / `.Combat.cs` / `.Turn.cs`). All files share the same instance fields and state — no refactoring of logic, purely a file organisation change.

## Proposed Split

| File | Lines (~) | Contents |
|------|-----------|----------|
| `CombatTheatre.cs` | ~80 | Fields, enums, `Start()`, `ResetCombat()`, `OpenArena()`, `IsMyPlayer()`, `EnterShowingResult()` |
| `CombatTheatre.Events.cs` | ~180 | `OnSpawnDice`, `OnAttackerDiceResult`, `OnDefenderDiceResult`, `OnCombatResult`, `OnBlitzResult`, `OnAttackSelection`, `OnStateChanged`, `OnCombatRollRequest` |
| `CombatTheatre.Settle.cs` | ~100 | `EarlySettleAttacker`, `WaitSettleAttacker`, `WaitSettleGhostRed`, `WaitSettleDefender`, `PlayFullRoll` |
| `CombatTheatre.Display.cs` | ~150 | `ShowBlitzDice`, `ShowBlitzDiceWithDelay`, `GhostRoll`, `EnterHiding`, `DismissAfterHold`, `HideAfterDelay`, `StartCameraSweep`, `PositionPanel`, `ZoomIn`, `ZoomOut`, `ShowPanel`, `ClearArena`, `GetArenaCentre` |

## Rules

- All files: `public partial class CombatTheatre : MonoBehaviour`
- Only the main file has `[Tooltip]` fields, enums, and `Start()` (Unity serialisation lives in one place)
- Each file gets a header comment describing its concern
- No logic changes — pure file split
- Region markers removed (the file IS the region now)

## File Headers

```csharp
// CombatTheatre.Events.cs
/// <summary>
/// Event handlers — receives SignalR events and routes them based on role/state.
/// </summary>
public partial class CombatTheatre
{
```

```csharp
// CombatTheatre.Settle.cs
/// <summary>
/// Dice settle, read, and submit logic — waits for physics then sends results to server.
/// </summary>
public partial class CombatTheatre
{
```

```csharp
// CombatTheatre.Display.cs
/// <summary>
/// Visual display — blitz dice, ghost rolls, camera control, panel show/hide, timing.
/// </summary>
public partial class CombatTheatre
{
```

## What Stays in Main File

```csharp
// CombatTheatre.cs
/// <summary>
/// State machine orchestrating the dice roll visual sequence on the Unity TV board.
/// Split into partial classes by concern: Events, Settle, Display.
/// </summary>
public partial class CombatTheatre : MonoBehaviour
{
    // [Tooltip] fields (Inspector-serialized)
    // Enums (CombatState, MyRole)
    // Instance state fields
    // Start() — find references, subscribe events
    // ResetCombat() — single reset point
    // OpenArena() — shared entry point
    // IsMyPlayer() — ownership check
    // EnterShowingResult() — state transition used by multiple concerns
}
```

## Risk

Zero. Partial classes compile identically to a single class. No runtime difference, no serialisation difference, no Inspector impact. Unity handles partial MonoBehaviours correctly.
