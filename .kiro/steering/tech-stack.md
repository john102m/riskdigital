# Tech Stack

## Server
- **Runtime:** .NET 8, ASP.NET Core minimal API.
- **Real-time:** SignalR (WebSocket transport preferred, LongPolling fallback).
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
- **Engine:** Unity 2D (2022 LTS or 6000 LTS).
- **Language:** C#.
- **SignalR client:** `com.microsoft.signalr` NuGet (or Best HTTP/SignalR asset).
- **Target device:** Amazon Fire TV Stick 4K Max (1st Gen, K2R2TE). Sideloaded via ADB.
- **Fallback:** Desktop (same Unity build, different target platform).
- **Rendering:** Static map sprite + army token overlays at x/y coordinates.

## Hardware
- **Dev machine:** HP Z440 (E5-1650 v4, 32GB, NVIDIA K2200).
- **TV:** Fire TV Stick on same WiFi LAN as dev machine.
- **ADB over WiFi** for deployment (`adb connect <ip>:5555`).
- **Handsets:** Any phone with a browser.

## Development Tools
- **Server:** VS2022 (ASP.NET workload).
- **TV scripts:** VS2022 (Game Development with Unity workload).
- **Unity scenes/assets:** Unity Editor.
- **Handset:** VS Code.
