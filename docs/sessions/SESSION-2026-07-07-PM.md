# Session Notes — 2026-07-07 (Afternoon)

## Multi-Household Dice: Test Infrastructure + CombatTheatre Rewrite

### What Was Fixed (Server)

1. **`ClearPending()` method** — added to `GameService`. Called in AI catch block so a failed bot attack doesn't permanently lock combat with stale `_pending`.

2. **AI error logging** — replaced rogue `File.AppendAllText("ai-error.log")` with proper `ILogger<AiService>`. Errors now visible in `/admin/app-log`.

3. **`/admin/testcombat` endpoint** — new admin endpoint for isolated dice flow testing:
   - `?attacker=0&defender=2&dice=3` — triggers full `AttackWithDice` sequential flow
   - Non-destructive: snapshots and restores all state (armies, ownership, phase, turn index, attack front)
   - Temporarily forces defender to IsAI so no RollPrompt needed
   - `&human=true` flag skips the AI override (sends RollPrompt to handset for real)
   - Auto-finds adjacent territory pair, pumps armies if needed
   - 2s delay before CombatResult broadcast so dice are visible
   - Returns full JSON result or error with stack trace
   - Clears `_pending` on failure

4. **Phase validation fix** — testcombat temporarily sets `TurnPhase.Attack` + `CurrentPlayerIndex` so `ResolveCombat` passes validation.

5. **VS2026 references** — updated README, AGENTS.md, preferences.md (was VS2022).

### What Was Fixed (Unity)

6. **CombatTheatre full rewrite** — replaced flag-based approach (`rollingAttacker`/`rollingDefender`/`cameraFlownThisTurn`) with role-per-combat enum:
   ```
   enum MyRole { None, Attacker, Defender, SameHousehold, Spectator }
   ```
   - Role determined once at combat start, used throughout
   - `ResetCombat()` clears everything between combats (role, state, dice, panel, cancels CTS)
   - `combatCts` cancellation token kills stale async tasks from previous combats
   - No persistent flags that leak between tests

7. **`OnCombatResult` always dismisses** — non-capture now triggers `DismissAfterHold(2f)` instead of doing nothing (was leaving arena hanging).

### Test Results

| # | Scenario | URL | Status |
|---|----------|-----|--------|
| 1 | You → Bob (cross-household) | `?attacker=0&defender=2&dice=3` | ✅ Works repeatedly |
| 2 | Bob → You (cross-household) | `?attacker=2&defender=0&dice=3` | ❌ Z440 rolls defender but never submits |
| 3 | You → Alice (same-household) | `?attacker=0&defender=1&dice=3` | ✅ Z440 works, laptop spectator has cosmetic issues |
| 4 | Alice → Bob (bot cross) | `?attacker=1&defender=2&dice=3` | ✅ Works (was fixed during session) |

### The One Remaining Bug

**Test #2: Z440 rolls defender dice visually but never calls `SubmitRolledDice("defender")`.**

- Server logs confirm: only `SubmitRolledDice(attacker)` appears, then 15s timeout
- Z440 Unity console shows NO `[Combat] Defender sent:` log
- Z440 shows the arena correctly (sweep, blue dice rolling)
- Dice appear to settle visually but `WaitForSettle()` never returns

**Root cause hypothesis:** `WaitForSettle` loops checking `activeDice` but either:
- The settle threshold (0.1 velocity) is too tight for the Z440's physics frame rate
- The `settleTimeout` (4s) is being exceeded and the method returns without logging
- Something about the dice spawned after `PlaceDiceAtValues` (static red) interferes

**What to investigate next session:**
1. Add `Debug.Log` inside `WaitForSettle` — log every frame: elapsed time, dice count, velocities
2. Add `Debug.Log` after `WaitForSettle` returns in `WaitSettleDefender` — confirm it returns
3. Check if `WaitForSettle` returns after timeout (4s) — if so, `ReadAll` might be reading 0 dice or crashing silently
4. Check if `settleTimeout` expiry path returns without calling `ReadAll`

### Server State

- Branch: `feat/multi-household-tv`
- Modified files: `GameService.cs`, `GameService.Combat.cs`, `AiService.cs`, `ManagementEndpoints.cs`, `PendingCombat.cs`
- Server flow proven correct for all 4 scenarios
- No server rebuild needed for next session (unless adding more logging)

### Unity State

- `CombatTheatre.cs` — full rewrite (role-per-combat)
- `DiceRoller.cs` — unchanged this session (staticDice fix from earlier still in place)
- Only Z440 editor build running; laptop has older build

### Key Insight

The `/admin/testcombat` endpoint was the breakthrough. Eliminated the "play a random game and hope the right scenario happens" loop. Build test tooling first next time.

---

*Session end: 18:11, 7 July 2026*
