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
- **Lobby flow (server)** — CreateGame (4-digit code, host assignment), JoinGame (validates code/capacity/name, assigns colour), StartGame (host-only, min 3), Rejoin, GetState, /admin/reset
- **Lobby flow (handset)** — ConnectScreen (name input, create/join with code), LobbyScreen (game code, player list with colours, host badge, start button)
- **Dark theme** — full dark UI matching Flutter handset style (bg-gray-900, red/amber/green accents, emoji branding)
- **Debug TV page** — wwwroot/tv.html, dark themed, SignalR connection, shows game code + phase + player list, auto-reconnects
- **App routing** — conditional render by phase (Connecting → Connect → Lobby → placeholder)

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

- Test full lobby flow end-to-end (create, join, see on TV)
- Initial Placement phase (deal territories, place armies)

### Design Notes

- **Min players reduced to 2** for dev/testing. Standard Risk is 3–6; will enforce 3+ in production or fill with AI.
- **AI players (grand plan):** Unlike Flutter where AI just needed personality-flavoured random moves on a linear track, Risk AI needs genuine strategic intelligence — territory clustering, continent control, threat assessment, alliance-breaking, bluff attacks. This is a real AI challenge. Server-driven (same as Flutter) but much deeper decision trees. Likely a tiered approach: dumb AI first for testing, then progressively smarter.

---

*Updated: 2026-06-20*
