# Tech Stack

## Server
- **Runtime:** .NET 8, ASP.NET Core minimal API.
- **Real-time:** SignalR (WebSocket transport preferred, LongPolling fallback).
- **AI:** ML.NET models, 5 difficulty tiers, personality system, auto-retrain pipeline.
- **No database.** All game state in-memory. Single-game sessions only.
- **No authentication.** LAN/online party game.
- **Territory data:** `Data/territories.json` — 42 nodes with adjacency lists.
- **Hosting:** WHUK (serves handset bundle from wwwroot).

## Handset
- **Framework:** React 18 with TypeScript.
- **Build:** Vite, dev server on port 3000 with `--host` (LAN accessible).
- **Styling:** Tailwind CSS.
- **SignalR client:** `@microsoft/signalr`.
- **No routing library.** Single-page with conditional rendering by game phase.

## TV
- **Engine:** Unity 6 LTS, 3D URP.
- **Language:** C#, `async Awaitable` pattern.
- **SignalR client:** `Microsoft.AspNetCore.SignalR.Client` 8.0.x (9.x+ incompatible with Unity runtime).
- **Target device:** Amazon Fire TV Stick 4K Max (1st Gen, K2R2TE). Sideloaded via ADB.
- **Fallback:** Desktop (same Unity build, different target platform).
- **Rendering:** Static map sprite + 3D army tokens + physics dice arena.

## Hardware
- **Dev machine:** HP Z440 (E5-1650 v4, 32GB, NVIDIA K2200).
- **TV:** Fire TV Stick on same WiFi LAN as dev machine.
- **ADB over WiFi** for deployment (`adb connect <ip>:5555`).
- **Handsets:** Any phone with a browser.

## Development Tools
- **Server:** VS2026 Enterprise (ASP.NET workload).
- **TV scripts:** VS2026 Enterprise (Game Development with Unity workload).
- **Unity scenes/assets:** Unity Editor (Unity 6 LTS).
- **Handset:** VS Code.
