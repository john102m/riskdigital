# Conventions

## Server (C#/.NET)
- Minimal API style — no controllers. All SignalR, no REST endpoints beyond health/admin.
- Hub methods are the API surface. Keep `GameHub.cs` thin — delegate to service classes.
- **Partial class split:** `GameService` split by concern (`GameService.cs`, `.Combat.cs`, `.Turn.cs`).
- **Statics at top:** Static fields and constants declared before instance members.
- **Regions:** Used to group related methods within a file.
- Models in `Models/` — plain C# classes, no EF annotations.
- Singleton game state for single-game sessions.
- Use `record` types for DTOs sent over SignalR where immutability makes sense.
- Territory adjacency loaded from JSON, not hardcoded.

## Handset (React/TypeScript)
- Functional components only, hooks for state.
- Single `App.tsx` with conditional rendering by game phase (lobby/placement/playing/gameOver).
- SignalR connection managed in a custom hook.
- Tailwind utility classes — no separate CSS files beyond `index.css`.
- No state management library — local state + SignalR push is sufficient.
- Territory selection via filtered list, not interactive map.

## TV — Web Board (tv.html)
- Single-page HTML + vanilla JS, served from `server/wwwroot/tv.html`.
- SignalR client connects to same hub as handsets.
- Full-viewport map image with positioned dot overlays (colour-coded, army count).
- Parchment theme, info box overlay, dice results overlay, activity feed.
- Target: any browser — Fire Stick Silk, phone, laptop. No install needed.

## TV — Unity Board (C#)
- Unity 6 LTS, 3D URP. Separate repo.
- Static map background image with army tokens at defined x/y coordinates.
- `SignalRClient.cs` handles connection and deserialization.
- `GameStateManager.cs` holds reactive state, fires change events.
- `CombatTheatre.cs` uses explicit `CombatState` enum (state machine, not flags).
- `async Awaitable` pattern (Unity 6) — not coroutines.
- `CancellationTokenSource` for interruptible animations.
- Dark theme. Designed for Fire TV Stick 4K Max via ADB sideload (desktop fallback).
- Ownership shown via coloured circles with army counts (v1), territory tint fills later (v2).

## Cross-cutting
- Game logic lives ONLY on the server. Clients are dumb renderers/controllers.
- SignalR method names: PascalCase (e.g. `JoinGame`, `Attack`, `GameStateUpdated`).
- Territory indices 0–41 used consistently across all components.
- 6 continents with bonus values. Standard Risk adjacency graph.
