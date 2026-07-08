# Proposal — Multi-TV Combat Refactor (3 Phases)

*2026-07-08*

---

## Goal

Improve robustness, readability, and correctness of the multi-TV dice system.
Three phases in order of risk — each can be committed independently.
Phase 1 is safe low-effort hardening. Phase 2 is the architectural improvement
that removes the root cause of most past bugs. Phase 3 is the clean-up that
Phase 2 enables.

---

## Phase 1 — Quick Hardening

Three independent fixes. No architectural change. Low risk.

---

### 1a — Reconnect preserves household config (`SignalRClient.cs`)

**Problem:** On any reconnect (WiFi blip, sleep, SignalR transport switch),
the `Reconnected` handler calls plain `RegisterAsTV` with no household info.
The server re-registers the TV as single-TV mode. Dice routing breaks for the
remainder of the session.

**Fix:** Use the same registration path as the initial join:

```csharp
// Before:
connection.Reconnected += async id =>
{
    if (!string.IsNullOrEmpty(joinedGameCode))
    {
        await connection.InvokeAsync("RegisterAsTV", joinedGameCode);
        await connection.InvokeAsync("GetState");
    }
};

// After:
connection.Reconnected += async id =>
{
    if (!string.IsNullOrEmpty(joinedGameCode))
    {
        try
        {
            if (!string.IsNullOrEmpty(householdId) && playerIndices.Length > 0)
                await connection.InvokeAsync("RegisterAsTVWithHousehold", joinedGameCode, householdId, playerIndices);
            else
                await connection.InvokeAsync("RegisterAsTV", joinedGameCode);
            await connection.InvokeAsync("GetState");
        }
        catch
        {
            joinedGameCode = null;
            UnityMainThread.Enqueue(() => OnGameStateUpdated?.Invoke(null));
        }
    }
};
```

**File:** `Assets/Scripts/SignalRClient.cs`

---

### 1b — Clear `_pending` on exception in `GameHub.Attack` (`GameHub.cs`)

**Problem:** If `AttackWithDice` throws an unhandled exception (e.g. during
broadcast), `_pending` is left set. The next attack is rejected with
"Combat in progress". AI service has a catch that calls `ClearPending()`,
but human attacks via `GameHub.Attack` do not.

**Fix:** Wrap in try/finally:

```csharp
// Before:
var (state, result) = await game.AttackWithDice(_hubContext, gameCode, Context.ConnectionId, sourceId, targetId, diceCount);

// After:
(GameState state, CombatResult result) attackResult;
try
{
    attackResult = await game.AttackWithDice(_hubContext, gameCode, Context.ConnectionId, sourceId, targetId, diceCount);
}
finally
{
    game.ClearPending();  // safe to call even if already null
}
var (state, result) = attackResult;
```

**File:** `server/Risk.Server/Hubs/GameHub.cs`

---

### 1c — Remove unused TCS fields from `PendingCombat` (`PendingCombat.cs`)

**Problem:** `AttackerRoll` and `DefenderRoll` are `TaskCompletionSource<int>`
fields that are pre-completed in both paths but never awaited. They're leftover
from an earlier sequential design. They add noise and confusion.

`AttackerSubmitted` is also unused in the current parallel flow — the cross-
household path no longer waits for attacker submission before spawning defender
dice. It can also be removed.

**Fix:** Remove `AttackerRoll`, `DefenderRoll`, and `AttackerSubmitted` from
`PendingCombat`. Update any callers that reference them.

```csharp
// Remove these:
public TaskCompletionSource<int> AttackerRoll { get; } = new();
public TaskCompletionSource<int> DefenderRoll { get; } = new();
public TaskCompletionSource<int[]> AttackerSubmitted { get; } = new();

// Also remove from SubmitAttackerDice:
AttackerSubmitted.TrySetResult(dice);  // ← remove this line
```

Check `AttackWithDice` for any remaining references to `AttackerRoll`,
`DefenderRoll`, `AttackerSubmitted` and remove them.

**File:** `server/Risk.Server/Models/PendingCombat.cs`,
`server/Risk.Server/Services/GameService.Combat.cs`

---

## Phase 2 — Explicit TV Role in `SpawnDice`

The root cause of most past bugs: each TV infers its role from event sequence
rather than being told explicitly. This phase eliminates that inference.

---

### The problem in detail

When `SpawnDice("attacker")` arrives, the TV doesn't yet know if it's:
- The **attacker** (isMine=true, should roll red and submit)
- The **non-owning attacker TV** (isMine=false, should ghost roll red)

It sets `currentRole = Attacker` or `currentRole = None` and waits for
`SpawnDice("defender")` to confirm the layout. During this window,
`AttackerDiceResult` may arrive — the handler must deal with an ambiguous
`currentRole = None` state.

This window is the source of every ordering-dependent bug fixed to date.

### The fix

Add a `TvRole` field to the `SpawnDice` record. The server knows which TV
is which (via `GetTVForPlayer`), so it can tell each TV its role directly:

```
"roll"      — this TV physically rolls these dice and submits
"ghost"     — this TV ghost-rolls for visual effect, does not submit
"spectate"  — this TV has no dice for this role (displays result statically)
```

**Server — `CombatResult.cs`:**

```csharp
public record SpawnDice(
    string Role,        // "attacker" or "defender"
    int DiceCount,
    int SourceId,
    int TargetId,
    int PlayerIndex,    // kept for backward compat / logging
    string TvRole       // "roll", "ghost", "spectate"  ← new
);
```

**Server — `GameService.Combat.cs` — same-household path:**

```csharp
// Both spawns go to group. Owning TV gets "roll", others get "spectate".
await hub.Clients.Group(gameCode).SendAsync("SpawnDice",
    new SpawnDice("attacker", diceCount, sourceId, targetId,
        _pending.AttackerPlayerIndex, "roll"));   // owning TV rolls

// Spectator TVs receive the same event but with "spectate"
// → server needs to send two events (one to owning TV, one to rest of group)
// OR Unity uses PlayerIndex + IsMyPlayer to determine role as before
// (see trade-offs below)
```

**Trade-off:** Sending different payloads to different TVs requires the server
to send one event to the owning TV and a different event to the rest of the group.
This means two `SendAsync` calls per spawn instead of one group broadcast.

Alternative: keep the group broadcast but add `TvRole` computed client-side
from `PlayerIndex + IsMyPlayer()`. This is essentially what we have now —
no improvement.

**Recommended approach:** Two sends per spawn:

```csharp
// Attacker spawn — cross-household path:
var attackerTv = GetTVForPlayer(_pending.AttackerPlayerIndex);
await hub.Clients.Client(attackerTv).SendAsync("SpawnDice",
    new SpawnDice("attacker", diceCount, sourceId, targetId,
        _pending.AttackerPlayerIndex, "roll"));
await hub.Clients.GroupExcept(gameCode, attackerTv).SendAsync("SpawnDice",
    new SpawnDice("attacker", diceCount, sourceId, targetId,
        _pending.AttackerPlayerIndex, "ghost"));
```

**Unity — `SignalRClient.cs`:**

Update `SpawnDice` handler to pass `tvRole` through:

```csharp
string tvRole = spawn.GetProperty("tvRole").GetString();
UnityMainThread.Enqueue(() => OnSpawnDice?.Invoke(role, diceCount, sourceId, targetId, playerIndex, tvRole));
```

**Unity — `CombatTheatre.cs`:**

`OnSpawnDice` becomes simple and explicit — no inference:

```csharp
void OnSpawnDice(string role, int diceCount, int sourceId, int targetId, int playerIndex, string tvRole)
{
    if (role == "attacker")
    {
        ResetCombat();
        currentSourceId = sourceId;
        currentTargetId = targetId;
        PositionPanel(sourceId, targetId);
        ShowPanel(true);
        StartCameraSweep();
        state = CombatState.Rolling;

        switch (tvRole)
        {
            case "roll":
                currentRole = MyRole.Attacker;
                diceRoller.SpawnSet(role, diceCount);
                break;
            case "ghost":
                currentRole = MyRole.None;
                diceRoller.SpawnSetGhost(role, diceCount);
                _ = WaitSettleGhostRed();
                break;
            case "spectate":
                currentRole = MyRole.None;
                // No dice — wait for AttackerDiceResult
                break;
        }
    }
    else if (role == "defender")
    {
        currentSourceId = sourceId;
        currentTargetId = targetId;

        switch (tvRole)
        {
            case "roll":
                if (currentRole == MyRole.Attacker)
                    currentRole = MyRole.SameHousehold;
                else
                    currentRole = MyRole.Defender;

                if (state != CombatState.Rolling)
                {
                    PositionPanel(sourceId, targetId);
                    ShowPanel(true);
                    StartCameraSweep();
                }
                state = CombatState.Rolling;

                if (lastAttackerValues.Length > 0)
                    diceRoller.SnapFacesForRole("attacker", lastAttackerValues);

                diceRoller.SpawnSet(role, diceCount);

                if (currentRole == MyRole.SameHousehold)
                    _ = WaitSettleAttacker();
                else
                    _ = WaitSettleDefender();
                break;

            case "ghost":
                diceRoller.SpawnSetGhost(role, diceCount);
                if (currentRole == MyRole.Attacker)
                    _ = WaitSettleAttacker(); // cross-household confirmed — submit attacker only
                break;

            case "spectate":
                // No dice — wait for DefenderDiceResult
                break;
        }
    }
}
```

`IsMyPlayer()` can be removed entirely.

**Files:**
- `server/Risk.Server/Models/CombatResult.cs` — `SpawnDice` gains `TvRole`
- `server/Risk.Server/Services/GameService.Combat.cs` — two sends per spawn
- `Assets/Scripts/SignalRClient.cs` — pass `tvRole` through event
- `Assets/Scripts/CombatTheatre.cs` — `OnSpawnDice` switch on `tvRole`, remove `IsMyPlayer()`

---

## Phase 3 — Split Role Handlers

Only attempt after Phase 2 is stable. Phase 2 makes the role explicit up-front,
which makes splitting natural.

---

### The idea

`CombatTheatre` currently handles all four roles in one class with branching
in every event handler. With explicit roles (Phase 2), each role's event
handling is a clean linear sequence. Split into a strategy pattern:

```csharp
interface ICombatRoleHandler
{
    void OnAttackerDiceResult(AttackerDiceResultDTO dto);
    void OnDefenderDiceResult(int[] values);
    void OnCombatResult(CombatResultDTO result);
}

class AttackerRoleHandler : ICombatRoleHandler { ... }
class DefenderRoleHandler : ICombatRoleHandler { ... }
class SameHouseholdRoleHandler : ICombatRoleHandler { ... }
class SpectatorRoleHandler : ICombatRoleHandler { ... }
```

`CombatTheatre` creates the appropriate handler when `SpawnDice("attacker")`
arrives (role is now known immediately from `tvRole`). All subsequent events
are forwarded to the handler. `ResetCombat` nulls the handler.

**Benefits:**
- Each handler is 30-50 lines, linear, easy to read
- Adding a new role doesn't require touching existing handlers
- Each handler can be tested in isolation
- No branching on `currentRole` inside event handlers

**Files:**
- `Assets/Scripts/CombatTheatre.cs` — gutted to coordinator only
- `Assets/Scripts/Combat/AttackerRoleHandler.cs` — new
- `Assets/Scripts/Combat/DefenderRoleHandler.cs` — new
- `Assets/Scripts/Combat/SameHouseholdRoleHandler.cs` — new
- `Assets/Scripts/Combat/SpectatorRoleHandler.cs` — new
- `Assets/Scripts/Combat/ICombatRoleHandler.cs` — new

---

## Phase Summary

| Phase | Risk | Effort | Gain |
|-------|------|--------|------|
| 1a — Reconnect fix | Very low | 5 min | Household config survives WiFi blip |
| 1b — ClearPending on exception | Very low | 5 min | No stuck "Combat in progress" on human attack crash |
| 1c — Remove unused TCS | Low | 15 min | Cleaner PendingCombat, less confusion |
| 2 — Explicit TvRole | Medium | 1-2 hours | Eliminates role inference, removes ordering bugs at root |
| 3 — Split handlers | Low (post Phase 2) | 2-3 hours | Readability, testability |

Each phase is independently committable. Do not start Phase 2 until Phase 1
is committed and tested. Do not start Phase 3 until Phase 2 is committed and
all 4 test scenarios pass.

---

*Created: 2026-07-08*
