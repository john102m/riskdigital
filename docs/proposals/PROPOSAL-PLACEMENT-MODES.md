# Proposal: Game Creation Settings & Placement Modes

## Problem
Initial placement takes a long time — especially with 4+ players taking turns placing one army at a time. For solo testing against bots it's tedious. For family games it's the bit before the fun starts.

Different groups want different speeds. A quick game night wants to skip straight to attacking. A proper session wants the strategy of choosing where to stack.

## Proposal: Placement Mode Selection

When the host starts a game, they choose a placement mode. Three options:

### Settings

| Setting | Options | Default |
|---------|---------|---------|
| **Placement mode** | Auto / Free-for-all / Manual | Auto |

Other house rules (missions, attack front, card values) unchanged — separate concern for later.

---

## Placement Modes

### 1. Auto (default — fast start)

Server places all armies intelligently. Game goes straight to Playing phase.

**Algorithm (per player):**
```
For each army to place:
  1. Score each owned territory:
     - Border territory (adjacent to enemy): +3
     - Adjacent to enemy with high armies: +2
     - Part of a continent with >50% owned: +1
     - Already has fewest armies among borders: +1
  2. Place on highest-scoring territory
  3. Add some randomness (±20% jitter on scores) for variety
```

**Advantages:**
- Instant. Zero wait.
- Perfect for solo testing against bots.
- Decent strategic placement (not random, not optimal).

**Disadvantages:**
- Removes player agency in placement.
- Experienced players may disagree with AI choices.

**Implementation:**
- New method: `AutoPlaceArmies(int playerIndex)` in GameService
- Called for all players in `StartGame` when mode is Auto
- Reuses the same scoring logic as AI Tier 3 reinforcement
- Skip InitialPlacement phase entirely — go straight to Playing

### 2. Free-for-all (the real contender)

Everyone places at the same time — no turn order during placement.

**How it works:**
- All players can place armies on their territories at any time.
- Once placed, armies stay (no take-backs).
- Phase ends when everyone has placed all armies (or taps "Done").
- TV shows placements appearing in real-time from all players.
- No waiting for anyone else's turn.

**Advantages:**
- Much faster (minutes → seconds for experienced players)
- More social (everyone engaged at once, not waiting)
- Reveals less — you can't see everyone else's full strategy before committing
- The way we actually played as kids

**Implementation:**
- Remove `CurrentPlayerIndex` gating during placement
- Allow any player to call `PlaceArmy` at any time (validate they have armies remaining + own the territory)
- Track `ReinforcementsRemaining` per player independently
- Phase ends when all players hit 0
- Bots auto-place immediately (weighted algorithm)
- Handset: no "waiting for X" screen — always show your territories

**No conflict risk:**
- Territories are pre-dealt — you can only place on your own.
- Parallel activity, not competing for the same resource.

### 3. Manual (legacy — strict turn order)

- Turn-based, one army at a time, round-robin.
- Slowest. Most strategic.
- Current implementation — no change needed.
- Likely only used if someone specifically wants to observe opponents' placement strategy.

---

## Bot Behaviour During Placement

Currently bots place one-at-a-time in turn order (same as humans in Manual mode).

| Mode | Bot behaviour |
|------|--------------|
| Manual | Place one per turn (current) |
| Simultaneous | Place all immediately using weighted algorithm |
| Auto | Server places for everyone (bots + humans) |

---

## Lobby UI

Add a simple 3-way toggle in the lobby (host only):

```
┌──────────────────────────┐
│  7742                    │
│                          │
│  ● John (Host)           │
│  ● Bot Alice 🤖 T3       │
│  ● Bot Bob 🤖 T4         │
│                          │
│  Placement:              │
│  [Auto] [Free] [Manual]  │
│                          │
│  [ Start Game ]          │
└──────────────────────────┘
```

Non-host players see the selection but can't change it.

---

## Server Changes

| File | Change |
|------|--------|
| `GameState.cs` | Add `PlacementMode` enum (Auto, FreeForAll, Manual) + field on `HouseRules` |
| `GameService.cs` | `StartGame` branches on mode. Auto: place all + skip to Playing. Free: allow any player to place anytime. |
| `GameService.cs` | `PlaceArmy` — remove turn check when mode is FreeForAll. Check all players done → advance to Playing. |
| `GameHub.cs` | Pass placement mode from lobby on `StartGame` |
| Handset `LobbyScreen.tsx` | 3-way toggle (host only) |
| Handset `PlacementScreen.tsx` | Free mode: no "waiting for X" screen, always show your territories |

## Priority

1. **Auto mode** — quickest to implement, biggest QoL for solo/dev
2. **Free-for-all** — best for real multiplayer games
3. Manual stays as-is (already done)

---

## Questions

1. Free-for-all — should bots place instantly or with a small staggered delay (1-2s per bot, feels more natural on TV)?
2. Auto mode — should players see the placement happen (animated) or just instant?
3. Default for multiplayer vs solo? (Could auto-select Auto when all opponents are bots)
