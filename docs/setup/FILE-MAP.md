# Project File Map

Complete map of both repos. All docs live here in RiskDigital (single source of truth).

---

## Server — `server/Risk.Server/`

.NET 8 + SignalR game server. All game logic lives here.

### Source

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point. ASP.NET minimal API, SignalR config, CORS, static files, ML model loading. |
| `Hubs/GameHub.cs` | SignalR hub — thin relay. Routes all calls via GameManager, uses SignalR groups for scoped broadcasts. |
| `Services/GameManager.cs` | Multi-game manager (singleton). ConcurrentDictionary of games, connection→game tracking, lifecycle. |
| `Services/GameService.cs` | Partial class root. Fields, constructor, lobby/setup, private helpers, `PendingCombat` class. Per-game instance. |
| `Services/GameService.Combat.cs` | Attack, Blitz, AttackWithDice, PlayerRoll, ResolveCombat, MoveAfterCapture, Unity dice delegation. |
| `Services/GameService.Turn.cs` | TradeCards, Reinforce, EndReinforce, EndTurn, Fortify. |
| `Services/AiService.cs` | AI player turn runner. Tiers 1–5 strategy, personality weights, ML-guided decisions. Uses AsyncLocal for per-game context. |
| `Services/MlModels.cs` | ML.NET model loading + predictions (blitz probability, behaviour models). |
| `Services/ActionLogger.cs` | Logs human player decisions to CSV (reinforce, attack, fortify). Path cascade for WHUK. |
| `Services/DiceAuditLogger.cs` | Logs all dice rolls for fairness analysis. |
| `Services/RingBufferLogger.cs` | In-memory ILoggerProvider (300 lines). Powers `/admin/app-log`. |
| `EndPointConfig/ManagementEndpoints.cs` | All admin/utility endpoints (reset, train, logs, testdice, games list, etc). |
| `Models/GameState.cs` | Core models: GameState, Player, Territory, Card, Mission, HouseRules, enums. |
| `Models/CombatResult.cs` | DTOs: CombatResult, BlitzResult, RollPrompt, SpawnDice, CombatRollRequest records. |
| `Models/TerritoryData.cs` | Positional records for territories.json deserialization. |
| `Training/BlitzSimulator.cs` | Generates 58K simulated battles for blitz model training. |
| `Training/ModelTrainer.cs` | Trains FastTree regression blitz model. |
| `Training/BehaviourTrainer.cs` | Trains reinforce/attack/fortify behaviour models from player logs. |

### Data

| Path | Purpose |
|------|---------|
| `Data/territories.json` | 42-territory adjacency graph (names, continents, connections). |
| `Data/models/blitz-model.zip` | Trained blitz probability model (58K rows). |
| `Data/models/*.zip` | Behaviour models (reinforce, attack). Bundled with deploy. |
| `Data/risk-models/*.zip` | Runtime-retrained models (written during gameplay). |
| `Data/logs/*.csv` | Player action logs (reinforce, attack, fortify). Training data source. |

### Static Assets — `wwwroot/`

| Path | Purpose |
|------|---------|
| `tv.html` | Web TV board — full-viewport map, dots, info panel, activity feed, sounds, glow, popups. |
| `guide.html` | Player guide — onboarding page served at `/guide`. |
| `index.html` | Handset entry point (Vite build output). |
| `picker.html` | Dev tool — click map to export territory x/y percentages. |
| `board-lined-blue.png` | Board map image (blue-lined, 16:9). |
| `sounds/` | 13 audio files (dice, capture, blitz, fail, alert, eliminated, turn, card, fortify, victory, place, army-rank-up, round_fanfare). |
| `avatars/` | 9 player avatar PNGs (female-1 to female-6, male-1 to male-3). |
| `assets/` | Vite build output (JS/CSS bundles — auto-generated, don't edit). |

### Admin & Utility Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/admin/games` | GET | List all active games (code, phase, players). |
| `/admin/reset` | GET | Reset ALL games. Broadcasts null to all clients. |
| `/admin/reset/{gameCode}` | GET | Reset specific game. Only that game's clients affected. |
| `/admin/gameover` | GET | Force game over (`?gameCode=XXXX` or single game). |
| `/admin/missions` | GET | Show all players' missions (`?gameCode=XXXX` or single game). |
| `/admin/testdice` | GET | Trigger test dice roll (`?gameCode=XXXX&a=3&d=2`). |
| `/admin/train` | GET | Train all ML models (blitz + behaviour). |
| `/admin/ml-status` | GET | Report loaded models + sample predictions. |
| `/admin/app-log` | GET | Last 300 log lines (ring buffer, plain text). |
| `/admin/logs-status` | GET | Active log directory, writability, file list. |
| `/admin/logs-download` | GET | Download all log CSVs as zip. |
| `/admin/logs-upload` | POST | Upload zip of CSVs to restore training data. |
| `/board` | GET | Serve web TV board (tv.html). |
| `/guide` | GET | Serve player guide. |

---

## Handset — `handset/`

React 18 + TypeScript + Vite + Tailwind. Player controller app.

| File | Purpose |
|------|---------|
| `src/main.tsx` | React entry point, renders App. |
| `src/App.tsx` | Root component. Phase routing, mission/status/card badges, roll prompt relay. |
| `src/index.css` | Tailwind base + body select-none. |
| `src/hooks/useConnection.ts` | SignalR hook — connect, reconnect, all event handlers, state. |
| `src/types/game.ts` | TypeScript interfaces: Player, Territory, GameState, Card, Mission, CombatResult, BlitzResult, RollPrompt. |
| `src/components/ConnectScreen.tsx` | Name input, colour/avatar picker, create/join game. |
| `src/components/LobbyScreen.tsx` | Game code, player list with avatars, add/remove AI (tier picker), start game. |
| `src/components/PlacementScreen.tsx` | Initial army placement — continent accordion, +1/All buttons. |
| `src/components/ReinforceScreen.tsx` | Place reinforcements — accordion, card trade panel, +1/All buttons. |
| `src/components/AttackScreen.tsx` | Source/target selection, dice, attack/blitz, move-in stepper, forced trade, defend overlay. |
| `src/components/FortifyScreen.tsx` | Source/target selection, army stepper, skip/fortify. |
| `src/components/GameOverScreen.tsx` | Winner/loser display, new game button (resets current game only). |
| `src/components/ContinentAccordion.tsx` | Shared collapsible continent-grouped territory list. |
| `src/components/CardTradePanel.tsx` | Shared card selection + trade UI. |
| `src/components/CardBadge.tsx` | 🃏 card count badge — tap to open trade panel. |
| `src/components/MissionBadge.tsx` | 🎯 top-left — tap to view secret mission. |
| `src/components/MissionWelcome.tsx` | One-time modal showing mission on game start. |
| `src/components/StatusBadge.tsx` | 📊 top-right — mission progress + continent breakdown. |
| `src/utils/groupByContinent.ts` | Groups territories by continent + colour map. |
| `src/utils/vibrate.ts` | Haptic helpers: tap() 40ms, heavyTap() 100ms. |
| `src/utils/shortName.ts` | Abbreviated territory names for compact displays. |
| `vite.config.ts` | Vite config — host mode, port 3000. |
| `package.json` | Dependencies + scripts (dev, build, build:deploy). |

---

## Unity TV Board — `D:\Unity Projects\RiskDigitalBoard\`

Separate repo: https://github.com/john102m/UnityDigitalRisk.git

Unity 6 LTS, 3D URP. Premium TV board with physics dice.

### Scripts — `Assets/Scripts/`

| File | Purpose |
|------|---------|
| `SignalRClient.cs` | SignalR connection, event deserialization, JoinGame, RegisterAsTV, SendDiceResult, poll for games/state. |
| `GameStateManager.cs` | Reactive game state holder, fires OnStateChanged. |
| `GameJoinScreen.cs` | Game selection UI — code input, clickable game list, auto-hides on join, reappears on reset. |
| `BoardRenderer.cs` | 42 territory tokens (3D cylinders), colour/army updates, attack glow + pulse, territory name labels. |
| `UIOverlay.cs` | Info bar, activity feed, welcome screen, turn/blitz popups, game ceremony. |
| `PopupManager.cs` | World-space popup system (turn announcements, blitz results). |
| `MissionReveal.cs` | Mission reveal display on game over. |
| `TerritoryNames.cs` | Static utility for abbreviated territory names. |
| `CombatTheatre.cs` | State machine orchestrating dice panel lifecycle (6 states, explicit transitions). |
| `DiceRoller.cs` | Spawns dice, physics simulation, SpawnSet/WaitAndReadAll, PlaceDiceAtValues (blitz display). |
| `DiceFaceReader.cs` | Reads settled die face from local-axis dot products. |
| `DiceSound.cs` | Collision-triggered dice rattle (one per throw). |
| `BoardCamera.cs` | Smooth zoom in/out for combat, panel-aware bias, post-game drift. |
| `MusicManager.cs` | Phase-based background music with crossfading. |
| `CameraFlypath.cs` | Catmull-Rom spline camera sweep with randomisation + result position. |
| `UnityMainThread.cs` | Dispatcher for marshalling SignalR callbacks to Unity main thread. |

### Docs (orphaned — duplicates of RiskDigital/docs/unity/)

| File | Status |
|------|--------|
| `docs/SESSION-NOTES-2026-06-27.md` | ⚠️ Duplicate — canonical copy in RiskDigital |
| `docs/HOW-DICE-ARENA-WORKS.md` | ⚠️ Duplicate — canonical copy in RiskDigital |

These can be deleted from the Unity repo. All docs live in `D:\Development\RiskDigital\docs\unity\`.

---

## Docs — `docs/`

All documentation for both repos lives here. Organised by area.

### Root

| File | Purpose |
|------|---------|
| `INDEX.md` | Doc index and navigation. |
| `GLOSSARY.md` | Term definitions. |
| `PLAYER-GUIDE.md` | How-to-play guide for playtesters. |
| `PROPOSAL-attack-dice-and-card-badge.md` | Early proposal for attack dice display + card badge. |

### `docs/proposals/`

| File | Purpose |
|------|---------|
| `ROADMAP.md` | Forward plan — tiered priority bands (A→E). |
| `MULTI-GAME-SERVER.md` | Multi-game server design (GameManager, groups, concurrent games). |
| `PROPOSAL-MULTI-HOUSEHOLD-TV.md` | Multi-household TV — each household rolls own dice, static placement for remote. |
| `PROPOSAL-PLACEMENT-MODES.md` | Placement mode selection (Auto/FreeForAll/Manual). |
| `PROPOSAL-AI-REFACTOR.md` | AiService structural refactor (DRY, ~28% line reduction). |
| `PROPOSAL-ERROR-HANDLING.md` | Error handling improvements (6 items). |
| `PROPOSAL-PROGRESSIVE-DISCLOSURE.md` | Progressive disclosure UX for new players. |
| `PROPOSAL-MISSION-FALLBACK-AND-CARD-UI.md` | Mission fallback notification + card UI. |
| `PROPOSAL-IDLE-DRIFT.md` | Idle drift camera for Unity board. |
| `GAME-START-END-POLISH.md` | Game ceremony design (welcome, win, start). |
| `GAME-IDEAS.md` | Future game ideas (next project). |
| `MULTI-GAME-SERVER.md` | Multi-game concurrent architecture design. |
| `REPLACE-ALERTS.md` | Replace native alerts with toast component. |
| `PROPOSAL-DEPLOY-ALL.md` | Deploy All button design. |
| `REFACTORING.md` | Handset code duplication analysis. |
| `IDEAS.md` | Future possibilities backlog (unordered brain dump). |
| `NEXT-STEPS.md` | ⚠️ Stale — superseded by ROADMAP.md. |

### `docs/sessions/`

| File | Purpose |
|------|---------|
| `PROGRESS.md` | Session-by-session development log (June 20 – July 3). |
| `PLAYTEST-NOTES.md` | Live bugs/ideas captured during play. |
| `SESSION-2026-07-06.md` | Multi-game server implementation session notes. |

### `docs/design/`

| File | Purpose |
|------|---------|
| `RISK-DESIGN.md` | Full game design doc (rules, combat, house rules, dice, cards). |
| `CARD-SYSTEM.md` | Card earn/trade design decisions. |
| `MISSIONS-PLAN.md` | Mission types, dealing, checking, edge cases. |
| `LOBBY-FLOW.md` | Game creation, lobby, late joiner design. |
| `HANDSET-PLAN.md` | Original handset UI plan. |
| `HANDSET-UI-IMPROVEMENTS.md` | UI analysis + priority ranking. |
| `TURN-VISIBILITY.md` | TV activity feed + animation design. |
| `BOARD-POLISH.md` | Sound/visual polish planning (web + Unity). |
| `GAME-CREATION.md` | Create game guard discussion. |

### `docs/ai/`

| File | Purpose |
|------|---------|
| `AI-PLAYER.md` | AI architecture overview + tier summary. |
| `AI-SELECTION.md` | Final tier selection design (lobby UI). |
| `AI-PERSONALITIES-IMPL.md` | Personality implementation notes. |
| `AI-TIER1-PLAN.md` | Tier 1 (random) plan. |
| `AI-TIER2-PLAN.md` | Tier 2 (aggressive) design. |
| `AI-TIER2-IMPL.md` | Tier 2 implementation notes. |
| `AI-TIER3-PLAN.md` | Tier 3 (strategic) design. |
| `AI-TIER3-ML-PLAN.md` | Tier 3 ML.NET integration plan. |
| `AI-TIER4-PLAN.md` | Tier 4 (opportunist) design + advanced capabilities. |
| `AI-TIER5-PLAN.md` | Tier 5 (learning) — full ML tutorial + plan. |
| `PROPOSAL-AI-TIER4.md` | Tier 4 proposal doc. |
| `ML-NET-PROPOSAL.md` | ML.NET integration proposal. |
| `ML-FOR-DUMMIES.md` | ML concepts explainer (written for learning). |

### `docs/unity/`

| File | Purpose |
|------|---------|
| `UNITY-PROGRESS.md` | Phase-by-phase Unity progress tracker. |
| `UNITY-TV-VISION.md` | Overall vision for Unity TV board. |
| `UNITY-GETTING-STARTED.md` | Unity learning path + setup. |
| `UNITY-PHASE2-COMBAT.md` | Phase 2 combat design (dice arena). |
| `UNITY-ASYNC-PATTERNS.md` | Unity 6 async Awaitable patterns reference. |
| `UNITY-SHOOTER-TUTORIAL.md` | Unity tutorial notes (learning exercise). |
| `HOW-DICE-ARENA-WORKS.md` | Complete breakdown of dice arena architecture. |
| `COMBAT-FLOW.md` | **Mermaid diagrams** — state machine, sequence diagrams, flowchart. |
| `PROPOSAL-COMBAT-STATE-MACHINE.md` | Combat state machine refactor (before/after, states, transitions). |
| `PROPOSAL-PLAYER-ROLLED-DICE.md` | Player-rolled dice design + 9 bugs encountered/fixed. |
| `PROPOSAL-TV-DRIVEN-DICE.md` | TV-driven dice design (server delegates to Unity). |
| `PROPOSAL-DICE-CAMERA-FLYPATH.md` | Camera flypath design (Catmull-Rom spline). |
| `SESSION-NOTES-2026-06-27.md` | Session notes — Phase 1+2 build, gotchas. |
| `SESSION-NOTES-2026-06-28.md` | Session notes — arena upgrade, FBX, TV-driven dice. |

### `docs/setup/`

| File | Purpose |
|------|---------|
| `DEV-SETUP.md` | Fresh machine setup guide. |
| `FILE-MAP.md` | ⚠️ Stale — superseded by this file. |
| `WORKSTATION-GUIDE.md` | Z440 reference guide. |
| `Z440-SETUP.md` | Hardware assembly notes. |

---

## Config — `.kiro/`

| File | Purpose |
|------|---------|
| `steering/architecture.md` | System overview, component responsibilities, communication patterns. |
| `steering/conventions.md` | Code style conventions (server, handset, Unity, web TV). |
| `steering/tech-stack.md` | Technology choices + versions. |
| `steering/game-rules.md` | Quick reference game rules. |
| `steering/unity-project.md` | Unity project relationship + working notes. |
| `agents/risk.json` | Kiro agent config. |

---

## Root Files

| File | Purpose |
|------|---------|
| `README.md` | Project overview, architecture, quick start, status. |
| `AGENTS.md` | AI agent guidelines (constraints, patterns). |
| `.gitignore` | Git ignore rules. |
| `server/Risk.Server.sln` | VS2026 solution file. |

---

*Updated: 2026-07-06*
