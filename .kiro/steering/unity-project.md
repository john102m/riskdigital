# Unity TV Board (External Project)

## Location
- **Path:** `D:\Unity Projects\RiskDigitalBoard`
- **Repo:** https://github.com/john102m/UnityDigitalRisk.git
- **Separate from this repo** — different commit cadence, heavy Unity assets.

## Relationship
The Unity project is the premium TV board for this game. It is a read-only SignalR client
that connects to the same .NET server in `server/`. It consumes the same events as the
web board (`tv.html`) but renders them with 3D dice, glow effects, and physics.

- Server defines the SignalR contract (hub methods, DTOs, events) — Unity must match.
- Territory data (indices 0–41, coordinates, adjacency) originates in `server/Data/territories.json`.
- Any new server events or DTO changes affect both tv.html AND the Unity project.

## When working on Unity
- Read/write files at `D:\Unity Projects\RiskDigitalBoard\Assets\Scripts\` for C# scripts.
- Unity docs live here in `docs/unity/` (design, progress, session notes).
- Unity 6 LTS, 3D URP, `async Awaitable` pattern (not coroutines).
- SignalR client 8.0.x (9.x+ incompatible with Unity runtime).
- No Unity CLI builds — use Unity Editor.
