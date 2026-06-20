# Risk Digital — Progress Log

## 2026-06-20 Session — Project Setup, Scaffolding & Lobby Flow

### Completed

- **Project scaffolding** — server (.NET 8 + SignalR) and handset (React + Vite + Tailwind 4 + TypeScript) created
- **Server structure** — Program.cs, GameHub.cs (thin), GameService.cs (singleton), Models/GameState.cs, Data/territories.json (42 territories, full adjacency graph)
- **Handset structure** — package.json, vite.config, App.tsx, useConnection hook with auto-reconnect
- **SignalR connection verified** — handset connects to server successfully
- **Solution file** — Risk.Server.sln for VS2022
- **Documentation** — README updated (hardware, Flutter predecessor), HANDSET-PLAN.md created
- **Git repo** — initialised, connected to GitHub
- **Lobby flow (server)** — CreateGame (4-digit code, host assignment), JoinGame (validates code/capacity/name, assigns colour), StartGame (host-only, min 2 for dev), Rejoin, GetState, /admin/reset
- **Lobby flow (handset)** — ConnectScreen (name input, create/join with code), LobbyScreen (game code, player list with colours, host badge, start button)
- **Dark theme** — full dark UI matching Flutter handset style (bg-gray-900, red/amber/green accents, emoji branding)
- **Debug TV page** — wwwroot/tv.html, dark themed, SignalR connection, shows game code + phase + player list, auto-reconnects
- **TV map display** — cropped board image with positioned territory circles (colour-coded by owner, army count, name labels), player bar with stats
- **Territory coordinate picker** — wwwroot/picker.html tool for clicking map to export x/y percentages
- **App routing** — conditional render by phase (Connecting → Connect → Lobby → Placement → placeholder)
- **Initial Placement (server)** — DealTerritories (random round-robin), SetStartingArmies (40/35/30/25/20 minus dealt), PlaceArmy (validates turn/ownership/remaining), auto-advance turn, transition to Playing when done
- **Initial Placement (handset)** — PlacementScreen with 2-col grid, player colour badge, alphabetical sort, colour-tinted buttons, h-dvh layout
- **Reinforce phase (server)** — CalculateReinforcements (territories/3 min 3 + continent bonuses), Reinforce hub method, EndReinforce (must place all → Attack)
- **Reinforce phase (handset)** — ReinforceScreen, same grid as placement, army counter, Done button
- **Attack phase (server)** — Attack (validate source/target/adjacency/dice, roll, resolve casualties, detect capture), MoveAfterCapture, EndAttack, CombatResult DTO broadcast
- **Attack phase (handset)** — AttackScreen with source/target pickers, dice selector, combat result display, capture move-in stepper, Done Attacking button
- **Fortify phase (server)** — Fortify (source/target/adjacent/owned, move armies), EndTurn (advance player, recalculate reinforcements)
- **Fortify phase (handset)** — FortifyScreen with source/target pickers, army stepper, Skip/Fortify buttons
- **House rules** — LockedAttackFront (toggleable, default true): must attack from starting territory or captured territories only, tracked via AttackFrontIds list
- **Full turn cycle** — Reinforce → Attack → Fortify → next player, working end-to-end
- **JSON enum serialization** — JsonStringEnumConverter so phases serialize as strings
- **CORS opened for dev** — SetIsOriginAllowed(_ => true) for LAN phone access
- **Vite host mode** — server.host: true + VITE_SERVER_URL env var for phone access
- **Reconnect/rejoin** — visibilitychange + onreconnected + wake lock + auto-rejoin on fresh connect
- **Admin reset broadcast** — /admin/reset now pushes null state to all clients via IHubContext
- **Models separated** — TerritoryData.cs records in Models/ (positional records for JSON deserialization)

### Files Changed

- `server/Risk.Server/Program.cs`
- `server/Risk.Server/Risk.Server.csproj`
- `server/Risk.Server/Properties/launchSettings.json`
- `server/Risk.Server/Hubs/GameHub.cs`
- `server/Risk.Server/Services/GameService.cs`
- `server/Risk.Server/Models/GameState.cs`
- `server/Risk.Server/Data/territories.json`
- `server/Risk.Server/wwwroot/tv.html`
- `server/Risk.Server.sln`
- `handset/package.json`
- `handset/vite.config.ts`
- `handset/tsconfig.json`
- `handset/index.html`
- `handset/src/main.tsx`
- `handset/src/App.tsx`
- `handset/src/index.css`
- `handset/src/vite-env.d.ts`
- `handset/src/hooks/useConnection.ts`
- `handset/src/types/game.ts`
- `handset/src/components/ConnectScreen.tsx`
- `handset/src/components/LobbyScreen.tsx`

### What's Next

- Card system (earn on capture, trade sets for armies)
- Blitz attack option (auto-repeat until win/threshold)
- Elimination detection (take cards, check forced trade)
- Game Over screen
- UI polish pass

### Bug Fixes (this session)

- Fixed attack front: source territory now always stays in valid front list after capture
- Fixed adjacency data: Central America↔Venezuela was missing, full audit corrected all 42 territories to match standard Risk rules
- Fixed move-in minimum: must move at least as many armies as dice used (3 dice = min 3 moved in)
- Fixed territory name: "Southeast Asia" renamed to "Siam"
- Fixed move-in stepper showing 0: now reads dice count from combat result directly (timing issue with state)
- Fixed source picker showing territories with no adjacent enemies (e.g. Argentina surrounded by friendlies)

### Next UI Improvement

- Group territories by continent (alphabetical continents, alphabetical within each) in 2-column layout with continent header blocks — applies to Placement, Reinforce, and Attack source picker

### Design Notes

- **Min players reduced to 2** for dev/testing. Standard Risk is 3–6; will enforce 3+ in production or fill with AI.
- **AI players (grand plan):** Unlike Flutter where AI just needed personality-flavoured random moves on a linear track, Risk AI needs genuine strategic intelligence — territory clustering, continent control, threat assessment, alliance-breaking, bluff attacks. This is a real AI challenge. Server-driven (same as Flutter) but much deeper decision trees. Likely a tiered approach: dumb AI first for testing, then progressively smarter.

---

*Updated: 2026-06-20*

### Ideas / Polish Backlog

- **BUG**: Phone browser screen-off disconnect — rejoin not recovering reliably
- TV map: territory circles grow in size as army count increases
- TV map: overlay SVG lines for cross-ocean adjacency (Alaska-Kamchatka, Brazil-N.Africa, etc.) — current map lines hard to see
- Handset: vibration on your turn
- Handset: animation lock after placing (prevent double-tap)
- Handset: undo/take-back for reinforcement placement (limit 4 per game per player)
- AI players: tiered intelligence (dumb → strategic)
- John has larger font on phone — keep handset UI compact

### Handset UI Polish Options

**Lightweight (no new deps)**
- CSS gradients, shadows, glows on buttons/cards
- CSS animations (pulse on your turn, slide-in transitions)
- SVG inline — mini continent icons, dice faces, shield/sword icons
- Canvas — small animated elements (dice roll, troop marching)
- Emoji as quick icons (🎲⚔️🛡️🏰)

**Small libraries**
- Framer Motion — smooth transitions, gesture support (swipe between phases)
- Lottie — after-effects animations (dice roll, explosion, victory confetti)
- react-spring — physics-based animations (bouncy numbers, army count ticking)

**Bigger options**
- Mini SVG map on handset — simplified continent outlines, tap continent to filter list
- Territory cards as visual card UI with silhouettes
- Themed backgrounds — parchment/war-table texture
- Haptics API — different vibration patterns for turn/attack/capture

**Initial Placement specifically**
- Mini continent map on handset — tap a region to filter territories in that area
- Continent grouping with coloured section headers
- Show adjacent enemy army count per territory (threat indicator)
- Highlight on TV when territory selected on phone (pulse/glow)
- Continent completion indicator (e.g. "3/4 South America")
- Territory buttons with small continent colour stripe/icon
