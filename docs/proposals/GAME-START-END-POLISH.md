# Discussion: Game Start & End Polish

The middle of the game (placement, attack, fortify) is solid. The beginning and end lack ceremony. These are the loose ends to tighten up before playtesting.

---

## Beginning of Game

### Current behaviour
- Unity board connects, shows empty map with "Waiting for players..." in the info bar.
- Players join from handsets — no visual feedback on TV.
- Game starts (host presses Start) — tokens appear, placement begins. No transition moment.

### What's missing

| Item | Impact | Effort |
|------|--------|--------|
| **Player join announcements** | Players see their name appear on the big screen — confirms they're in, builds excitement. | Low — feed entry or popup on `PlayerJoined` event |
| **Player list on screen** | See who's in the lobby, their colours, ready status. | Medium — new UI element |
| **"Game On!" popup** | Marks the transition from lobby → placement. A moment of ceremony. | Low — popup on phase change |
| **Placement intro** | Brief explanation or title card: "Place your armies" before first placement begins. | Low — popup |

### Proposed flow

```
1. Player joins → activity feed: "🟢 John joined" (in their colour)
2. More players join → same pattern
3. Host starts game → popup: "GAME ON!" (or "The war begins..." / "Risk!" — something dramatic)
4. Brief pause (1.5s)
5. Placement phase begins, tokens appear
```

### SignalR events needed

- **`PlayerJoined`** — does the server already broadcast this? If not, add it. If yes, just hook it in Unity.
- **Phase transition** — already available via `OnStateChanged` (phase goes from "Lobby" → "InitialPlacement").

---

## End of Game

### Current behaviour
- **Mission win:** `OnMissionComplete` fires → camera zooms out → popup: "🟢 John wins! (mission description)" → victory sound.
- **Conquest win (all 42 territories):** No dedicated handler. The game state transitions to `GameOver` phase, but nothing visual happens beyond the info bar updating to "Game Over".
- **After win:** Board goes silent. Music stops (no `GameOver` → Victory mapping was added, so this should now play). Popup dismisses after 30s. Then... nothing.

### What's missing

| Item | Impact | Effort |
|------|--------|--------|
| **Conquest win detection** | If no mission, detect GameOver phase + identify winner (player owning all territories). Show same win sequence. | Medium — logic to find winner from state |
| **End-game summary** | Stats: territories held, players eliminated, rounds played. Displayed after winner popup fades. | Medium — new UI overlay |
| **"Play again?" state** | TV shows something useful after game ends — lobby code to rejoin, or just a dignified holding screen. | Low |
| **Camera behaviour post-win** | Slow drift/pan across the board showing the winner's colour everywhere. Satisfying. | Low — gentle camera pan loop |
| **Fireworks / particles** | Victory particle effect (confetti, embers, etc). Premium feel. | Medium — particle system |

### Proposed flow

```
1. Win condition met (mission or conquest)
2. Camera zooms out to full board
3. Clear dice arena
4. Popup: "🟢 John wins!" (+ mission text if applicable)
5. Victory music plays (from pool)
6. Victory sound sting
7. Optional: confetti particles for 5s
8. Popup holds for 10s then fades
9. Camera begins slow drift across board
10. Info bar shows: "Game Over — [winner] wins! | Code: XXXX"
11. Music continues looping until server resets
```

### Conquest win — how to detect winner

The server sets `phase = "GameOver"`. To find the winner without a dedicated event:
- Check `state.territories` — if all 42 have the same `ownerId`, that's the winner.
- Or: check if only one player has `territories > 0`.

Alternatively, the server could broadcast a `GameWon(playerIndex, reason)` event for both mission and conquest wins. Cleaner.

---

## Questions to Decide

1. **Player join event** — does the server already broadcast `PlayerJoined` to all clients, or only update state? If state-only, we can detect new players by diffing the player list on each state change.

2. **"Game On!" text** — what tone? Options:
   - "Game On!"
   - "The war begins..."
   - "Risk!" (dramatic, short)
   - Player names listed with colours as a roll-call

3. **Conquest win** — add a new server event (`GameWon`) or detect client-side from state?

4. **End-game summary** — worth building for tomorrow, or just get the win popup + music working and save stats for later?

5. **Confetti/particles** — worth the effort or tacky?

---

## Priority for Tomorrow's Playtest

Must-have (quick wins):
1. ✅ Player join → activity feed entry (confirms connection works)
2. ✅ "Game On!" popup on Lobby → InitialPlacement transition
3. ✅ Conquest win detection + same popup/sound as mission win
4. ✅ Lock attack input while dice are in flight (UB only)

Nice-to-have (if time):
5. Slow camera drift after game over
6. End-game holding screen with game code

Later:
7. Stats summary
8. Particles
9. Play again flow

---

## Dice In-Flight Input Lock (Unity Board Only)

### Problem
Players can press Attack (or Blitz) on their handset while the Unity board is still resolving a dice roll — physics haven't settled, result hasn't been submitted. This can cause:
- Stale `PendingCombat` on the server being overwritten.
- Dice arena showing overlapping rolls.
- Race conditions between `SubmitDiceResult` and new `SpawnDice` events.

### Current behaviour
- Server creates `PendingCombat` on each Attack call.
- If a new Attack arrives before the Unity board submits the result, the pending state is replaced.
- The old dice roll is orphaned — Unity submits a result for a combat that no longer exists.

### Proposed fix
When the Unity board is the active dice resolver (i.e. `CombatState != Idle`), tell the server to reject or queue new attack requests until the current roll resolves.

**Option A: Server-side lock (cleanest)**
- `GameService` checks if `_pendingCombat != null` before accepting a new `Attack` call.
- If pending, return an error to the caller: "Wait for current combat to resolve."
- Handset shows a brief "Dice rolling..." disabled state.
- Lock releases when `SubmitDiceResult` arrives or timeout fires.

**Option B: Handset-side lock via SignalR event**
- Server broadcasts `CombatStarted` / `CombatResolved` events.
- Handset disables Attack/Blitz buttons between those two events.
- Less robust (handset could miss an event), but simpler.

**Option C: Server-side lock + handset feedback (belt and braces)**
- Server rejects the call (Option A).
- Handset also listens for lock/unlock events to grey out buttons proactively.
- Best UX — button is visibly disabled, and server still guards against race conditions.

### Recommendation
Option A as minimum (server rejects). Option C for polish. The server already has the `_pendingCombat` field — just add a null check at the top of the `Attack` method.

### Scope
- This only applies when Unity board is connected and handling dice physics.
- When Unity is NOT connected (web board / no TV), the server rolls instantly — no lock needed.
- The server already knows if Unity is connected (dice delegation flag).
