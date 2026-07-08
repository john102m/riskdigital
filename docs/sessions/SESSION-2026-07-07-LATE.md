# Session Notes — 2026-07-07 (Late Evening)

## Goal
Implement simultaneous arena sweep for multi-household combat. Both TVs open their
dice arenas and roll at the same moment, regardless of household.

---

## Result
All 4 tests produce physics dice rolls on both TVs simultaneously. Far superior UX
to the sequential approach. Branch `feat/simultaneous-arena-sweep` is keeper.

---

## What Was Built

### Phase 1 — Simultaneous arena sweep
Server broadcasts `SpawnDice` to ALL TVs (group broadcast) instead of only the owning TV.
Each TV self-determines ownership via `PlayerIndex` in the payload.
Non-owning TV opens arena and starts camera sweep immediately alongside the owning TV.

### Phase 2 — Ghost rolls on non-owning TV
Non-owning TV spawns real physics dice concurrently with the owning TV.
When authoritative values arrive (`AttackerDiceResult` / `DefenderDiceResult`),
faces are snapped to the correct values. Looks like a genuine roll on both screens.
Server uses only the submission from the owning TV — ghost physics is pure display.

### Key fix — same-household spectator (test #3)
Same-household path was still sending `SpawnDice` to a single client. Changed to group
broadcast so spectator TVs also receive it and ghost roll.

---

## Architecture

**Cross-household (e.g. England attacks Scotland):**

| Event | England TV (attacker, mine=True) | Scotland TV (defender, mine=True) |
|-------|----------------------------------|-----------------------------------|
| `SpawnDice("attacker", player=0)` | Roll red dice (real physics) | Ghost roll red dice |
| `SpawnDice("defender", player=2)` | Ghost roll blue dice | Roll blue dice alongside ghost red |
| `AttackerDiceResult` | Ignored (I'm Attacker) | Ignored (I'm Defender, stores values) |
| `DefenderDiceResult` | Snap blue ghost dice to values | Ignored (I'm Defender) |
| `CombatResult` | Already ShowingResult — ignored | Already ShowingResult — ignored |

**Same-household with spectator (test #3):**

| Event | England TV (owns both roles) | Scotland TV (spectator) |
|-------|------------------------------|-------------------------|
| `SpawnDice("attacker")` | Roll red (SameHousehold path) | Ghost roll red |
| `SpawnDice("defender")` | Roll blue alongside red | Ghost roll blue alongside ghost red |
| `AttackerDiceResult` | Ignored (SameHousehold) | Snap red ghost dice |
| `DefenderDiceResult` | Ignored (SameHousehold) | Snap blue ghost dice, DismissAfterHold |

---

## Files Changed

### Server
| File | Change |
|------|--------|
| `Models/CombatResult.cs` | `SpawnDice` record gains `PlayerIndex` field |
| `Services/GameService.Combat.cs` | All `SpawnDice` sends → group broadcast (including same-household); parallel flow: both spawns sent simultaneously for bot defender; `PlayerRoll` defender spawn → group |

### Unity
| File | Change |
|------|--------|
| `Assets/Scripts/SignalRClient.cs` | `OnSpawnDice` event gains `playerIndex` param; `using System.Linq` added |
| `Assets/Scripts/DiceRoller.cs` | `SpawnSetWithTargetFaces` — physics + snap; `SpawnSetGhost` — physics only; `SnapFacesForRole` — snap existing dice; `using System.Threading` added |
| `Assets/Scripts/CombatTheatre.cs` | `OnSpawnDice` — `IsMyPlayer()` ownership check; owning TV rolls, non-owning TV ghost rolls; `OnAttackerDiceResult` — Defender early-return guard; `OnCombatResult` — `ShowingResult` guard + spectator ghost guard; `OnStateChanged` — `ShowingResult` guard |

---

## Test Matrix Status

| # | Scenario | Status |
|---|----------|--------|
| 1 | You → Bob (cross-household) | ✅ Both arenas roll simultaneously |
| 2 | Bob → You (cross-household) | ✅ Both arenas roll simultaneously |
| 3 | You → Alice (same-household, Sco spectator) | ✅ Scotland ghost rolls both sets |
| 4 | Alice → Bob (bot cross) | ✅ Both arenas roll simultaneously |

---

## Tomorrow's Test Plan

1. **Human defender holds up play** — Player taps Roll late. Does attacker TV hold correctly
   while ghost red dice are settled? Does defender arena stay open until tap?

2. **Dice face accuracy** — After snap, do displayed values match the server's authoritative
   values? Cross-check `AttackerDiceResult`/`DefenderDiceResult` against what's shown on screen.

3. **Capture hold** — Does `EnterHiding` (longer hold) work correctly on both TVs when a
   territory is captured?

4. **Blitz still works** — Verify blitz display unaffected on both TVs (no physics, static
   final dice + scroll popup).

5. **Test #2 with real human defender** — Bob attacks You, you tap Roll on handset. Does your
   TV open arena when Bob's attack fires? Does Roll prompt arrive? Do ghost red dice show?

6. **Multiple attacks in succession** — State resets cleanly between combats, no stale dice
   or state leaking between rounds.

---

## Branch
`feat/simultaneous-arena-sweep` — branched from `feat/multi-household-tv`. DO NOT DELETE.

---

*Session end: ~23:49, 7 July 2026*
