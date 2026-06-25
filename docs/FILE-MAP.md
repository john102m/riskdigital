# Project File Map

## Server — `server/Risk.Server/`

.NET 8 + SignalR game server. All game logic lives here.

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point. ASP.NET minimal API, SignalR config, CORS, static files, ML model loading at startup. |
| `EndPointConfig/ManagementEndpoints.cs` | All admin/utility endpoints (see Admin Endpoints table below) |
| `Hubs/GameHub.cs` | SignalR hub — thin relay. All client↔server methods (CreateGame, JoinGame, Attack, Blitz, etc). Delegates to GameService. |
| `Services/GameService.cs` | Singleton. All game logic: combat resolution, reinforcement calc, card trading, mission checking, territory dealing, turn management. |
| `Services/AiService.cs` | AI player turn runner. Triggered when current player is AI. Tier 1 (random) with delays for natural pacing. |
| `Models/GameState.cs` | Core models: GameState, Player, Territory, Card, Mission, HouseRules, enums (GamePhase, TurnPhase, MissionType, CardType) |
| `Models/CombatResult.cs` | DTOs: CombatResult, BlitzResult records broadcast to clients |
| `Models/TerritoryData.cs` | Positional records for JSON deserialization of territories.json |
| `Data/territories.json` | 42-territory adjacency graph with names, continents, connections |
| `wwwroot/tv.html` | Web TV board — full-viewport map, territory dots, info panel, activity feed, sounds, attack glow, phase popups |
| `wwwroot/index.html` | Handset entry point (production — Vite build output) |
| `wwwroot/picker.html` | Dev tool — click map to export territory x/y percentages |
| `wwwroot/risk-board-game-map-cropped.jpg` | Board map image (cropped, no borders) |
| `wwwroot/sounds/` | Audio files: dice, capture, blitz, fail, alert, eliminated, turn, card, fortify, victory, place |
| `wwwroot/avatars/` | 9 player avatar PNGs (avatar_0 through avatar_8) |
| `wwwroot/assets/` | Vite build output (JS/CSS bundles — auto-generated, don't edit) |

### Admin & Utility Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/admin/reset` | GET | Reset game state. `?debug=true` for reduced armies. |
| `/admin/gameover` | GET | Force game over (testing). |
| `/admin/missions` | GET | Show all players' missions (debug). |
| `/admin/train` | GET | Train all ML models (blitz from simulation + behaviour from player logs). |
| `/admin/ml-status` | GET | Report which ML models are loaded + sample predictions. |
| `/admin/app-log` | GET | Last 300 log lines (plain text). Ring buffer — no file I/O. |
| `/admin/logs-status` | GET | Show active log directory, writability, file list. |
| `/admin/logs-download` | GET | Download all log CSVs as zip. |
| `/admin/logs-upload` | POST | Upload zip of CSVs to restore training data. |
| `/board` | GET | Serve TV web board (tv.html). |
| `/guide` | GET | Serve player guide (guide.html). |

---

## Handset — `handset/`

React + TypeScript + Vite + Tailwind. Player controller app.

| File | Purpose |
|------|---------|
| `src/main.tsx` | React entry point, renders App |
| `src/App.tsx` | Root component. Phase routing, vibration on turn, mission/status badges |
| `src/hooks/useConnection.ts` | SignalR connection hook — connect, reconnect, event handlers, state |
| `src/types/game.ts` | TypeScript interfaces: Player, Territory, GameState, Card, Mission, CombatResult, BlitzResult |
| `src/components/ConnectScreen.tsx` | Name input, colour/avatar picker, create/join game |
| `src/components/LobbyScreen.tsx` | Game code, player list with avatars, add/remove AI, start game |
| `src/components/PlacementScreen.tsx` | Initial army placement — continent accordion, tap/All buttons |
| `src/components/ReinforceScreen.tsx` | Place reinforcements — accordion, card trade panel, tap/All buttons |
| `src/components/AttackScreen.tsx` | Source/target selection, dice, attack/blitz, move-in stepper, forced trade |
| `src/components/FortifyScreen.tsx` | Source/target selection, army stepper, skip/fortify |
| `src/components/GameOverScreen.tsx` | Winner/loser display, new game button |
| `src/components/ContinentAccordion.tsx` | Shared collapsible continent-grouped territory list |
| `src/components/CardTradePanel.tsx` | Shared card selection + trade UI (used in Reinforce + Attack) |
| `src/components/MissionBadge.tsx` | 🎯 top-left badge — tap to view secret mission |
| `src/components/MissionWelcome.tsx` | One-time modal showing mission on game start |
| `src/components/StatusBadge.tsx` | 📊 top-right badge — mission progress + continent breakdown |
| `src/utils/groupByContinent.ts` | Groups territories by continent + continent colour map |
| `src/utils/vibrate.ts` | Haptic helpers: tap() 40ms, heavyTap() 100ms |
| `vite.config.ts` | Vite config — host mode, port 3000 |
| `package.json` | Dependencies, scripts (dev, build, build:deploy) |
| `index.html` | HTML shell |

---

## TV (Unity) — `tv/`

Unity 2D project — not yet started. Premium board experience for Fire TV.

---

## Docs — `docs/`

| File | Purpose |
|------|---------|
| `PROGRESS.md` | Session-by-session development log |
| `PLAYER-GUIDE.md` | How-to-play guide for playtesters |
| `PLAYTEST-NOTES.md` | Live bugs/ideas captured during play |
| `LOBBY-FLOW.md` | Game creation, lobby, late joiner design |
| `RISK-DESIGN.md` | Full game design doc (rules, combat, house rules) |
| `CARD-SYSTEM.md` | Card earn/trade design decisions |
| `MISSIONS-PLAN.md` | Mission types, dealing, checking, edge cases |
| `AI-PLAYER.md` | AI architecture + tier overview |
| `AI-TIER1-PLAN.md` | Tier 1 (random) implementation plan |
| `AI-TIER2-PLAN.md` | Tier 2 (aggressive) design |
| `AI-TIER3-PLAN.md` | Tier 3 (strategic) design |
| `AI-TIER4-PLAN.md` | Tier 4 (personality-based) design |
| `TURN-VISIBILITY.md` | TV activity feed + animation design |
| `HANDSET-UI-IMPROVEMENTS.md` | UI analysis + priority ranking |
| `BOARD-POLISH.md` | Sound/visual polish planning (web + Unity) |
| `IDEAS.md` | Future possibilities backlog |
| `NEXT-STEPS.md` | Priority task list |
| `REFACTORING.md` | Handset code duplication analysis |
| `DEV-SETUP.md` | Fresh machine setup guide |
| `GAME-CREATION.md` | Create game guard discussion |
| `Z440-SETUP.md` | Workstation hardware setup notes |
| `WORKSTATION-GUIDE.md` | Z440 reference guide |
| `UNITY-GETTING-STARTED.md` | Unity learning path for the TV app |

---

*Created: 2026-06-23*
