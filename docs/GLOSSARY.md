# Glossary

## Components

| Term | Meaning |
|------|---------|
| **Server** | .NET 8 SignalR game server (`server/Risk.Server/`) |
| **Handset** | React phone controller (`handset/`) — what players hold |
| **UB** | Unity Board — the premium TV display (`D:\Unity Projects\RiskDigitalBoard`) |
| **WebTV** | `tv.html` — the browser-based TV board (Fire Stick Silk, laptop, etc.) |

## Dice System

| Term | Meaning |
|------|---------|
| **Arena** | The rectangular 3D box (box lid) where dice are thrown — off-screen in Unity, viewed via DiceCamera |
| **DiceCamera** | Perspective camera inside the arena, renders to a RenderTexture shown as picture-in-picture |
| **Dice panel** | The UI overlay (RawImage) showing the DiceCamera feed on the TV screen |
| **Flypath** | Catmull-Rom camera spline — the dramatic sweep into the arena |
| **Result position** | Overhead camera resting point after the flypath completes |
| **Spawn point** | Position above the arena floor where dice are instantiated before being thrown |
| **Settle** | When all dice velocities drop below threshold — physics done, faces readable |
| **Face reader** | `DiceFaceReader.cs` — dot-product axis check to determine which face points up |

## Game Flow (with Unity connected)

| Term | Meaning |
|------|---------|
| **Roll prompt** | Server asks a human defender to tap Roll on their handset |
| **SpawnDice** | Server tells UB to spawn one player's dice (attacker or defender) |
| **Two-phase spawn** | Attacker dice spawn first, defender dice spawn when they roll |
| **Auto-roll** | Server triggers a bot's roll immediately (no human input) |
| **Blitz display** | Static dice placed at final values after a blitz resolves server-side |

## Combat States (proposed refactor)

| State | Meaning |
|-------|---------|
| **Idle** | No dice panel visible, no combat in progress |
| **Awaiting spawn** | Panel shown, camera flying, waiting for SpawnDice events |
| **Settling** | Both sets spawned, physics running, waiting for all dice to stop |
| **Showing result** | Dice settled, holding for players to read before next action |
| **Hiding** | Delayed panel dismiss in progress (after capture) |
| **Showing blitz** | Blitz final dice on display |

## Conventions

| Term | Meaning |
|------|---------|
| **Attacker** | The player whose turn it is, initiating the attack |
| **Defender** | The player who owns the target territory |
| **Capture** | Target territory armies reach 0, ownership transfers |
| **Move-in** | Mandatory troop movement after capture (min = dice used) |
| **Token** | 3D cylinder (tilted) representing armies on a territory. Coloured by owner, labelled with army count. One per territory. |

## Technical

| Term | Meaning |
|------|---------|
| **TCS** | `TaskCompletionSource` — a .NET primitive that creates a Task you complete manually. Used in `PendingCombat` to await player rolls and Unity dice results without blocking. |
| **PendingCombat** | Single nullable object holding all state for a combat awaiting dice results from Unity. Nulled after resolve or timeout. |
| **Hub method** | A server method callable from clients via SignalR (e.g. `Attack`, `RollDice`, `Fortify`). |
| **Broadcast** | Server sends an event to all connected clients (`Clients.All.SendAsync`). |
| **Caller** | The specific client that invoked the current hub method (`Clients.Caller`). |
| **Rejoin** | Client reconnects after disconnect, sends player name, server updates their connection ID. |
| **Awaitable** | Unity 6 async pattern — like `Task` but Unity-aware (runs on main thread, respects play mode). Replaces coroutines. |
