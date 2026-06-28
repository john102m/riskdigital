# Ideas & Future Possibilities

All the "could do" items in one place. Not commitments — just the full menu.

---

## Client Variants

- **Unity TV app** — 2D map, dice animations, sound effects, Fire TV sideload (the learning exercise)
- **Web TV board** — polish tv.html into a proper shared display, works in any browser inc. Fire Stick Silk
- **Blazor handset** — server-side rendered alternative to React, same SignalR connection
- **Jetpack Compose TV** — same approach as Flutter project, Android TV native
- **Service Worker / PWA** — offline-capable handset, install to homescreen (did this for Flutter)

## AI Players

- Tiered intelligence: random → aggressive → strategic → personality-based
- Mission concealment / misdirection at higher tiers
- Server-driven, same events as humans, timed delays for feel
- Design doc: `docs/AI-PLAYER.md`

## Gameplay Features

- **Fixed card values** ✅ (implemented as house rule)
- **Connected-path fortify** — move to any connected owned territory, not just adjacent
- **Fog of war** — hide army counts for non-adjacent territories
- **Game timer** — per-turn time limit
- **Spectator mode** — join as viewer only
- **Taunts/chat** — predefined taunt messages, displayed as toasts on TV board

## TV Display Enhancements

- Territory circles scale with army count
- SVG overlay lines for cross-ocean adjacency
- Dice roll animation (spin → land → reveal)
- Sound effects (dice rattle, capture fanfare, elimination crash)
- Camera zoom to battle region during combat
- Territory highlight/pulse on attack

## Handset UX

- Vibration on your turn (Haptics API)
- Animation lock after placing (prevent double-tap)
- Undo reinforcement placement (limit per game)
- Mini SVG continent map for filtering
- Territory cards as visual card UI with silhouettes
- Themed backgrounds (parchment/war-table)

## Deployment & Infrastructure

- WHUK deployment (server + handset bundle + web TV)
- Fire TV Silk browser for web board (no sideload needed)
- Remote play over internet (already supported by architecture)

## Technical Exploration

- AI decision-making as a case study (game theory, hidden info, bluffing)
- LLM-powered AI personalities (contextual taunting, adaptive strategy)
- NUnit tests for game logic (combat resolution, card validation, mission checking)

---

*Living document — add ideas as they come up.*

## Sound Effects Needed

### Have (from Flutter)
- ✅ Dice roll
- ✅ Card swipe

### Need — TV Board

| Sound | Trigger | Notes |
|-------|---------|-------|
| Army placed (thud/stamp) | ArmiesPlaced | Subtle, repeats often. Short. |
| Territory captured (fanfare/clash) | Capture detected | Satisfying, brief |
| Blitz (rapid drums/gunfire) | BlitzResult | Longer, builds tension |
| Player eliminated (dramatic sting) | PlayerEliminated | Dark, final |
| Turn start (drum tap/horn) | TurnStarted | Quick attention-grab |
| Card traded (coin/cash register) | CardTraded | Rewarding |
| Fortify (march/boots) | FortifyMoved | Troops moving |
| Game over / victory (trumpet fanfare) | MissionComplete / GameOver | Big, triumphant |
| Game start (war horn/bugle) | StartGame | Sets the tone |
| Timer warning (tick/clock) | Turn timer threshold | Subtle pressure |

### Need — Handset (optional, lower priority)

| Sound | Trigger | Notes |
|-------|---------|-------|
| Your turn (notification ping) | Phase becomes your turn | Distinct, attention |
| Button tap (click) | Any action button | Subtle feedback |
| Error/invalid (buzz) | Failed action | Short negative |
| Capture celebration (short chime) | You captured a territory | Positive feedback |

### Sources (free/CC0)
- freesound.org — search by keyword, filter CC0
- mixkit.co — free SFX library
- pixabay.com/sound-effects — no attribution needed
- Record your own dice with phone mic (authentic!)

### Format
- MP3 or OGG for web (tv.html + handset)
- WAV/OGG for Unity TV app
- Keep files short (<2s for most, <5s for fanfares)
- Normalise volume across all clips
