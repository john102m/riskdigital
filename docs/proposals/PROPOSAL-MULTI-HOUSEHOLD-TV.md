# Proposal — Multi-Household Unity Board

## Scenario

Scotland vs England. One shared game, played remotely over the internet. Each household has a TV with the Unity board. Both want the premium experience — 3D dice, camera flypaths, scrolls, soundtrack.

## What Already Works

- Both Unity .exe instances can connect to the same server simultaneously
- Both receive the same SignalR events (state updates, combat results, blitz results, turn started, etc.)
- Both render the same map, tokens, activity feed, popups, soundtrack
- Handsets from both households connect to the same game code

## The Problem: Dice Delegation

Currently the server has one "TV slot":
- `RegisterTV` stores one connection ID
- `SpawnDice` is sent only to that connection
- That TV does physics, reads faces, calls `SubmitDiceResult`
- Server resolves combat with those values
- 10s timeout if TV doesn't respond

With two Unity boards, who owns the dice?

## Solutions Considered

### Option A — Primary/Secondary TV
One TV does all dice physics, the other spectates. Simple but one household always gets a lesser experience.

### Option B — Both Do Physics (First Response Wins)
Both TVs roll independently, server takes first result. Both see live dice but results might briefly mismatch.

### Option C — Video/Trajectory Relay
Primary rolls, trajectory replayed on secondary. Over-engineered. Physics determinism is hard. Skip.

### Option D — Server Rolls, Both Animate
Server generates values, both TVs animate to predetermined results. Loses the "genuine roll" magic.

---

## Recommendation: Option A2 — Each Household Rolls Their Own Dice

Each player's dice roll with live physics on their own household's TV. The other household sees those dice arrive statically (using the same placement method as blitz final dice). Both arenas end up showing the same final state.

This maps to real Risk — each player rolls their own dice in front of them.

---

## Detailed Flow — Human vs Human (Cross-Household)

**Setup:** England (players 0, 2) vs Scotland (players 1, 3). England TV registered with players [0, 2]. Scotland TV registered with players [1, 3].

**Player 0 (England) attacks Player 1 (Scotland):**

```
Step 1 — Attacker rolls
  Server: sends SpawnDice("attacker", 3) to England TV only
  England TV: spawns 3 red dice, physics tumble, camera fly
  Scotland TV: waiting (knows attack started via AttackSelection event)
  
Step 2 — Attacker dice settle
  England TV: reads faces [5, 4, 2], calls SubmitDiceResult("attacker", [5, 4, 2])
  Server: stores attacker values, broadcasts AttackerDiceResult([5, 4, 2]) to ALL TVs
  Scotland TV: arena swings into position, places 3 red dice statically showing 5, 4, 2 — awaits defender
  England TV: dice already visible (they rolled them)

Step 3 — Defender rolls
  Server: sends RollPrompt to Player 1's handset (Scotland)
  Player 1 taps Roll, chooses 2 dice
  Server: sends SpawnDice("defender", 2) to Scotland TV only
  Scotland TV: spawns 2 blue dice alongside the static red ones, physics tumble
  England TV: waiting (attacker's dice still visible)

Step 4 — Defender dice settle
  Scotland TV: reads faces [6, 3], calls SubmitDiceResult("defender", [6, 3])
  Server: resolves combat (5v6 = defender wins, 4v3 = attacker wins)
  Server: broadcasts CombatResult to ALL
  England TV: places 2 blue dice statically [6, 3] alongside their red - all 5 visible
  Scotland TV: dice already visible (they rolled them)

Step 5 — Result
  Both TVs: show combat result (1 loss each), update tokens
  Both TVs: identical arena display - 3 red [5,4,2] + 2 blue [6,3]
```

**Key insight:** Remotely arriving dice use static placement (same as blitz final dice display). Each household's own dice tumble with live physics. Both arenas end up identical. Note: in local play today, both human and bot single-attack dice roll live with physics — only blitz shows dice statically. The static placement for remote dice is a new use of the existing blitz display method.

---

## Other Scenarios

### Same-Household Combat

Player 0 (England) attacks Player 2 (England):
- Both belong to England TV
- Flow is exactly like today — both SpawnDice go to England TV
- Scotland TV: receives CombatResult, shows all dice statically (spectator for this combat)

### Bot Attacks / Bot Defends

- Bots have no household — their dice action happens on the TV of the human they're fighting
- If bot attacks Player 0 (England): both dice sets roll live on England TV (same as today)
- Scotland TV: sees results arrive statically (like a blitz display)
- No change from current behaviour — bots are always local to the combat

### Blitz (Any Scenario)

- Always server-side (too many rounds for physics)
- BlitzResult sent to all TVs
- All TVs show final dice statically + scroll popup
- No household routing needed

---

## What Each TV Sees

| Scenario | Your TV | Their TV |
|----------|---------|----------|
| You attack them | Your red tumble live, their blue arrive static | Your red arrive static, their blue tumble live |
| They attack you | Their red arrive static, your blue tumble live | Their red tumble live, your blue arrive static |
| Same-household combat | All dice tumble live on your TV | All dice arrive static (spectator) |
| Bot attacks you | Bot red static, your blue tumble live | All static |
| Blitz (any) | All static + scroll | All static + scroll |

---

## New Server Events

| Event | Sent to | Payload | When |
|-------|---------|---------|------|
| `AttackerDiceResult` | All TVs | `int[] values` | After attacker submits, before defender rolls |
| `DefenderDiceResult` | All TVs | `int[] values` | After defender submits, before combat resolution |

These fire between rolls so the other TV can display dice incrementally. Current `CombatResult` fires after resolution — too late for the step-by-step display.

---

## Server Changes

1. `_registeredTVs` changes from single connectionId to `Dictionary<string, TVRegistration>` (householdId, connectionId, playerIndices)
2. `RegisterTV(string householdId, int[] playerIndices)` replaces current `RegisterTV()`
3. `SpawnDice` routing: look up rolling player's household, send only to their TV
4. New `AttackerDiceResult` broadcast after attacker submits
5. New `DefenderDiceResult` broadcast after defender submits
6. `SubmitDiceResult` validates submitter is correct household for the role
7. Timeout: extended to 60s+ (effectively unwanted — physics dice should always resolve; timeout is a last-resort safety net only)
8. Backward compat: single TV with no householdId = today's behaviour

## Unity Changes

1. Inspector field: `householdId` (string) — set per build/household
2. `RegisterTV` sends household ID + player indices on connect
3. New handler `OnAttackerDiceResult(int[] values)`: if not my roll, place red dice statically
4. New handler `OnDefenderDiceResult(int[] values)`: if not my roll, place blue dice statically
5. `SpawnDice` triggers physics as before (server only sends it to the right TV)
6. After `CombatResult`: ensure both sets visible, show result

## Lobby — Household Assignment

Players need to be associated with a household. Options:

1. **Auto-detect by IP** — handset on same subnet as a TV = same household. Zero user effort.
2. **Lobby dropdown** — "Which TV are you at?" Shows registered TV names.
3. **Host assigns** — host drags players to households.

**Decision:** Start simple — lobby dropdown or host-assigns. Auto-detect breaks for the core remote play scenario (different public IPs across internet). Can add as a hint later.

---

## Non-Dice Considerations

Everything else just works with multiple TVs:
- State updates: broadcast to all
- Turn popups, blitz scrolls, card trade: all triggered by same events
- Activity feed: same events
- Camera zoom: each board decides locally (both zoom to the same combat)
- Soundtrack: each board plays independently

## Latency

Remote play over internet adds 20-50ms. For a turn-based board game this is invisible. Defender has 30s to tap Roll — no issue.

---

## Implementation Order

1. Server: `RegisterTV` accepts householdId + playerIndices (backward compat: no params = single-TV mode)
2. Server: route `SpawnDice` to correct household TV
3. Server: new `AttackerDiceResult` / `DefenderDiceResult` broadcasts
4. Unity: add householdId field, send on connect
5. Unity: new handlers for remote dice (place statically)
6. Unity: spectator mode for combat not involving your household
7. Lobby: household assignment (auto-detect or picker)
8. Test with two Unity instances on same machine

## Plan of Action

### Branch: `feature/multi-household-tv`

### Test Plan

**Test 1 — Backward Compatibility (single TV):**
- One Unity instance, no householdId (or default)
- Play a full game: single attacks, blitz, bot combat, defender rolls
- Confirm: dice physics, timing, popups, sounds all identical to current main branch
- Pass criteria: no regressions

**Test 2 — Two TVs (Z440 + Laptop):**
- Z440 runs Unity with householdId "england"
- Laptop runs Unity with householdId "scotland"
- Create game, assign players to households
- Player from England attacks player from Scotland:
  - England TV: attacker dice roll live
  - Scotland TV: attacker dice arrive statically, arena in position, awaits defender
  - Scotland player taps Roll: defender dice roll live on Scotland TV
  - England TV: defender dice arrive statically alongside their red
  - Both TVs show identical final state
- Test same-household combat (England vs England): all dice on England TV, Scotland spectates
- Test bot combat: live on local TV, static on remote
- Test blitz: both TVs show static + scroll

### Hardware
- Z440 (desktop): primary dev + Unity instance 1
- Laptop (E540 or similar): Unity instance 2
- Both on same LAN, both connecting to server on Z440 (localhost:5000 / LAN IP:5000)
- Phone handsets connect as players

---

*Created: 2026-07-05*
