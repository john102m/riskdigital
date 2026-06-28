# Missions — Implementation Plan

## Overview

Each player is secretly dealt a mission card at game start. First player to complete their mission wins. If your mission becomes impossible (e.g. "eliminate Red" but Red was eliminated by someone else), your mission reverts to world domination (own all 42).

---

## Mission Types

### Continent Conquest (6 missions)
- Control North America + Africa
- Control North America + Australia
- Control Asia + South America
- Control Asia + Africa
- Control Europe + South America + a third continent of your choice
- Control Europe + Australia + a third continent of your choice

### Territory Count (2 missions)
- Control 18 territories with at least 2 armies on each
- Control 24 territories (any army count)

### Elimination (6 missions)
- Eliminate the Red player
- Eliminate the Blue player
- Eliminate the Green player
- Eliminate the Yellow player
- Eliminate the Purple player
- Eliminate the Orange player

**Total: 14 mission cards** (not all used per game — deal 1 per player from shuffled deck)

---

## Elimination Mission — Edge Cases

- If your target is yourself → replace with world domination
- If your target is eliminated by another player → your mission becomes world domination
- Checked at game start (deal) and on each elimination event

---

## Win Condition Check

Check after:
1. `MoveAfterCapture` (territory count / continent / elimination missions all resolve here)
2. Card trade territory bonus placing armies (could push to 2+ on 18 territories)

The check runs for the **current player only** (you can only win on your own turn).

---

## Server Model

```csharp
public enum MissionType { ContinentConquest, TerritoryCount, Elimination }

public class Mission
{
    public MissionType Type { get; set; }
    public string Description { get; set; } = "";
    
    // ContinentConquest: list of required continent names (2-3)
    public List<string>? RequiredContinents { get; set; }
    
    // TerritoryCount: target count + min armies
    public int? TerritoryCount { get; set; }
    public int? MinArmiesPerTerritory { get; set; }
    
    // Elimination: target player index (-1 if fallback to world domination)
    public int? TargetPlayerIndex { get; set; }
}
```

### GameState Additions

```csharp
public bool UseMissions { get; set; }  // house rule toggle
// Per-player: Mission property (JsonIgnored like Cards)
```

### Player Addition

```csharp
[JsonIgnore]
public Mission? Mission { get; set; }
```

---

## Mission Deck Generation

At `StartGame` (if missions enabled):
1. Build all 14 missions
2. Remove elimination missions that target colours not in the game
3. Shuffle remaining
4. Deal 1 per player
5. If a player gets their own colour elimination → redeal that one card

---

## Hub Methods

| Method | Direction | Notes |
|--------|-----------|-------|
| `GetMission` | Caller → Server → Caller | Returns player's mission (private) |
| `MissionComplete` | Server → Caller | You won! |
| `GameOver` | Server → All | Winner announced |

No new hub method needed for checking — it happens inside existing `MoveAfterCapture` logic.

---

## Handset UI

### Mission Display
- Small 🎯 button in header (like the 🃏 card badge)
- Tap to show/hide mission description in a panel
- Always available (even when not your turn) — it's your secret objective

### Win Screen
- Full-screen overlay: "🏆 Victory! Mission Complete"
- Show the mission text
- Other players see: "[Player] completed their mission!"

---

## Server Implementation Order

1. Add `Mission` model and `MissionType` enum
2. Add `UseMissions` to `HouseRules`, `Mission` to `Player`
3. Build mission deck (14 cards) and deal logic in `StartGame`
4. Add `CheckMissionComplete(playerIndex)` method
5. Call it from `MoveAfterCapture` (after territory changes)
6. Handle elimination fallback (when someone else kills your target)
7. Add `GetMission` hub method (caller-only response)
8. Broadcast `GameOver` with winner + mission text

## Handset Implementation Order

1. Add `Mission` type to `game.ts`
2. Add `MissionUpdated` event handler in `useConnection`
3. Add 🎯 badge + mission panel (reuse expandable pattern from cards)
4. Game Over screen showing winner's mission

---

## House Rule Toggle

`HouseRules.UseMissions` — default `false` for dev/testing. When false, classic "own all 42" is the only win condition (current behaviour). Host can toggle in lobby before starting.

---

## "Third continent of your choice"

For the two Europe+ missions that say "a third continent of your choice" — in the digital version, the server just checks if the player controls Europe + the named continent + any other complete continent. No player choice needed; it's automatic.

---

*Implement after: Blitz, Game Over screen. Missions are the final major gameplay feature.*
