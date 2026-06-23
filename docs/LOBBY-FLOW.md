# Game Creation & Lobby Flow

## Current State

### What works
- Host creates game → gets 4-digit code → enters lobby
- Other handsets join via code → see player list
- Host adds AI players → all see updated list
- Host starts game → all transition to placement
- TV shows board once game starts
- If a handset refreshes mid-game, `GetLobbyStatus` returns the active game code → auto-fills join
- Late joiner who refreshes sees the correct code and can join

### Known Issues

1. **TV shows black screen before game created** — connected but nothing to render (no game state). Should show a waiting/splash screen.
2. **Other handsets don't auto-update when game is created** — they still show "Create Game" until they refresh. The `LobbyStatus` is only fetched on initial connect, not pushed when a game is created.
3. **Can't remove an AI player** — once added, they're stuck. Host should be able to kick AI before starting.

---

## The Full Flow (How It Should Work)

### Phase: No Game Exists

**TV:**
- Shows splash/waiting screen: Risk logo, "Waiting for game..." or parchment background with game title. Not a black void.

**Handsets:**
- Show Connect screen with name input + "Create Game" / "Join Game" buttons
- Both buttons visible (no game to protect)

### Phase: Game Created (Lobby)

**TV:**
- Shows lobby info: game code prominently, player list (names + colours), "Waiting for host to start"
- Updates live as players join / AI added

**Host handset:**
- Lobby screen: game code, player list, "Add AI" button, "Start Game" button
- Can remove AI players (tap to remove)

**Other handsets (already connected):**
- Should receive a push event (`LobbyStatus` or `GameStateUpdated`) when game is created
- Auto-transition to join screen with code pre-filled
- Currently: they don't know a game was created until they refresh

**Late-arriving handsets:**
- Connect → `GetLobbyStatus` → game exists → hide "Create Game", show code pre-filled → join

### Phase: Game In Progress

**TV:**
- Shows the board (current behaviour — works fine)

**Handsets (in-game):**
- Their phase screen (placement/reinforce/attack/fortify)

**Late-arriving handset:**
- Connect → `GetLobbyStatus` → game exists, in progress → show join with code
- If slots available and game rules allow: can join mid-game (stretch goal, not v1)
- If no slots: show "Game in progress" message (spectator mode later)

### Phase: Game Over

**TV:**
- Winner overlay (current behaviour)

**Handsets:**
- Game Over screen (current behaviour)
- Host sees "New Game" button → resets to lobby

---

## Fixes Needed

### 1. TV Splash Screen (black screen fix)

When TV connects and no game exists (or state is null), show:
```
┌─────────────────────────────────────┐
│                                     │
│            🎲 RISK                  │
│        Digital Board Game           │
│                                     │
│       Waiting for game...           │
│                                     │
│         Connected ✓                 │
└─────────────────────────────────────┘
```

When game is in Lobby phase, show:
```
┌─────────────────────────────────────┐
│                                     │
│          Game Code: 4453            │
│                                     │
│     🔴 John (Host)                  │
│     🔵 Bot Alice 🤖                 │
│     🟢 Bot Bob 🤖                   │
│                                     │
│      Waiting for host to start      │
│                                     │
└─────────────────────────────────────┘
```

**Implementation:** In the `render()` function, handle `state.phase === 'Lobby'` as a distinct view (not just the map). Handle `state === null` as the splash.

### 2. Push LobbyStatus to All Clients on Game Creation

When `CreateGame` is invoked on the hub:
- After creating the game, broadcast `LobbyStatus` to **all connected clients** (not just caller)
- Other handsets receive this → auto-hide "Create Game", show join with code pre-filled

```csharp
// In GameHub.cs after CreateGame:
await Clients.All.SendAsync("LobbyStatus", new { GameExists = true, GameCode = state.GameCode, Phase = "Lobby", PlayerCount = state.Players.Count });
```

Also broadcast on `JoinGame` and `AddAI` so all clients stay in sync.

### 3. Remove AI Player (Host Only)

New hub method: `RemoveAI(int playerIndex)`
- Validates: host only, lobby phase only, target is AI
- Removes from player list, frees colour
- Broadcasts updated state

**Handset:** Show a ✕ or 🗑️ next to AI players in lobby (host only).

---

## Open Questions

1. **Should the TV show lobby info?** Or just splash until game starts? (Showing lobby = family can see the code on TV without asking the host)
2. **Max players with AI?** Currently 6 total. If 2 humans + 4 AI, can one human leave and rejoin later? (Rejoin by name already works)
3. **Mid-game join?** Park for now — adds complexity (dealing territories, adjusting balance). Not v1.
4. **Spectator mode?** Also park — TV is the spectator. If someone arrives late they can watch the TV or open tv.html on their phone.

---

## Priority

1. **TV splash screen** — quick fix, removes the confusing black screen
2. **Broadcast LobbyStatus on game creation** — fixes the stale "Create Game" button on other handsets
3. **Remove AI** — nice-to-have for lobby flexibility

---

*Created: 2026-06-23*
