# Multi-TV Single-Game — Architecture Discussion

*2026-07-08*

---

## Overview

The multi-TV single-game feature splits physics dice rolling across two Unity
board instances. Each household's TV rolls its own player's dice with live physics.
The other TV ghost-rolls and has its faces snapped to the authoritative result.
The server orchestrates the flow and resolves combat from the submitted values.

This document covers where the complexity lives, which parts are brittle, and
ideas for refactoring and edge case hardening.

---

## Where the Complexity Lives

### 1. `CombatTheatre.cs` — Role inference from unordered events

The hardest part. Each TV receives the same SignalR events but must behave
differently depending on which players it owns. Role is inferred at runtime
from event arrival, not set up-front.

The core problem: **events arrive in unpredictable order**. The TV may receive
`SpawnDice("defender")` before `AttackerDiceResult`, or vice versa. Every handler
must be written defensively — store data when it arrives, act when enough data
is present.

Example from today: the defender TV snap of ghost red dice was originally
placed in `OnSpawnDice("defender")`, assuming `AttackerDiceResult` would have
arrived first. Logs proved it hadn't. Fix: moved the snap to
`OnAttackerDiceResult` when `currentRole == Defender` — fires whenever the
data arrives, not at a assumed-ordering point.

### 2. `AttackWithDice` — Async state machine with two paths

The server manages a single `PendingCombat` object through an async flow
that branches on same-household vs cross-household. Both paths must:
- Time out safely (15s) and fall back to server roll
- Handle TV disconnection mid-combat
- Broadcast results at the right time with the right payload shape

The same-household and cross-household paths share `PendingCombat` but use
different TCS fields — the combined `DiceResult` for same-household,
`SubmitAttackerDice`/`SubmitDefenderDice` + `TryComplete` for cross-household.
This is correct but not obvious.

### 3. Household routing — `IsMyPlayer()` and `GetTVForPlayer()`

Everything depends on each TV knowing which players it owns, and the server
knowing which TV to send dice to. A single misconfiguration breaks the entire
flow silently — wrong path taken, wrong TV submits, timeout fires.

Today's bug: Inspector-serialized `householdId = "Household"` survived into
runtime because `ApplyHouseholdConfig` never cleared fields on invalid input.
Result: TV thought it was in multi-household mode with `playerIndices = [0]`,
took the cross-household Attacker path, submitted only attacker dice, server
timed out waiting for defender.

---

## Brittle Parts

### B1 — Role inferred late, from event ordering

`currentRole` starts as `None` when `SpawnDice("attacker")` arrives for a
non-owning TV. It stays `None` until `SpawnDice("defender")` confirms the
layout (same-household or cross-household). During this window, `AttackerDiceResult`
may arrive and must be handled without knowing the final role.

**Risk:** Any new event handler added during the `None` window must handle the
ambiguous state explicitly. Easy to miss.

### B2 — `OnStateChanged` as global kill switch

Any `GameStateUpdated` broadcast with `turnPhase != "Attack"` will call
`ResetCombat()` unless explicitly guarded. Current guards:
- `state == ShowingBlitz` — exempt
- `state == ShowingResult` — exempt

Any new state (`Hiding`, future states) must be added to the exemption list
or it will be killed mid-display. Pattern exists but relies on all future
contributors knowing about it.

### B3 — Concurrent async tasks with `combatCts`

`WaitSettleAttacker`, `WaitSettleDefender`, `WaitSettleGhostRed`, `DismissAfterHold`,
`HideAfterDelay` all fire-and-forget. `combatCts` is cancelled on `ResetCombat()` to
kill stale tasks. But tasks that don't check `token.IsCancellationRequested` after
every `await` can still act on stale state.

`WaitSettleGhostRed` fires on the non-owning attacker TV when ghost red dice spawn.
It waits for physics settle, then snaps faces to `lastAttackerValues` if already
available. It races with `OnAttackerDiceResult` — whichever fires first calls
`SnapFacesForRole`, the second call is harmless (dice already kinematic). The
`ghostRedSettled` flag tracks which fired first. This is the intended pattern for
any future "snap on settle" requirement.

### B4 — `PendingCombat` TCS fields partly unused

`AttackerRoll` and `DefenderRoll` TCS fields are pre-completed in both paths
but never awaited. They're a leftover from an earlier sequential design.
They don't cause bugs but add confusion about what the object represents.

### B5 — No server-side validation of who submits

`SubmitRolledDice` accepts any TV's submission for either role. First submission
wins via `TrySetResult`. The non-owning TV's ghost roll never calls submit,
so in practice only the correct TV submits. But there's no enforcement — a
misconfigured TV could submit the wrong role's dice and the server would use
those values.

Acceptable for a trusted LAN party game. Worth noting for remote play.

### B6 — Household config stored on `SignalRClient` as mutable public fields

`householdId` and `playerIndices` are public fields on `SignalRClient`,
set at join time by `GameJoinScreen.ApplyHouseholdConfig`. They persist for
the lifetime of the connection. If `ApplyHouseholdConfig` fails to clear them
(as happened today), stale values from a previous session or Inspector default
silently corrupt the routing.

### B7 — Reconnect loses household config

`SignalRClient.Reconnected` handler re-registers via plain `RegisterAsTV`
(no household). After a WiFi blip, the TV re-registers as a single-TV and
`GetTVForPlayer` falls back to first-TV-wins. Known issue, not yet fixed.

---

## Refactoring Ideas

### R1 — Explicit role assignment instead of inference

Instead of inferring role from event sequence, have the server tell each TV
its role explicitly in the `SpawnDice` payload:

```csharp
public record SpawnDice(
    string Role,
    int DiceCount,
    int SourceId,
    int TargetId,
    int PlayerIndex,
    string TvRole   // ← "roll", "ghost", "spectate"
);
```

Each TV gets told exactly what to do. No inference, no `None` window,
no ordering dependency. `IsMyPlayer()` becomes redundant.

Downside: server must know each TV's household assignment at event-send time,
which requires household config to be registered before combat starts.
Already the case — just not currently exploited.

### R2 — Separate `CombatTheatre` into role handlers

Currently one class handles all four roles with branching in every handler.
Could split into:

- `AttackerCombatHandler` — rolls red, submits, waits for blue snap
- `DefenderCombatHandler` — ghost red, rolls blue, submits
- `SameHouseholdCombatHandler` — rolls both, submits combined
- `SpectatorCombatHandler` — ghost both, snaps both

A factory or strategy pattern selects the handler once per combat.
Each handler only implements the events it cares about.

Benefit: each handler is simple and linear. No branching on `currentRole`
inside event handlers.
Cost: more files, more abstraction. May be over-engineering for a game.

### R3 — Replace `combatCts` pattern with a combat sequence object

Instead of a CTS passed through fire-and-forget tasks, wrap each combat
in a `CombatSequence` object that owns its lifecycle:

```csharp
class CombatSequence : IDisposable
{
    CancellationTokenSource cts = new();
    public CancellationToken Token => cts.Token;
    public void Cancel() => cts.Cancel();
    public void Dispose() => cts.Dispose();
}
```

`ResetCombat` disposes the old sequence and creates a new one.
All async methods receive the sequence object, not a loose token.
Makes the lifecycle explicit and testable.

### R4 — Remove unused `AttackerRoll`/`DefenderRoll` TCS from `PendingCombat`

They add noise. Remove them and document the two remaining paths clearly:
- Same-household: `DiceResult` via `SubmitDiceResult`
- Cross-household: `DiceResult` via `TryComplete` (both `SubmitAttackerDice` + `SubmitDefenderDice`)

### R5 — Make household config immutable after join

Instead of mutable public fields on `SignalRClient`, make them readonly
properties set once in the constructor or via an explicit `Configure` method
that can only be called before `JoinGame`. Prevents stale value bugs entirely.

---

## Edge Case Hardening

### E1 — TV disconnects mid-combat (attacker or defender)

**Current behaviour:** `UnregisterTV` calls `DiceResult.TrySetCanceled()`.
`AttackWithDice` detects cancellation and falls back to server roll.
Game continues — dice values are random rather than physics.

**Gap:** Only the combined `DiceResult` TCS is cancelled. If attacker TV
disconnects after submitting attacker dice but before defender submits,
`_attackerDice` is set but `DiceResult` hasn't fired yet. The cancellation
from `UnregisterTV` will fire it as cancelled — correct. But the already-submitted
attacker values are discarded. Server re-rolls both sides randomly.

**Improvement:** On attacker TV disconnect after attacker submission, use the
already-submitted attacker values and only re-roll the defender side.

### E2 — Reconnect loses household assignment

**Current behaviour:** `Reconnected` handler calls plain `RegisterAsTV` —
no household. All dice for that game route to first registered TV.

**Fix:** Store household config on `SignalRClient` and re-register with
household on reconnect. One-line change to `Reconnected` handler (proposal
already written in `PROPOSAL-FIX-HOUSEHOLD-DICE-ROUTING.md`).

### E3 — Both TVs submit for the same role

If ghost roll code were accidentally changed to call `SendRolledDice`,
`TrySetResult` would ignore the second submission silently. First wins.
No crash, but potentially wrong values used.

**Improvement:** Server could validate that the submitting connection ID
matches the expected household for that role. Requires server to store
which connection ID belongs to each role at combat start.

### E4 — `_pending` not cleared on exception in `AttackWithDice`

If an unhandled exception occurs inside `AttackWithDice` after `_pending` is
set, the catch block in `AiService` calls `ClearPending()`. But for human
attacks via `GameHub.Attack`, exceptions propagate to the hub and `_pending`
may be left set. Next attack would be rejected with "Combat in progress".

**Fix:** Wrap `AttackWithDice` call in `GameHub.Attack` with a try/finally
that calls `game.ClearPending()`.

### E5 — Timeout is silent to the player

When dice submission times out (15s), the server silently falls back to random
roll. The TV and handset show different dice from what was physically rolled,
with no explanation. Players notice but can't diagnose.

**Improvement:** Broadcast a `DiceTimeout` event when fallback fires.
TV can display a brief "connection timeout — dice re-rolled" overlay.
Handset shows a toast.

### E6 — Multiple rapid attacks while arena is dismissing

If a player attacks again while `DismissAfterHold` is still awaiting,
`SpawnDice("attacker")` fires `ResetCombat()` which cancels `combatCts` —
`DismissAfterHold` checks `state == ShowingResult` after its await and does
nothing. Correct.

But `HideAfterDelay` uses `hideCts`, not `combatCts`. If `EnterHiding` fired
and then a new combat starts, `HideAfterDelay` could fire `ResetCombat()` on
the new combat's state. Current code cancels `hideCts` in `ResetCombat()` —
correct. Pattern is sound but easy to break if new hide paths are added without
cancelling `hideCts`.

---

## Summary Table

| Item | Severity | Effort | Notes |
|------|----------|--------|-------|
| B7 — Reconnect loses household | High | Low | One-line fix, documented |
| E4 — `_pending` not cleared on hub exception | Medium | Low | try/finally in GameHub.Attack |
| E5 — Silent timeout | Medium | Low | New event + simple UI |
| B2 — OnStateChanged kill switch | Medium | Low | Document pattern clearly |
| R1 — Explicit TV role in SpawnDice | High value | Medium | Eliminates inference entirely |
| E1 — Partial disconnect recovery | Low | Medium | Nice-to-have |
| R2 — Split role handlers | Low | High | Over-engineering risk |
| E3 — Role submission validation | Low | Medium | Only matters for adversarial play |
| R4 — Remove unused TCS fields | Low | Low | Cleanup only |
| R5 — Immutable household config | Low | Low | Defensive, prevents today's bug class |

---

*Created: 2026-07-08*
