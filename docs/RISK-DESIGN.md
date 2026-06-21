# Risk — Digital Adaptation Design

## Architecture

```
Phone (React/Tailwind) ──SignalR──▶ .NET 8 Server (WHUK) ◀──SignalR── Fire TV (Unity)
```

| Component | Tech | Role |
|-----------|------|------|
| Server | .NET 8, SignalR, hosted on WHUK | Game logic, state, combat resolution. Single source of truth. |
| Handset | React + TypeScript + Vite + Tailwind | Player controller — deploy troops, select attacks, trade cards. Served from wwwroot. |
| TV | Unity (C#), sideloaded to Fire Stick | Shared display — world map, armies, dice battles, animations. Read-only. |

## Game Rules Summary

- 2–6 players, 42 territories across 6 continents
- **Objective:** Eliminate all opponents (classic) or complete a secret mission (mission variant)
- **Turn phases:** Reinforce → Attack → Fortify
- **Reinforcements:** territories/3 (min 3) + continent bonuses + card set trade-ins
- **Combat:** Attacker rolls 1–3 dice, defender rolls 1–2. Compare highest pairs, defender wins ties.
- **Cards:** Earn one per turn if you captured a territory. Sets of 3 (matching or one-of-each) trade for escalating troop bonuses.
- **Fortify:** Move troops from one territory to one adjacent friendly territory (one move per turn)

---

## Phases of Build

### Phase 1 — Server: State & Lobby

- Game state model: territories (id, owner, armies), players (id, name, cards, colour), turn phase, current player
- Territory graph: 42 nodes with adjacency list (hardcoded or JSON)
- Continent definitions: which territories, bonus value
- SignalR hub: `CreateGame`, `JoinGame`, `StartGame`
- Random territory assignment on start (deal territories evenly, 1 army each)
- Initial army placement phase (players take turns placing remaining armies)

### Phase 2 — Server: Reinforcement Phase

- Calculate reinforcements: territories/3 + continent bonuses
- Card trading: validate sets, return escalating troop bonus (4, 6, 8, 10, 12, 15, +5...)
- Player places troops on owned territories (one-by-one or batch)
- Broadcast state after each placement
- "Done Reinforcing" ends phase → Attack

### Phase 3 — Server: Combat

- `Attack(fromTerritory, toTerritory, numDice)` — validate adjacency, ownership, army counts
- Dice resolution: sort attacker/defender dice descending, compare pairs, apply losses
- Territory capture: attacker moves troops in (min = dice used), ownership transfers
- Card earned flag: one card per turn on first capture
- Elimination: if defender loses last territory, attacker takes their cards (force trade if >5)
- Win condition: one player owns all 42 territories (or mission complete)
- "Blitz" option: repeat attack until one side falls below threshold

### Phase 4 — Server: Fortify & Turn End

- `Fortify(from, to, numTroops)` — validate adjacency, ownership, troop count
- End turn → advance to next player → reinforcement phase
- Deal card if capture happened this turn

### Phase 5 — Handset UI

- **Lobby:** Create/join game, player list, colour selection, start button (host)
- **Reinforcement:** Territory list (owned), tap to place troops, card trade UI, "Done" button
- **Attack:** Select source territory → select target → choose dice count → "Attack" / "Blitz" → result display → "Move troops in" slider → "Done Attacking"
- **Fortify:** Select source → destination → troop count slider → "Done"
- **Passive view:** Other players' turns — show what's happening (who attacked where, results)
- **Cards in hand:** Always visible, trade button when valid set available

### Phase 6 — TV (Unity): Map & Display

- 2D top-down world map with territory regions (SVG-derived mesh or sprite regions)
- Army count displayed per territory
- Player colour overlay on owned territories
- Active player highlight
- Current phase indicator

### Phase 7 — TV: Animations & Polish

- Dice battle animation (attacker vs defender dice, rolling, comparison)
- Troop movement animation (march from territory to territory)
- Territory capture flash/pulse
- Territory highlight/glow options:
  - v1: Animated ring/circle at territory x/y — expanding + fading "radar ping". No extra assets needed.
  - v2: White-filled territory silhouette overlay, additive blend, alpha pulse. Comes free once territory masks exist for tinting.
  - Alt: Bloom post-processing on emissive tokens (quick but affects everything bright on screen).
  - Alt: Glow shader on the army token sprite (highlights marker, not territory shape).
- Camera zoom to battle region during combat
  - Default: full map view with highlight/pulse on attacker + defender territories
  - Zoom only for big moments: elimination, final capture, single non-blitz combat
  - Blitz: rapid results ticker, no zoom (avoids nauseating camera movement on multi-attack turns)
  - Experiment once static version works — simple highlight may be sufficient
- Elimination fanfare
- Victory screen with stats

### Dice & Combat Theatre (TV)

Rolling dice was part of the fun in physical Risk. Even digitised, *watching and hearing* the roll adds drama. Flutter proved this — the dice rattle + spin animation on the TV made every roll a communal moment, even though the result was already decided server-side.

**Architecture:**
- Server resolves combat instantly (already done) and broadcasts `CombatResult`
- TV receives result but **delays applying state** until animation completes
- Animation sequence gates further events (same pattern as Flutter's `showDice` flag)

**Sequence (single attack):**
1. Sound: dice rattle plays immediately on event receipt
2. Visual: dice sprites spin/tumble (~1s, gradually slowing, cycling random faces)
3. Dice land on actual values — attacker dice (red) vs defender dice (white)
4. Brief pause (~1s) — let players react, see the comparison
5. Apply result: flash casualties, update army counts, show capture if applicable
6. Ungate — ready for next event

**Sequence (blitz):**
- No per-roll animation (would be nauseating for 10+ rolls)
- Instead: rapid-fire dice rattle sound, rolling counter showing rounds, then final summary
- Optional: show last decisive roll in full if it resulted in capture

**Sound effects (from Flutter, adapt for Risk):**
| Event | Sound | Notes |
|-------|-------|-------|
| Attack roll | dice_rattle | Short sharp rattle, plays every combat |
| Territory captured | capture_fanfare | Triumphant, brief |
| Elimination | elimination_crash | Dramatic — player knocked out |
| Card traded | card_flip | Subtle |
| Mission complete / victory | victory | Celebratory |
| Blitz in progress | rapid_ticks | Fast metronome during auto-resolve |

**Handset (lightweight):**
- Vibration pulse on dice result (Haptics API)
- Brief CSS shake animation on result display
- No sound on handset — TV is the theatre, phone is the controller

**Implementation notes:**
- Unity: `AudioSource` + clips in `Resources/Audio/`, triggered by `CombatResolver.cs`
- Unity: dice prefab with tumble animation (2D sprite rotation or simple frame animation)
- Gate pattern: `CombatResolver` queues incoming events, processes sequentially with async/await between animations
- Debug TV (tv.html): can show dice values immediately (no animation needed for dev)

**Key insight from Flutter:** The delay between "dice rolled" and "result applied" is what creates tension. Even 1.5 seconds transforms a data update into a shared experience. Without it, combat feels like a spreadsheet recalculating.

---

### Phase 8 — Optional Features

- **Secret missions** — dealt at start, checked on capture/elimination
- **AI players** — same pattern as Flutter game (server-driven, personalities)
- **Fog of war** — hide army counts for non-adjacent territories (variant rule)
- **Game timer** — per-turn time limit to keep pace
- **Spectator mode** — join as viewer only
- **Remote play** — already supported since server is on WHUK

---

## Data Model

### Territory

```csharp
public class Territory
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int ContinentId { get; set; }
    public List<int> AdjacentIds { get; set; }
    public string? OwnerId { get; set; }
    public int Armies { get; set; }
}
```

### Continent

```csharp
public class Continent
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int BonusArmies { get; set; }
    public List<int> TerritoryIds { get; set; }
}
```

### Player

```csharp
public class Player
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int ColourIndex { get; set; }
    public List<Card> Cards { get; set; }
    public bool EarnedCardThisTurn { get; set; }
    public bool IsEliminated { get; set; }
}
```

### Card

```csharp
public record Card(int TerritoryId, CardType Type); // Infantry, Cavalry, Artillery, Wild

public enum CardType { Infantry, Cavalry, Artillery, Wild }
```

### Game State

```csharp
public class GameState
{
    public string GameCode { get; set; }
    public GamePhase Phase { get; set; }
    public TurnPhase TurnPhase { get; set; }
    public List<Player> Players { get; set; }
    public List<Territory> Territories { get; set; }
    public string CurrentPlayerId { get; set; }
    public int CardTradeCount { get; set; } // escalating bonus tracker
    public int ReinforcementsRemaining { get; set; }
}

public enum GamePhase { Lobby, InitialPlacement, Playing, GameOver }
public enum TurnPhase { Reinforce, Attack, Fortify }
```

---

## SignalR Methods

### Client → Server

| Method | Params | Phase |
|--------|--------|-------|
| `CreateGame` | playerName | Lobby |
| `JoinGame` | gameCode, playerName | Lobby |
| `StartGame` | — | Lobby (host) |
| `PlaceTroop` | territoryId | InitialPlacement / Reinforce |
| `TradeCards` | cardId1, cardId2, cardId3 | Reinforce |
| `DoneReinforcing` | — | Reinforce |
| `Attack` | fromId, toId, numDice | Attack |
| `Blitz` | fromId, toId | Attack |
| `MoveTroopsIn` | count | Attack (after capture) |
| `DoneAttacking` | — | Attack |
| `Fortify` | fromId, toId, count | Fortify |
| `SkipFortify` | — | Fortify |

### Server → All Clients

| Method | Payload |
|--------|---------|
| `GameStateUpdated` | Full game state |
| `CombatResult` | attacker dice, defender dice, losses, captured? |
| `PlayerEliminated` | playerId, eliminatedBy |
| `GameOver` | winnerId |

### Server → Caller Only

| Method | Payload |
|--------|---------|
| `Error` | message |
| `CardsUpdated` | player's current hand |

### Server → TV Only

| Method | Payload |
|--------|---------|
| `AnimateCombat` | fromId, toId, attacker dice, defender dice, result |
| `AnimateCapture` | territoryId, newOwnerId |
| `AnimateFortify` | fromId, toId, count |

---

## Territory Map

42 territories, 6 continents. Standard Risk layout:

| Continent | Territories | Bonus |
|-----------|-------------|-------|
| North America | 9 (Alaska, NW Territory, Greenland, Alberta, Ontario, Quebec, W US, E US, C America) | 5 |
| South America | 4 (Venezuela, Brazil, Peru, Argentina) | 2 |
| Europe | 7 (Iceland, Scandinavia, Great Britain, N Europe, W Europe, S Europe, Ukraine) | 5 |
| Africa | 6 (N Africa, Egypt, E Africa, Congo, S Africa, Madagascar) | 3 |
| Asia | 12 (Ural, Siberia, Yakutsk, Kamchatka, Irkutsk, Mongolia, Japan, Afghanistan, China, India, Siam, Middle East) | 7 |
| Australia | 4 (Indonesia, New Guinea, W Australia, E Australia) | 2 |

Adjacency list stored as JSON — loaded at startup.

---

## Handset UI Screens (Conditional Rendering)

1. **Lobby** — join/create, player list with colours, start
2. **Initial Placement** — map overview (text list of territories), tap to place one army at a time
3. **Playing** — tabbed by phase:
   - Reinforce tab: owned territories, place troops, trade cards
   - Attack tab: source selector → target selector → dice picker → results
   - Fortify tab: source → destination → troop slider
4. **Game Over** — winner announcement, stats

---

## Unity TV App Structure

```
Assets/
├── Scripts/
│   ├── Networking/
│   │   └── SignalRClient.cs      — connect, receive events, deserialize
│   ├── Game/
│   │   ├── GameStateManager.cs   — holds current state, fires change events
│   │   ├── TerritoryData.cs      — static map data (adjacency, positions)
│   │   └── CombatResolver.cs     — animation sequencing for battles
│   └── UI/
│       ├── MapRenderer.cs        — draws territories, colours, army counts
│       ├── DiceBattle.cs         — dice roll animation
│       ├── TroopAnimation.cs     — movement between territories
│       └── HUD.cs                — current player, phase, log
├── Prefabs/
│   ├── Territory.prefab
│   ├── ArmyToken.prefab
│   └── Dice.prefab
├── Scenes/
│   └── GameScene.unity
└── Data/
    └── territories.json          — positions, adjacency, continent mapping
```

---

## Territory Adjacency Data (territories.json)

Full 42-territory graph. Load at server startup. Each territory has an id (0–41), name, continent, and adjacency list.

```json
{
  "continents": [
    { "id": 0, "name": "North America", "bonus": 5, "territoryIds": [0,1,2,3,4,5,6,7,8] },
    { "id": 1, "name": "South America", "bonus": 2, "territoryIds": [9,10,11,12] },
    { "id": 2, "name": "Europe", "bonus": 5, "territoryIds": [13,14,15,16,17,18,19] },
    { "id": 3, "name": "Africa", "bonus": 3, "territoryIds": [20,21,22,23,24,25] },
    { "id": 4, "name": "Asia", "bonus": 7, "territoryIds": [26,27,28,29,30,31,32,33,34,35,36,37] },
    { "id": 5, "name": "Australia", "bonus": 2, "territoryIds": [38,39,40,41] }
  ],
  "territories": [
    { "id": 0, "name": "Alaska", "continentId": 0, "adjacent": [1,3,29] },
    { "id": 1, "name": "Northwest Territory", "continentId": 0, "adjacent": [0,2,3,4] },
    { "id": 2, "name": "Greenland", "continentId": 0, "adjacent": [1,4,5,13] },
    { "id": 3, "name": "Alberta", "continentId": 0, "adjacent": [0,1,4,6] },
    { "id": 4, "name": "Ontario", "continentId": 0, "adjacent": [1,2,3,5,6,7] },
    { "id": 5, "name": "Quebec", "continentId": 0, "adjacent": [2,4,7] },
    { "id": 6, "name": "Western United States", "continentId": 0, "adjacent": [3,4,7,8] },
    { "id": 7, "name": "Eastern United States", "continentId": 0, "adjacent": [4,5,6,8] },
    { "id": 8, "name": "Central America", "continentId": 0, "adjacent": [6,7,9] },
    { "id": 9, "name": "Venezuela", "continentId": 1, "adjacent": [8,10,11] },
    { "id": 10, "name": "Brazil", "continentId": 1, "adjacent": [9,11,12,20] },
    { "id": 11, "name": "Peru", "continentId": 1, "adjacent": [9,10,12] },
    { "id": 12, "name": "Argentina", "continentId": 1, "adjacent": [10,11] },
    { "id": 13, "name": "Iceland", "continentId": 2, "adjacent": [2,14,15] },
    { "id": 14, "name": "Scandinavia", "continentId": 2, "adjacent": [13,15,16,19] },
    { "id": 15, "name": "Great Britain", "continentId": 2, "adjacent": [13,14,16,17] },
    { "id": 16, "name": "Northern Europe", "continentId": 2, "adjacent": [14,15,17,18,19] },
    { "id": 17, "name": "Western Europe", "continentId": 2, "adjacent": [15,16,18,20] },
    { "id": 18, "name": "Southern Europe", "continentId": 2, "adjacent": [16,17,19,20,21,37] },
    { "id": 19, "name": "Ukraine", "continentId": 2, "adjacent": [14,16,18,26,33,37] },
    { "id": 20, "name": "North Africa", "continentId": 3, "adjacent": [10,17,18,21,22,23] },
    { "id": 21, "name": "Egypt", "continentId": 3, "adjacent": [18,20,22,37] },
    { "id": 22, "name": "East Africa", "continentId": 3, "adjacent": [20,21,23,24,25,37] },
    { "id": 23, "name": "Congo", "continentId": 3, "adjacent": [20,22,24] },
    { "id": 24, "name": "South Africa", "continentId": 3, "adjacent": [22,23,25] },
    { "id": 25, "name": "Madagascar", "continentId": 3, "adjacent": [22,24] },
    { "id": 26, "name": "Ural", "continentId": 4, "adjacent": [19,27,33,34] },
    { "id": 27, "name": "Siberia", "continentId": 4, "adjacent": [26,28,29,30,31] },
    { "id": 28, "name": "Yakutsk", "continentId": 4, "adjacent": [27,29,30] },
    { "id": 29, "name": "Kamchatka", "continentId": 4, "adjacent": [0,28,30,31,32] },
    { "id": 30, "name": "Irkutsk", "continentId": 4, "adjacent": [27,28,29,31] },
    { "id": 31, "name": "Mongolia", "continentId": 4, "adjacent": [27,29,30,32,34] },
    { "id": 32, "name": "Japan", "continentId": 4, "adjacent": [29,31] },
    { "id": 33, "name": "Afghanistan", "continentId": 4, "adjacent": [19,26,34,35,37] },
    { "id": 34, "name": "China", "continentId": 4, "adjacent": [26,27,31,33,35,36] },
    { "id": 35, "name": "India", "continentId": 4, "adjacent": [33,34,36,37] },
    { "id": 36, "name": "Siam", "continentId": 4, "adjacent": [34,35,38] },
    { "id": 37, "name": "Middle East", "continentId": 4, "adjacent": [18,19,21,22,33,35] },
    { "id": 38, "name": "Indonesia", "continentId": 5, "adjacent": [36,39,40] },
    { "id": 39, "name": "New Guinea", "continentId": 5, "adjacent": [38,40,41] },
    { "id": 40, "name": "Western Australia", "continentId": 5, "adjacent": [38,39,41] },
    { "id": 41, "name": "Eastern Australia", "continentId": 5, "adjacent": [39,40] }
  ]
}
```

This is ready to drop into the server project as `Data/territories.json` and deserialize at startup.

---

## House Rules

Toggleable server-side flags in `HouseRules` class. No lobby UI yet — set in code.

| Rule | Default | Description |
|------|---------|-------------|
| `LockedAttackFront` | true | Must attack from starting territory or territories captured this turn. Prevents scattergun attacks across the map. |
| `UseMissions` | true | Secret mission cards dealt at game start. Win by completing your mission instead of world domination. |
| `FixedCardValues` | true | Classic UK card trade values (Infantry=4, Cavalry=6, Artillery=8, One-of-each=10). When false, uses escalating global values (4/6/8/10/12/15/+5). |

---

## Deployment

- **Server:** .NET 8 on WHUK, serves handset bundle from wwwroot
- **Handset:** `npm run build` → wwwroot. Players access via URL on phones.
- **TV:** Unity build → Android APK → sideload to Fire Stick via ADB

---

## Risks & Decisions

| Decision | Options | Recommendation |
|----------|---------|----------------|
| Map rendering (Unity) | SVG import / sprite regions / procedural mesh | Sprite regions — simplest, looks good enough |
| Territory selection (handset) | Tap map image / dropdown list / search | List with filter — reliable on small screens |
| Combat speed | Roll-by-roll / batch with animation | Batch (Blitz) with animated summary on TV |
| Initial placement | Strict alternating / free placement | Alternating (one troop per turn, classic rules) |
| Card art | Territory images / simple icons | Icons (Infantry/Cavalry/Artillery) — less asset work |

---

---

## Graphics & Assets

**Map:**
- ✅ Sourced: `docs/risk-board-game-map.jpg` (2560×1773, 1.3MB)
- Black background — ideal for Fire TV dark theme
- 6 continents in distinct bold colours: yellow (N. America), red (S. America), light blue (Europe), green (Asia), dark gold (Africa), purple (Australia)
- Territory boundaries clearly drawn with grey/dark lines within each continent
- Cross-continent adjacency lines visible (Alaska↔Kamchatka, Brazil↔N.Africa, etc.)
- No text labels — clean silhouette style, overlay army counts and player colours at runtime
- **Ownership rendering (layered approach):**
  - v1: Static map + coloured circles with army counts at territory centre points (like plastic armies on the board)
  - v2: Add territory masks → player colour tint fill underneath the tokens
  - v3 (optional): Drop tokens if tint + number label is clear enough alone
- Territory centre coordinates defined once, reused across all versions for token/label placement.

**Cards:**
- Infantry / Cavalry / Artillery silhouette icons — simple SVGs, free or hand-drawn
- Wild cards: globe or star icon

**Dice:**
- 3D cube model with face textures, or flat 2D sprites with rotation animation

**UI elements:**
- Tailwind handles handset styling
- Unity UI toolkit for any TV overlays (HUD, phase indicator, combat log)

---

## Development Tooling

**Server:** VS2022 or Rider — standard .NET 8 project, same as Flutter game.

**Handset:** VS Code — React/TypeScript/Vite/Tailwind, same as Flutter game.

**TV (Unity):**
- **Unity Editor** — scene building, assets, running the game (Play button)
- **VS2022** — C# script editing and debugging (install "Game Development with Unity" workload)
- Both open side-by-side: edit in VS, run in Unity Editor
- Debug: VS → Debug → Attach to Unity Process → breakpoints work
- Unity auto-recompiles on save — no manual `dotnet build`
- Rider is the premium option (deeper Unity integration) but VS2022 is fine

**Version control:**
- `.gitignore` the `Library/`, `Temp/`, `Logs/`, `obj/` folders
- Commit `Assets/`, `Packages/`, `ProjectSettings/` only

---

## Design Clarifications & Gaps

### Initial Placement Phase — Detailed Flow

Territories are dealt randomly (1 army each). Players then take turns placing remaining armies one at a time.

**Starting armies by player count:**

| Players | Armies Each | Territories Each (~) | To Place |
|---------|-------------|---------------------|----------|
| 2 | 40 | 21 | 19 |
| 3 | 35 | 14 | 21 |
| 4 | 30 | 10–11 | 19–20 |
| 5 | 25 | 8–9 | 16–17 |
| 6 | 20 | 7 | 13 |

Server needs: `ReinforcementsRemaining` per player during this phase, cycling through players until all have placed. The `GamePhase.InitialPlacement` enum value handles this — it's a distinct phase from `Playing`.

### Post-Capture Troop Movement — Mandatory State

After a successful capture, the attacker **must** move troops before doing anything else. Add a sub-state within the Attack phase:

```csharp
public bool AwaitingTroopMove { get; set; }
public int MinTroopsToMove { get; set; } // = dice used in winning attack
public int MaxTroopsToMove { get; set; } // = source armies - 1
```

Until `MoveTroopsIn` is called, all other actions (Attack, DoneAttacking, Fortify) are rejected.

### Card Escalation — Full Rules

`CardTradeCount` is global (correct — not per-player). Escalation sequence:

| Trade # | Armies |
|---------|--------|
| 1 | 4 |
| 2 | 6 |
| 3 | 8 |
| 4 | 10 |
| 5 | 12 |
| 6 | 15 |
| 7+ | +5 each (20, 25, 30...) |

**Forced trades:**
- If a player starts their turn with 5+ cards → must trade before reinforcing
- If eliminating a player pushes you over 5 cards → must trade immediately (mid-attack)

Add a `TurnPhase.ForcedTrade` or handle as a validation gate in the Reinforce/Attack logic.

### Territory Bonus on Card Trade

If any traded card matches a territory the player owns, they get +2 bonus armies on that territory (placed immediately, not pooled). This is easy to miss.

### Multi-Game — Deferred

Start single-game like Flutter. `GameCode` stays in the model but use a singleton `GameState` for now. Refactor to `Dictionary<string, GameState>` later when the core game works. This avoids lobby complexity and SignalR group routing during initial build.

### Fortify — Connected Path Variant

Classic rules: fortify to one adjacent territory only. Common house rule / newer editions: fortify along any connected path of friendly territories. Pick one and document it. **Recommendation:** adjacent-only for v1 (simpler validation), connected-path as optional later.

### Two-Player Rules

Standard Risk doesn't work well with 2 players. Common fixes:
- Neutral third army (territories dealt to 3, neutral doesn't take turns but defends)
- Skip 2-player entirely (minimum 3)

**Recommendation:** minimum 3 players for v1. Avoids special-case logic.

---

## Refined Build Sequence

| Phase | Deliverable | Tech | Notes |
|-------|-------------|------|-------|
| 1 | Server: models, territory graph JSON, lobby hub | .NET 8 / SignalR | Familiar — same pattern as Flutter |
| 2 | Server: initial placement + reinforcement + cards | .NET 8 | Card escalation + forced trade logic |
| 3 | Server: combat + fortify + win condition | .NET 8 | AwaitingTroopMove state, elimination |
| 4 | Handset: lobby + all game phases | React/TS/Tailwind | Territory list UI, card trade interface |
| 5 | Unity spike: static map + territory colouring + SignalR | Unity 2D / C# | Learning phase — see UNITY-GETTING-STARTED.md |
| 6 | Unity: full map + army counts + combat display | Unity 2D | HUD, phase indicator, player info |
| 7 | Unity: animations — dice battles, troop movement | Unity 2D | DOTween or built-in animation |
| 8 | Polish & optional features | All | Missions, AI, fog of war |

Phases 1–4 are all known tech. Phase 5 is where Unity learning happens with a focused spike.

---

## Repo & Project Structure

This will be a separate `RiskDigital` repo once the Z440 workstation arrives. Same top-level layout:

```
RiskDigital/
├── server/         — .NET 8 + SignalR (VS2022)
├── handset/        — React + Vite + Tailwind (VS Code)
├── tv/             — Unity project (Unity Editor + VS2022)
├── docs/           — Design docs, rules, territory data
└── README.md
```

The design docs in this Flutter repo serve as the planning workspace until the new repo is created.

---

*Same proven pattern as the Flutter board game — server owns logic, handset controls, TV displays. The new piece is Unity rendering the map instead of Compose.*
