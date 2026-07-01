# Risk — Digital Board Game

Digital adaptation of the classic Risk board game. A TV displays the shared world map; players deploy troops, attack, and fortify from their phones.

## Architecture

```
Phone (React) ──SignalR──▶ .NET 8 Server (WHUK) ◀──SignalR── TV Board (Web or Unity)
```

- **server/** — .NET 8 + SignalR game server (open in VS2026)
- **handset/** — React 18 + Vite + Tailwind player controller (open in VS Code)
- **docs/** — Design docs, rules, territory data, map asset

### TV Board Targets

| Target | Path | Access | Use Case |
|--------|------|--------|----------|
| **Web board** | `server/wwwroot/tv.html` | Any browser (Fire Stick Silk, phone, laptop) | Easy family access — no install, just open a URL |
| **Unity board** | Separate repo: `D:\Unity Projects\RiskDigitalBoard` | Android APK sideloaded via ADB | Premium experience — 3D dice physics, camera flypath, sound |

Unity repo: https://github.com/john102m/UnityDigitalRisk.git

## Features

- 2–6 players, 42 territories, 6 continents
- Turn phases: Reinforce → Attack → Fortify
- Secret missions (toggleable)
- Locked attack front (house rule, toggleable)
- Card trading (fixed UK values or escalating)
- AI players: 5 tiers, ML.NET models, personality system, auto-retrain
- Unity TV: physics-based dice rolling, camera flypath, player-rolled combat (defender prompted), blitz final dice display
- Elimination, forced card trades, territory bonuses

## Quick Start

### Server
```bash
cd server/Risk.Server
# Open in VS2026 and run (do not use dotnet CLI)
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

## Docs

- [Design](docs/RISK-DESIGN.md)
- [Unity Progress](docs/unity/UNITY-PROGRESS.md)
- [Glossary](docs/GLOSSARY.md)
- [Map Asset](docs/risk-board-game-map.jpg)

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
- ML.NET AI models with 5 difficulty tiers

## Status

Actively in development. Web TV board and handset fully functional. Unity TV board functional with physics dice, camera flypath, and player-rolled combat — known timing bugs in human-vs-human dice prompts being addressed via combat state machine refactor.
