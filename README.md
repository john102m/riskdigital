# Risk — Digital Board Game

Digital adaptation of the classic Risk board game. A TV displays the shared world map; players deploy troops, attack, and fortify from their phones.

## Architecture

```
Phone (React) ──SignalR──▶ .NET 8 Server (WHUK) ◀──SignalR── Fire TV (Unity)
```

- **server/** — .NET 8 + SignalR game server (open in VS2022)
- **handset/** — React + Vite + Tailwind player controller (open in VS Code)
- **tv/** — Unity 2D project for TV display (Unity Editor + VS2022)
- **docs/** — Design docs, rules, territory data, map asset

## Quick Start

### Server
```bash
cd server/Risk.Server
dotnet run
```
Runs on `http://0.0.0.0:5000` — SignalR hub at `/gamehub`.

### Handset
```bash
cd handset
npm install
npm run dev
```
Runs on `http://localhost:3000` (exposed on LAN via `--host`).

### TV
Open `tv/` in Unity Editor. Build to Android for Fire TV Stick, or run in editor for desktop.
```bash
adb connect <fire-stick-ip>:5555
```

## Hardware

- **Current dev machine:** Lenovo E540 laptop
- **Upgrading to:** HP Z440 workstation (E5-1650 v4, 32GB, NVIDIA K2200) — awaiting setup
- **TV:** Amazon Fire TV Stick 4K Max (1st Gen, K2R2TE) — or desktop fallback
- **Handsets:** Any phone with a browser
- **Server:** Z440 on same WiFi LAN (once set up)

## Docs

- [Design](docs/RISK-DESIGN.md)
- [Unity Getting Started](docs/UNITY-GETTING-STARTED.md)
- [Map Asset](docs/risk-board-game-map.jpg)

## Prior Art — Flutter (Stock Exchange Game)

This project builds on a completed predecessor: a digital adaptation of "Flutter" (1955 Spear & Sons stock exchange board game) using the same multi-screen architecture. Source: `F:\Development\Flutter\`.

**Same patterns proven there:**
- .NET 8 + SignalR server (game logic singleton, thin hub → service delegation)
- React handset (hooks, localStorage rejoin, animation-locked controls, host-only admin)
- TV display (Jetpack Compose for Android TV in that case — overlay card queues, ticker, sound effects)
- AI players (server-driven, personality-based, adaptive timing)
- Fire TV Stick 4K Max deployment via ADB
- SignalR reconnect resilience, session persistence, `GetState` recovery

**What's new in Risk:**
- Unity 2D replaces Jetpack Compose (the learning exercise)
- 42-territory map with adjacency graph replaces 6 linear stock tracks
- Combat dice mechanics replace buy/sell/dividend cycles

## Status

Planning phase — awaiting Z440 workstation setup. Unity TV app is the learning exercise.
