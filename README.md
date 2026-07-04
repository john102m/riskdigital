# Risk — Digital Board Game

Digital adaptation of the classic Risk board game. A TV displays the shared world map; players deploy troops, attack, and fortify from their phones.

Live at **risk.spooch.co.uk**

## Architecture

```
Phone (React) ──SignalR──▶ .NET 8 Server (WHUK) ◀──SignalR── TV Board (Web or Unity)
```

- **server/** — .NET 8 + SignalR game server (open in VS2022)
- **handset/** — React 18 + Vite + Tailwind player controller (open in VS Code)
- **docs/** — Design docs, rules, territory data, map asset

### TV Board Targets

| Target | Path | Access | Use Case |
|--------|------|--------|----------|
| **Web board** | `server/wwwroot/tv.html` | Any browser (Fire Stick Silk, phone, laptop) | Easy family access — no install, just open a URL |
| **Unity board** | Separate repo: `D:\Unity Projects\RiskDigitalBoard` | Windows .exe or Android APK via ADB | Premium experience — 3D dice physics, camera flypath, soundtrack, normal map relief |

Unity repo: https://github.com/john102m/UnityDigitalRisk.git

## Features

- 2–6 players, 42 territories, 6 continents
- Turn phases: Reinforce → Attack → Fortify
- Card trading (fixed UK values by default; escalating available as house rule)
- Secret missions — 14 cards (continent conquest, territory count, elimination with fallback)
- Blitz attack with ML-predicted capture odds
- Locked attack front (house rule, toggleable)
- AI players: 5 tiers (random → heuristic → strategic → personality → ML learning)
- ML.NET models with auto-retrain after every game
- Unity TV: physics-based dice rolling, camera flypath, normal map relief, phase-based soundtrack, game ceremony (welcome/win)
- Dice input lock — server rejects attacks while Unity dice in flight
- Web TV: parchment theme, territory glow, activity feed, sounds
- Handset: continent accordions, haptics, card UI, mission badges, colour/avatar picker
- Elimination, forced card trades, territory bonuses

## Quick Start

### Server
```bash
cd server/Risk.Server
# Open in VS2022 and run (do not use dotnet CLI)
```
Runs on `http://0.0.0.0:5000` — SignalR hub at `/gamehub`.

### Handset
```bash
cd handset
npm install
npm run dev
```
Runs on `http://localhost:3000` (exposed on LAN via `--host`).

### Unity TV Board
Open `D:\Unity Projects\RiskDigitalBoard` in Unity Editor. Build to Android for Fire TV Stick, or run in editor for desktop.

## Hardware

- **Dev machine:** HP Z440 (E5-1650 v4, 32GB, NVIDIA K2200)
- **TV:** Amazon Fire TV Stick 4K Max (1st Gen, K2R2TE) — or desktop fallback
- **Handsets:** Any phone with a browser
- **Server:** WHUK (WebHosting UK) for production. Z440 on LAN for dev.

## House Rules

| Rule | Default | Description |
|------|---------|-------------|
| Fixed card values | On | Infantry=4, Cavalry=6, Artillery=8, One-of-each=10 |
| Escalating card values | Off | 4, 6, 8, 10, 12, 15, +5 each |
| Locked attack front | On | Must attack from starting territory or captured territories |
| Missions | On | Secret mission cards — first to complete wins |

## AI Tiers

| Tier | Style | Description |
|------|-------|-------------|
| 1 | Random | Places/attacks randomly. Cannon fodder. |
| 2 | Aggressive | Always attacks weakest neighbour, blitzes at 5+ |
| 3 | Strategic | ML-predicted blitz odds, continent completion scoring, card timing |
| 4 | Opportunist | Elimination hunting, continent denial, chokepoint recognition |
| 5 | Personality | 4 characters (Opportunist, Cautious, Aggressive, Continental) using ML pipeline |

## Docs

- [Design](docs/design/RISK-DESIGN.md)
- [The Story So Far](docs/THE-STORY-SO-FAR.md)
- [Unity Progress](docs/unity/UNITY-PROGRESS.md)
- [Player Guide](docs/PLAYER-GUIDE.md)
- [Glossary](docs/GLOSSARY.md)
- [Roadmap](docs/proposals/ROADMAP.md)
- [File Map](docs/setup/FILE-MAP.md)

## Prior Art — Flutter (Stock Exchange Game)

This project builds on a completed predecessor: a digital adaptation of "Flutter" (1955 Spear & Sons stock exchange board game) using the same multi-screen architecture. Source: `F:\Development\Flutter\`.

**Same patterns proven there:**
- .NET 8 + SignalR server (game logic singleton, thin hub → service delegation)
- React handset (hooks, localStorage rejoin, animation-locked controls, host-only admin)
- TV display (Jetpack Compose for Android TV in that case)
- AI players (server-driven, personality-based, adaptive timing)
- Fire TV Stick 4K Max deployment via ADB
- SignalR reconnect resilience, session persistence, `GetState` recovery

**What's new in Risk:**
- Unity 3D replaces Jetpack Compose (the learning exercise)
- 42-territory map with adjacency graph replaces 6 linear stock tracks
- Combat dice mechanics replace buy/sell/dividend cycles
- ML.NET AI models with 5 difficulty tiers and auto-retrain

## Status

Fully playable and deployed. Built in two weeks (20 Jun – 3 Jul 2026). Web TV board, Unity TV board, and handset all functional. Pre-playtest polish complete — game ceremony, soundtrack, dice input locking, timing tweaks all in place.
