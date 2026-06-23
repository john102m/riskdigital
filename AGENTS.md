# AI Agent Guidelines

## Project Context
Digital adaptation of the classic Risk board game. .NET 8 SignalR server, React/Vite handset, two TV board targets (web + Unity). All communicate via SignalR WebSockets.

## Key Principles
- **DO NOT modify code directly.** All code changes must be written as a proposal doc first (explain what, where, and why) for human review and approval before any file is touched.
- **DO NOT run .NET CLI commands** (dotnet build, dotnet publish, dotnet run). VS2022 handles all .NET builds.
- **DO NOT run builds or deploys** without explicit permission.
- Game logic lives ONLY on the server. Clients are dumb renderers/controllers.
- Territory indices 0–41 used consistently across all components.
- SignalR method names: PascalCase (e.g. `JoinGame`, `Attack`, `GameStateUpdated`).
- Keep code minimal and clean — no unnecessary abstractions.

## Server (C#/.NET 8)
- Minimal API, no controllers. SignalR hub is the API surface.
- `GameHub.cs` stays thin — delegates to `GameService`.
- Singleton game state, no database.
- `record` types for DTOs. Plain classes for mutable state.
- Territory adjacency loaded from `Data/territories.json` at startup.

## Handset (React/TypeScript/Vite/Tailwind)
- Functional components, hooks for state. No state management library.
- SignalR connection via custom hook.
- Conditional rendering by game phase — no router.
- Territory selection via list/filter UI (not map tap).

## TV (Unity 2D / C#)
- Static world map background image + army token overlays at territory centre coordinates.
- `SignalRClient.cs` handles connection and event deserialization.
- `GameStateManager.cs` holds reactive state, fires change events.
- Dark theme. Designed for Fire TV Stick 4K Max via ADB sideload (desktop fallback).
- Ownership shown via coloured circles with army counts (v1), territory tint fills later (v2).

## TV (Web Board — tv.html)
- Single-page HTML + vanilla JS, served from `server/wwwroot/tv.html`.
- Full-viewport map with positioned dot overlays. Parchment theme.
- Any browser — Fire Stick Silk, phone, laptop. No install needed.

## Testing
- Debug hub methods for triggering test scenarios.
- Admin reset: `GET /admin/reset`.

## Deployment
- Server: .NET 8 on WHUK, serves handset bundle + web board from wwwroot
- TV (Unity): Android APK → sideload to Fire Stick via ADB
- TV (Web): just browse to `http://<server>:5000/tv.html`
- Local dev: server `:5000`, Vite `:3000`, Unity in editor
