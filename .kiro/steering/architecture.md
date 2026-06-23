# Architecture

## System overview
```
Phone (React) ──SignalR──▶ .NET 8 Server ◀──SignalR── TV Board (Web or Unity)
```

Three components communicate over SignalR WebSockets. The TV board has two targets.

## Component responsibilities

| Component | Path | Responsibility |
|-----------|------|----------------|
| Server | `server/Risk.Server/` | Game state, turn logic, combat resolution, reinforcement calculation, card trading, territory graph. Single source of truth. |
| Handset | `handset/` | Player controller — lobby, deploy troops, select attacks, trade cards, fortify. Thin client; no game logic. |
| TV (Web) | `server/wwwroot/tv.html` | Web board — any browser (Fire Stick Silk, phone, laptop). Easy family access, no install. |
| TV (Unity) | `tv/` | Premium board — Android APK sideloaded via ADB. Dice animations, sound, polish. |

Both TV targets are read-only views of server state, consuming the same SignalR events.

## Server structure
- `Program.cs` — ASP.NET Core minimal API setup, SignalR + CORS config, static file serving.
- `Hubs/GameHub.cs` — SignalR hub. All client↔server communication flows through here.
- `Services/GameService.cs` — Singleton game logic: combat, reinforcement, card trading, win detection.
- `Models/` — `GameState`, `Player`, `Territory`, `Continent`, `Card`, DTOs.
- `Data/territories.json` — 42-territory adjacency graph, loaded at startup.
- No database — all state in-memory for single-game sessions.

## Communication pattern
- **Handset → Server:** Hub method invocations (e.g. `JoinGame`, `Attack`, `Fortify`).
- **Server → All clients:** Broadcast via `Clients.All.SendAsync` (game state updates, combat results).
- **Server → Caller only:** `Clients.Caller.SendAsync` (validation errors, card hand updates).
- **Server → TV only:** Group-based for TV-specific animation events.

## Game phases
1. **Lobby** — players join, TV shows waiting screen.
2. **Initial Placement** — territories dealt, players place remaining armies in turn.
3. **Playing** — turn-based: Reinforce → Attack → Fortify.
4. **Game Over** — one player owns all 42 territories.
