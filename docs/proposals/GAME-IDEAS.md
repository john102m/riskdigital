# Game Ideas — Next Project

Achievable projects (weeks/months, not years). Inspired by C64 era, Elite, and the multi-screen architecture already proven with Flutter and Risk.

---

## 1. Elite-Lite: Trade & Combat

**The pitch:** Stripped-down Elite — trade between systems, upgrade your ship, occasional combat. 2D top-down or simple 3D (like the original wireframe). No procedural universe of 2000 systems — just 20-30 hand-crafted ones.

| Aspect | Scope |
|--------|-------|
| Universe | 20–30 star systems, each with a station |
| Trading | Buy low, sell high. 8–10 commodities. Prices vary by system economy type. |
| Combat | Simple encounters during hyperspace jumps. 2D dogfight or auto-resolve. |
| Ship upgrades | Cargo hold, weapons, shields, engine. 5–6 tiers. |
| Goal | Reach "Elite" rating (combat + trade milestones). Or open-ended sandbox. |
| Rendering | Retro wireframe 3D (like original Elite) OR 2D top-down star map with pixel art |
| Time to build | 4–8 weeks |

**Why it works:** The core loop (trade → earn → upgrade → trade further) is simple and proven. No story writing, no NPCs with dialogue trees. Just systems, prices, and a ship.

---

## 2. Multiplayer Space Trader (Multi-Screen)

**The pitch:** Elite meets your Risk architecture. TV shows the galaxy map / space view. Handsets are ship consoles — each player trades, fights, and upgrades independently but in a shared universe.

| Aspect | Scope |
|--------|-------|
| Architecture | Same as Risk: .NET server + React handset + Unity TV |
| Players | 2–4 competing traders in the same galaxy |
| TV board | Galaxy map showing all ship positions, trade routes, encounters |
| Handset | Buy/sell, set course, manage cargo, engage combat |
| Interaction | Piracy (attack other players), trade wars (buy out stock), racing to rare goods |
| Time to build | 8–12 weeks (leverages existing SignalR + handset patterns) |

**Why it works:** Reuses everything you've built. The competitive element adds tension that single-player Elite lacked.

---

## 3. Dungeon Crawl (Roguelike-Lite)

**The pitch:** Procedurally generated dungeon, turn-based, permadeath. Think Rogue/NetHack but with modern visuals (or deliberately retro). Simple enough to finish.

| Aspect | Scope |
|--------|-------|
| Map | Grid-based dungeon, procedurally generated floors |
| Combat | Turn-based, simple stats (HP, ATK, DEF, speed) |
| Items | Weapons, potions, scrolls. 20–30 item types. |
| Enemies | 10–15 types with distinct behaviours |
| Goal | Reach floor 10, defeat boss. Or endless mode. |
| Rendering | Pixel art top-down, or ASCII (full retro) |
| Time to build | 3–6 weeks |

**Why it works:** Roguelikes are the perfect scope — procedural content means less hand-crafting, permadeath means no save system complexity, grid-based means simple collision/pathfinding.

---

## 4. Multiplayer Dungeon (Multi-Screen)

**The pitch:** Same dungeon crawl but cooperative. TV shows the dungeon map. Each player's handset is their character sheet — choose moves, use items, attack. Like a digital D&D board.

| Aspect | Scope |
|--------|-------|
| Architecture | .NET server + React handset + Unity TV (same pattern) |
| Players | 2–4 cooperative |
| TV | Top-down dungeon map, fog of war reveals as party explores |
| Handset | Move, attack, use item, character stats |
| Time to build | 6–10 weeks |

---

## 5. Wireframe Racer (C64 Nostalgia)

**The pitch:** Retro wireframe 3D racing — think Revs or Stunt Car Racer but with that C64 aesthetic. Neon wireframes on black, scanline shader, chip-tune soundtrack.

| Aspect | Scope |
|--------|-------|
| Tracks | 5–8 hand-built tracks |
| Physics | Simple arcade (not sim). Speed, drift, boost. |
| Visuals | Wireframe 3D, CRT shader, limited colour palette (C64 16 colours) |
| Multiplayer | Split screen or time trials |
| Time to build | 4–6 weeks |

**Why it works:** Visually striking with minimal art assets (it's wireframes). Physics can be simple arcade-style. Short races = instant fun.

---

## 6. Artillery / Worms-Lite

**The pitch:** Turn-based artillery game. Destructible terrain, wind, weapon selection. 2–4 players hot-seat or multi-screen.

| Aspect | Scope |
|--------|-------|
| Terrain | Procedurally generated 2D landscape (destructible) |
| Weapons | 8–10 types (grenade, missile, cluster bomb, teleport, etc.) |
| Players | 2–4, turn-based |
| Physics | Projectile arcs, wind, explosion radius |
| Multi-screen option | Handset: aim angle + power + weapon select. TV: the battlefield. |
| Time to build | 3–5 weeks |

**Why it works:** Tiny scope, huge fun. The core mechanic (angle + power + wind) is one afternoon to implement. Everything else is polish.

---

## 7. Stock Exchange (Digital Flutter v2)

**The pitch:** You already built Flutter. Rebuild it in Unity as the premium version — same game logic (proven), but with 3D stock ticker board, animated tokens, market crash effects.

| Aspect | Scope |
|--------|-------|
| Logic | Already written and tested (port from F:\Development\Flutter) |
| Visually | 3D stock board, flying numbers, market bell, ticker tape animations |
| Time to build | 3–4 weeks (logic exists, just visual layer) |

**Why it works:** Zero design risk — the game is already done. Pure visual upgrade exercise.

---

## Recommendation

| If you want... | Build this |
|----------------|-----------|
| Scratch the Elite itch | #1 (solo) or #2 (multiplayer) |
| Quickest to playable | #6 Artillery or #3 Dungeon |
| Reuse Risk architecture | #2 or #4 (multi-screen) |
| Maximum nostalgia | #5 Wireframe Racer |
| Lowest risk, proven fun | #7 Flutter v2 |

---

## Shared Patterns (Already Proven)

All multi-screen options reuse:
- .NET 8 + SignalR server (game logic singleton, hub → service delegation)
- React handset (hooks, localStorage rejoin, animation-locked controls)
- Unity TV display (SignalR client, state manager, event-driven rendering)
- AI players (server-driven, personality-based)
- Suno soundtrack per phase
- Inno Setup distribution + optional local hosting
