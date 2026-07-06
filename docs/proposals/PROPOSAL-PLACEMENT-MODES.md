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

## Decisions

1. **Free-for-all bot pace** — Bots mirror human placement pace. Staggered delay (1–2s per placement) so you see them appearing on the TV board alongside human placements. Feels like everyone's at the table together, not bots dumping instantly.

2. **Auto mode animation** — Placements animate the same way as reinforce (pulse on territory, click sound, staggered per player). Not instant — you see the board populate over a few seconds. Same visual language as the rest of the game.

3. **Smart default selection** — The lobby defaults based on player composition:
   - **All bots (solo play):** Default to Auto. You're testing/playing fast, skip the tedium.
   - **Any humans present:** Default to Free-for-all. The social mode — everyone placing at once.
   - **Host can always override** to any of the three modes regardless.
   
   This means most games "just work" without the host thinking about it. Solo against bots → instant start. Family game night → free-for-all energy.

---

## Implementation Plan

### Step 1: Server — PlacementMode enum + HouseRules (5 min)
- Add `PlacementMode` enum: `Auto`, `FreeForAll`, `Manual`
- Add `PlacementMode` field to `HouseRules` (default `Auto`)
- Pass mode from `StartGame` hub method

**Files:** `server/Risk.Server/Models/GameState.cs`, `server/Risk.Server/Hubs/GameHub.cs`

### Step 2: Server — Auto mode (20 min)
- New method `AutoPlaceAllArmies()` in `GameService.cs`
- Scoring algorithm (border +3, threat +2, continent progress +1, weakest border +1, ±20% jitter)
- `StartGame` checks mode: if Auto → call `AutoPlaceAllArmies()` for each player → skip to `GamePhase.Playing`
- Broadcast `ArmiesPlaced` per territory for animation on TV/handset (staggered with small delay)

**Files:** `server/Risk.Server/Services/GameService.cs`

### Step 3: Server — Free-for-all mode (15 min)
- `PlaceArmy` — when mode is `FreeForAll`, remove `CurrentPlayerIndex` check
- Any player with `ReinforcementsRemaining > 0` can place anytime
- After each placement, check if ALL players have 0 remaining → advance to Playing
- Bots: trigger placement on a 1–2s timer per army (mirror human pace)

**Files:** `server/Risk.Server/Services/GameService.cs`, `server/Risk.Server/Services/AiService.cs`

### Step 4: Handset — Lobby toggle (15 min)
- Single tap-to-cycle button: `🚀 Auto` → `🤝 Free` → `📋 Manual` → loops
- Host taps to cycle through modes. One button, minimal footprint.
- Smart default: check if all non-host players are AI → preselect Auto, else preselect Free
- Non-host sees current mode (read-only, no tap)
- Send selected mode with `StartGame` invocation

**Files:** `handset/src/components/LobbyScreen.tsx`, `handset/src/types/game.ts`

### Step 5: Handset — PlacementScreen for Free-for-all (10 min)
- When mode is FreeForAll: always show your territories (never show "waiting for X")
- Show remaining count. Hide "Done" until 0 remaining.
- Keep the existing screen — just remove the `!isMyTurn` early return

**Files:** `handset/src/components/PlacementScreen.tsx`

### Step 6: TV boards — no changes needed
- Auto mode: `ArmiesPlaced` events arrive with stagger → existing pulse animation handles it
- Free-for-all: same `ArmiesPlaced` events from multiple players → same rendering
- Web board and Unity board both already react to `ArmiesPlaced` regardless of who sent it

**Files:** None

### Step 7: Manual mode — no changes
- Already works. Just the default path when `PlacementMode.Manual` is set.

**Files:** None

---

## Execution Order

| # | What | Blocked by |
|---|------|-----------|
| 1 | Enum + HouseRules | Nothing |
| 2 | Auto placement algorithm | Step 1 |
| 3 | Free-for-all server logic | Step 1 |
| 4 | Lobby toggle UI | Step 1 |
| 5 | PlacementScreen FFA mode | Step 3 |
| 6 | Test all three modes | Steps 2–5 |

Steps 2, 3, and 4 can be done in parallel after Step 1. Total estimate: ~1 hour.
