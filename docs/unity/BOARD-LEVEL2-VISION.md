# Unity Board — Level 2 Vision: Relief Map with Drone Camera

## Goal

Transform the flat sprite board into an immersive 3D war table with terrain depth and a cinematic camera system that responds to game events. The board should feel like a physical relief map on a general's desk — you're looking down at it, the terrain has depth, and the camera moves to tell the story.

---

## Layers (built incrementally)

### Layer 1 — Tabletop Foundation
*Do this first. Everything else builds on it.*

| Element | Description |
|---------|-------------|
| Board plane | Flat quad with the map texture. Slight elevation off a table surface. |
| Table surface | Wood or dark felt underneath/around the board. Extends to screen edges. |
| Camera angle | Perspective camera looking down at ~60° (not orthographic, not perfectly top-down). |
| Shadows | Directional light casting token shadows onto the board. Immediate depth. |
| Board edge | Subtle thickness on the board quad — like a mounted map or game board with sides. |
| Ambient lighting | Warm, slightly dim. War room / study feel. |

### Layer 2 — Relief Depth
*Normal map makes the terrain look raised without expensive geometry.*

| Element | Description |
|---------|-------------|
| Normal map | Painted or generated from a height map. Continents raised, oceans flat, mountain ranges as ridges. |
| Material | Standard URP Lit with base colour (map texture) + normal map. Roughness high (matte parchment/printed map feel). |
| Parallax | Camera movement reveals the fake depth. Slow drift makes normal map very convincing. |
| Borders | Coastlines and continent borders appear as subtle carved lines (in the normal map). |
| Optional: height map | If normal map isn't enough, minimal vertex displacement on a subdivided quad. Only if Fire Stick handles it. |

### Layer 3 — Drone Camera System
*Camera responds to game events — idle drift, zoom to battle, pull back.*

| Behaviour | Trigger | Camera action |
|-----------|---------|---------------|
| Idle drift | No events for 5s | Slow random drift across board, slight rotation, varying height |
| Attack zoom | `AttackSelection` event | Pan + zoom to frame source and target territories together |
| Combat hold | `SpawnDice` / dice settling | Hold position, dice arena visible in context |
| Pull back | `CombatResult` + 2s delay | Smooth return to overview / resume idle drift |
| Turn start | `TurnStarted` event | Quick pan toward active player's territory cluster |
| Blitz | `BlitzResult` | Already at battle position (from attack zoom) — hold longer |
| Game over | `GamePhase.GameOver` | Dramatic pull-back to full board, winner's territories prominent |
| Reinforce | `ArmiesPlaced` event | Subtle drift toward placed territory (don't interrupt, just bias direction) |

### Layer 4 — Environment Polish (optional, future)
*Dressing around the board for atmosphere. Not required for gameplay.*

| Element | Description |
|---------|-------------|
| Info panel as monitor | Small in-scene screen/tablet showing game code, phase, players |
| Desk lamp | Warm spot light on the board, soft shadows |
| Background | Dark blurred room, shelves, maybe a flag or globe. Very out of focus. |
| Particles | Subtle dust motes in the light |

---

## Camera System Design

### Architecture

Extend or complement existing `CameraFlypath.cs`:

```
BoardCamera.cs
├── IdleDrift()        — random slow movement, Perlin noise for organic feel
├── FlyTo(target, zoom) — smooth pan to a world position at specified distance
├── FramePair(posA, posB) — calculate midpoint + distance to frame two territories
├── PullBack()         — return to overview position (or resume drift)
└── holds/delays       — configurable per event type
```

### Data available for targeting

- Territory world positions: already placed by `BoardRenderer.cs` from coordinate data
- Source/target IDs: from `AttackSelection` SignalR event
- Active player territories: from `GameState`
- Midpoint calculation: `(posA + posB) / 2`
- Zoom distance: `Vector3.Distance(posA, posB) * factor`

### Smooth movement

- Use `Vector3.Lerp` or DOTween-style easing for position
- `Quaternion.Slerp` for rotation
- Or: Catmull-Rom spline from current position to target (reuse flypath maths)
- Speed should feel cinematic — not instant, not sluggish. ~1.5-2s transitions.

### Interaction with dice arena

The dice arena is at an off-screen position rendered via separate camera to RenderTexture. The board camera movement doesn't affect the dice panel — they're independent systems. This is already the case.

The *visual effect* is that the board camera zooms to the battle, then the dice panel appears overlaid. Player sees: map zooms in → dice roll → result → map zooms out. Cinematic sequence.

---

## Normal Map Creation

### Option A: Paint in Photoshop/GIMP
- Use the map image as reference layer
- Paint greyscale height map (white = high, black = low)
- Continents white/light grey, oceans black, mountains lighter
- Convert to normal map (Filter → 3D → Generate Normal Map, or online tool)
- Produces subtle perceived depth at zero GPU cost beyond a texture sample

### Option B: Generate from real-world data
- Download heightmap data (USGS, MapZen) for approximate continent elevation
- Crop/project to match your board image
- Convert to normal map
- More realistic but may not match the stylised Risk board

### Option C: Minimalist
- Just oceans recessed, land flat, borders as grooves
- Very subtle — enough for parallax to read
- Fastest to produce

**Recommendation:** Option A. You control the look, it matches the board style, and you can exaggerate features (make continents chunkier than real life for visual clarity).

---

## Token Adjustments for Angled Camera

Current tokens are tilted cylinders viewed from roughly overhead. With a ~60° perspective camera:

- Tokens may need slight rotation adjustment so the army count label faces the camera
- Label could be a world-space TextMesh or billboard sprite (always faces camera)
- Token height may need to increase slightly to not be hidden by terrain normal map fake depth
- Shadow casting becomes important — confirms tokens are "on" the surface

---

## Performance Budget (Fire Stick 4K Max)

| Element | Cost |
|---------|------|
| Board quad + normal map | Negligible (1 draw call, 2 textures) |
| 42 tokens (cylinders) | Low (instanced, simple mesh) |
| Directional light + shadows | Medium — shadow map resolution matters. 1024 should be fine. |
| Camera movement | Free (just transform updates) |
| Dice arena (existing) | Already budgeted and working |
| Environment dressing | Keep minimal — 2-3 extra meshes max |

**Total:** Well within Fire Stick capability. The bottleneck would be shadow map resolution if pushed too high.

---

## Implementation Order

1. **Camera angle + table surface** — change from orthographic/top-down to perspective ~60°, add a plane underneath as table
2. **Lighting** — directional light, token shadows, warm ambient
3. **Board edge** — give the map quad some thickness (extrude or box mesh)
4. **Idle drift** — `BoardCamera.cs` with Perlin-driven slow movement
5. **Attack zoom** — respond to `AttackSelection`, frame source+target
6. **Pull back** — return to overview after combat resolves
7. **Normal map** — paint height map, convert, apply to board material
8. **Turn start pan** — subtle camera bias toward active player
9. **Game over** — dramatic pullback
10. **Environment (optional)** — info panel as object, desk lamp, background

Steps 1–3 are the Level 1 foundation.
Steps 4–6 are the drone camera core.
Step 7 is the relief depth.
Steps 8–10 are polish.

---

## Open Questions

- **Board image aspect ratio** — current 16:9 image works flat. On a 3D plane viewed at an angle, the far edge compresses (perspective). May need the board plane slightly larger than screen to avoid seeing edges. Test in editor.
- **Token readability** — are army counts still readable at 60° with smaller apparent size on distant territories? May need scaling or LOD labels.
- **Idle drift extent** — how far should the camera wander? Too much and players lose orientation. Too little and it's static. Needs tuning in-game.
- **Attack zoom speed** — if bots attack rapidly, constant zooming would be nauseating. May need to skip zoom for bot-vs-bot or rate-limit camera moves.

---

*Created: 2026-06-29*
