# Multi-Game Server — Concurrent Game Instances

## Purpose
Allow multiple independent games to run simultaneously on the same server. Enables two households (Scotland + England) to each play their own game at the same time without interference.

## Current State
- Single `GameService` registered as a singleton.
- One game at a time, one game code.
- All hub calls route to the single instance.

## Proposed Design

### Game Manager
Replace the singleton `GameService` with a `GameManager` that holds multiple instances:

```csharp
public class GameManager
{
    private readonly ConcurrentDictionary<string, GameService> _games = new();

    public GameService CreateGame()
    {
        var code = GenerateCode(); // e.g. 4-letter code
        var game = new GameService();
        _games[code] = game;
        return game;
    }

    public GameService GetGame(string code)
    {
        _games.TryGetValue(code, out var game);
        return game;
    }

    public void RemoveGame(string code)
    {
        _games.TryRemove(code, out _);
    }

    private string GenerateCode()
    {
        // 4-char alphanumeric, collision-checked
        // ...
    }
}
```

Register as singleton: `builder.Services.AddSingleton<GameManager>();`

### Hub Changes
`GameHub` methods gain a `gameCode` parameter (or get it from the connection's group):

```csharp
public async Task JoinGame(string gameCode, string playerName, string colour)
{
    var game = _gameManager.GetGame(gameCode);
    if (game == null) { /* error */ return; }

    await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);
    game.JoinGame(Context.ConnectionId, playerName, colour);
}
```

### Broadcasts Scoped to Game
Replace `Clients.All.SendAsync(...)` with `Clients.Group(gameCode).SendAsync(...)`.

Each game's players and TV clients are in a SignalR group keyed by game code. Broadcasts only reach participants of that game.

### Connection Tracking
Each `GameService` tracks its own connection IDs. On disconnect/reconnect, the `GameManager` looks up which game a connection belongs to.

### TV Board (Unity + Web)
TV client joins a specific game code (entered on screen or passed via URL param for web):
- Web: `tv.html?game=ABCD`
- Unity: enter code on a simple join screen before connecting.

### Lifecycle
- Game created when first player creates a lobby.
- Game removed when:
  - Game over (after a timeout).
  - All players disconnect (after a grace period, e.g. 10 minutes).
  - Admin reset for that game code.

## What Changes

| File | Change |
|------|--------|
| `GameService.cs` | No longer singleton. Instantiated per game. Remove static state if any. |
| `GameHub.cs` | All methods route through `GameManager` by game code. Use SignalR groups. |
| `Program.cs` | Register `GameManager` as singleton instead of `GameService`. |
| Handset (`App.tsx`) | Already sends game code on join — no change needed. |
| Web TV (`tv.html`) | Read game code from URL param, pass on connection. |
| Unity TV | Add simple code entry screen before connecting. |

## What Stays the Same
- All game logic inside `GameService` — untouched.
- Combat, reinforcement, fortify, cards, AI — all unchanged.
- SignalR method names — same contract, just scoped.
- Handset UI — already uses game codes.

## Migration Path
1. Create `GameManager` class.
2. Refactor `GameHub` to route by game code + use groups.
3. Replace `Clients.All` with `Clients.Group(gameCode)` in `GameService` (pass `IHubContext` or a broadcast delegate).
4. Add game code param to TV connections.
5. Test two simultaneous games locally.

## Scaling Considerations
- Each `GameService` is lightweight (in-memory state, no threads blocked).
- 10 concurrent games would use negligible resources.
- No database needed — still in-memory. Games are ephemeral.

## Admin
- `/admin/games` — list active games + player counts.
- `/admin/reset/{gameCode}` — reset a specific game.
- `/admin/reset` — reset all (existing behaviour, hits all instances).

---

## Footnote: Unity Board Distribution

For distributing the Unity TV board to family (e.g. daughter's desktop in England):

| Item | Approach |
|------|----------|
| **Installer** | Inno Setup (free) — professional wizard-style installer, single setup.exe. |
| **Code signing** | Optional: cheap cert (~£60-80/year, Certum/Tucows). Signs all projects. Eliminates SmartScreen warning. Without it, one-time "Run Anyway" click. |
| **Delivery** | Shared OneDrive/Google Drive link. Or host on personal site (home server). |
| **Updates** | Rebuild in Unity → re-package with Inno Setup → upload new version to same link. |
| **Server connection** | Installer doesn't bundle the server. Unity board connects to your hosted server (WHUK or home) via URL entered on the join screen (or a default baked in). |
