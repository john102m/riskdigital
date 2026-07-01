# Deployment Targets

How the Unity TV board can be deployed, what works, what doesn't, and how it relates to the existing web board.

---

## Current Architecture

```
Fire TV Stick ← Android APK (sideloaded via ADB)     ← Premium 3D experience
Any browser   ← tv.html (served from server:5000)     ← Accessible web experience
Phones        ← handset (React, served from server)    ← Player controllers
```

Both TV targets consume the same SignalR events. The web board is already universally accessible.

---

## Target Comparison

| Target | Install | SignalR | Physics | Threads | Effort from current |
|--------|---------|---------|---------|---------|-------------------|
| **Fire TV (Android)** | Sideload APK via ADB | .NET client ✅ | Full ✅ | Full ✅ | Zero — already done |
| **Desktop (Windows)** | Run .exe | .NET client ✅ | Full ✅ | Full ✅ | Zero — editor build |
| **WebGL (browser)** | Just a URL | JS interop ❌ rewrite needed | Works ✅ | No C# threads ❌ | Medium-high |
| **tv.html (web board)** | Just a URL | JS client ✅ | N/A (2D dots) | N/A | Already done |

---

## Fire TV Stick (Android) — Primary Target

### How it works
1. Unity builds Android APK (ARM64)
2. Fire TV Stick has ADB debugging enabled
3. Connect over WiFi: `adb connect <ip>:5555`
4. Install: `adb install -r risk.apk`
5. Launch from Fire TV app list

### What works
- Full .NET SignalR client (`Microsoft.AspNetCore.SignalR.Client` 8.0.x)
- Full physics simulation (dice rolling, settling, face reading)
- `async Awaitable` patterns, `TaskCompletionSource`, threading — all fine
- Camera flypath, emission glow, RenderTexture pipeline
- Runs in background (`Application.runInBackground = true`)

### Limitations
- Need to rebuild + re-sideload for every update
- No auto-update mechanism (not on any app store)
- Fire TV remote not needed (passive display) but may trigger unwanted UI focus
- GPU capable but not desktop-class — keep draw calls reasonable
- Fire TV Stick 4K Max (1st Gen, K2R2TE) — 2GB RAM, Mali GPU

### Deployment command
```bash
adb connect 192.168.1.XX:5555
adb install -r "D:\Unity Projects\RiskDigitalBoard\Builds\risk.apk"
```

---

## Desktop (Windows/Mac/Linux) — Dev Fallback

### How it works
1. Build as Standalone Player in Unity Editor
2. Run the `.exe` on any PC connected to a TV/monitor

### What works
- Everything. No limitations whatsoever.
- Same build, just different platform target.

### When to use
- Development/debugging (run in editor or built player)
- When Fire TV Stick isn't available
- Z440 connected to TV via HDMI as fallback display

### Limitations
- Requires a PC connected to the TV
- Not as clean as a dedicated streaming device

---

## WebGL (Browser) — Theoretical Future Option

### How it would work
1. Unity builds WebGL target → `index.html` + WASM bundle + data files
2. Host on server (alongside handset and tv.html)
3. Any browser navigates to URL → 3D board loads

### What works
- 3D rendering (WebGL/WebGPU)
- Physics engine (dice rolling, settling)
- Scene, materials, camera — all render correctly
- Audio (Web Audio API — basic but functional)

### What does NOT work (limitations)

| Limitation | Impact on this project |
|------------|----------------------|
| **No .NET SignalR client** | `Microsoft.AspNetCore.SignalR.Client` uses `System.Net.WebSockets` which is unavailable in browser sandbox. Must use JavaScript SignalR client via `.jslib` interop bridge. `SignalRClient.cs` needs full rewrite. |
| **No C# managed threads** | `Task.Run()`, `TaskCompletionSource`, `async/await` with thread pool — all broken or behave differently. `DiceRoller.WaitAndReadAll()` and the settle-detection pattern would need rethinking. |
| **Single-threaded execution** | Physics runs on main thread only. Fine for 6 dice, but can't parallelise anything. |
| **Large initial download** | WASM bundle typically 10-30MB+ compressed. Slow first load, cached after. Fire Stick Silk on WiFi: acceptable. Phone on 4G: poor. |
| **No filesystem access** | Can't read/write local files. Doesn't affect us (no local state needed). |
| **Mobile browsers unsupported** | Unity officially doesn't support WebGL on mobile. Fire Stick Silk is desktop Chromium — likely fine. Phone browsers — unreliable. |
| **Limited audio** | Web Audio API only. Basic playback works but no advanced mixing/spatial audio. |

### Community workaround: Unity-WebGL-SignalR

[github.com/evanlindsey/Unity-WebGL-SignalR](https://github.com/evanlindsey/Unity-WebGL-SignalR)

A plugin that bridges SignalR JavaScript client to Unity C# via `.jslib` interop:
- Uses a custom WebGL template that includes the SignalR JS `<script>`
- C# calls go through `[DllImport("__Internal")]` to JavaScript
- JavaScript calls back via `SendMessage()` to Unity GameObjects
- Different API surface from .NET client — can't just swap in

### Effort estimate
- **SignalRClient.cs** — full rewrite to use JS interop (or the plugin above)
- **DiceRoller.cs** — refactor settle detection to avoid `TaskCompletionSource` / threading
- **CombatTheatre.cs** — may need coroutine fallback or Unity's `Awaitable` (which does work in WebGL as of Unity 6, but with caveats)
- **Build pipeline** — new WebGL template, compression settings, hosting config
- **Testing** — different behaviour in browser vs editor; browser-specific bugs

### When would it be worth it?
- If someone wants the 3D dice experience but **cannot sideload** (no ADB access, not a Fire TV)
- Remote play where participants have a shared screen that's just a browser tab
- Demoing the project without requiring any installs

### Verdict
Park it. The web board (`tv.html`) already covers the "any browser" use case. WebGL would only add 3D dice in a browser — significant effort for a niche scenario.

---

## Why tv.html Already Covers the Gap

The web board is:
- Zero install (any browser, any device)
- Sub-second load time
- Shows full game state: map, dots, glow, dice results overlay, sounds, activity feed
- Works on Fire Stick Silk, phone browsers, laptops, JVC smart TV
- Maintained in vanilla JS — easy to tweak

It doesn't have:
- 3D dice physics (shows result numbers instead)
- Camera flypath
- Physical dice face reading

But players get the same information, just presented as an overlay rather than a 3D simulation. For family game night where one TV has the Fire Stick and another room has a laptop, it works perfectly.

---

## Decision Matrix

| Scenario | Best target |
|----------|-------------|
| Main TV in living room (Fire Stick) | Android APK |
| Dev testing | Desktop / Unity Editor |
| Secondary screen (laptop, tablet, phone) | tv.html |
| Remote family member watching | tv.html |
| No Fire Stick, want 3D dice | Desktop .exe on HDMI PC |
| No install at all, want 3D dice | WebGL (future, high effort) |

---

*Created: 2026-06-29*
