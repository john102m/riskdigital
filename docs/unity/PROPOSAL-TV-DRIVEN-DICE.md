# Proposal: TV-Driven Dice Resolution

## Summary
When a Unity TV client is connected, the server delegates single-attack dice rolls to it. Unity's physics simulation determines the result — no face correction needed. Existing gameplay is unchanged: if no Unity client is connected, or for blitz attacks, the server rolls as it does today.

## Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Scope | Single attacks only | Blitz stays server-side (it's meant to be fast) |
| Detection | Unity identifies itself on connect | Server tracks whether to delegate |
| Fallback | 5s timeout → server rolls | Game never gets stuck |
| Web board | No changes | Never receives roll requests |
| Security | None needed | Family LAN game, TV is trusted |
| Existing flow | Unchanged when no Unity connected | Zero regression risk |

## Flow

### Without Unity (unchanged)
```
Player taps Attack → Server rolls dice → Resolves combat → Broadcasts CombatResult
```

### With Unity connected
```
Player taps Attack → Server validates → Broadcasts CombatRollRequest (dice counts, source/target)
                                                    ↓
                        Unity spawns dice → Physics simulate → Dice settle naturally
                                                    ↓
                        DiceFaceReader reads faces → Unity sends SubmitDiceResult to server
                                                    ↓
                        Server resolves combat with those values → Broadcasts CombatResult
```

### Blitz (always server-side)
```
Player taps Blitz → Server rolls all rounds internally → Broadcasts BlitzResult (as today)
```

## Files to Touch

### Server — `Risk.Server/Hubs/GameHub.cs`
- Add `RegisterAsTV()` hub method — Unity calls this on connect, server adds to "unity-tv" group and sets a flag
- Modify `Attack()` — if Unity TV connected, broadcast `CombatRollRequest` and await result (with 5s timeout) instead of resolving immediately
- Add `SubmitDiceResult(int sourceId, int targetId, int[] attackerDice, int[] defenderDice)` hub method — Unity sends results back

### Server — `Risk.Server/Services/GameService.cs`
- Add `ResolveCombat(string connectionId, int sourceId, int targetId, int[] attackerDice, int[] defenderDice)` — same logic as current `Attack()` but uses provided dice values instead of calling `RollDice()`
- Current `Attack()` still exists for fallback (no Unity connected, or timeout)

### Server — `Risk.Server/Models/` (new DTO)
- `CombatRollRequest` record: `{ int SourceId, int TargetId, int AttackerDiceCount, int DefenderDiceCount }`

### Unity — `Assets/Scripts/SignalRClient.cs`
- Call `RegisterAsTV()` after connection established
- Listen for `CombatRollRequest` event
- Add `SendDiceResult()` method to invoke `SubmitDiceResult` on hub

### Unity — `Assets/Scripts/CombatTheatre.cs`
- Handle `CombatRollRequest` (new event) — spawn dice, wait for settle, read faces, send result
- Still handle `CombatResult` (comes back after server resolves) — for UI updates/state sync

### Unity — `Assets/Scripts/DiceRoller.cs`
- Remove `CorrectFace`, `GetRotationForFace`, `GetFaceLocalAxis`
- New method: `RollAndRead()` — spawns dice, waits for settle, reads faces with `DiceFaceReader`, returns values
- No face manipulation at all

## Sequence Detail

### Server-side (Attack method modification)
```csharp
public async Task Attack(int sourceId, int targetId, int diceCount)
{
    // ... existing validation ...

    if (_game.IsUnityTVConnected)
    {
        int defenderDiceCount = target.Armies >= 2 ? 2 : 1;
        await Clients.Group("unity-tv").SendAsync("CombatRollRequest", new {
            sourceId, targetId, attackerDiceCount = diceCount, defenderDiceCount
        });
        // Server waits for SubmitDiceResult (handled via TaskCompletionSource with 5s timeout)
    }
    else
    {
        // Existing flow — server rolls and resolves immediately
        var (state, result) = _game.Attack(Context.ConnectionId, sourceId, targetId, diceCount);
        await Clients.All.SendAsync("CombatResult", result);
        await BroadcastState(state);
    }
}
```

### Unity-side (CombatTheatre)
```csharp
async void OnCombatRollRequest(int sourceId, int targetId, int attackerCount, int defenderCount)
{
    ShowDicePanel(true);
    var (attackerValues, defenderValues) = await diceRoller.RollAndRead(attackerCount, defenderCount);
    await signalRClient.SendDiceResult(sourceId, targetId, attackerValues, defenderValues);
    // Panel stays visible until CombatResult comes back from server confirming outcome
}
```

## Timeout Handling

Server uses a `TaskCompletionSource` with 5-second timeout:
```csharp
private TaskCompletionSource<DiceResultDTO>? _pendingDiceResult;

// In Attack (when Unity connected):
_pendingDiceResult = new TaskCompletionSource<DiceResultDTO>();
await Clients.Group("unity-tv").SendAsync("CombatRollRequest", ...);
var completed = await Task.WhenAny(_pendingDiceResult.Task, Task.Delay(5000));
if (completed == _pendingDiceResult.Task)
    // Use Unity's dice values
else
    // Timeout: fall back to server random roll
```

## What Gets Removed from Unity
- `CorrectFace()` method
- `GetRotationForFace()` method  
- `GetFaceLocalAxis()` method
- All face correction logic in `RollDice()`
- The 1.5s "hold for readability" delay (replaced by waiting for server CombatResult response)

## What Gets Added to Unity
- `RegisterAsTV()` call on connect
- `CombatRollRequest` event handler
- `RollAndRead()` method on DiceRoller (spawn → settle → read → return values)
- `SendDiceResult()` method on SignalRClient

## Implementation Order
1. Server: Add `RegisterAsTV` hub method + tracking flag
2. Server: Add `CombatRollRequest` DTO and broadcast
3. Server: Add `SubmitDiceResult` hub method with TaskCompletionSource + timeout
4. Server: Modify `Attack()` to branch on Unity connection
5. Server: Add `ResolveCombat()` in GameService (accepts external dice values)
6. Unity: Call `RegisterAsTV` on connect
7. Unity: Listen for `CombatRollRequest`
8. Unity: Implement `RollAndRead()` (strip correction code)
9. Unity: Send results back via `SubmitDiceResult`
10. Test: with Unity → physics dice, without Unity → server rolls as before
