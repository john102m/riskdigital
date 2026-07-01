# Proposal: Error Handling & Recovery Improvements

## Problem

The game can reach stuck states that require a manual `/admin/reset`. Most failures are silent — the game just stops progressing with no indication of what went wrong. The system assumes everything works; it has no fallback paths when things don't.

---

## 1. Stuck Game Timeout on Roll Phase

### Current behaviour
`AttackWithDice` awaits both `AttackerRoll.Task` and `DefenderRoll.Task` with no timeout:
```csharp
await Task.WhenAll(_pending.AttackerRoll.Task, _pending.DefenderRoll.Task);
```
If a handset disconnects, the prompt is lost, or the event never arrives — infinite await.

### Proposed fix
Add a 30s timeout wrapping the roll phase. If it fires, abandon the pending combat and fall back to server-side roll.

```csharp
var rollsCompleted = Task.WhenAll(_pending.AttackerRoll.Task, _pending.DefenderRoll.Task);
var timeout = Task.Delay(30000);
if (await Task.WhenAny(rollsCompleted, timeout) == timeout)
{
    _pending = null;
    return Attack(connectionId, sourceId, targetId, diceCount);
}
```

### Why 30s
- Long enough that a human defender can pick up their phone, unlock it, and tap Roll
- Short enough that if something is genuinely stuck, the game recovers within half a minute
- Bots roll within 1s, so this only triggers on real failures

---

## 2. AI Turn Failure Recovery

### Current behaviour
```csharp
private async Task RunTurnAsync()
{
    try { ... }
    catch (Exception ex)
    {
        Console.WriteLine($"AI error: {ex.Message}");
    }
}
```
If the AI throws mid-turn (after reinforcing, during attack, etc.), the catch swallows it and the bot's turn never ends. Next player never gets control. Game frozen.

### Proposed fix
In the catch block, force-end the turn so the game advances:

```csharp
catch (Exception ex)
{
    Console.WriteLine($"AI error: {ex.Message}");
    try
    {
        // Force advance to next player
        if (game.State?.TurnPhase == TurnPhase.Reinforce && player.ReinforcementsRemaining > 0)
        {
            // Can't end reinforce with armies remaining — place them randomly
            var owned = game.State.Territories.Where(t => t.OwnerId == game.State.CurrentPlayerIndex).ToList();
            while (player.ReinforcementsRemaining > 0 && owned.Count > 0)
                game.Reinforce(connId, owned[Random.Shared.Next(owned.Count)].Id);
            game.EndReinforce(connId);
        }
        if (game.State?.TurnPhase == TurnPhase.Attack)
            game.EndAttack(connId);
        if (game.State?.TurnPhase == TurnPhase.Fortify)
            game.EndTurn(connId);
        
        await Broadcast();
        await hub.Clients.All.SendAsync("TurnStarted", game.State!.CurrentPlayerIndex);
        TriggerIfAi();
    }
    catch { /* Last resort — at least we tried */ }
}
```

### Why this matters
AI errors are silent. The only sign is the game stops. With 4 bot players in a test, one error kills the entire session.

---

## 3. Unity Disconnect — Immediate Fallback

### Current behaviour
When Unity disconnects (`OnDisconnectedAsync`), we call `UnregisterTV` which nulls the connection ID. But if `_pending` exists (combat in flight), the `DiceResult` TCS waits until the 10s timeout before falling back.

### Proposed fix
On `UnregisterTV`, if there's an active pending combat, immediately cancel it:

```csharp
public void UnregisterTV(string connectionId)
{
    if (_unityTVConnectionId == connectionId)
    {
        _unityTVConnectionId = null;
        // Force-fail any pending dice result so server falls back immediately
        _pending?.DiceResult.TrySetCanceled();
    }
}
```

Then in `AttackWithDice`, handle the cancellation:
```csharp
var completed = await Task.WhenAny(_pending.DiceResult.Task, Task.Delay(10000));
if (completed == _pending.DiceResult.Task && !_pending.DiceResult.Task.IsCanceled)
{
    var (attackerDice, defenderDice) = await _pending.DiceResult.Task;
    _pending = null;
    return ResolveCombat(connectionId, sourceId, targetId, attackerDice, defenderDice);
}

// Timeout OR Unity disconnected — fall back
_pending = null;
return Attack(connectionId, sourceId, targetId, diceCount);
```

### Result
Instead of 10s frozen stare at the TV, the game continues within milliseconds of Unity dropping.

---

## 4. ForcedTrade Re-send on Rejoin

### Current behaviour
If a player disconnects during the forced trade gate (5+ cards, must trade before placing), the `ForcedTradeRequired` event is lost. On reconnect, `Rejoin` sends `GameStateUpdated` and `CardsUpdated` but not `ForcedTradeRequired`. The player sees the Reinforce screen but can't place because the server blocks it — with no trade UI visible.

### Proposed fix
In `GameHub.Rejoin`, after re-sending cards, check if forced trade applies:

```csharp
if (player is not null)
{
    await Clients.Caller.SendAsync("CardsUpdated", player.Cards);
    if (player.Mission is not null)
        await Clients.Caller.SendAsync("MissionUpdated", player.Mission);

    // Re-send RollPrompt if pending defender (already done)
    ...

    // Re-send ForcedTradeRequired if applicable
    if (_game.State.Phase == GamePhase.Playing
        && _game.State.TurnPhase == TurnPhase.Reinforce
        && _game.State.Players[_game.State.CurrentPlayerIndex] == player
        && player.Cards.Count >= 5)
    {
        await Clients.Caller.SendAsync("ForcedTradeRequired", player.Cards);
    }
}
```

### Why
Same pattern as the `RollPrompt` re-send. Rejoin should restore the player to the exact state they were in, including any modal/gate that was active.

---

## 5. Toast Instead of Alert (Lower Priority)

### Current behaviour
Every hub invocation error shows a blocking `alert()` dialog on the handset. Typically "Not your turn" or "Not enough armies."

### Proposed fix
Create a `<Toast>` component:
- Red banner at top of screen
- Auto-dismisses after 3s
- Doesn't block interaction
- Stacks if multiple errors arrive quickly

### Implementation
```tsx
// Toast state in App.tsx
const [toast, setToast] = useState<string | null>(null);

// Pass setToast to all screens, replace alert(e.message) with setToast(e.message)

// Auto-dismiss
useEffect(() => {
  if (toast) { const t = setTimeout(() => setToast(null), 3000); return () => clearTimeout(t); }
}, [toast]);
```

### Why not urgent
`alert()` works. It's ugly and blocking, but you never miss the message. Toast risks being missed. Only worth doing during a proper UX polish pass.

---

## 6. State Persistence (Future / Large)

### Current behaviour
All state in memory. Server restart = game lost.

### Proposed fix (when ready)
- Serialize `GameState` to JSON file after every state-changing action
- On startup, check for saved state file → offer resume
- Admin endpoint to force-save and force-load
- Store in `Data/saved-game.json`

### Why not now
- WHUK is stable, rarely recycles
- Local dev restarts are intentional (testing)
- Games are 30-60 minutes, not overnight sessions
- Adds complexity to every state mutation

---

## Summary

| # | Fix | Impact | Effort | Dependencies |
|---|-----|--------|--------|--------------|
| 1 | Roll phase 30s timeout | Prevents infinite stuck | ~5 lines in `AttackWithDice` | None |
| 2 | AI turn failure recovery | Prevents bot-freeze | ~20 lines in `AiService` | None |
| 3 | Unity disconnect immediate fallback | Removes 10s delay | ~5 lines in `UnregisterTV` + guard in `AttackWithDice` | None |
| 4 | ForcedTrade re-send on rejoin | Fixes edge case stuck | ~8 lines in `GameHub.Rejoin` | None |
| 5 | Toast component | UX polish | New component + replace all `alert()` | Lower priority |
| 6 | State persistence | Disaster recovery | Serialization + resume flow | Future |

Items 1–4 are independent, minimal, and can be done in any order. Each prevents a specific "game frozen" scenario you've already encountered or will encounter.

---

*Created: 2026-06-29*
