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

---

## 2026-06-21 Session — Card System & Handset UI Polish

### Completed

- **Card system (server)** — 44-card deck (42 territory + 2 wild), shuffled at game start
- **Card earning** — flag set on first capture per turn, card dealt from deck at EndAttack
- **TradeCards hub method** — validates sets (all same / all different / wild combos), escalating bonus (4/6/8/10/12/15/+5), territory bonus (+2 auto-placed on matching owned territories), cards returned to deck and reshuffled
- **Forced trade gate** — Reinforce blocks placement if 5+ cards; post-elimination transfers cards to attacker, fires ForcedTradeRequired if >5
- **Elimination detection** — MoveAfterCapture detects eliminated players, transfers cards, broadcasts PlayerEliminated
- **Card privacy** — Cards and Deck JsonIgnored from broadcast; CardCount (computed) public; CardsUpdated sent to caller only (Rejoin, GetState, EndAttack, TradeCards)
- **Card system (handset)** — Card/CardType types, CardsUpdated + ForcedTradeRequired events in useConnection hook
- **ReinforceScreen trade UI** — card count badge (🃏 N), expandable card panel, tap-to-select 3, trade button, placement blocked until traded down
- **AttackScreen forced trade modal** — full-screen overlay after elimination, same card selection UI, loops until <5
- **Debug TV alert** — CardTraded event shows alert with player name and army bonus
- **Continent grouping** — all territory lists grouped by continent with coloured headers matching map (yellow NA, red SA, blue Europe, dark gold Africa, green Asia, purple Australia)
- **Collapsible accordion** — Reinforce, Attack, Fortify all use mutually-exclusive accordion (one group open at a time), first group open by default
- **Bigger touch targets** — all territory buttons bumped to px-3 py-2 text-sm (pill buttons) or px-2 py-2 text-sm (grid buttons)
- **Fortify multi-step flow** — split into 3 screens (Source → Target → Army stepper + confirm) to avoid overcrowding
- **Auto-collapse on selection** — Attack source picker collapses accordion when source chosen, freeing space for target
- **CARD-SYSTEM.md** — design doc with decisions locked in (counts public, hands private, cards reshuffled back in, trade all on elimination)

### Files Changed

- `server/Risk.Server/Models/GameState.cs` — Deck, CardTradeCount, EarnedCardThisTurn, Card.TerritoryId nullable, JsonIgnore
- `server/Risk.Server/Services/GameService.cs` — GenerateDeck, ShuffleDeck, TradeCards, IsValidSet, earn/forced trade logic, elimination
- `server/Risk.Server/Hubs/GameHub.cs` — TradeCards, CardsUpdated broadcasts, ForcedTradeRequired, PlayerEliminated
- `server/Risk.Server/wwwroot/tv.html` — CardTraded alert handler
- `handset/src/types/game.ts` — Card, CardType, cardCount on Player
- `handset/src/hooks/useConnection.ts` — CardsUpdated, ForcedTradeRequired events, cards/forcedTrade state
- `handset/src/App.tsx` — passes cards/forcedTrade to screens
- `handset/src/utils/groupByContinent.ts` — shared utility + continent colours (corrected to match map)
- `handset/src/components/PlacementScreen.tsx` — continent grouping, bigger buttons
- `handset/src/components/ReinforceScreen.tsx` — continent accordion, card trade UI, bigger buttons
- `handset/src/components/AttackScreen.tsx` — continent accordion, forced trade modal, bigger buttons
- `handset/src/components/FortifyScreen.tsx` — multi-step flow, continent accordion, bigger buttons
- `docs/CARD-SYSTEM.md` — new design doc

### What's Next

- Blitz attack option
- Missions (plan doc ready)
- TV: card trade event display (replace alert with proper overlay)

---

## Additional Completed (same session)

- **Game Over screen (handset)** — 🏆 for winner, 💀 for losers, player standings with territory counts and elimination markers, "New Game" button (host only)
- **Game Over overlay (TV)** — full-screen dark overlay with trophy + winner name in colour
- **Debug endpoint** — `GET /admin/gameover` forces game over for testing
- **PlacementScreen accordion** — now uses ContinentAccordion like all other screens
- **Card system verified working** — earn, trade, escalation, forced trade all tested end-to-end on Lenovo

---

*Updated: 2026-06-21*

---

## 2026-06-21 Late Session — Missions & Blitz

### Completed

- **Mission system (server)** — 14 mission cards (6 continent conquest, 2 territory count, 6 elimination), dealt at game start
- **Mission types** — ContinentConquest (including "third continent of choice" auto-check), TerritoryCount (18×2+ or 24), Elimination (with fallback to world domination if target killed by another player)
- **Mission deck & dealing** — filters to active colours, shuffles, avoids self-elimination deals
- **CheckMissionComplete** — called from MoveAfterCapture, checks all mission types
- **Elimination fallback** — when a player's elimination target is killed by someone else, their mission reverts to world domination
- **Mission privacy** — Mission is JsonIgnored, sent privately via MissionUpdated on StartGame/Rejoin/GetState
- **GetMission hub method** — caller-only response
- **MissionComplete broadcast** — on win, sends player index + mission description to all
- **UseMissions house rule** — defaults to `true` (missions always on for now)
- **GET /admin/missions** — debug endpoint showing all players' missions
- **Mission welcome modal (handset)** — shows on game enter with mission description + hint about 🎯 icon
- **🎯 Mission badge (handset)** — top-left, tap to view your secret mission anytime
- **📊 Status badge (handset)** — top-right, tap to see mission progress + continent breakdown
- **Blitz attack (server)** — Blitz hub method, loops max-dice attacks until capture or source ≤ 1
- **BlitzResult DTO** — rounds, total losses both sides, captured flag
- **⚡ Blitz button (handset)** — purple button alongside regular Attack, no dice selection needed
- **Blitz result display** — shows total losses summary instead of individual dice
- **ConnectScreen subtitle** — changed from "World Domination" to "Digital Board Game"
- **GameOverScreen text** — now says "Mission complete" instead of world domination
- **PLAYTEST-NOTES.md** — created for live note-taking during play

### Files Changed

- `server/Risk.Server/Models/GameState.cs` — Mission, MissionType, HouseRules.UseMissions, Player.Mission
- `server/Risk.Server/Models/CombatResult.cs` — BlitzResult record
- `server/Risk.Server/Services/GameService.cs` — DealMissions, CheckMissionComplete, CheckContinentMission, Blitz
- `server/Risk.Server/Hubs/GameHub.cs` — Blitz, GetMission, MissionUpdated sends, MissionComplete broadcast
- `server/Risk.Server/Program.cs` — /admin/missions endpoint
- `handset/src/types/game.ts` — Mission, BlitzResult interfaces
- `handset/src/hooks/useConnection.ts` — MissionUpdated handler, mission state
- `handset/src/App.tsx` — MissionBadge, StatusBadge, MissionWelcome wiring
- `handset/src/components/MissionBadge.tsx` — new (top-left)
- `handset/src/components/MissionWelcome.tsx` — new (one-time modal)
- `handset/src/components/StatusBadge.tsx` — new (top-right)
- `handset/src/components/AttackScreen.tsx` — Blitz button, BlitzResult handler
- `handset/src/components/ConnectScreen.tsx` — subtitle text
- `handset/src/components/GameOverScreen.tsx` — win text
- `docs/PLAYTEST-NOTES.md` — new

### Next Steps (morning)

1. **Build & test missions** — create game with 2-3 players, check `/admin/missions`, verify MissionWelcome shows, check mission badge works, test win via mission
2. **Build & test blitz** — pick a territory with lots of armies, blitz a weak neighbour, verify capture + move-in flow
3. **Implement playtest UI fixes:**
   - Auto-collapse accordion when card panel opens
   - Hint popup when tradeable card set available at reinforce start
   - Bump instruction label contrast ("ATTACK FROM" etc)
   - `select-none` on all buttons/text (add to body or index.css)
   - Don't collapse attacker accordion after source selection
   - Dice rolls display on debug TV
   - "Max" button on move-in stepper
4. **Merge branches** — missions + blitz → main

---

*Updated: 2026-06-21 02:38*

---

## 2026-06-21 Afternoon — Playtest, Bug Fixes, TV Web Board

### Completed

- **Playtest UI tweaks (all 8 items):**
  - Auto-collapse accordion when card panel opens
  - Tradeable set hint banner (4s, tappable)
  - Instruction label contrast bumped
  - `select-none` on body (no long-press text selection)
  - Source accordion stays open after attacker selection
  - Dice results overlay on TV (bottom-right, 5s fade)
  - Max button on move-in stepper (blue)
  - Attack/Blitz buttons split to own row (phone overflow fix)
- **Blitz move-in bug fixes:**
  - Min move-in now uses last round's dice count (tracked in loop)
  - Min can never exceed max (Math.Min guard)
  - Empty attackerDice array handled correctly for blitz
- **Fortify screen** — reverted to single screen with accordions + Max button
- **Fixed card values house rule** — `HouseRules.FixedCardValues` (default: true). Infantry=4, Cavalry=6, Artillery=8, One-of-each=10
- **Attack screen enhancements:**
  - Auto-open strongest attacker's continent after move-in
  - Idle hint (10s) with context-aware prompts
  - Blitz summary (rounds/losses) on move-in screen
  - "Territory Name Captured!" instead of generic text
- **TV web board layout overhaul:**
  - Full-viewport map (no header/player bar stealing space)
  - Info box overlay bottom-left (game code, phase, players stacked)
  - JS `syncOverlay()` — positions dot overlay to match image rendered area
  - Dots hidden until positioned (opacity fade-in, no snap)
  - `vw`-based circle scaling with max-size cap (34px)
  - Wake lock API for screensaver prevention
  - Game-over overlay cleared on reset + missionWinDesc shown on win
- **Server fixes:**
  - `UseDefaultFiles()` + `MapFallbackToFile("index.html")` — handset served from wwwroot
  - Image confirmed as cropped version (no black borders)
- **Handset build** — `build:deploy` script updated (no --emptyOutDir, preserves tv.html/map)
- **Dice & Combat Theatre** — design section added to RISK-DESIGN.md (from Flutter learnings)
- **House Rules documented** — all 3 rules in RISK-DESIGN.md table
- **New docs created:**
  - `docs/AI-PLAYER.md` — AI design (tiers, mission concealment, architecture)
  - `docs/IDEAS.md` — consolidated future possibilities
  - `docs/BOARD-POLISH.md` — shared web/Unity polish planning

### Bug Fixes

- Blitz move-in min=3 when source only had 2 armies (min > max crash)
- Blitz move-in allowed 0 troops (empty attackerDice fallback)
- LastDiceCount for blitz used startSourceArmies instead of final round's actual dice
- TV game-over overlay not cleared on /admin/reset (early return skipped removal)

### Tested & Confirmed Working

- Blitz (single die when source depletes, capture + move-in flow)
- TV web board on: Edge desktop, Chrome desktop, Fire TV Stick Silk, JVC native browser
- Handset served from wwwroot via server on :5000
- Board viewable on phone browser (spectator)

### Known Issues (parked)

- F11 fullscreen (Edge/Chrome): dots drift outward from centre — overlay/image size mismatch. Desktop-only, works fine on actual TV targets.
- JVC native browser: minor dot stretch on X axis (non-standard CSS rendering)
- Silk browser Wake Lock API may not be supported (use Fire Stick screensaver settings instead)

### Files Changed

- `server/Risk.Server/Program.cs`
- `server/Risk.Server/Models/GameState.cs`
- `server/Risk.Server/Services/GameService.cs`
- `server/Risk.Server/wwwroot/tv.html`
- `handset/package.json`
- `handset/src/index.css`
- `handset/src/components/ReinforceScreen.tsx`
- `handset/src/components/AttackScreen.tsx`
- `handset/src/components/FortifyScreen.tsx`
- `docs/RISK-DESIGN.md`
- `docs/CARD-SYSTEM.md`
- `docs/NEXT-STEPS.md`
- `docs/PLAYTEST-NOTES.md`
- `docs/AI-PLAYER.md` (new)
- `docs/IDEAS.md` (new)
- `docs/BOARD-POLISH.md` (new)

---

*Updated: 2026-06-21 17:30*

---

## 2026-06-21 Evening — UI Polish, TV Parchment Theme, Bug Fixes

### Completed

- **Badge dismiss on tap-outside** — MissionBadge (🎯) and StatusBadge (📊) both dismiss when tapping anywhere outside the popup
- **Fortify accordion context** — defaults to continent of last attack front territory
- **Mission complete overlay (TV)** — smaller centred modal, map visible behind
- **Header badges 44px touch targets** — min-w/min-h 44px on mission/status badges
- **Card badge pill-shaped** — rounded-full, flatter (32px height)
- **TV turn timer** — seconds elapsed next to active player, resets on turn change
- **TV territory glow on attack selection** — green glow (source) / red glow (target) via SelectAttack hub broadcast
- **Mission check bug fix** — now checks after Reinforce and Fortify
- **Parchment theme** — handset popups + TV info box + dice overlay
- **TV info box tweaks** — wider, bold, nudged right, dark green active player border
- **Dice overlay centred on screen**
- **Capture report line break** — territory name on second line
- **Card trade alert → overlay** — no more blocking alert()
- **Pending move-in bug fix** — server tracks PendingMoveSource/Target; handset shows move-in on refresh

### Bug Fixes

- Mission not detected during Reinforce/Fortify
- Refresh during move-in allowed skipping troop placement (0 armies on territory)

### Files Changed

- `server/Risk.Server/Hubs/GameHub.cs`
- `server/Risk.Server/Models/GameState.cs`
- `server/Risk.Server/Services/GameService.cs`
- `server/Risk.Server/wwwroot/tv.html`
- `handset/src/types/game.ts`
- `handset/src/components/AttackScreen.tsx`
- `handset/src/components/MissionBadge.tsx`
- `handset/src/components/StatusBadge.tsx`
- `handset/src/components/ReinforceScreen.tsx`
- `handset/src/components/FortifyScreen.tsx`

---

*Updated: 2026-06-21 20:57*

---

## 2026-06-22 — Handset UI Improvements & TV Tweaks

### Completed

- **Minimal waiting screens** — all 4 phases (Placement, Reinforce, Attack, Fortify) now show only current player's coloured name + phase label when not your turn. No accordions, no buttons, no territory lists.
- **Coloured top border** — 3px border in current player's colour on all waiting screens for instant identification.
- **Chip-collapse source picker (Attack)** — after selecting source, accordion collapses to compact chip `🟢 Brazil (8) ✕`. Tap ✕ to reselect.
- **Chip-collapse source picker (Fortify)** — same pattern applied.
- **Must-trade = card panel only** — when forced to trade (5+ cards), Reinforce screen shows only the card panel. No territory list cluttering the view.
- **Default max dice + merged buttons** — Attack button now shows dice count inline `⚔️ 3🎲`. Small override toggles only appear when maxDice > 1. Saves an entire row.
- **Compact single-line headers** — all phases use tight header rows. Attack/Fortify phase badges centred with action buttons (`Done → Fortify`, `Skip → End`) pinned at bottom.
- **Badge size reduction** — 🎯 and 📊 badges reduced to 75% (33px) to fit alongside headers on one line.
- **Header alignment** — headers aligned vertically with fixed badges using `pt-2` + `min-h-[33px]`.
- **TV activity feed coalescing** — repeated placements to same territory now bump count (`+3 Western US`) instead of 3 separate lines.
- **TV activity feed bold text** — better readability on TV.
- **TV turn popup during Initial Placement** — shows `"John's turn"` popup (in player colour) when active player changes, with 1.2s delay for animation clearance.
- **HANDSET-UI-IMPROVEMENTS.md** — design doc created with full analysis and priority ranking.

### Bug Fixes

- Fixed header overlapping fixed mission/status badges (pt-14 → pt-2 + alignment)
- Fixed Fortify stray `</div>` from action button refactor

### Files Changed

- `handset/src/components/PlacementScreen.tsx`
- `handset/src/components/ReinforceScreen.tsx`
- `handset/src/components/AttackScreen.tsx`
- `handset/src/components/FortifyScreen.tsx`
- `handset/src/components/MissionBadge.tsx`
- `handset/src/components/StatusBadge.tsx`
- `server/Risk.Server/wwwroot/tv.html`
- `docs/HANDSET-UI-IMPROVEMENTS.md` (new)
- `docs/PLAYTEST-NOTES.md`

### Hardware

- **Z440 workstation arrived!** Next session will be from the new machine after full setup (VS2022, Unity, Node, Git, etc.)

---

*Updated: 2026-06-22*

---

## 2026-06-22 Late Afternoon — Z440 Setup

### Hardware Assembled

- **WiFi card installed** — Intel AX210 PCIe, both antenna connected, USB header for Bluetooth connected. Working on 5GHz (had to set Preferred Band in Device Manager → Advanced after it defaulted to 2.4GHz).
- **1TB HDD installed** — SATA data cable (D1/D2 daisy chain), borrowed power connector from CD drive (original connector was dead). Drive detected, labelled, working.
- **Dual monitors** — both on DisplayPort with DP-to-HDMI adapters. Required NVIDIA Quadro K2200 driver (Windows 11 doesn't bundle it — only shows Basic Display Adapter until manually installed). Third monitor available via DVI port.
- **2 RAM slots free** — 4x 8GB = 32GB currently, room for 48GB or 64GB later.

### Software Started

- Git installed + SSH key generated and added to GitHub
- Ready for: .NET 8 SDK, Node.js LTS, VS2022, Unity Hub, VS Code

### Docs Created/Updated This Session

- `docs/HANDSET-UI-IMPROVEMENTS.md` — design doc with analysis + priority ranking
- `docs/DEV-SETUP.md` — full project dev environment setup for fresh machine
- `docs/PLAYER-GUIDE.md` — onboarding guide for playtesters (rules, UI, tactics)
- `docs/PROGRESS.md` — this file
- `docs/PLAYTEST-NOTES.md` — colour picker noted
- `docs/IDEAS.md` — sound effects list added

### Notes for Next Session (from Z440)

- Clone repo: `git clone git@github.com:<username>/riskdigital.git`
- Follow `docs/DEV-SETUP.md` for remaining software installs
- This chat has full context of the project — new session on Z440 will need to catch up via `/docs` or reading PROGRESS.md
- Handset UI improvements branch is active with all 6 changes implemented + TV tweaks (activity coalescing, bold text, placement turn popup)
- Build verified clean: `cd handset && npx tsc --noEmit` passes

---

*Updated: 2026-06-22 18:23*

---

## 2026-06-23 Morning — Deploy All, Bug Fixes, TV Polish

### Completed

- **Deploy All button (Reinforce + Placement)** — territory buttons split into 70/30 layout: left tap = place 1, right "All" = place all remaining. Always visible, consistent layout.
- **Server count parameter** — `Reinforce(territoryId, count)` and `PlaceArmy(territoryId, count)` now accept optional count (default 1). Returns `(GameState, int Placed)` tuple. Single round-trip for bulk placement.
- **Haptic feedback** — `tap()` = 40ms buzz on +1, `heavyTap()` = 100ms buzz on All. Increased from 10/30ms (too short to feel on Android Chrome).
- **Active shade fix** — replaced invisible `active:brightness-125` on tinted backgrounds with `active:bg-white/30` — visible white overlay on press regardless of player colour.
- **Attack glow not cleared (bug fix)** — TV now clears glow when `turnPhase !== 'Attack'` on every state update. Stored selection reset.
- **AI attack glow not showing (bug fix)** — extracted `applyGlow()` function with stored source/target. Re-applied after every territory DOM render to fix race condition where `AttackSelection` event arrived before territories existed in DOM.
- **Glow pulsing animation** — source (green) and target (red) dots now pulse (scale 1→1.2) with bigger box-shadow (18px 8px). Fixes green glow invisible on green map areas.
- **Dice → capture sound sequence** — `CombatResult` now plays dice sound first, then capture fanfare after 1s delay (was playing capture immediately, skipping dice). Matches existing `BlitzResult` pattern.
- **Attack selection thud on TV** — plays placement thud sound when attacker selects source or target, giving audio feedback on the TV that someone is choosing.

### Bug Fixes

- Attack glow persisted after turn ended (Fortify/Reinforce/next player) — never cleared
- AI bot glow not appearing — timing race, `AttackSelection` arrived before `GameStateUpdated` rendered territory DOM elements
- Glow animation shifted dots (transform: scale without preserving translate(-50%, -50%)) — fixed in keyframes
- Haptic not felt on Android Chrome (10ms too short, bumped to 40/100ms)
- `active:brightness-125` invisible on faint colour-tinted buttons

### Files Changed

- `server/Risk.Server/Services/GameService.cs` — count param on Reinforce + PlaceArmy, tuple returns
- `server/Risk.Server/Hubs/GameHub.cs` — count param passed through, broadcast actual placed count
- `server/Risk.Server/wwwroot/tv.html` — glow clear on phase change, applyGlow(), pulse animation, dice→capture sequence, selection thud
- `handset/src/components/ReinforceScreen.tsx` — split button 70/30, active:bg-white/30, heavyTap
- `handset/src/components/PlacementScreen.tsx` — split button 70/30, active:bg-white/30, heavyTap
- `handset/src/utils/vibrate.ts` — tap() 40ms, heavyTap() 100ms

---

*Updated: 2026-06-23*

---

## 2026-06-23 Continued — Lobby Flow, Avatars, TV Polish, Deploy Prep

### Completed

- **TV splash screen** — Risk logo + "Waiting for game..." when no game exists. No more black screen.
- **TV lobby screen** — shows game code + player list (avatars, colours, host badge, bot icon) in 3-column grid.
- **LobbyStatus broadcast** — pushed to all clients on CreateGame/JoinGame/AddAI. Other handsets auto-update without refresh.
- **Remove AI** — host can tap ✕ next to AI players in lobby. Server validates host-only, lobby-only, AI-only.
- **Colour picker** — 6 colour circles on ConnectScreen. Server validates colour not already taken on join.
- **Avatar picker** — 9 avatar thumbnails on ConnectScreen. Persisted in localStorage.
- **Avatars everywhere** — lobby player list, TV info panel, activity feed, turn popups, phase announcements.
- **Lobby vertical space** — tightened padding, removed heading, smaller code text, compact rows.
- **Phase announcements on TV** — "🏰 Place your armies!" (Lobby→Placement) and "⚔️ Game on!" (Placement→Playing) with avatar + sound.
- **Suppress duplicate turn popup** — first turn after phase announcement doesn't get redundant "X's turn".
- **Game-over auto-dismiss** — big winner overlay for 10s, then shrinks to small badge in top-right corner.
- **Game-over cleared on new game** — overlay removed when Lobby phase starts.
- **Phase shown in info box** — "Connected 4456 · Reinforce" etc.
- **Random starting player** — no longer always the host.
- **Standard starting armies** — restored to 40/35/30/25/20. Debug mode via `/admin/reset?debug=true` for reduced armies.
- **Blitz fail sound** — plays `fail.mp3` after 1s delay when blitz doesn't capture.
- **Attack selection sound dedup** — alert only plays when source+target pair actually changes (no repeat on same pair).
- **Clean /tv URL** — `http://server:5000/tv` serves tv.html without .html extension.
- **FILE-MAP.md** — full project file map with descriptions.
- **PLAYER-GUIDE.md** — updated with colour/avatar picker, All button, fixed card values, all 14 missions listed, blitz warning.

### Files Changed

- `server/Risk.Server/Program.cs` — /tv route, /admin/debug query param, reset clears debug
- `server/Risk.Server/Models/GameState.cs` — AvatarIndex on Player
- `server/Risk.Server/Services/GameService.cs` — colour/avatar on Create/Join, RemoveAI, random start, standard armies + debug flag
- `server/Risk.Server/Hubs/GameHub.cs` — colour/avatar params, RemoveAI, BroadcastLobbyStatus helper
- `server/Risk.Server/wwwroot/tv.html` — splash, lobby screen, avatars, phase popups, suppress, game-over dismiss, fail sound, selection dedup, phase in info box
- `handset/src/components/ConnectScreen.tsx` — colour/avatar picker, compact layout
- `handset/src/components/LobbyScreen.tsx` — avatars, remove AI button, tighter spacing
- `handset/src/types/game.ts` — avatarIndex on Player
- `docs/FILE-MAP.md` — new
- `docs/LOBBY-FLOW.md` — new
- `docs/PLAYER-GUIDE.md` — updated
- `docs/PROGRESS.md` — this entry

---

## 2026-06-23 Evening — AI Tier 2, ML.NET Tier 3, Strategic Heuristics

### Completed

- **Tier 2 Aggressive AI** — always attacks weakest neighbour, reinforces front-line, blitzes at 5+, moves max on capture, always fortifies from rear to front.
- **AI Tier Chooser** — lobby shows `🤖 Tier-1` (blue) / `⚔️ Tier-2` (purple) / `🧠 Tier-3` (green) buttons. AddAI accepts tier param. Tier shown in player list.
- **ML.NET Integration (Phase 1)** — blitz probability model trained from 58K simulated battles. FastTree regression (R²=0.64, MAE=0.16). Model saved as `Data/models/blitz-model.zip`.
- **`/admin/train` endpoint** — generates training data + trains + loads model in one call.
- **MlModels service** — singleton, auto-loads model at startup, `PredictBlitz(atk, def)` returns 0–1 capture probability. Falls back to ratio heuristic if model absent.
- **Tier 3 Strategic Attack** — evaluates all attacks using `ScoreAttack()` (ML probability + continent completion bonus). Blitzes at >0.7, single attack 0.4–0.7, skips <0.4.
- **Attack Restraint** — Tier 3 stops attacking after earning a card (preserve armies for defence).
- **Smart Reinforce** — `ScoreReinforceTarget()` weights: continent gap territories ×3, owned continent borders ×2, enemy threat, shore up weak points.
- **Smart Fortify** — `FindStrategicFortify()` protects weakest border of owned continents from inland surplus. Falls back to Tier 2 logic if no continents owned.
- **Card Timing** — Tier 3 holds cards until territory bonus available or 4+ cards held (vs Tier 2 which trades immediately).
- **Exposed `MapData`** on GameService for continent access from AiService.
- **AiTier field** on Player model. Clamps 1–3.
- **Docs created:** AI-TIER2-IMPL.md, AI-TIER3-ML-PLAN.md, AI-SELECTION.md (final design), ML-FOR-DUMMIES.md, ML-NET-PROPOSAL.md

### Files Changed

- `server/Risk.Server/Risk.Server.csproj` — Microsoft.ML + FastTree NuGet packages
- `server/Risk.Server/Models/GameState.cs` — AiTier on Player
- `server/Risk.Server/Services/GameService.cs` — MapData property, AddAiPlayer tier param, tier clamp
- `server/Risk.Server/Services/AiService.cs` — Tier 2 + Tier 3 branches, strategic helpers (ScoreReinforceTarget, ScoreAttack, FindStrategicFortify, HasTerritoryBonusSet)
- `server/Risk.Server/Services/MlModels.cs` — new (loads model, PredictBlitz)
- `server/Risk.Server/Training/BlitzSimulator.cs` — new (generates training CSV)
- `server/Risk.Server/Training/ModelTrainer.cs` — new (trains FastTree regression)
- `server/Risk.Server/Program.cs` — MlModels DI, /admin/train endpoint, model load at startup
- `server/Risk.Server/Hubs/GameHub.cs` — AddAI tier param
- `handset/src/components/LobbyScreen.tsx` — Tier-1/2/3 buttons
- `handset/src/types/game.ts` — aiTier on Player

### Next

- Tier 4 personalities (Carl/Alice/Chris/Ollie) — weight profiles on Tier 3 engine
- Mystery mode toggle
- Test Tier 3 vs Tier 2 behaviour

---

*Updated: 2026-06-23 22:03*


---

## 2026-06-24 Evening — Handset UI Fixes, TV Board Polish, AI Timing

### Completed

- **Placement/Reinforce +1 button** — refactored territory rows to discrete `+1` and `All` buttons (right side), territory name still tappable for +1 (backward compat). Army count restored next to name.
- **Truncation fix** — long territory names (e.g. "Eastern United States") now truncate with ellipsis. Buttons stay fixed width (`w-12 shrink-0`) so they never get pushed off screen. Name uses `min-w-0 flex-1`.
- **Disabled state** — name tap, +1, and All buttons all disabled/greyed when reinforcements remaining = 0.
- **TV board image resized to 16:9** — height 944px × width 1678px. Fills TV screen with no black bars. Dot overlays follow (percentage-based).
- **Map fill mode** — switched from `object-fit: contain` to `width: 100%; height: 100%; object-fit: fill`. Eliminates black line at bottom.
- **Info panel narrowed** — `min-width: 180px; max-width: 220px` (was 240/300). Tighter padding.
- **Abbreviated territory names in info panel** — `shortNames` lookup (e.g. "Eastern United States" → "East. US", "North Africa" → "N. Africa", "Northwest Territory" → "NW. Terr."). Popups still use full names via `tFullName()`.
- **Avatar removed from activity feed** — coloured dot `⬤` + player name only. Saves width in info panel.
- **Mute button moved to top-right** — was bottom-right, now `top: 12px; right: 24px`.
- **AI turn delay** — bots now wait 2.5–3s before starting their turn, letting the TV turn popup display and clear first.
- **AI Tier 4 plan updated** — added "Advanced Capabilities" section: game-tree lookahead, opponent modelling, alliance/threat diplomacy, adaptive personality shifting.

### Files Changed

- `handset/src/components/PlacementScreen.tsx` — +1 button, army count, truncation, disabled state
- `handset/src/components/ReinforceScreen.tsx` — +1 button, army count, truncation, disabled state
- `server/Risk.Server/wwwroot/tv.html` — map fill, info panel narrow, shortNames, pName simplified, mute btn moved
- `server/Risk.Server/Services/AiService.cs` — 2.5–3s initial delay in RunTurnAsync
- `docs/AI-TIER4-PLAN.md` — advanced capabilities section added

### Status

- Ready for live playtesting with real players
- Tier 4 AI (Opportunist Ollie) implemented and available in lobby
- Tier 5 pipeline in place: logging → training → prediction (needs more game data)

---

*Updated: 2026-06-24 21:04*

---

## 2026-06-24 Late Evening — AI Tier 4 + Tier 5 Learning Pipeline

### Completed

- **AI Tier 4 (Opportunist Ollie)** — personality-based AI with enhanced Risk heuristics:
  - `PersonalityWeights` record + `AiPersonality` enum (4 characters defined, Ollie active)
  - Elimination hunting (targets weakest player for card steal)
  - Continent denial (blocks opponents 1 territory from completing)
  - Chokepoint recognition (values Ukraine, Siam, N. Africa etc.)
  - Card escalation awareness (eliminations worth more as trade count rises)
  - Personality timing multiplier (Ollie = 0.9× speed)
  - Weighted reinforcement toward weakest player's borders
  - Fortify toward elimination targets
- **Lobby Tier-4 button** — amber `🦊 Tier-4`, two-row layout (Tier 1-2 / Tier 3-4)
- **Action Logger** — `Services/ActionLogger.cs` logs every human player decision with board context:
  - Reinforce: territory armies, border status, enemy threat, continent progress
  - Attack: source/target armies, continent progress, blitz usage, elimination proximity
  - Fortify: target border status, enemy threat, skip detection
  - Only logs non-AI players. Appends to CSVs in `Data/logs/`
- **Behaviour Trainer** — `Training/BehaviourTrainer.cs`:
  - Trains reinforce model (predict placement likelihood from territory features)
  - Trains attack model (predict attack likelihood from source/target features)
  - FastTree regression, 50 trees, min 10 rows to train
- **`/admin/train-behaviour` endpoint** — reads CSVs, trains models, hot-loads into memory (no restart)
- **MlModels extended** — `PredictHumanReinforce()` and `PredictHumanAttack()` with graceful fallback
- **Pipeline verified end-to-end** — played one game, logs collected, trained successfully
- **TV info panel** — removed player names from activity feed (name already in heading)
- **AI-TIER5-PLAN.md** — comprehensive tutorial: data science basics → ML.NET → Risk implementation

### Files Changed

- `server/Risk.Server/Models/GameState.cs` — AiPersonality enum, PersonalityWeights record
- `server/Risk.Server/Services/GameService.cs` — tier clamp 1–4, personality assignment
- `server/Risk.Server/Services/AiService.cs` — Tier 4 branches + heuristic helpers
- `server/Risk.Server/Services/ActionLogger.cs` — new
- `server/Risk.Server/Services/MlModels.cs` — behaviour model loading + predictions
- `server/Risk.Server/Training/BehaviourTrainer.cs` — new
- `server/Risk.Server/Hubs/GameHub.cs` — ActionLogger injection + logging calls
- `server/Risk.Server/Program.cs` — DI, /admin/train-behaviour, startup load
- `server/Risk.Server/wwwroot/tv.html` — activity feed name removal
- `handset/src/components/LobbyScreen.tsx` — Tier-4 button, two-row layout
- `docs/AI-TIER5-PLAN.md` — full ML tutorial + plan (replaced)
- `docs/AI-TIER4-PLAN.md` — advanced capabilities added
- `docs/PROPOSAL-AI-TIER4.md` — updated

---

*Updated: 2026-06-24 22:20*

---

## 2026-06-24 Night — Deployment to WHUK + Playtest

### Completed

- **Deployed to WHUK** — full stack live at `https://risk.spooch.co.uk`
- **Fixed startup crash** — `ActionLogger` directory creation + `LoadBehaviourModels` now fail gracefully (try-catch, no crash if Data dirs missing)
- **`/admin/train-behaviour` hardened** — full try-catch, friendly message when no logs exist
- **TV map `object-fit: contain`** — reverted from `fill`/`cover` back to `contain` + `syncOverlay()`. 16:9 image fills TV perfectly, dots track correctly on desktop too.
- **Won a game on WHUK** — full playtest successful (but action logging not writing — investigate tomorrow)
- **Gold colour** — renamed "Yellow" to "Gold" in mission descriptions (matches actual hex appearance)

### Known Issue (tomorrow)

- Action logs not being created on WHUK despite game completing. Likely a path issue — `ContentRootPath` on WHUK might not be what we expect, or write permissions on `Data/logs/`. Need to add a debug endpoint to check paths.

### Files Changed

- `server/Risk.Server/Services/ActionLogger.cs` — try-catch on directory creation + file append
- `server/Risk.Server/Services/MlModels.cs` — guard against missing models directory
- `server/Risk.Server/Program.cs` — train-behaviour endpoint hardened with full error surfacing
- `server/Risk.Server/wwwroot/tv.html` — reverted to object-fit: contain
- `server/Risk.Server/Services/GameService.cs` — "Yellow" → "Gold" in mission descriptions

---

*Updated: 2026-06-24 23:38*

---

## 2026-06-25 Morning — Logging Fix, Endpoint Refactor, AI Personality Restructure

### Completed

- **Action logger fix (WHUK)** — logs weren't writing due to access denied on `Data/logs/`. Added fallback: tries app-local first, falls back to `%TEMP%\risk-logs`. Verified working on WHUK (`C:\Windows\TEMP\risk-logs`).
- **ILogger added to ActionLogger** — surfaces first write failure instead of silently swallowing all exceptions.
- **`/admin/logs-status` endpoint** — reports active log directory, existence, writability, error, and file list with sizes.
- **`/admin/logs-download` endpoint** — zips all CSVs and returns `risk-logs.zip` for backup.
- **`/admin/logs-upload` endpoint** — POST a zip of CSVs to restore training data after deploy/restart. Appends to existing files (skips duplicate headers).
- **EndPointConfig refactor** — all `app.MapGet`/`app.MapPost` endpoints moved to `EndPointConfig/ManagementEndpoints.cs` as extension method. `Program.cs` slimmed down.
- **AI personality moved from Tier 4 to Tier 5** — Tier 4 is now single personality (Opportunist) with enhanced heuristics. Tier 5 has 4 personalities (Opportunist, Cautious, Aggressive, Continental) that use the ML learning pipeline.
- **Tier 5 personality picker in lobby** — tap 🧬 Tier-5 to expand: 🦊 Opportunist, 🛡️ Cautious, 🔥 Aggressive, 🗺️ Continental, 🎲 Mystery (random).
- **Mystery personality** — server picks random personality, player won't know which until they observe behaviour.
- **Docs updated** — PLAYER-GUIDE.md, AI-PLAYER.md, guide.html all updated with corrected tier structure.

### Files Changed

- `server/Risk.Server/Program.cs` — slimmed, calls `MapManagementEndpoints()`
- `server/Risk.Server/EndPointConfig/ManagementEndpoints.cs` — new (all admin/board/guide endpoints)
- `server/Risk.Server/Services/ActionLogger.cs` — ILogger, LogDir property, temp fallback
- `server/Risk.Server/Services/GameService.cs` — personality param, random for mystery, tier 5 assignment
- `server/Risk.Server/Hubs/GameHub.cs` — AddAI accepts personality param
- `server/Risk.Server/wwwroot/guide.html` — AI table updated
- `handset/src/components/LobbyScreen.tsx` — Tier-5 personality picker + mystery button
- `docs/PLAYER-GUIDE.md` — AI bots section updated
- `docs/AI-PLAYER.md` — quick reference table updated

---

*Updated: 2026-06-25 08:51*

---

## 2026-06-25 Evening — Stable Storage, Unified Training, App Logging

### Completed

- **Stable writable path found** — `D:\Inetpub\vhosts\spooch.co.uk\tmp\risk-logs` (and `risk-models`). Per-site, survives deploys and app pool recycling.
- **ActionLogger path cascade** — tries `Data/logs` → `../tmp/risk-logs` → `%TEMP%/risk-logs`. WHUK lands on vhost tmp.
- **Unified `/admin/train` endpoint** — single endpoint trains all models (blitz from 58K simulated battles + behaviour from player logs). Replaced separate `/admin/train-behaviour`.
- **`/admin/ml-status` endpoint** — reports loaded models + sample predictions for verification.
- **`/admin/app-log` endpoint** — in-memory ring buffer (300 lines), captures all ILogger output. No file I/O, no permissions needed.
- **`RingBufferLogger.cs`** — ILoggerProvider implementation, registered in DI, passively captures all app logging.
- **`/admin/logs-download` endpoint** — zips CSVs for backup.
- **`/admin/logs-upload` endpoint** — POST zip to restore (blocked by WHUK IIS — use FTP instead).
- **Blitz model now trains on WHUK** — writes to `tmp/risk-models/blitz-model.zip`, loaded on startup.
- **Program.cs ML loading** — checks temp models dir first, falls back to `Data/models`.
- **All models verified on WHUK** — blitz (58K rows), reinforce (115 rows), attack (232 rows).
- **Route group consistency** — all admin endpoints on `MapGroup("/admin")`, no mixed `app.MapGet("/admin/...")`.

### Files Changed

- `server/Risk.Server/Program.cs` — RingBufferLogger registration, ML load from temp path
- `server/Risk.Server/EndPointConfig/ManagementEndpoints.cs` — unified train, ml-status, app-log, logs-status with alt-path testing, route group cleanup
- `server/Risk.Server/Services/ActionLogger.cs` — path cascade (Data → tmp → TEMP)
- `server/Risk.Server/Services/RingBufferLogger.cs` — new (in-memory log provider)
- `docs/FILE-MAP.md` — endpoints table updated
- `docs/PROGRESS.md` — this entry

### Next

- Unity TV board! 🎮

---

*Updated: 2026-06-25 20:59*

---

## 2026-06-25 Late — Auto-Retrain on Game Over

### Completed

- **Background retrain on game-over** — fire-and-forget `Task.Run()` triggers after every completed game. No player-facing delay.
- **Fortify behaviour trainer** — `TrainFortify` in BehaviourTrainer, `FortifyInput` + `PredictHumanFortify` in MlModels.
- **All 3 behaviour models now auto-retrain** — reinforce, attack, fortify.
- **ILogger in GameHub** — retrain results visible in `/admin/app-log` ring buffer.
- **`/admin/train` endpoint** — also trains fortify model now.

### Files Changed

- `server/Risk.Server/Hubs/GameHub.cs` — MlModels + ILogger injection, RetrainModels with logging
- `server/Risk.Server/EndPointConfig/ManagementEndpoints.cs` — fortify in /admin/train
- `server/Risk.Server/Training/BehaviourTrainer.cs` — TrainFortify + FortifyRow
- `server/Risk.Server/Services/MlModels.cs` — FortifyInput, PredictHumanFortify, LoadBehaviourModels loads fortify

---

## One Week Summary (2026-06-20 → 2026-06-25)

From zero to a fully playable, deployed digital board game:

- ✅ Full game loop: lobby → placement → reinforce → attack → fortify → game over
- ✅ Card system with escalation, forced trades, territory bonuses
- ✅ 14 mission cards with elimination fallback
- ✅ Blitz attack with ML-predicted odds
- ✅ 5-tier AI system (random → heuristic → strategic → personality → learning)
- ✅ 4 AI personalities (Opportunist, Cautious, Aggressive, Continental)
- ✅ ML.NET training pipeline: blitz probability + 3 behaviour models from human play
- ✅ Auto-retrain on game-over (background, fire-and-forget)
- ✅ Web TV board with map, dots, glow, sounds, activity feed, phase popups
- ✅ Handset with continent accordions, haptics, card UI, mission badges
- ✅ Live deployment on WHUK (risk.spooch.co.uk)
- ✅ Ring buffer logging, admin diagnostics, training data backup/restore
- ✅ 22+ docs covering design, AI, ML, setup, progress

*Updated: 2026-06-25 20:59*
