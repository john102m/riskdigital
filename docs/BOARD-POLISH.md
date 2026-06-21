# Board Polish — Web & Unity

Shared planning for visual/audio enhancements across both board clients.

---

## Shared Assets (usable by both Web and Unity)

| Asset | Format | Notes |
|-------|--------|-------|
| Dice rattle | .mp3/.ogg | Short sharp roll sound |
| Capture fanfare | .mp3/.ogg | Territory taken |
| Elimination crash | .mp3/.ogg | Player knocked out |
| Card flip | .mp3/.ogg | Subtle trade sound |
| Victory | .mp3/.ogg | Game won |
| Turn start chime | .mp3/.ogg | Your turn notification |

Source: free game SFX sites (freesound.org, mixkit.co) or generate.

---

## Web Board (tv.html)

### Audio
- Web Audio API or simple `<audio>` elements
- Triggered on SignalR events (CombatResult, BlitzResult, PlayerEliminated, MissionComplete)
- Mute toggle (bottom-right icon)

### Visuals
- Dice roll display (current: text overlay → future: animated dice face sprites?)
- Territory pulse/glow on attack (CSS animation on the circle)
- Capture flash (circle briefly expands + colour change)
- Turn indicator (highlight active player's territories with subtle glow)
- Troop movement line (brief SVG line from source → target on attack)

---

## Unity Board

### Audio
- AudioSource + clips in Resources/Audio/
- Same sound files as web (just different import format if needed)
- SoundManager singleton (same pattern as Flutter TV)

### Visuals
- 3D/2D dice tumble animation (the main event)
- Territory tint fills (v2)
- Troop march animation between territories
- Camera zoom to combat region
- Particle effects (capture, elimination)

---

## What Goes Where

| Feature | Web | Unity | Notes |
|---------|-----|-------|-------|
| Sound effects | ✅ | ✅ | Same source files |
| Dice animation | Simple (CSS/sprite) | Full (3D tumble) | Unity is the premium experience |
| Territory pulse | CSS animation | Shader/glow | Both doable |
| Combat line | SVG overlay | Line renderer | Nice-to-have |
| Camera zoom | N/A (static view) | ✅ | Unity only |
| Particle effects | N/A | ✅ | Unity only |

---

## Priority for Web Board

1. Sound effects (biggest feel improvement for least effort)
2. Territory pulse on attack/capture
3. Dice face sprites (replace text)
4. Turn glow on active player territories

---

*Living document — add ideas as we go.*
