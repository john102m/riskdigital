# Proposal: Household Player Picker (TV Join Screen UX)

## Problem

Current household config requires typing `"england 0,2"` into a text field. Nobody knows their player index without inspecting the lobby order. Error-prone, dev-only scaffolding that confuses real players.

## Design

After the TV joins a game, show a player picker screen. Players are listed by **name and colour** — tap to toggle who's in this room. No indices, no typing.

## Flow

```
TV starts → Join Screen (game list) → tap game → Household Picker → Confirm → Board
```

### Step 1: Game list (existing)
Same as now — shows available games, tap to join.

### Step 2: Household Picker (new screen, replaces text input)

```
┌─────────────────────────────────────────┐
│                                         │
│     Who's watching THIS TV?             │
│                                         │
│     ☑  John          ● (green)          │
│     ☐  Alice         ● (red)            │
│     ☑  Bob           ● (blue)           │
│     ☐  Dave          ● (purple)         │
│                                         │
│     [ Everyone ]          [ Confirm ]   │
│                                         │
└─────────────────────────────────────────┘
```

- Player rows: name + colour dot, styled like the existing lobby display
- Tap row to toggle checkbox
- **"Everyone" button** — selects all (single-TV mode shortcut). Most common case for a family game on one TV.
- **"Confirm" button** — locks selection, transitions to board
- If all players selected → single-TV mode (no household ID, no split routing)
- If subset selected → multi-household mode (generates householdId, sets playerIndices)

### Step 3: Board (existing)
Normal game board. Household config applied.

## Auto-skip Rules

| Scenario | Behaviour |
|----------|-----------|
| Only one TV registered for this game | Skip picker, go straight to board (single-TV mode) |
| `autoJoinGameCode` set in Inspector | Skip join screen AND picker (dev/test shortcut) |
| Game already in progress on rejoin | Skip picker, use last selection |

## Server Integration

No server changes. The picker is purely client-side UI that sets the same `signalR.householdId` and `signalR.playerIndices` fields. The TV re-registers with `RegisterAsTVWithHousehold` after confirmation.

**householdId generation:** Use a short random string (e.g. `tv-{4 hex chars}`). The ID only needs to be unique per game, not human-readable — it's never shown to players. Or use the device name if available.

## Timing

The picker appears **after** `JoinGame` succeeds and game state is received (so we have the player list). This means:
1. TV calls `RegisterAsTV(code)` (no household yet)
2. Server sends `GameStateUpdated` → TV has player names/colours
3. Picker appears with player list populated
4. User selects → TV calls `RegisterAsTVWithHousehold(code, householdId, indices)`

This requires the server to allow re-registration (upgrade from plain TV to household TV). Check if `RegisterAsTVWithHousehold` already handles this — if it just overwrites the existing registration, we're fine.

## Edge Cases

| Case | Handling |
|------|----------|
| Game hasn't started (lobby phase, no fixed player list) | Show picker after game starts (players are assigned). During lobby, just show the board — household only matters for combat. |
| Player joins after TV already confirmed | Show a "Players changed — review?" prompt, or just include new players automatically if "Everyone" was selected. |
| Only 1 player in game (solo + bots) | Auto-select that player, skip picker. |

## What Gets Removed

- `TMP_InputField householdInput` from GameJoinScreen
- `ApplyHouseholdConfig()` text parsing
- Placeholder string stripping hack
- The entire "england 0,2" format

## Implementation Scope

| File | Change |
|------|--------|
| `GameJoinScreen.cs` | Remove household text input; add picker panel with toggle list; add Confirm handler that sets householdId/playerIndices and re-registers |
| Scene | Remove TMP_InputField for household; add picker panel UI (can be code-generated like the game list rows) |
| `SignalRClient.cs` | No change (fields already exist) |
| Server | No change (re-registration already supported) |


## CRITICAL: Prevent Duplicate Player Claims

**Problem:** All checkboxes are ticked by default. If two TVs both confirm without unticking anyone, both claim all players. Both will try to submit dice for the same player — broken.

**Required fix:** When a TV confirms a household, the server should broadcast which players are claimed. Other TVs' pickers should:
- Grey out / disable already-claimed players
- Or untick them automatically
- Or refuse to confirm if there's overlap with another TV's selection

**Server-side:** `RegisterAsTVWithHousehold` already knows which players belong to which TV. It could reject overlapping registrations or broadcast a `HouseholdClaimed` event with the taken indices.

**Priority:** High — easy to accidentally double-claim with default-all-checked behaviour. Must be fixed before real multi-TV play.
