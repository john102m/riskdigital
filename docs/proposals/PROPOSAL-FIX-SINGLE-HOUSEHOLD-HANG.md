# Findings & Proposal — Fix Single-Household Dice Hang

*2026-07-08*

---

## Background

Branch `feat/simultaneous-arena-sweep` introduced the parallel-spawn architecture:
server broadcasts `SpawnDice` to all TVs simultaneously, each TV self-determines
ownership via `PlayerIndex`, and ghost-rolls play on non-owning TVs with faces snapped
on result arrival.

All four multi-household tests were reported green at end of SESSION-2026-07-07-LATE.
Single-household play (one TV, no `householdId` set) is broken: arena opens, dice
roll, then the arena dismisses early and re-opens showing only the defender dice.
Feels like a broken sequence from the player's perspective.

---

## Findings

### What actually happens (single-household flow trace)

**Server — `AttackWithDice`, same-household path:**

1. `GetTVForPlayer(attacker)` → `_registeredTVs.Count == 1` → returns the single TV
2. `GetTVForPlayer(defender)` → same → same connection ID
3. `sameHousehold = true`
4. Sends `SpawnDice("attacker")` and `SpawnDice("defender")` to group simultaneously
5. Pre-fires `AttackerRoll` / `DefenderRoll` TCS (unused by this path)
6. Waits on `_pending.DiceResult.Task` (15s timeout)

**Unity — `CombatTheatre` / `DiceRoller`:**

1. `SpawnDice("attacker")` arrives → `IsMyPlayer()` → empty `householdId` → returns
   `true` → `currentRole = Attacker`, spawn red dice
2. `SpawnDice("defender")` arrives → `currentRole == Attacker && isMine` → upgrades
   to `SameHousehold`, spawns blue dice, calls `WaitSettleAttacker()`
3. All dice settle → `WaitAndReadAll()` returns → calls `signalR.SendDiceResult()`
   → hub method `SubmitDiceResult(attackerDice[], defenderDice[])` on server

**`SubmitDiceResult` in `GameHub.cs` — the bug:**

```csharp
public async Task SubmitDiceResult(int[] attackerDice, int[] defenderDice)
{
    game.SubmitDiceResult(attackerDice, defenderDice);  // ← fires DiceResult TCS ✓

    // Added in this branch to support spectator TVs:
    if (attackerDice.Length > 0)
        await GameGroup(gameCode).SendAsync("AttackerDiceResult", attackerDice);   // ← plain int[]
    if (defenderDice.Length > 0)
        await GameGroup(gameCode).SendAsync("DefenderDiceResult", defenderDice);
}
```

This fires `DiceResult.TrySetResult` correctly — the server DOES unblock.

However it then broadcasts `AttackerDiceResult` as a **plain `int[]`**.

The Unity `SignalRClient` handler for `AttackerDiceResult` now expects a JSON object
with `.values`, `.sourceId`, `.targetId` properties:

```csharp
connection.On<JsonElement>("AttackerDiceResult", result =>
{
    var dto = new AttackerDiceResultDTO
    {
        values  = result.GetProperty("values").EnumerateArray()...  // CRASH on int[]
        sourceId = result.GetProperty("sourceId").GetInt32()...
    };
    ...
});
```

When a plain `int[]` arrives, `GetProperty("values")` throws. The exception is
swallowed silently in the SignalR background thread. **The event handler never fires.**

Meanwhile the server has unblocked, resolved combat, and continues in `AttackWithDice`:

```csharp
var (shAttacker, shDefender) = await sameHouseDiceTask;
await hub.Clients.Group(gameCode).SendAsync("AttackerDiceResult",
    new { values = shAttacker, sourceId, targetId });    // ← correct shape, second broadcast
await hub.Clients.Group(gameCode).SendAsync("DefenderDiceResult", shDefender);
await Task.Delay(500);
_pending = null;
return ResolveCombat(...);
```

This sends `AttackerDiceResult` a **second time**, now with the correct shape.

### The double-broadcast sequence

| # | Event | From | Shape | Unity handler result |
|---|-------|------|-------|----------------------|
| 1 | `AttackerDiceResult` | `SubmitDiceResult` in Hub | `int[]` | **Crashes silently** — no handler fires |
| 2 | `DefenderDiceResult` | `SubmitDiceResult` in Hub | `int[]` | Fires correctly (handler uses `On<int[]>`) |
| 3 | `AttackerDiceResult` | `AttackWithDice` | `{values, sourceId, targetId}` | Fires correctly — but state is wrong |
| 4 | `DefenderDiceResult` | `AttackWithDice` | `int[]` | Fires again — second time |

**What the TV sees:**

- Event 2 (`DefenderDiceResult`) arrives while `state == ShowingResult` and
  `currentRole == SameHousehold` → correctly ignored (guard fires)
- Event 3 (`AttackerDiceResult`) arrives after `DismissAfterHold(3f)` has already
  been scheduled. `currentRole` may still be `SameHousehold` → ignored. Or if
  `ResetCombat()` already ran, `state == Idle` and `currentRole == None` → falls
  into the spectator/ghost path → **arena panel re-opens**
- Event 4 (`DefenderDiceResult`) arrives into the re-opened arena → spectator path →
  shows only defender dice

This produces the observed symptom: **arena dismisses early, then re-opens on the
same panel showing only the defender result.**

### Why previous AI attempts failed

Every previous attempt focused on `CombatTheatre.cs` or `DiceRoller.cs` — the Unity
side. The root cause is a server-side double-broadcast introduced when `SubmitDiceResult`
in `GameHub.cs` was upgraded to also emit `AttackerDiceResult`/`DefenderDiceResult`
for spectator support. That upgrade was correct in intent but created a conflict with
the identical broadcasts already inside `AttackWithDice`. The Unity code is correct.

---

## Proposed Fix

**One file. Three lines removed.**

### `server/Risk.Server/Hubs/GameHub.cs`

Remove the broadcasts from `SubmitDiceResult`. The legacy combined submit only needs
to fire the TCS — `AttackWithDice` already handles all broadcasts with correct timing
and correct payload shape.

```csharp
// Before:
public async Task SubmitDiceResult(int[] attackerDice, int[] defenderDice)
{
    var game = _manager.GetGameByConnection(Context.ConnectionId);
    var gameCode = _manager.GetGameCode(Context.ConnectionId);
    if (game == null || gameCode == null) return;

    game.SubmitDiceResult(attackerDice, defenderDice);

    // Broadcast to all TVs so remote households can place statically
    if (attackerDice.Length > 0)
        await GameGroup(gameCode).SendAsync("AttackerDiceResult", attackerDice);
    if (defenderDice.Length > 0)
        await GameGroup(gameCode).SendAsync("DefenderDiceResult", defenderDice);
}

// After:
public Task SubmitDiceResult(int[] attackerDice, int[] defenderDice)
{
    var game = _manager.GetGameByConnection(Context.ConnectionId);
    if (game == null) return Task.CompletedTask;

    game.SubmitDiceResult(attackerDice, defenderDice);
    return Task.CompletedTask;
}
```

No other changes needed.

---

## Why this is safe

- `AttackWithDice` already broadcasts `AttackerDiceResult` and `DefenderDiceResult`
  with the correct `{values, sourceId, targetId}` shape after both dice sets are
  known, with a 500ms pause before `ResolveCombat`. This serves all TVs including
  spectators.
- The `SubmitDiceResult` broadcasts were redundant at best, destructive at worst
  (wrong shape, wrong timing, fired before `AttackWithDice` has a chance to act).
- `SubmitRolledDice` (the multi-household split path) never broadcasts — it only
  sets TCS values. That's already correct and untouched.
- Single-TV, multi-household, and spectator paths all rely on `AttackWithDice`'s
  broadcasts. None of them need `SubmitDiceResult` to also broadcast.

---

## Verification

After the server is rebuilt:

1. **Single-household (the broken case):**
   `/admin/testcombat?attacker=0&defender=1&dice=3`
   — One arena opens, red + blue dice tumble, both settle, both visible.
   Arena holds 3s then dismisses. No second arena.

2. **Cross-household (regression check):**
   `/admin/testcombat?attacker=0&defender=2&dice=3`
   — Z440 rolls red, laptop rolls blue (or ghost rolls). Both arenas correct.
   Spectator TV shows correct dice via `AttackerDiceResult`/`DefenderDiceResult`
   from `AttackWithDice` (unchanged).

3. **No 15s timeout** on server log — confirms `SubmitDiceResult` TCS fires and
   `AttackWithDice` unblocks promptly.

---

## Files Changed

| File | Change |
|------|--------|
| `server/Risk.Server/Hubs/GameHub.cs` | `SubmitDiceResult` — remove 3 broadcast lines, change `async Task` to `Task` |

No Unity changes. No model changes.

---

*Created: 2026-07-08*
