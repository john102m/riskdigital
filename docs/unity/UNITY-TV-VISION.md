# Unity TV App — Vision & Plan

## Why Unity?

The web TV board (tv.html) handles all gameplay display needs. The Unity version exists for:

1. **Learning exercise** — bounded, real-purpose project to learn Unity with
2. **Spectacle upgrade** — visual wow factor beyond what a web page delivers
3. **Foundation** — skills transfer to future projects (maze, shooter, characters, etc.)

## What Unity Adds Over the Web Board

| Feature | Web Board | Unity Board |
|---------|-----------|-------------|
| Map + ownership dots | ✅ | ✅ |
| Activity feed | ✅ | ✅ |
| Turn indicators | ✅ | ✅ |
| Attack glow | ✅ | ✅ (+ shader effects) |
| Sound effects | ✅ | ✅ (spatial audio, layered) |
| 3D dice physics | ❌ | ✅ Real tumbling dice with camera tracking |
| Particle effects | ❌ | ✅ Explosions, confetti, smoke |
| Animated troop movement | ❌ | ✅ Tokens sliding between territories |
| Shader territory effects | ❌ | ✅ Glow, pulse, weather overlays |
| Camera swoops | ❌ | ✅ Dramatic transitions between attacks |
| Animated army tokens | ❌ | ✅ Tiny soldiers instead of dots (stretch goal) |

## Architecture (unchanged)

```
Unity TV App ──SignalR──▶ .NET 8 Server
```

- Read-only display. No player input needed.
- Same SignalR events as web board (GameStateUpdated, CombatResult, BlitzResult, etc.)
- Same hub, same data, different renderer.

## Implementation Phases

### Phase 1 — Minimum Viable Board ✅ (2026-06-27)
- ✅ Static map background (sprite, board-lined-blue.png)
- ✅ Territory tokens at x/y coordinates (3D cylinders, coloured by owner, army count text)
- ✅ SignalR connection receiving GameStateUpdated (System.Text.Json + JsonElement)
- ✅ Info panel (game code, phase, player list with coloured dots, current turn)
- ✅ Live updates from production server (https://risk.spooch.co.uk)

**Tech notes:**
- 3D URP project, orthographic camera
- NuGetForUnity for SignalR client (v8.x — v9+ incompatible with Unity runtime)
- UnityMainThread dispatcher for SignalR→main thread marshalling
- Territory coordinates reused from tv.html (percentage-based)
- Text labels spawned as separate objects (avoids 3D rotation/child offset issues)

### Phase 2 — Combat Theatre
- 3D dice roll on attack (physics-based, camera cut to dice)
- Dice result display with brief pause
- Capture animation (token colour change + pulse)
- Sound effects (roll, impact, capture fanfare, fail)

### Phase 3 — Polish & Atmosphere
- Particle effects (territory capture explosion, win confetti)
- Troop movement animation (fortify/capture slide)
- Territory glow/pulse on selection (matching web board's green/red)
- Camera pans to active combat zone
- Ambient music + dynamic intensity

### Phase 4 — Stretch Goals
- Animated soldier tokens (Mixamo or KayKit characters)
- Territory tint fills (shader-based ownership colouring)
- Weather/atmosphere effects (fog of war, storms on contested borders)
- 3D terrain map (elevated continents, ocean shader)

## Build Targets

| Target | Path/Access | Use Case |
|--------|-------------|----------|
| **WebGL** | `server/wwwroot/unity/` — any browser | Premium visuals, no install, just a URL. Best of both worlds. |
| **Android APK** | Sideloaded via ADB to Fire TV Stick | Maximum performance, native rendering. |
| **Desktop** | Run in Unity Editor or standalone exe | Dev/testing fallback. |

All three built from the same Unity project — just switch Build Target. WebGL is the likely default for family use (same accessibility as tv.html, but with 3D dice and particles). APK reserved for when performance matters.

### WebGL Considerations
- Output is HTML + JS + WASM (~20-50MB) served from wwwroot
- Slower startup than tv.html (WASM load) but cached after first visit
- No threading — keep coroutines simple
- Some shader restrictions — test particle effects on target browsers
- Fire Stick Silk browser: test early, may need APK fallback for that device

## Tech Decisions

- **Unity 6 LTS** (URP)
- **2D map with 3D overlays** — background sprite + 3D dice/particles/tokens
- **SignalR client:** `com.microsoft.signalr` NuGet via NuGetForUnity (or Best HTTP)
- **No player input** — no Input System needed

## Key Scripts (planned)

| Script | Responsibility |
|--------|---------------|
| `SignalRClient.cs` | Connect to hub, deserialize events, fire C# events |
| `GameStateManager.cs` | Hold current state, notify listeners on change |
| `BoardRenderer.cs` | Position/update territory tokens from state |
| `DiceRoller.cs` | 3D dice physics scene, camera, result detection |
| `CombatTheatre.cs` | Orchestrate attack sequence (glow → dice → result → capture) |
| `InfoPanel.cs` | UI overlay — game code, players, phase |
| `SoundManager.cs` | Audio playback, spatial positioning |

## Timeline

No rush. The web board serves game night. Unity board is the learning journey — built incrementally between sessions as skills develop.

---

*Created: 2026-06-26*
