# The Story So Far

A two-week sprint from blank repo to fully deployed digital board game. This is the narrative.

---

## The Idea

Take the classic Risk board game and make it digital — same multi-screen architecture proven with the Flutter (stock exchange) project. A TV shows the shared map. Everyone plays from their phones. No physical board, no lost pieces, no arguments about dice rolls.

The real goal: learn Unity by building something real. The Flutter project used Jetpack Compose for the TV display. Risk would use Unity 3D — physics dice, camera flypaths, lighting, the works.

---

## Week 1 (20–25 June 2026)

### Day 1 — Scaffolding & Lobby

Started from scratch. .NET 8 server with SignalR, React handset with Vite and Tailwind. Got the lobby working — create game, join with code, player list, host controls. Dark theme from the start (matching Flutter's look). Debug TV page (`tv.html`) showing game state.

By end of day: players could join from phones, see each other in the lobby, and the TV showed who was in.

### Day 1 continued — Core Game Loop

Didn't stop at the lobby. Got initial placement working (deal territories, take turns placing armies), then pushed through reinforce → attack → fortify → end turn. Full game loop, end to end, in one session. The attack system had proper dice mechanics — attacker rolls 1–3, defender 1–2, compare highest pairs, defender wins ties.

House rule added immediately: **locked attack front** — must attack from your starting territory or territories you've captured this turn. Stops boring turtling.

### Day 1–2 — Card System & Blitz

44-card deck. Earn one per turn on first capture. Trade sets for armies — three of a kind or one of each. Fixed UK values (4/6/8/10) as default. Territory bonus (+2) if you trade a card matching a territory you own.

Blitz: auto-repeat attacks until capture or source depleted. One button press, instant resolution.

### Day 2 — Missions

14 mission cards dealt secretly. Six continent-conquest missions, two territory-count, six elimination. The elimination missions have a fallback — if someone else kills your target, your mission reverts to world domination.

Mission badge on the handset (🎯) to check your secret objective anytime. Status badge (📊) for progress tracking.

### Day 2 — UI Polish & First Playtest

Continent accordion grouping across all screens. Bigger touch targets. Card trade UI with tap-to-select. Forced trade gate when holding 5+ cards.

First real playtest on the Lenovo laptop — everything worked. Found 8 bugs, fixed them all same session. Fortify screen was too cramped — split into multi-step flow, then reverted to single screen with better layout.

### Day 2 — TV Web Board

Rebuilt `tv.html` from debug page into a proper game display. Full-viewport map image, positioned dot overlays (colour-coded, army count), info box overlay, dice results, activity feed. Parchment theme. Wake lock for screensaver prevention. Territory glow on attack selection (green source, red target).

Tested on: Edge desktop, Chrome desktop, Fire TV Stick Silk browser, JVC native TV browser. Works everywhere.

### Day 3 — AI Begins

**Tier 1** — Random. Places anywhere, attacks anyone. Cannon fodder for testing.

**Tier 2** — Aggressive. Always attacks weakest neighbour. Blitzes at 5+. Reinforces front-line territories. Moves max on capture. Actually dangerous in groups.

**Tier 3** — Strategic. ML.NET integration. Trained a FastTree regression model on 58,000 simulated battles to predict blitz capture probability. The AI only blitzes when odds are >70%, does single attacks at 40–70%, skips below 40%. Also scores continent completion, times card trades for territory bonuses, and stops attacking after earning a card (preserve armies for defence).

### Day 3–4 — Lobby Polish & Avatars

Colour picker (6 colours), avatar picker (9 avatars), lobby broadcasts (see others join in real-time), remove AI button for host. TV splash screen, TV lobby display with player grid. Phase announcements ("Game On!" on start). Random starting player. Standard starting armies restored (40/35/30/25/20).

### Day 4 — AI Tier 4 & Tier 5

**Tier 4** (Opportunist) — Personality-based. Elimination hunting (targets weakest player for card steal). Continent denial (blocks opponents one territory from completing). Chokepoint recognition (values strategic territories like Ukraine, Siam, North Africa). Card escalation awareness.

**Tier 5** (Learning) — Four personalities: Opportunist 🦊, Cautious 🛡️, Aggressive 🔥, Continental 🗺️. ML pipeline that learns from human player behaviour. Auto-retrain after every game. Action logger captures every human decision with full board context (what territories they reinforced, what they attacked, when they traded). Three behaviour models: reinforce, attack, fortify.

### Day 5 — Deploy & ML Pipeline

Deployed to WHUK. Fixed startup crashes (directory permissions, model loading). Stable writable path found for logs and models. Unified training endpoint. Action logger with cascading paths (app-local → vhost tmp → %TEMP%). Ring buffer logger for in-memory diagnostics.

Won a real game on the live server. ML pipeline verified end-to-end.

**One-week mark: fully playable, deployed, with 5-tier AI and ML learning.**

---

## Week 2 (25 June – 3 July 2026)

### The Unity Journey Begins (25–29 June)

This was the main event — the reason Risk was chosen over other projects. Learning Unity by building a premium TV board.

**Phase 1 — Minimum viable board.** 3D URP project, static map sprite, 42 coloured cylinders for territories, army count labels, SignalR connection to the live server, real-time state updates. Took one evening.

**Phase 2a — Attack glow.** Emission-based colour change on source/target during attacks. Pulse animation (breathing scale) on the glowing tokens. Later reworked to use point lights interacting with the normal map.

**Phase 2b — Dice arena.** The centrepiece. A 3D box (wood-lid aesthetic), physics dice that tumble and bounce, perspective camera from table level, rendered to a picture-in-picture RawImage. FBX dice models imported. Catmull-Rom camera flypath with randomised waypoints — the camera swoops around the arena during each roll.

**Phase 2c — TV-driven dice.** Server delegates combat resolution to Unity when connected. Player attacks → server creates pending combat → Unity spawns dice → physics settle → Unity reads faces → submits result back to server. 10-second timeout fallback if Unity disconnects. Blitz stays server-side (too many rounds for physics).

**Phase 2d — Player-rolled dice.** Two-phase combat: attacker dice spawn immediately, then defender gets a "Defend!" prompt on their handset with a Roll button (haptic buzz). They choose dice count and tap Roll. If defender is a bot, auto-rolled after 1 second. Human vs human gets the full dramatic pause.

### Combat State Machine Refactor (29 June – 2 July)

The dice system worked but the code was fragile — boolean flags, race conditions on reconnect, blitz panels stomping active combat. Rewrote `CombatTheatre.cs` with an explicit `CombatState` enum (state machine, not flags). Server-side: extracted pending roll fields into a `PendingCombat` class with player indices instead of connection IDs (survives reconnect).

Fixed a stack of bugs: defender roll deadlock on reconnect, blitz panel stomping next combat, arena not dismissed on server timeout, 30-second roll phase timeout, AI turn failure recovery, Unity disconnect immediate fallback.

### Phase 3 — Parity with Web Board (2 July)

Turn popup (3D world-space canvas with scale-bounce animation), activity feed, card trade alerts, game-over winner announcement (zoom out + popup + victory sound), fortify animation (pulse shrink/grow), reinforce pulse + click sound, blitz rattle, capture fanfare, attack fail sting, victory music.

### Normal Map & Relief (1 July)

Replaced the flat sprite with a 3D Quad + URP Lit material. Hand-painted a greyscale height map (flood-fill in PaintShop Pro from the board image), converted to a normal map. Continents now have visible depth under directional lighting. The attack glow (red/blue point lights) interacts beautifully with the relief.

### The Final Push (3 July)

Pre-playtest polish day. Everything tightened up:

- **Soundtrack system** — `MusicManager.cs` with phase-based music pools, crossfading between phases, Suno AI-generated tracks.
- **Dice input lock** — server rejects attacks while Unity dice are in flight + handset disables buttons with "🎲 Dice rolling..." indicator. Belt and braces.
- **Game start ceremony** — welcome screen on connect, player join announcements in activity feed, "Game On!" popup on game start, tokens hidden until placement begins.
- **Game end ceremony** — conquest win detection (all 42 territories), same popup/sound as mission win, slow camera drift post-game.
- **Timing polish** — turn popup waits for camera zoom-out, blitz display shortened (6s → 3.5s), capture hold reduced (4s → 2.5s), Alaska↔Kamchatka attacks skip zoom (opposite sides of board).

---

## What Exists Now

A fully playable digital Risk board game. Three screens working in concert:

**The Server** (.NET 8 + SignalR) — all game logic, combat resolution, AI players, ML models. Single source of truth. No database — in-memory state for party game sessions.

**The Handset** (React + Tailwind) — phone controller. Lobby, placement, reinforce, attack, fortify, cards, missions. Continent accordions, haptics, colour/avatar picker. Thin client — no game logic.

**The TV — Web** (`tv.html`) — any browser, zero install. Parchment theme, positioned dots on the map, territory glow, activity feed, sounds. Works on Fire Stick Silk, phone, laptop, any screen.

**The TV — Unity** (separate repo) — the premium experience. 3D dice physics with camera flypaths, normal map relief, point-light attack glow, phase-based soundtrack, game ceremony (welcome, "Game On!", winner announcement), fortify/reinforce animations. Designed for Fire TV Stick 4K Max or desktop.

### By the Numbers

- 42 territories, 6 continents, 14 missions
- 5 AI tiers, 4 AI personalities, 3 ML models
- 44-card deck, 4 house rules
- ~20 SignalR hub methods
- ~15 Unity C# scripts
- 60+ documentation pages
- 2 weeks, 1 developer

---

## The Predecessor — Flutter

This isn't the first rodeo. "Flutter" — a 1955 Spear & Sons stock exchange board game — was digitised using the same architecture before Risk. Same .NET server, same React handset, same Fire TV Stick deployment. That project proved the pattern:

- SignalR reconnect resilience works (phone sleeps, wakes up, rejoins seamlessly)
- Host-only admin controls work (one player runs the show)
- AI players server-side with personality-flavoured timing works
- The multi-screen party game format is genuinely fun

Risk took that proven foundation and added: Unity 3D, a 42-node territory graph, combat dice mechanics, ML.NET AI with auto-retrain, and a far more complex game state. Flutter had 6 linear stock tracks. Risk has a world map with adjacency, borders, chokepoints, and continent control.

---

## What's Next

The game is done. What remains is polish and expansion:

- **Dice physics tuning** — settle detection, angular velocity checks, floor material
- **Replace alert() calls** — 14 ugly native browser popups → toast notifications
- **Mission fallback bug** — `MissionUpdated` not sent when someone else kills your target
- **Connected-path fortify** — move through chains of owned territory, not just adjacent
- **Multi-game server** — concurrent game instances for two households playing simultaneously
- **Home server** — replace WHUK with a self-hosted mini PC (static IP ready)
- **Family distribution** — Inno Setup installer for Unity board, QR code to join

---

## The Tech That Made It Fast

Why two weeks? Because nothing was new except Unity:

| Ingredient | Why it helped |
|-----------|---------------|
| .NET 8 + SignalR | Proven in Flutter. Server pattern copy-pasted then extended. |
| React + Tailwind | Fast iteration. No routing, no state library, just hooks + conditional render. |
| ML.NET | Microsoft's ML library — no Python, no separate environment. Same codebase. |
| Kiro (AI assistant) | Pair programming. Design discussions, code generation, bug diagnosis, docs. |
| Flutter precedent | Same architecture, same reconnect patterns, same lobby flow. Known territory. |
| Single developer | No coordination overhead. Full context in one head. Move fast, break nothing. |

Unity was the learning exercise — and it delivered. Camera flypaths, physics dice, async Awaitable patterns, SignalR integration, material/shader basics, normal maps. All learned in-context, building something real.

---

*Written: 4 July 2026*
