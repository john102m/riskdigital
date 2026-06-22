# Turn Visibility — TV Board Activity Feed

## The Problem

When you're the only human and AI players are taking turns, you have no idea what's happening. The TV board shows dots changing colour/count but there's no narrative. You're out of the loop.

Even with multiple humans, when it's not your turn you're staring at a map waiting. The board needs to *tell the story* of what's happening — like watching someone play at the physical table, where you can see them pick up dice, point at territories, move pieces.

The physical game has:
- You SEE them place armies on specific territories
- You SEE them pick up dice and point at a border
- You HEAR them announce "I'm attacking Kamchatka from Japan with 3 dice"
- You SEE the dice roll
- You SEE them move armies forward after capture
- You SEE them shift armies during fortify

The digital game currently has: dots silently changing numbers.

## Design Goals

- Every player action should be **visible and readable** on the TV within seconds
- Watching should be **entertaining**, not just informative — build tension
- Works for AI turns AND human turns (you don't watch your own handset on the TV, you watch the map)
- Doesn't obscure the map permanently — transient overlays that fade

## Current TV Layout

```
┌─────────────────────────────────────────┐
│                                         │
│              MAP (full viewport)         │
│                                         │
│   [territory dots with army counts]     │
│                                         │
│                          ┌────────────┐ │
│                          │ Dice       │ │
│  ┌──────────────┐       │ Results    │ │
│  │ Info Box     │       │ (5s fade)  │ │
│  │ Code/Phase   │       └────────────┘ │
│  │ Players      │                       │
│  └──────────────┘                       │
└─────────────────────────────────────────┘
```

## Proposal: Combined Info + Activity Panel (Bottom-Left)

Keep everything in the existing Pacific Ocean panel location. No second panel fighting for map space. The info box *becomes* the activity feed — it already has context (who's playing, what phase), now it just shows what they're doing too.

```
┌─────────────────────────────────────────┐
│                                         │
│              MAP (full viewport)         │
│                                         │
│                                         │
│         ┌───────────────┐               │
│         │ CENTRAL POPUP │  ← big moments│
│         │ (captures,    │    (fades 3-4s)│
│         │  eliminations)│               │
│         └───────────────┘               │
│                                         │
│  ┌──────────────────┐                   │
│  │ Connected 4453  │  ← state + code  │
│  │ 🔴 Alice 0:45    │  ← who + timer   │
│  │ ⚔️ Japan → Kam.  │  ← current action│
│  │ 🎲 6 5 2 | 4 3   │  ← last dice    │
│  └──────────────────┘                   │
└─────────────────────────────────────────┘
```

### The Panel — Rolling Context

Single compact panel, same position as now, but redesigned. The current player list with cryptic stats (`26t · 50a · 15↓`) is gone — that's debug info, unreadable from the sofa and irrelevant to watching the game unfold.

**Line 1 — CONNECTION + CODE (small, persistent):**
```
Connected 4453
```
Shows SignalR connection state (Connected/Reconnecting/Disconnected) plus game code. Smaller font than current — it's reference info, not the headline.

**Line 2 — NOW PLAYING:**
```
🔴 Alice 0:45
```
Player colour, name, timer adjacent. Already works this way — keep it.

**Lines 2-4 — Activity (rolling, last 2-3 actions):**
```
🟢 +3 Ukraine
🟢 +2 Ural
```
or during attack:
```
⚔️ Japan → Kamchatka (3 dice)
🎲 6 5 2 | 4 3 — Defender loses 2
```
or blitz:
```
⚡ N.Africa → Congo — 4 rounds
🏴 Captured! (moved 5 in)
```

### Central Popup — Big Moments

For events that deserve full attention, a centred overlay that fades after 3-4 seconds:

- **Capture:** "🏴 Alice captures Iceland!"
- **Elimination:** "💀 Dave eliminated by Alice!"
- **Card trade:** "🃏 Alice trades for 10 armies"
- **Turn change:** "── Bob's Turn ──" (brief, 2s)
- **Mission complete:** already exists (winner overlay)

These are the punctuation marks. The panel is the running sentence.

### Feed Content by Phase

**Initial Placement:**
```
🟢 Alice placed 1 on Ukraine
🟢 Bob placed 1 on Brazil
🟢 Alice placed 1 on Siam
```
Same dot pulse as reinforce — circle grows/shrinks on the placed territory. Players take turns one-at-a-time, so you see each placement land.

**Reinforce:**
```
🟢 Alice placed 3 armies on Ukraine
🟢 Alice placed 2 armies on Ural
```

**Attack:**
```
⚔️ Alice attacks Iceland from Scandinavia (3 dice)
🎲 Attacker: 6 5 2 | Defender: 5 3
💀 Defender loses 2 armies
⚔️ Alice attacks Iceland from Scandinavia (3 dice)
🏴 Alice captures Iceland! (moved 3 in)
```

**Blitz:**
```
⚡ Alice blitzes Congo from North Africa
⚡ 4 rounds — Alice lost 3, defender lost 7
🏴 Alice captures Congo! (moved 5 in)
```

**Fortify:**
```
🔄 Alice moved 4 armies: Brazil → North Africa
```

**Card Trade:**
```
🃏 Alice traded cards for 8 armies
```

**Elimination:**
```
💀 Dave has been eliminated by Alice!
🃏 Alice takes Dave's 3 cards
```

**Turn transition:**
```
── Bob's turn ──
```

### Design Details

- **Parchment-themed** to match existing UI
- **Semi-transparent background** so map is still partially visible behind
- **Auto-scrolls** — newest at bottom, old entries fade/scroll off top
- **Colour-coded player names** (same colours as dots)
- **Max ~6-8 visible lines** — enough context without overwhelming
- **Entries fade in** with a brief slide animation
- **Turn separator** acts as a visual break between players

## Board Animations — All Players

The board must make every player's actions visible at a glance. When it's not your turn, you should be able to look at the TV and instantly understand what's happening — who's reinforcing where, who's attacking whom, where armies are moving. Not their intentions, just their actions.

This applies equally to AI and human players. The difference is timing — AI needs artificial delays between actions; humans provide their own natural pace. But the visual feedback (dot pulses, panel updates) is identical regardless of who's acting.

### Reinforce

When armies are placed on a territory:
- **Dot pulses** — circle grows ~20% and shrinks back over ~0.5s
- **Count increments** during the pulse
- **Panel updates:** `🟢 +3 Ukraine`

Each placement is a discrete visual event. Whether it's a human tapping fast or an AI with 2s gaps, the TV shows each one individually.

### Attack

- **Source dot pulses green**, target dot pulses red (scale pulse on top of existing glow)
- **Panel:** `⚔️ Japan → Kamchatka`
- **Dice shown** in panel
- **Losing dot shrinks slightly** as armies are removed
- **On capture:** target dot transitions colour (old → new owner) with a flash

### Fortify

- **Source dot shrinks** (losing armies)
- **Target dot grows** (gaining armies)
- Both pulse simultaneously — visual "flow"
- **Panel:** `🔄 Brazil → N.Africa (4)`

### AI-Specific Timing

Humans pace themselves. AI needs deliberate delays so the animations land and spectators can follow:

| Action | Delay before |
|--------|-------------|
| Each reinforce placement | 2s |
| Attack declaration | 2-3s |
| Each attack roll | 1.5s |
| Move-in after capture | 2s |
| Fortify | 2-3s |
| End turn | 1s |

Slight randomisation (±0.5s) stops it feeling robotic.

### Implementation

- **Dot pulse CSS:** `@keyframes pulse { 0% { transform: scale(1) } 50% { transform: scale(1.2) } 100% { transform: scale(1) } }` — triggered by class, removed after animation ends
- **Server broadcasts per-action** (`ArmiesPlaced`, `FortifyMoved`) so TV can animate each individually — not just a full `GameStateUpdated` after everything's done
- **TV renders from events first**, then reconciles with full state update (event = animate, state = confirm)



Already have green/red glow on source/target during attack selection. Extend:

- **Reinforce:** brief green pulse on territory when armies placed
- **Fortify:** green pulse on source (losing), blue pulse on target (gaining)
- **Capture:** territory transitions colour with a brief flash

## What the Server Needs to Broadcast

Currently the server broadcasts:
- `GameStateUpdated` (full state)
- `CombatResult` (dice, casualties)
- `SelectAttack` (source/target indices for glow)
- `PlayerEliminated`
- `CardTraded`
- `MissionComplete`

**Missing broadcasts for the activity feed:**
- `ArmiesPlaced` — { playerIndex, territoryIndex, count } (during reinforce/placement)
- `FortifyMoved` — { playerIndex, sourceIndex, targetIndex, count }
- `TurnStarted` — { playerIndex, phase } (or derive from GameStateUpdated)
- `BlitzResult` already exists but may need territory names added

Some of these can be derived from consecutive `GameStateUpdated` diffs, but explicit events are cleaner and cheaper for the TV to render without diffing.

## Implementation Phases

### Phase 1: Combined Panel
- Redesign info box: headline (active player + phase), rolling activity lines, compact stats row
- New server broadcasts (ArmiesPlaced, FortifyMoved)
- Activity lines slide in/out as actions happen

### Phase 2: Central Popups
- Capture, elimination, card trade, turn change — centred overlay with timed fade
- Replace existing dice overlay (bottom-right) — dice results move into the panel

### Phase 3: Dot Animations + AI Pacing
- Pulse/grow/shrink on reinforce, attack loss, capture colour transition, fortify flow
- AI timing increased to allow animations to land (2s per placement, etc.)
- Per-action server broadcasts (not batched)

## Open Questions

- **Clear on turn change?** Keep rolling across turns (with separator) or wipe clean at each new turn?
- **Sound?** A subtle tick/chime per event would add to the "watching a game" feel. Probably a later enhancement.
- **Human rapid-fire:** When a human places 5 reinforcements quickly, do we queue the animations (may feel laggy) or just pulse each as it arrives?

---

*Created: 2026-06-22*
