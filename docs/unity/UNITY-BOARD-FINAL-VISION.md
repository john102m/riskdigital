# Unity Board — Final Vision

## The Experience

A cinematic war table on the TV. Players glance up and see a physical-looking relief map on a dark wood table, warm lighting casting token shadows. The camera drifts, zooms to combat, holds on dramatic moments, then pulls back. Minimal UI — the camera tells the story. Dice arena appears as a picture-in-picture overlay during combat with drone-footage fly-by.

The board should feel like something from a general's study — not a flat screen with dots on it.

---

## The Board

| Element | Description |
|---------|-------------|
| Map surface | Flat quad with map texture + normal map for relief depth. Continents raised, oceans recessed, mountain ridges visible. |
| Table | Dark wood or felt surface extending to screen edges beneath/around the board. |
| Board edge | Visible thickness — mounted map with sides. Not a floating image. |
| Lighting | Warm directional light (~top-left), token shadows on board. Slightly dim ambient. War room feel. |
| Camera | Perspective at ~60° looking down. Far edge naturally compressed. Not orthographic. |
| Tokens | Coloured circles/cylinders with army counts. Shadows confirm they sit on the surface. Labels billboard toward camera. |

---

## Camera Behaviour (The Storyteller)

The camera replaces most text UI. It communicates what's happening through movement.

| Trigger | Camera Action | Duration |
|---------|---------------|----------|
| Idle (no events 5s+) | Slow Perlin-noise drift across board. Subtle rotation, varying height. | Continuous |
| Turn start | Gentle bias toward active player's territory cluster. Not a snap — a drift. | ~2s |
| Attack selection | Smooth fly to frame source + target together. | ~1.5s |
| Dice roll | Hold position. Dice panel appears overlaid. | Until resolve |
| Capture | Hold on captured territory. Token colour changes. | ~2s |
| Pull back | Ease back toward overview / resume idle drift. | ~2s after resolve |
| Blitz | Stay zoomed through all rounds, pull back after summary. | Duration of blitz + 3s |
| Reinforce | Subtle drift toward placed territory (bias, don't interrupt). | Passive |
| Game over | Dramatic slow pull-back to full board. Winner's territories pulse/glow. | 5s+ |

### Rate limiting
Bot-vs-bot rapid attacks: don't zoom for every one. If camera is already near the action, hold. Only fly if the new attack is >30% of board away from current position.

---

## UI Layer (Minimal, Permanent)

The camera can't communicate everything. These elements stay on screen:

```
┌─────────────────────────────────────────────────────────┐
│ [● John · Attack]                         [🔇] [4456]  │  ← top bar
│                                                         │
│                                                         │
│              ~ 3D relief map fills screen ~              │
│                                                         │
│                                                         │
│                       ┌─────────┐                       │
│                       │  Dice   │                       │  ← dice panel (combat only)
│                       │  Arena  │                       │
│                       └─────────┘                       │
│                                                         │
│  ┌─────────────────────┐                                │
│  │ John → Brazil (3v2) │                                │  ← activity feed
│  │ Captured! +3 moved  │                                │
│  │ Ollie traded cards  │                                │
│  └─────────────────────┘                                │
└─────────────────────────────────────────────────────────┘
```

### Top Bar
- Semi-transparent dark strip across top edge
- Left: coloured dot + player name + phase (updates live)
- Right: mute button + game code
- Small, unobtrusive. Glanceable.

### Activity Feed
- Bottom-left, 3-4 lines max
- Old entries fade out after 8-10s
- Covers events the camera can't show: card trades, reinforcement totals, fortify, elimination alerts
- Semi-transparent background
- Coloured player names inline

### Dice Panel
- As-is: RawImage overlay, dynamically positioned (left/right/centre)
- Drone footage camera flypath
- Appears on combat, disappears after resolve

### Phase Transition Flash
- Brief full-width text overlay when phase changes: "⚔️ Attack Phase"
- Fades after 1.5s
- Not a popup — just a momentary label to mark the shift

### Game Over
- Full-screen semi-transparent overlay
- Winner name in colour + trophy
- Winner's territories pulse on the map beneath
- Auto-dismiss after 10s to small badge (same as tv.html pattern)

### Lobby
- Centred text on dark background: game code + player list with colours/avatars
- Dissolves when game starts (map revealed)

---

## What NOT to Build

| Avoid | Why |
|-------|-----|
| Big info panel (player list, stats) | War-table aesthetic — no floating boxes covering the map |
| Large turn popup that dominates screen | Camera drift IS the turn announcement. Top bar carries the name. |
| Territory name labels on map | Clutters the relief aesthetic. Army counts only. |
| Phase-specific screen states | The board is always visible. Only overlays change. |
| Duplicate of tv.html layout | This is a different experience, not a port |

---

## Implementation Order

### Now (Phase 3 — playable without HTML)
1. Top bar (turn name + phase + game code)
2. Activity feed (bottom-left, 3-4 lines, auto-fade)
3. Phase transition flash
4. Game over overlay
5. Lobby screen (game code + player list)

### Next (Level 2 — war table foundation)
6. Switch to perspective camera (~60°)
7. Table surface beneath board
8. Directional light + token shadows
9. Board edge thickness

### Then (Level 2 — drone camera)
10. Idle drift (Perlin noise)
11. Convert current `BoardCamera.ZoomToAction` to perspective fly-to
12. Turn-start drift bias
13. Pull-back timing refinement
14. Rate limiting for bot-vs-bot

### Later (Level 2 — relief depth)
15. Paint height map → normal map
16. Apply to board material (URP Lit + normal)
17. Token height/billboard adjustments for angled camera

### Polish (Level 2 — environment)
18. Desk lamp spot light
19. Background (blurred room, very dark)
20. Dust particles in light beam
21. Info panel as in-scene monitor/tablet (replaces top bar?)

---

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Turn announcement | Persistent top bar, not big popup | Camera movement is the drama. Don't fight it with text. |
| Activity feed | Small, bottom-left, auto-fade | Context for off-camera events. Doesn't compete. |
| Info panel | None (top bar replaces it) | War-table aesthetic. No floating boxes. |
| Phase transitions | Brief flash text, then gone | Marks the moment. Camera + top bar carries after. |
| Dice panel | Overlay, dynamically positioned | Already works. The premium feature. |
| Camera → perspective | Required for war-table feel | Orthographic is flat/clinical. Perspective gives depth. |
| Normal map | Painted (Option A from Level 2 doc) | Control the look, match board style, exaggerate for clarity. |
| Token labels | Billboard toward camera | Readable at any camera angle. |

---

## Relationship to Existing Docs

- **Supersedes:** `BOARD-LEVEL2-VISION.md` (folded in here with UI decisions added)
- **Complements:** `DICE-PHYSICS-TUNING.md` (dice arena is part of this experience)
- **Complements:** `PROPOSAL-DICE-PANEL-POSITION.md` (panel positioning survives as-is)

---

*Created: 2026-07-01*
