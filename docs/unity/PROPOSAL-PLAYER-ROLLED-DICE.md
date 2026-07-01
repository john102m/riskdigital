# Proposal: Player-Rolled Dice ✅ IMPLEMENTED (2026-06-29)

## Summary

Let the attacker and defender each trigger their own dice roll from their handset, rather than the server auto-rolling on attack. The Unity TV still does the physics — but each player gets to "throw" their dice with a tap.

## Current Flow

```
Attacker taps Attack → Server broadcasts CombatRollRequest → Unity rolls ALL dice → sends result back
```

## Implemented Flow

```
Human vs Bot:
  Attacker taps Attack → both sides roll immediately → dice fly on TV → result

Bot vs Human:
  Bot attacks → attacker dice fly immediately → defender gets "Defend!" prompt → taps Roll → defender dice fly → result

Human vs Human:
  Attacker taps Attack → attacker dice fly immediately → defender gets "Defend!" prompt → taps Roll → defender dice fly → result

Bot vs Bot:
  Both sides auto-roll after 1s delay → dice fly → result
```

## Key Design Decisions

### Attacker Always Rolls Immediately
- Original proposal had attacker prompted too, but this caused a SignalR deadlock — the hub blocks the caller's connection during `await`, so the same connection can't invoke `RollDice` while `Attack` is still running.
- Solution: attacker already committed by tapping Attack, so their dice fly immediately. Only defender gets prompted.

### Ordering
- Attacker dice always spawn first (immediate on Attack call)
- Defender dice spawn when they tap Roll (or immediately if bot)
- Camera sweep triggers on first spawn, purely cosmetic — dice physics run regardless of camera position
- Both sets can be in the air simultaneously if defender rolls quickly

### No Timeout
- Original design had 8s auto-roll timeout for humans — removed because it's annoying when you're thinking
- Bots handle themselves; humans roll when ready
- Game already waits indefinitely for other actions (reinforce, fortify)

### Bot Handling
- Bot defending: rolls immediately when human attacker's dice spawn (via `AutoRollBotOpponent`)
- Bot attacking: rolls both sides immediately, no prompts
- Bot vs bot: 1s delay then both roll (gives TV time to show the arena)

### Camera Sweep
- First roll of each attack phase gets the dramatic camera fly
- Subsequent rolls in same turn stay at result position (arena already visible)
- Camera flag resets on: phase change OR panel dismissal after capture

### WebTV Compatibility
- `IsUnityTVConnected` gate at top of `AttackWithDice` — if no Unity, entire player-roll system is bypassed
- Handset never shows Roll prompt without Unity connected
- WebTV game flow is 100% unchanged

## Files Modified

### Server (`server/Risk.Server/`)

| File | Changes |
|------|---------|
| `Models/CombatResult.cs` | Added `RollPrompt` record, `SpawnDice` record |
| `Services/GameService.cs` | Pending roll state fields, `AttackWithDice` rewritten for two-phase flow, `PlayerRoll` method, `AutoRollBotOpponent` helper |
| `Hubs/GameHub.cs` | Added `RollDice` hub method, `IHubContext` injection |

### Handset (`handset/`)

| File | Changes |
|------|---------|
| `src/types/game.ts` | Added `RollPrompt` interface |
| `src/hooks/useConnection.ts` | `RollPrompt` state + listener at app level (always registered), vibrate on prompt, clears on CombatResult/BlitzResult |
| `src/App.tsx` | Pass `rollPrompt` + `clearRollPrompt` to AttackScreen |
| `src/components/AttackScreen.tsx` | Accept rollPrompt as prop, defender sees "Defend!" overlay with dice choice + Roll button |

### Unity (`D:\Unity Projects\RiskDigitalBoard\Assets\Scripts\`)

| File | Changes |
|------|---------|
| `SignalRClient.cs` | Added `OnSpawnDice` event + handler |
| `DiceRoller.cs` | Added `SpawnSet(role, count)`, `WaitAndReadAll()`, `ReadAll()` — order-independent spawning |
| `CombatTheatre.cs` | `OnSpawnDice` handler (two-phase), `spawnCount` tracking, `cameraFlownThisTurn` flag, reset on capture dismiss |

## Bugs Encountered & Fixed

### 1. SignalR Hub Deadlock (Attacker Roll)
**Symptom:** Human attacker taps "Roll!" — nothing happens.
**Cause:** `Attack()` hub method `await`s `AttackWithDice()` which waits for the roll. SignalR processes messages per-connection sequentially, so `RollDice` from the same connection queues behind the still-running `Attack`.
**Fix:** Removed attacker prompt entirely — attacker rolls immediately since they already chose to attack.

### 2. Defender Not Seeing Roll Prompt
**Symptom:** Defender handset shows "Jim Attacking" with no Roll button.
**Cause:** `RollPrompt` listener was registered inside `AttackScreen` component via `useEffect` with `[connection, isMyTurn]` dependency. Event arrived before effect re-registered or was lost on re-render.
**Fix:** Moved listener to `useConnection` hook (app-level, always mounted). Passed prompt as prop.

### 3. Bot Delay When Human Rolls
**Symptom:** Human attacks bot, taps Roll, bot dice appear 1-3s later.
**Cause:** Fire-and-forget `Task.Run` with random delay for bot auto-roll ran independently.
**Fix:** `AutoRollBotOpponent` — when a human rolls, immediately trigger the bot's roll in the same call.

### 4. Sequential Await Causing Delay
**Symptom:** Both rolls complete but 8s timeout still fires.
**Cause:** `await attackerTask; await defenderTask;` — sequential. If attacker completes and auto-rolls bot defender, the defender task was already created with the 8s delay.
**Fix:** `await Task.WhenAll(...)` — parallel wait. Then removed timeout entirely.

### 5. Dice Not Clearing Between Attacks
**Symptom:** More dice keep arriving in the arena without clearing old ones.
**Cause:** `OnSpawnDice` only cleared dice when `!panelVisible`, but panel stayed visible between rapid attacks.
**Fix:** Clear on `spawnCount == 0` (reset by `CombatResult`), not by panel visibility.

### 6. Camera Sweep Skipped After Capture
**Symptom:** After a capture dismisses the arena, next attack doesn't get camera sweep.
**Cause:** `cameraFlownThisTurn` stayed `true` for entire attack phase.
**Fix:** Reset flag in `HidePanelAfterDelay` when panel dismisses after capture.

### 7. MoveAfterCapture Stuck — Wrong Minimum Move-In
**Symptom:** "Must move between 2 and 2 armies" error after capturing with Unity dice, can't proceed.
**Cause:** `ResolveCombat` (Unity dice path) never set `_state.LastDiceCount`. The value was stale from a previous roll, causing incorrect min move-in calculation.
**Fix:** Added `_state.LastDiceCount = attackerDice.Length` to `ResolveCombat`.

### 8. Defender Roll Prompt Lost After Reconnect — Game Frozen
**Symptom:** Bot attacks human, defender's phone screen went off and SignalR reconnected. No "Defend!" prompt appears. Game freezes — both handsets show "Bot Alice Attacking", TV shows glowing tokens, nothing progresses.
**Cause:** `PendingCombat` stored the defender's `ConnectionId` at combat creation time. After phone sleep → reconnect, `Rejoin` assigns a new connection ID to the player, but `PlayerRoll` compared against the stale ID stored in `PendingCombat`. The defender could never match, so `DefenderRoll` TCS never completed, and `AttackWithDice` awaited forever.
**Fix:** Store `AttackerPlayerIndex` / `DefenderPlayerIndex` (stable) instead of connection IDs. `PlayerRoll` looks up the *current* connection ID from `_state.Players[index].ConnectionId` at match time. Additionally, `Rejoin` now re-sends `RollPrompt` to the caller if they're the pending defender — handles cases where the original broadcast was lost during reconnect.

### 9. Blitz Panel Stomps Next Combat — Arena Dismissed Mid-Roll
**Symptom:** Bot blitzes and captures a bot territory, then immediately attacks a human. Human sees "Roll 1" defend prompt but the dice arena is dismissed on Unity. Looks frozen.
**Cause:** `ShowBlitzDice` is an ~8-9s async sequence (camera sweep + 6s display). Bot's next attack fires `SpawnDice("attacker")` after ~4s, which correctly transitions to `WaitingForDice` and shows the panel. But the old `ShowBlitzDice` awaitable is still running — when its 6s await completes, it calls `ShowPanel(false)` and `ClearDice()`, stomping on the new active combat.
**Fix:** Added state guards (`if (state != CombatState.ShowingBlitz) return;`) after each await in `ShowBlitzDice`. If something else has taken over (state changed away from `ShowingBlitz`), the old async bails out instead of hiding the panel.

## UX Result

- **Human attacks bot:** Tap Attack → dice fly immediately (both sides) → satisfying instant feedback
- **Bot attacks human:** Phone vibrates → "Defend!" button appears → tap → your dice fly → result
- **Human vs human:** Attacker dice fly on Attack tap → defender phone vibrates → they roll → result
- **Bot vs bot:** 1s pause → both roll → spectators watch
- **No Unity:** Completely unchanged — instant server rolls, no prompts, no buttons
