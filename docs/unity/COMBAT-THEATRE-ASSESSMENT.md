# CombatTheatre.cs — Assessment vs MULTI-HOUSEHOLD-DICE-FLOW

*2026-07-07, post-session review*

## Verdict

CombatTheatre faithfully implements all four scenarios from `MULTI-HOUSEHOLD-DICE-FLOW.md`. The architecture is correct. The one remaining bug (test #2 — defender never submits on Z440) is almost certainly in `DiceRoller.WaitAndReadAll` or the settle detection, not in CombatTheatre's orchestration logic.

---

## Scenario Coverage

| # | Scenario | Flow Doc Requires | CombatTheatre Does | Status |
|---|----------|-------------------|---------------------|--------|
| 1 | Human → Bot (cross-household) | Attacker TV rolls red, receives blue statically | `OnSpawnDice("attacker")` → physics → submit → hold → `OnDefenderDiceResult` places blue | ✅ |
| 2 | Bot → Human (cross-household) | Defender TV receives red statically, rolls blue | `OnAttackerDiceResult` places red → `OnSpawnDice("defender")` → physics → submit | ✅ (code correct, runtime bug in DiceRoller) |
| 3 | Same-household | One TV rolls both, submits combined | Role upgrades `Attacker → SameHousehold`, `WaitSettleAttacker` detects and sends both | ✅ |
| 4 | Human vs Human (cross-household) | Same as 1+2 with RollPrompt gate | Same code paths; RollPrompt is server-side | ✅ |

The **Key Principle** ("defender SpawnDice must wait for attacker to submit") is a server-side constraint enforced in `AttackWithDice`. CombatTheatre correctly just reacts to events as they arrive — no client-side ordering logic needed.

---

## Structural Strengths

- **Clean state machine** — `CombatState` enum (Idle → Rolling → ShowingResult → Hiding). No leaked flags between combats.
- **Role-per-combat** — `MyRole` enum determined once per combat, reset on next. Matches the spec's "no persistent flags" principle.
- **`ResetCombat()` is comprehensive** — cancels both CTS tokens, clears dice, hides panel, resets role and state.
- **State checks after every `await`** — prevents stale async tasks from acting on superseded combats.
- **`combatCts` pattern** — each new combat cancels all prior async work. Eliminates ghost dismiss/hide tasks.

---

## Issues Found

### 1. Test #2 Bug — Defender Never Submits (CRITICAL)

**Symptom:** Z440 rolls defender dice visually, sweep plays, dice appear to settle — but `SubmitRolledDice("defender")` never reaches server. 15s timeout fires.

**Code path:** `OnSpawnDice("defender")` → `WaitSettleDefender()` → `diceRoller.WaitAndReadAll()` → never returns.

**Root cause candidates (from session notes):**
1. Static attacker dice still in `activeDice` → `ReadTopFace()` NullRef on dice without `DiceFaceReader`
2. Velocity threshold (0.1) too tight for Z440 physics framerate → settle never detected
3. `settleTimeout` (4s) exceeded → method returns empty/null → `SendRolledDice` gets bad data

**No logging exists** in `WaitSettleDefender` after the await. Can't distinguish "never returns" from "returns garbage".

### 2. `OnAttackerDiceResult` Sets Role to None

```csharp
currentRole = MyRole.None; // will be set to Defender or Spectator by next event
```

Works because subsequent events (`SpawnDice("defender")` or `OnDefenderDiceResult`) set the correct role. But fragile — role inference relies on absence/presence of future events rather than explicit assignment. If event ordering changes, this breaks silently.

### 3. 1.5s Hardcoded Same-Household Window

```csharp
await Awaitable.WaitForSecondsAsync(1.5f); // wait for SpawnDice("defender") to arrive
```

If both `SpawnDice` calls arrive within 1.5s (they should on LAN), same-household detection works. Over WAN or under load, this could race. Not a problem today (LAN only) but worth noting.

### 4. `OnDefenderDiceResult` Uses Local CTS for Flypath

```csharp
var cts = new CancellationTokenSource();
await cameraFlypath.Fly(diceCamera.transform, cts.Token);
```

Should use `combatCts.Token` so a new combat cancels the spectator flypath. Currently, if a new combat starts during this await, the old flypath continues to completion.

### 5. Dead Parameters in `SendDiceResult`

```csharp
await signalR.SendDiceResult(0, 0, attackerValues, defenderValues);
```

Source/target IDs passed as 0 — server ignores them (uses `PendingCombat` state). Harmless but confusing. Should either pass real IDs or remove the parameters from the API.

### 6. Missing Logging at Critical Points

No `Debug.Log` at:
- Entry/exit of `WaitSettleDefender`
- After `WaitAndReadAll` returns (or if it throws)
- When `combatCts` cancellation fires mid-settle

These are the exact points needed to diagnose test #2.

---

## Recommendations (Next Session)

### Priority 1 — Fix Test #2

1. Add `Debug.Log` before and after `diceRoller.WaitAndReadAll()` in `WaitSettleDefender`
2. Add try/catch around the await — log any exception (NullRef from static dice?)
3. Log `diceRoller.ActiveDiceCount` at defender spawn time — confirm only blue dice are active
4. If `WaitAndReadAll` truly never returns: add frame-by-frame velocity logging inside `WaitForSettle`

### Priority 2 — Robustness

5. Replace local CTS in `OnDefenderDiceResult` with `combatCts.Token`
6. Consider explicit role assignment in `OnAttackerDiceResult` (set `MyRole.PendingDefender` or similar)
7. Add a safety timeout in `WaitSettleDefender` that logs + submits empty on expiry (prevents permanent hang)

### Priority 3 — Cleanup

8. Remove dead `0, 0` params from `SendDiceResult` call (or pass real IDs)
9. Make the 1.5s same-household window a `[SerializeField]` for tuning
10. Once stable: strip diagnostic logging to just state transitions

---

## File References

| File | Location |
|------|----------|
| CombatTheatre.cs | `D:\Unity Projects\RiskDigitalBoard\Assets\Scripts\CombatTheatre.cs` |
| DiceRoller.cs | `D:\Unity Projects\RiskDigitalBoard\Assets\Scripts\DiceRoller.cs` |
| Flow spec | `docs/unity/MULTI-HOUSEHOLD-DICE-FLOW.md` |
| Morning session | `docs/sessions/SESSION-2026-07-07.md` |
| Afternoon session | `docs/sessions/SESSION-2026-07-07-PM.md` |

---

---

## Fix Applied (18:17, 7 July 2026)

### Root Cause Confirmed

`ReadAll()` splits `activeDice` into attacker/defender values using `attackerDiceCount`. On the defender TV path (test #2), only `SpawnSet("defender")` is called — but `attackerDiceCount` retains its stale value from the previous combat (e.g. 3 from test #1).

With `activeDice.Count = 2` (defender dice only) and `attackerDiceCount = 3` (stale):
- `defenderCount = 2 - 3 = -1`
- Attacker read loop indexes `activeDice[0..2]` → **IndexOutOfRangeException**
- Exception swallowed silently inside async Awaitable → submit never fires → 15s server timeout

### Changes Made

**DiceRoller.cs:**

1. `SpawnSet("defender")` now sets `attackerDiceCount = activeDice.Count` — captures how many attacker dice are already in the list (0 on defender-only TV, real count on same-household).

2. `ClearDice()` resets `attackerDiceCount = 0` — prevents leakage between combats.

**CombatTheatre.cs:**

3. `WaitSettleDefender` wrapped in try/catch with `Debug.LogError` — surfaces any future exceptions instead of silent failure.

### Verification Matrix

| Path | attackerDiceCount | activeDice.Count | defenderCount | Correct? |
|------|-------------------|------------------|---------------|----------|
| Defender TV (test #2) | 0 | 2 | 2 | ✅ |
| Attacker TV (test #1) | 3 | 3 | 0 | ✅ |
| Same-household (test #3) | 3 | 5 | 2 | ✅ |
| Legacy RollAndRead | set explicitly | att+def | def | ✅ |

### Status: Awaiting runtime verification next session.

---

## Test Plan — Next Session

Linear progression with cumulative regression. No advancing until all prior tests still pass.

### Sequence

| Step | Run | Pass criteria | Regression |
|------|-----|---------------|------------|
| 1 | `?attacker=0&defender=2&dice=3` | Z440 rolls red, submits. Laptop rolls blue, submits. CombatResult on both. | — |
| 2 | `?attacker=2&defender=0&dice=3` | Laptop rolls red, submits. Z440 rolls blue, submits. CombatResult on both. | Re-run #1 ✅ |
| 3 | `?attacker=0&defender=1&dice=3` | Z440 rolls both (red+blue), submits combined. Laptop sees static dice (spectator). | Re-run #1 ✅ #2 ✅ |
| 4 | `?attacker=1&defender=2&dice=3` | Z440 rolls red (Alice), submits. Laptop rolls blue (Bob), submits. CombatResult on both. | Full sweep #1 ✅ #2 ✅ #3 ✅ |

### What to watch for

- **Unity console:** `[Combat] Defender sent:` / `[Combat] Attacker sent:` — confirms submission
- **Unity console:** `[DiceRoller] Spawned X role dice (attackerDiceCount=Y, activeDice=Z)` — confirms count fix
- **Unity console:** `[Combat] WaitSettleDefender FAILED:` — catches any new exceptions
- **Server app-log:** `DICE: SubmitRolledDice(role): [values]` — confirms server received
- **No 15s timeout** — if server falls back to random roll, something didn't submit

### If a test fails

1. Check Unity console for the new logging
2. Check server app-log for where the flow stopped
3. Fix before continuing — don't skip ahead
4. After fix, re-run ALL prior tests (full regression from #1)


---

## Evening Session Update (20:35, 7 July 2026)

Worked through the test matrix live. Summary of what was found, fixed, and what remains.

### Test Results (current deployed build)

| # | Scenario | Status |
|---|----------|--------|
| 1 | You → Bob (cross-household) | ✅ Passing (incl. flash-fix regression) |
| 2 | Bob → You (cross-household) | ✅ Passing — `attackerDiceCount` fix confirmed |
| 3 | You → Alice (same-household) | ⚠️ Works but spectator (laptop) display is jumpy/erratic |
| 4 | Alice → Bob (bot cross-household) | ✅ Passing — "arguably better", places known dice then rolls + sweeps |

### Fixes Landed This Session (in deployed build)

**DiceRoller.cs**
- `attackerDiceCount` bug (test #2 root cause): `SpawnSet("defender")` now sets `attackerDiceCount = activeDice.Count` (0 on defender-only TV), and `ClearDice()` resets it to 0. Fixes IndexOutOfRange in `ReadAll()` that silently killed defender submission.

**CombatTheatre.cs**
- `WaitSettleDefender` wrapped in try/catch with `Debug.LogError`.
- Flash fix: `OnAttackerDiceResult` no longer calls `ShowPanel(true)` — defender TV panel stays hidden until `SpawnDice("defender")` arrives with the sweep.
- Spectator display routed through the existing, proven `ShowBlitzDice` method (builds a `BlitzResultDTO` from the two dice sets). Reuses the working blitz sequence instead of a bespoke sweep.
- `OnCombatResult` guarded to return early when `state == ShowingBlitz`, so the resolve broadcast can't cut short the spectator display.

**GameService.Combat.cs (server)**
- Same-household fast path in `AttackWithDice`: when `GetTVForPlayer(attacker) == GetTVForPlayer(defender)`, send both `SpawnDice` calls immediately and wait for the combined `DiceResult` (no sequential attacker-submit wait). Cross-household path unchanged.
- Scope fixes: renamed same-household locals to `sameHouseDiceTask` / `shAttacker` / `shDefender` to avoid CS0136 collisions with the cross-household path.

### Root Cause of #3 Spectator Jumpiness (diagnosed, fix pending)

Confirmed via gameplay: **a blitz is a single broadcast** the spectator renders directly (smooth), **but a single roll** makes the spectator await the roller's dice settle, arriving as **two staggered events** — `AttackerDiceResult` then `DefenderDiceResult`. The intermediate `OnAttackerDiceResult` places the red dice, then `ShowBlitzDice` clears and re-places them → flicker/stutter. Blitz has no such intermediate event, so it's smooth.

### Pending Edits (ON DISK, NOT YET BUILT/DEPLOYED)

Two edits to `CombatTheatre.cs`, aimed at making the spectator render once (like blitz):

1. `OnAttackerDiceResult` — no longer places dice. Only stores `lastAttackerValues` and does the fresh-combat reset (`ClearDice`, `PositionPanel`, `state = Rolling`). Panel stays hidden.
2. Defender-spawn branch — now places the red attacker dice via `diceRoller.PlaceAttackerDiceOnly(lastAttackerValues, centre)` before spawning blue (moved from `OnAttackerDiceResult`).

Net effect: red placement moves out of the shared `OnAttackerDiceResult` into the defender-specific branch. The spectator (never hits the defender branch) gets a single clean `ShowBlitzDice` render.

**Regression risk:** #2 and #4 both use the defender-spawn branch. Both currently pass on the build WITHOUT these edits. After building the pending edits, retest #2, #3, #4.

### Next Steps

1. Rebuild + redeploy Unity to BOTH devices (Z440 editor + laptop build).
2. Retest matrix: #3 (target — spectator should now be smooth), then regression #2 and #4, then #1.
3. Report back.

> **Note on test endpoint vs gameplay:** `/admin/testcombat` snapshots and restores state, which fires an extra `BroadcastState`. If the restored phase ≠ "Attack", the spectator's `OnStateChanged` calls `ResetCombat` and can disturb the display — an artifact not present in real gameplay. Trust gameplay behaviour over the endpoint for spectator smoothness.

> **Deferred:** Blitz sequence is sweep-then-place (not place-then-sweep). Accepted as-is for now; noted as a possible future polish.
