# Risk Digital — Dev Environment Setup (Fresh Machine)

Everything needed to clone this repo and have all three components running.

---

## 1. Prerequisites (install in this order)

### Git
```
winget install Git.Git
```
Then configure:
```bash
git config --global user.name "John McKinney"
git config --global user.email "your@email"
ssh-keygen -t ed25519
# Add public key to GitHub: https://github.com/settings/keys
```

### .NET 8 SDK
```
winget install Microsoft.DotNet.SDK.8
```
Verify: `dotnet --version` → 8.x.x

### Node.js LTS (v20+)
```
winget install OpenJS.NodeJS.LTS
```
Verify: `node --version` → v20+, `npm --version` → 10+

### Visual Studio 2022
Workloads needed:
- ASP.NET and web development
- Game development with Unity (installs Unity scripting tools)

### VS Code
```
winget install Microsoft.VisualStudioCode
```
Extensions:
- Tailwind CSS IntelliSense
- ESLint
- GitLens

### Unity Hub + Unity Editor
- Install Unity Hub: https://unity.com/download
- Install Unity **2022.3 LTS** or **6000.x LTS** via Hub
- Target platform: Android Build Support (for Fire TV APK)
- Script editor: VS2022 or VS Code (set in Unity Preferences → External Tools)

### Android SDK / ADB (for Fire TV deployment)
Either via Android Studio or standalone:
```
winget install Google.AndroidStudio
```
- SDK Manager: install API 34 platform + Build Tools
- Add to PATH: `C:\Users\<you>\AppData\Local\Android\Sdk\platform-tools`
- Verify: `adb --version`

---

## 2. Clone & Run

### Clone
```bash
cd D:\
git clone git@github.com:<your-username>/riskdigital.git
cd riskdigital
```

### Server
```bash
cd server/Risk.Server
dotnet restore
dotnet run
```
Runs on `http://0.0.0.0:5000`. SignalR hub at `/gamehub`.

Open in VS2022: `server/Risk.Server.sln`

### Handset
```bash
cd handset
npm install
npm run dev
```
Runs on `http://localhost:3000` (LAN-accessible via `--host`).

Open in VS Code: `handset/` folder.

### TV (Web board — immediate)
Navigate to `http://<server-ip>:5000/tv.html` on any browser/Fire Stick Silk.

### TV (Unity — later)
- Open `tv/` folder in Unity Hub as a project
- Open scene, hit Play in editor for desktop preview
- Build → Android for Fire TV deployment

---

## 3. LAN Access (phones/TV)

Find your machine's LAN IP:
```bash
ipconfig
```
Look for IPv4 on your WiFi adapter (e.g. `192.168.1.50`).

- Handset on phone: `http://192.168.1.50:3000` (dev) or `http://192.168.1.50:5000` (production build served from wwwroot)
- TV board: `http://192.168.1.50:5000/tv.html`
- Both devices must be on same WiFi network

### Vite env for phone access (dev mode)
Create `handset/.env.local`:
```
VITE_SERVER_URL=http://192.168.1.50:5000
```

---

## 4. Fire TV Stick Deployment

```bash
# Enable ADB on Fire Stick: Settings → My Fire TV → Developer Options → ADB Debugging ON
# Find Fire Stick IP: Settings → My Fire TV → About → Network

adb connect 192.168.1.XX:5555
adb devices  # should show connected

# For Unity APK:
adb install -r tv/Build/risk-tv.apk

# For web board (just open Silk browser to server URL — no install needed)
```

---

## 5. Production Build & Deploy

### Build handset for serving from server wwwroot:
```bash
cd handset
npm run build:deploy
```
This outputs to `server/Risk.Server/wwwroot/` (preserves tv.html and map assets).

### Publish server:
```bash
cd server/Risk.Server
dotnet publish -c Release -o ./publish
```

### Deploy to WHUK:
Upload `publish/` folder contents to hosting. The server serves both the API (SignalR) and the handset bundle from wwwroot.

---

## 6. Useful Commands

| Task | Command |
|------|---------|
| Reset game | `curl http://localhost:5000/admin/reset` |
| Force game over | `curl http://localhost:5000/admin/gameover` |
| View missions | `curl http://localhost:5000/admin/missions` |
| Type check handset | `cd handset && npx tsc --noEmit` |
| Build handset | `cd handset && npm run build` |
| Run server | `cd server/Risk.Server && dotnet run` |

---

## 7. Project Structure (key files)

```
riskdigital/
├── server/Risk.Server/
│   ├── Program.cs              — entry point, SignalR + CORS config
│   ├── Hubs/GameHub.cs         — SignalR hub (thin, delegates to service)
│   ├── Services/GameService.cs — all game logic
│   ├── Services/AiService.cs   — AI player logic
│   ├── Models/GameState.cs     — state, player, territory, card models
│   ├── Data/territories.json   — 42-territory adjacency graph
│   └── wwwroot/                — static files (tv.html, handset bundle)
├── handset/
│   ├── src/App.tsx             — phase routing
│   ├── src/hooks/useConnection.ts — SignalR hook
│   ├── src/components/         — per-phase screens
│   └── src/types/game.ts       — TypeScript interfaces
├── tv/                         — Unity 2D project (not yet started)
└── docs/                       — design docs, progress, plans
```

---

*Created: 2026-06-22*
