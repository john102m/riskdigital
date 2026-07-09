# Proposal: Fix Defender TV Red Dice Snap Timing

## Problem

On the defender's TV (cross-household), the ghost red attacker dice settle with random physics faces and sit there while the human defender ponders. When the defender taps Roll, the blue dice spawn and roll — but the red dice only snap to correct values around the same time as (or after) the blue dice settle. The defender never sees the actual attacker result before their own dice roll.

## Root Cause

The attacker TV physically settles its dice immediately (physics/gravity), so they look correct on the attacker's screen. But the **read and submit** step doesn't start until `SpawnDice("defender")` arrives. The code deliberately waits: "Don't settle yet — wait for SpawnDice("defender") to know the household layout."

For a human defender, `SpawnDice("defender")` only arrives when they tap Roll (via `PlayerRoll`). So the sequence is:

1. Attacker TV spawns red dice — they physically settle and look fine on attacker's screen
2. Defender TV ghost-rolls red, settles with **random physics faces** (no authoritative values yet)
3. Defender handset shows RollPrompt, human ponders (sees random red faces on TV)
4. Human taps Roll → `PlayerRoll` → `SpawnDice("defender")` broadcast
5. Attacker TV finally **reads and submits** the already-settled dice → `AttackerDiceResult` broadcast
6. Defender TV snaps red to correct values (too late — blue dice already rolling/settled)

The attacker needs to read and submit **before** the defender taps Roll, so `AttackerDiceResult` reaches the defender TV while the human is still pondering.

## Fix

Start reading and submitting attacker dice immediately once they physically settle, rather than waiting for `SpawnDice("defender")`. The dice are already settled on the attacker TV — we just need to read faces and submit to the server straight away. If the role later upgrades to SameHousehold (both events arrive before read completes), abort and let the combined handler take over.

### CombatTheatre.cs Changes

**1. Add a flag to prevent double-submit:**

```csharp
bool attackerAlreadySubmitted = false; // reset in ResetCombat()
```

In `ResetCombat()`:
```csharp
attackerAlreadySubmitted = false;
```

**2. In `OnSpawnDice`, attacker `isMine` branch — start settling immediately:**

```csharp
if (isMine)
{
    currentRole = MyRole.Attacker;
    state = CombatState.Rolling;
    diceRoller.SpawnSet(role, diceCount);
    _ = EarlySettleAttacker();
}
```

**3. New method — `EarlySettleAttacker`:**

```csharp
/// <summary>
/// Settle attacker dice immediately and submit if still in Attacker role.
/// If role upgrades to SameHousehold before settle completes, abort — the
/// SpawnDice("defender") handler's WaitSettleAttacker handles the combined submit.
/// This ensures AttackerDiceResult reaches the defender TV before they tap Roll.
/// </summary>
async Awaitable EarlySettleAttacker()
{
    var token = combatCts?.Token ?? default;
    await diceRoller.WaitForSettle();
    if (token.IsCancellationRequested) return;

    // Upgraded to SameHousehold — let WaitSettleAttacker handle combined submit
    if (currentRole != MyRole.Attacker) return;
    if (state != CombatState.Rolling) return;

    var (attackerValues, _) = diceRoller.ReadAll();
    attackerAlreadySubmitted = true;
    await signalR.SendRolledDice("attacker", attackerValues);
    Debug.Log($"[Combat] EarlySettleAttacker sent: [{string.Join(",", attackerValues)}]");
    state = CombatState.ShowingResult;
    // Hold open — wait for DefenderDiceResult to snap blue dice and dismiss
}
```

**4. In `OnSpawnDice` defender, `currentRole == Attacker && !isMine` branch — guard against double work:**

```csharp
else if (currentRole == MyRole.Attacker && !isMine)
{
    // Cross-household confirmed — ghost roll blue
    Debug.Log($"[Combat] Cross-household confirmed — ghost rolling blue");
    diceRoller.SpawnSetGhost(role, diceCount);
    // If attacker already submitted via EarlySettleAttacker, nothing more to do.
    // If still settling (fast defender tap), EarlySettleAttacker will finish and submit.
    if (!attackerAlreadySubmitted)
        _ = WaitSettleAttacker();
}
```

**5. Make `ReadAll` public** (currently private — `EarlySettleAttacker` needs to call settle and read separately):

In `DiceRoller.cs`, change:
```csharp
(int[] attackerValues, int[] defenderValues) ReadAll()
```
to:
```csharp
public (int[] attackerValues, int[] defenderValues) ReadAll()
```

### Why This Is Safe

| Scenario | Behaviour |
|----------|-----------|
| Cross-household, human defender (the bug) | Attacker settles immediately → submits → server broadcasts `AttackerDiceResult` → defender TV snaps red while human ponders → human taps Roll → blue roll alongside correct red faces ✅ |
| Cross-household, bot defender | `SpawnDice("defender")` arrives almost immediately (server sends it in `AttackWithDice`). If before attacker settles: `EarlySettleAttacker` still running, `!attackerAlreadySubmitted` → `WaitSettleAttacker()` starts too. `EarlySettleAttacker` completes first, submits, sets flag. `WaitSettleAttacker` checks `currentRole == Attacker` and submits again... **WAIT** — need guard here too. |
| Same-household | `SpawnDice("defender")` with `isMine` arrives → role upgrades to `SameHousehold` → `EarlySettleAttacker` sees role changed, aborts → `WaitSettleAttacker` (SameHousehold path) handles combined submit ✅ |

### Bot Defender Double-Submit Guard

`WaitSettleAttacker` also needs the guard for the `Attacker` path:

```csharp
else if (currentRole == MyRole.Attacker)
{
    var (attackerValues, _) = await diceRoller.WaitAndReadAll();
    if (token.IsCancellationRequested || state != CombatState.Rolling) return;
    if (attackerAlreadySubmitted) return; // EarlySettleAttacker already handled this

    attackerAlreadySubmitted = true;
    await signalR.SendRolledDice("attacker", attackerValues);
    Debug.Log($"[Combat] Attacker sent: [{string.Join(",", attackerValues)}]");
    state = CombatState.ShowingResult;
}
```

## DiceRoller.cs Change

Make `ReadAll()` public. No other changes needed.

## Server Changes

None. The server already handles `SubmitRolledDice("attacker")` arriving at any time — it fires `AttackerSubmitted` TCS, and the `Task.Run` broadcasts `AttackerDiceResult` to the group. The only difference is it now fires before `RollPrompt` is acted on (instead of after).

## Test Matrix

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Cross-household: you attack bot (human attacker, bot defender) | Attacker settles immediately, submits. Bot defender spawns from server. Both TVs show correct dice. |
| 2 | Cross-household: bot attacks you (human defender) | Attacker TV settles fast, submits. Defender TV snaps red while human ponders. Human taps Roll, blue roll with correct red visible. **This is the fix.** |
| 3 | Same-household | EarlySettleAttacker aborts on role upgrade. Combined submit via WaitSettleAttacker. Unchanged. |
| 4 | Bot vs bot cross-household | Both TVs ghost roll. EarlySettleAttacker submits on attacker TV. Defender TV gets early snap. |
| 5 | Single TV (no household) | All players are "mine". Attacker spawns, EarlySettleAttacker starts. Defender spawns with isMine → SameHousehold upgrade → EarlySettleAttacker aborts. Combined submit. Unchanged. |

## Summary of File Changes

| File | Change |
|------|--------|
| `Assets/Scripts/CombatTheatre.cs` | Add `attackerAlreadySubmitted` flag; reset in `ResetCombat()`; new `EarlySettleAttacker()` method; start it in attacker `isMine` branch; guard in defender `Attacker && !isMine` branch; guard in `WaitSettleAttacker` Attacker path |
| `Assets/Scripts/DiceRoller.cs` | Make `ReadAll()` public |
