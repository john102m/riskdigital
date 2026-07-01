# Architecture

## System overview
```
Phone (React) ──SignalR──▶ .NET 8 Server ◀──SignalR── TV Board (Web or Unity)
```

Three components communicate over SignalR WebSockets. The TV board has two targets.

## Component responsibilities

| Component | Path | Responsibility |
|-----------|------|----------------|
| Server | `server/Risk.Server/` | Game state, turn logic, combat resolution, reinforcement calculation, card trading, territory graph, AI players, mission validation. Single source of truth. |
| Handset | `handset/` | Player controller — lobby, deploy troops, select attacks, trade cards, fortify, defend (roll prompt). Thin client; no game logic. |
| TV (Web) | `server/wwwroot/tv.html` | Web board — any browser (Fire Stick Silk, phone, laptop). Easy family access, no install. |
| TV (Unity) | `D:\Unity Projects\RiskDigitalBoard` | Premium board — 3D dice physics, camera flypath, player-rolled combat. Separate repo. |

Both TV targets are read-only views of server state, consuming the same SignalR events.

## Server structure
- `Program.cs` — ASP.NET Core minimal API setup, SignalR + CORS config, static file serving.
- `Hubs/GameHub.cs` — SignalR hub. Thin — delegates to `GameService`.
- `Services/GameService.cs` — Partial class, split by concern:
  - `GameService.cs` — Fields, constructor, lobby/setup, private helpers, `PendingCombat` class.
  - `GameService.Combat.cs` — Attack, Blitz, ResolveCombat, MoveAfterCapture, AttackWithDice, PlayerRoll.
  - `GameService.Turn.cs` — TradeCards, Reinforce, EndReinforce, EndTurn, Fortify.
- `Models/` — `GameState`, `Player`, `Territory`, `Continent`, `Card`, DTOs.
- `Data/territories.json` — 42-territory adjacency graph, loaded at startup.
- No database — all state in-memory for single-game sessions.

## Communication pattern
- **Handset → Server:** Hub method invocations (e.g. `JoinGame`, `Attack`, `Fortify`, `RollDice`).
- **Server → All clients:** Broadcast via `Clients.All.SendAsync` (game state updates, combat results).
- **Server → Caller only:** `Clients.Caller.SendAsync` (validation errors, card hand updates).
- **Server → TV only:** `SpawnDice`, `CombatRollRequest` for Unity-specific dice events.

## Unity dice delegation flow

When Unity TV is connected, single attacks use physics dice:

1. Player calls `Attack` → server creates `PendingCombat`.
2. Attacker dice spawn immediately (`SpawnDice("attacker")`).
3. If defender is human → `RollPrompt` sent to their handset → they tap Roll → `SpawnDice("defender")`.
4. If defender is bot → auto-rolled immediately.
5. Unity reads settled faces → calls `SubmitDiceResult`.
6. Server resolves combat with those values.
7. Fallback: 10s timeout → server rolls randomly.
8. `_pending` nulled after resolve.

Blitz stays server-side (too many rounds for physics). Final dice displayed statically.

## Game phases
1. **Lobby** — players join, TV shows waiting screen.
2. **Initial Placement** — territories dealt, players place remaining armies in turn.
3. **Playing** — turn-based: Reinforce → Attack → Fortify.
4. **Game Over** — mission complete or one player owns all 42 territories.
