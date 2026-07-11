# Multi-Household Combat Scenarios

Complete enumeration of every combat path when players are split across multiple
physical TVs (households). Derived from `GameService.Combat.cs` (`AttackWithDice`,
`Blitz`, `PlayerRoll`, `ResolveCombat`) and the TV routing in `GameService.cs`
(`GetTVForPlayer`, `IsUnityTVConnected`).

Companion to [MULTI-HOUSEHOLD-DICE-FLOW.md](MULTI-HOUSEHOLD-DICE-FLOW.md), which has
the sequence diagrams. This doc is the exhaustive matrix + fallback paths.

---

## The dimensions that decide a combat path

Combat routing is decided by five inputs:

| Dimension | Values | Where it's read |
|-----------|--------|-----------------|
| **Attack type** | Single attack / Blitz | `AttackWithDice` vs `Blitz` |
| **TV setup** | No TV / Single TV / Multiple TVs | `IsUnityTVConnected`, `_registeredTVs.Count` |
| **Household relationship** | Same-household / Cross-household | `attackerTvConn == defenderTvConn` |
| **Defender type** | Human / Bot (AI) | `defenderPlayer.IsAI` |
| **Defender dice** | 1 die (target = 1 army) / 2 dice (target ≥ 2) | `target.Armies >= 2 ? 2 : 1` |

Two dimensions that **do not** branch the code:
- **Attacker type (human vs bot)** — attacker dice always spawn on the attacker's TV
  and Unity rolls them automatically. A bot attack and a human attack follow the
  identical dice path; only the trigger differs (hub `Attack` call vs AI turn logic).
- **Continent / territory identity** — never affects the dice path.

---

## Decision tree

```
Attack requested
│
├─ Blitz? ───────────────────────────────► SERVER-SIDE LOOP (path B1)
│                                            (rolls every round on server, no physics,
│                                             regardless of households; rejected if
│                                             _pending is active)
│
└─ Single attack (AttackWithDice)
   │
   ├─ IsUnityTVConnected == false ────────► SERVER FALLBACK (path S0)
   │                                         plain Attack(), both sides rolled on server
   │
   ├─ _pending != null ───────────────────► REJECTED "Combat in progress"
   │
   └─ Unity TV connected
      │
      ├─ attackerTvConn == defenderTvConn ─► SAME-HOUSEHOLD (path H)
      │                                       both dice spawn on one TV, combined submit
      │                                       (also the path for a Single-TV setup, since
      │                                        that one TV owns every player)
      │
      └─ Cross-household
         ├─ defender.IsAI == true ─────────► CROSS, BOT DEFENDER (path X-Bot)
         └─ defender.IsAI == false ────────► CROSS, HUMAN DEFENDER (path X-Human)
```

---

## Full scenario matrix

Legend: **A** = attacker, **D** = defender. "Auto" = Unity rolls with no tap.
"Tap" = human defender must tap Roll on their handset.

| # | Setup | A type | D type | Household | D dice | Code path | A dice | D dice roll | Timeout → fallback |
|---|-------|--------|--------|-----------|--------|-----------|--------|-------------|--------------------|
| 1 | No TV | any | any | n/a | 1 or 2 | `Attack()` (S0) | server | server | none (synchronous) |
| 2 | 1 TV | Human | Human | same | 2 | same-household (H) | Auto on TV | Auto on TV | 15s → server roll |
| 3 | 1 TV | Human | Bot | same | 2 | same-household (H) | Auto on TV | Auto on TV | 15s → server roll |
| 4 | 1 TV | Bot | Human | same | 2 | same-household (H) | Auto on TV | Auto on TV | 15s → server roll |
| 5 | 1 TV | Bot | Bot | same | 2 | same-household (H) | Auto on TV | Auto on TV | 15s → server roll |
| 6 | 2+ TV | Human | Bot | same | 1/2 | same-household (H) | Auto A-TV | Auto A-TV | 15s → server roll |
| 7 | 2+ TV | Human | Human | same | 1/2 | same-household (H) | Auto A-TV | Auto A-TV (no tap) | 15s → server roll |
| 8 | 2+ TV | Bot | Bot | same | 1/2 | same-household (H) | Auto A-TV | Auto A-TV | 15s → server roll |
| 9 | 2+ TV | Human | Bot | cross | 2 | X-Bot | Auto A-TV | Auto D-TV | A:10s / D:15s → server |
| 10 | 2+ TV | Human | Bot | cross | 1 | X-Bot | Auto A-TV | Auto D-TV (1 die) | A:10s / D:15s → server |
| 11 | 2+ TV | Bot | Bot | cross | 2 | X-Bot | Auto A-TV | Auto D-TV | A:10s / D:15s → server |
| 12 | 2+ TV | Human | Human | cross | 2 | X-Human | Auto A-TV | **Tap** → D-TV | A:10s / D:15s → server |
| 13 | 2+ TV | Human | Human | cross | 1 | X-Human | Auto A-TV | **Tap** → D-TV (1 die) | A:10s / D:15s → server |
| 14 | 2+ TV | Bot | Human | cross | 1/2 | X-Human | Auto A-TV | **Tap** → D-TV | A:10s / D:15s → server |

Rows 2–5 collapse because a single TV owns all players, so `GetTVForPlayer` returns
that one TV for both sides and `sameHousehold` is always true.

---

## Path details

### S0 — No Unity TV (web board or headless)
`IsUnityTVConnected == false`, so `AttackWithDice` immediately delegates to `Attack()`.
Both attacker and defender dice are rolled on the server (`RollDice`), combat resolves
synchronously, `CombatResult` broadcast. The web `tv.html` shows dice statically. No
`PendingCombat`, no timeouts, no roll prompt. This is the original pre-Unity flow.

### H — Same-household (one TV rolls both sets)
Triggered when `attackerTvConn == defenderTvConn` (including any single-TV setup).

1. Create `PendingCombat`.
2. Send **both** `SpawnDice("attacker")` and `SpawnDice("defender")` to the shared TV.
3. Pre-complete `AttackerRoll` / `DefenderRoll` (no waiting between them).
4. Wait for a single combined `SubmitDiceResult(attacker[], defender[])` — **15s** timeout.
5. Broadcast `AttackerDiceResult` + `DefenderDiceResult` to the group (spectator TVs
   place them statically), 500ms render pause, then `ResolveCombat`.

Note the asymmetry: a **human defender in the same household does NOT tap Roll** — the
attacker's TV physically rolls both red and blue dice together. The tap prompt only
exists cross-household.

### X-Bot — Cross-household, bot defender
Sequential, so the defender TV never rolls before the attacker result is visible.

1. Create `PendingCombat`.
2. `SpawnDice("attacker")` → attacker's TV; pre-complete `AttackerRoll`.
3. Wait for `SubmitRolledDice("attacker")` — **10s** timeout.
4. Broadcast `AttackerDiceResult` to group (defender TV places red dice statically),
   500ms pause.
5. Bot defender: pre-complete `DefenderRoll`, `SpawnDice("defender")` → defender's TV
   (Unity auto-rolls blue dice).
6. Wait for `SubmitRolledDice("defender")` — **15s** timeout.
7. Broadcast `DefenderDiceResult`, 500ms pause, `ResolveCombat`.

### X-Human — Cross-household, human defender
Same as X-Bot through step 4, then instead of auto-spawning:

5. Send `RollPrompt("defender", …)` to the group; the human defender sees it on their
   handset and taps Roll.
6. Their tap invokes `PlayerRoll`, which clamps dice to `DefenderDiceCount`, completes
   `DefenderRoll`, and sends `SpawnDice("defender")` to **their** TV.
7. Wait for `SubmitRolledDice("defender")` — **15s** timeout — then broadcast + resolve.

### B1 — Blitz (all setups)
`Blitz` is **always server-side**, regardless of households or connected TVs — the
multi-round loop would be too many physics rolls. It only interacts with the dice
system by refusing to start if `_pending != null` ("Combat in progress"). The web/Unity
boards display the final round's dice statically from `BlitzResult`. There is no
per-household routing, no roll prompt, and no timeout.

---

## Defender dice count (1 vs 2)

Set once per attack: `defenderDiceCount = target.Armies >= 2 ? 2 : 1`. It changes how
many blue dice spawn/roll but not the routing path. For a human cross-household
defender, `PlayerRoll` clamps their requested count with
`Math.Min(diceCount, DefenderDiceCount)`, so a defender can choose 1 die even when 2 are
allowed, but never more than the territory permits.

---

## Fallback & failure paths

Every asynchronous single-attack path degrades to a full server-side roll (`Attack()`)
rather than stalling the game:

| Trigger | Where | Result |
|---------|-------|--------|
| Attacker doesn't submit in 10s | X-Bot / X-Human step 3 | `_pending = null`, server rolls both sides |
| Defender doesn't submit in 15s | X-Bot / X-Human step 6/7 | `_pending = null`, server rolls both sides |
| Combined submit missing in 15s | Same-household step 4 | `_pending = null`, server rolls both sides |
| TV disconnects mid-combat | `UnregisterTV` → `DiceResult.TrySetCanceled()` | pending wait is cancelled → immediate server fallback |
| New attack while combat active | `AttackWithDice` / `Blitz` guard | `HubException "Combat in progress"` |
| `attackerTvConn == null` | defensive guard in cross-household | attacker `SpawnDice` skipped; roll pre-completed so flow continues (cannot occur while `IsUnityTVConnected`, since `GetTVForPlayer` always returns a TV when ≥1 registered) |

Because the fallback re-rolls **both** sides on the server, any partial physics result
already shown on a TV is discarded — the authoritative dice are the server's. Timeouts
therefore trade dice-animation fidelity for guaranteed progress.

---

## Routing reference (`GetTVForPlayer`)

- **0 TVs** → returns `null` → `IsUnityTVConnected` is false → path S0.
- **1 TV** → always returns that TV → every combat is same-household (path H).
- **2+ TVs** → matches the TV whose `PlayerIndices` contains the player; if no TV claims
  the player, falls back to the **first** registered TV (misconfiguration guard — dice
  would render on the wrong TV but the game still resolves).

---

*Created: 2026-07-07 — enumerated from `GameService.Combat.cs` / `GameService.cs`.*
