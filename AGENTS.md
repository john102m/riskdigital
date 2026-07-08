# AI Agent Guidelines

## Project Context
Digital adaptation of the classic Risk board game. .NET 8 SignalR server, React/Vite handset, two TV board targets (web + Unity). All communicate via SignalR WebSockets.

## Key Principles
- **DO NOT modify code directly.** All code changes must be written as a proposal doc first (explain what, where, and why) for human review and approval before any file is touched.
- **DO NOT run .NET CLI commands** (dotnet build, dotnet publish, dotnet run). VS2026 handles all .NET builds.
- **DO NOT run builds or deploys** without explicit permission.
- Game logic lives ONLY on the server. Clients are dumb renderers/controllers.
- Territory indices 0–41 used consistently across all components.
- SignalR method names: PascalCase (e.g. `JoinGame`, `Attack`, `GameStateUpdated`).
- Keep code minimal and clean — no unnecessary abstractions.

## Debugging & Diagnosis Rules
- **Read the session docs first.** `docs/sessions/` contains exhaustive notes on what was tried, what failed, and why. Do not diagnose a problem without reading the relevant session docs. Confident wrong answers have caused multiple wasted sessions on this project.
- **Trace the full event flow before suggesting a fix.** For any SignalR/Unity issue, read both the server send path AND the Unity receive path end-to-end. A fix that looks correct on one side may be broken by the other side.
- **Do not assume event ordering.** SignalR events on the Unity client can arrive in any order. Never write code that assumes event A arrives before event B unless the server explicitly sequences them with an await between sends.
- **Check what `_pending` is doing.** Most combat bugs involve `PendingCombat` being null, stale, or having the wrong TCS fired. Read `PendingCombat.cs` and `AttackWithDice` before touching combat code.
- **Silent failures are common in Unity async.** Exceptions inside `async Awaitable` methods are swallowed. If something "never fires", add a try/catch with `Debug.LogError` before assuming the logic is wrong.
- **Inspector-serialized values persist.** Unity serializes field values into the scene. Code changes that clear fields at runtime must always write the cleared value explicitly — returning early without writing leaves stale Inspector values in place.

## Coding Conventions

### C# / Server
- Partial class split: `GameService.cs`, `GameService.Combat.cs`, `GameService.Turn.cs` — keep concerns separated.
- Statics and constants at top of class, before instance members.
- Regions used to group related methods within a file — match existing region names.
- `record` types for immutable DTOs sent over SignalR. Plain `class` for mutable state.
- No `Console.WriteLine` — use `ILogger` (visible in `/admin/app-log`) or `Debug.WriteLine` (VS Output). `Console.WriteLine` is invisible in this project's logging setup.
- `System.Diagnostics.Debug.WriteLine` is also unreliable — prefer `ILogger`.
- Hub methods stay thin — delegate to `GameService`. No game logic in `GameHub.cs`.
- Always null-check `_pending` before accessing it. It can be null at any time (timeout, TV disconnect, game reset).
- `TrySetResult` is safe to call multiple times — use it. `SetResult` throws on double-call.

### Unity / C#
- `async Awaitable` pattern (Unity 6) — not coroutines.
- All SignalR callbacks arrive on background threads. Must be marshalled to main thread via `UnityMainThread.Enqueue()` before touching Unity objects or firing events.
- `combatCts` cancellation token must be checked after every `await` inside combat async methods. A new combat can start (and cancel the token) while any await is in flight.
- `activeDice` (physics dice with `DiceFaceReader`) and `staticDice` (display-only, no reader) are separate lists. Never mix them. `ReadAll()` only processes `activeDice`.
- `attackerDiceCount` on `DiceRoller` must be set correctly before `ReadAll()` — it determines the split between attacker and defender values. Set it in `SpawnSet()` for the defender by using `activeDice.Count` at that point. `ClearDice()` resets it to 0.
- `ResetCombat()` is the single correct way to reset combat state. Do not reset individual fields directly.
- Every "kill arena" code path (`OnStateChanged`, `OnCombatResult`) must exempt `ShowingBlitz` and `ShowingResult` states — they manage their own teardown.
- `FindAnyObjectByType<T>()` in `Start()` — not in `Update()` or event handlers.

### React / Handset
- Functional components only. No class components.
- Tailwind utility classes only — no separate CSS files beyond `index.css`.
- No state management library. Local state + SignalR push is sufficient.
- `showToast` for user-facing errors. Never `console.error` as a substitute.
- Phase-conditional rendering in `App.tsx` — no router.

### General
- Match existing naming exactly — same casing, same abbreviations, same prefixes.
- No speculative abstractions. Solve the problem asked. Do not add configurability, extra parameters, or "future-proofing" that wasn't requested.
- When adding a new SignalR event: add it in server, `SignalRClient.cs`, and any handler (TV/handset) in the same proposal. Partial implementations that leave one side unhandled cause silent failures.

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
