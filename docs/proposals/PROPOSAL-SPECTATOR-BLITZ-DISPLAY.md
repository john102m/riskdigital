# Proposal — Fix Spectator Panel Position (Test #3)

## Problem

Test #3: You attack Alice (same-household, Z440). Laptop is spectator.

The laptop never receives `SpawnDice` — that only goes to the rolling TV. So
`currentSourceId`/`currentTargetId` are never set by `OnSpawnDice`. They rely
entirely on `OnAttackSelection` having fired first.

If `AttackerDiceResult` arrives before `AttackSelection` is processed,
`ShowBlitzDice` calls `PositionPanel(-1, -1)` → panel in wrong position.

Tests 1, 2, 4 are unaffected because on those TVs `OnSpawnDice` always fires
and sets source/target directly.

## Fix

Add `sourceId` and `targetId` to the `AttackerDiceResult` broadcast payload so
the spectator gets authoritative territory info regardless of event ordering.

### Server — `GameService.Combat.cs`

Both places that broadcast `AttackerDiceResult` (same-household path and
cross-household path):

```csharp
// Before:
await hub.Clients.Group(gameCode).SendAsync("AttackerDiceResult", attackerDiceValues);

// After:
await hub.Clients.Group(gameCode).SendAsync("AttackerDiceResult", new {
    values = attackerDiceValues,
    sourceId = _pending.SourceId,
    targetId = _pending.TargetId
});
```

### Unity — `SignalRClient.cs`

Add a DTO and update the event:

```csharp
public class AttackerDiceResultDTO
{
    public int[] values;
    public int sourceId;
    public int targetId;
}

// Change:
public event Action<int[]> OnAttackerDiceResult;
// To:
public event Action<AttackerDiceResultDTO> OnAttackerDiceResult;
```

Update the SignalR handler registration to deserialise the new shape.

### Unity — `CombatTheatre.cs`

Store sourceId/targetId when the event arrives:

```csharp
void OnAttackerDiceResult(AttackerDiceResultDTO dto)
{
    if (currentRole == MyRole.Attacker || currentRole == MyRole.SameHousehold)
    {
        Debug.Log($"[Combat] Ignoring AttackerDiceResult (I'm {currentRole})");
        return;
    }

    Debug.Log($"[Combat] AttackerDiceResult: [{string.Join(",", dto.values)}] src={dto.sourceId} tgt={dto.targetId}");
    lastAttackerValues = dto.values;
    currentSourceId = dto.sourceId;   // ← guarantees PositionPanel has correct values
    currentTargetId = dto.targetId;   // ← regardless of AttackSelection ordering

    hideCts?.Cancel();
    diceRoller.ClearDice();
    currentRole = MyRole.None;
    state = CombatState.Rolling;
}
```

## Files

| File | Change |
|------|--------|
| `server/Risk.Server/Services/GameService.Combat.cs` | Both `AttackerDiceResult` broadcasts add sourceId/targetId |
| `Assets/Scripts/SignalRClient.cs` | New DTO, event type updated |
| `Assets/Scripts/CombatTheatre.cs` | `OnAttackerDiceResult` stores sourceId/targetId |

No other flows affected. Tests 1, 2, 4 unaffected (they never reach the spectator branch).

---

*Created: 2026-07-07*
