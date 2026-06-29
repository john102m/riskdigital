# Unity TV Board — Progress

## Phase 1 — Minimum Viable Board ✅ (2026-06-27)

- ✅ 3D URP project created (`D:\Unity Projects\RiskDigitalBoard`)
- ✅ Static map background (board-lined-blue.png as sprite)
- ✅ 42 territory tokens (3D cylinders, coloured by owner)
- ✅ Army count labels on each token
- ✅ SignalR connection to live server (System.Text.Json + JsonElement)
- ✅ Real-time state updates (GameStateUpdated)
- ✅ Info panel (game code, phase, player list with coloured dots, current turn)
- ✅ 5-second poll fallback for missed broadcasts
- ✅ Repo: https://github.com/john102m/UnityDigitalRisk.git

## Phase 2a — Attack Selection Glow ✅ (2026-06-27)

- ✅ AttackSelection event received (source + target, supports null target)
- ✅ Emission glow on source (green) and target (red) territories
- ✅ Pulse animation (scale breathing) on glowing tokens
- ✅ Glow clears on phase change (not on individual combat results)
- ✅ Pulse speed and amount exposed to Inspector

## Phase 2b — Dice Arena ✅ (2026-06-27)

- ✅ Dice arena (5-cube box at off-screen position, sized to catch all dice)
- ✅ Dice prefab (cube + Rigidbody + PhysicsMaterial)
- ✅ DiceCamera directly above arena → RenderTexture → square RawImage (picture-in-picture)
- ✅ DiceRoller spawns attacker (red) and defender (white) dice
- ✅ Physics simulation — dice tumble, bounce, settle
- ✅ Face correction to match server values after settle
- ✅ CombatTheatre orchestrates show panel → roll → hide panel
- ✅ Event queue for rapid combat results
- ✅ Panel persists through repeated rolls in same battle
- ✅ Panel hides on: capture, phase change, or new attack selection

## Phase 2c — Dice Arena Upgrade ✅ (2026-06-28)

- ✅ Reshaped arena to rectangular cuboid (12×7 box lid)
- ✅ Low-angle perspective camera (table-level view)
- ✅ Directional throw (+Z along the box, not straight down)
- ✅ RenderTexture + RawImage aspect ratio matched to arena (960×560)
- ✅ Layer isolation (DiceArena layer) — dice camera only sees arena
- ✅ FBX dice model imported (replaces plain cubes)
- ✅ Catmull-Rom camera flypath (CameraFlypath.cs)
  - Waypoints as draggable empties in scene
  - OnDrawGizmos for spline visualisation
  - Randomised per-roll: jitter, speed variation, direction reversal
  - Smooth transition to overhead result position (random point on circle)
- ✅ CombatTheatre fires flypath in parallel with dice roll

## Phase 2d — TV-Driven Dice Physics ✅ (2026-06-29)

- ✅ Server: RegisterAsTV hub method + Unity TV connection tracking
- ✅ Server: CombatRollRequest DTO + broadcast to Unity
- ✅ Server: SubmitDiceResult hub method (receives physics results)
- ✅ Server: ResolveCombat method (uses external dice values)
- ✅ Server: Attack() branches — delegates to Unity if connected, else server rolls
- ✅ Server: 10s timeout fallback to server roll
- ✅ Server: AttackWithDice() shared method — used by both hub and AI
- ✅ Server: Bot attacks now delegate to Unity dice (single attacks, not blitz)
- ✅ Server: BlitzResult includes final round dice for display
- ✅ Server: /admin/testdice endpoint for triggering test rolls
- ✅ Unity: RegisterAsTV call on connect
- ✅ Unity: CombatRollRequest event handler
- ✅ Unity: DiceRoller.RollAndRead() — pure physics, no face correction
- ✅ Unity: DiceFaceReader axis→face mapping calibrated and verified
- ✅ Unity: Sends results back to server immediately (before visual hold)
- ✅ Unity: Blitz final dice display (PlaceDiceAtValues — scattered, jaunty, correct faces)
- ✅ Unity: Camera snaps to result position for blitz display
- ✅ Unity: Throw direction randomised for even face distribution
- ✅ Unity: Capture hold (4s) so players can see the killing blow

## Phase 2e — Player-Rolled Dice ✅ (2026-06-29)

- ✅ Server: RollPrompt + SpawnDice DTOs
- ✅ Server: AttackWithDice two-phase flow (attacker immediate, defender prompted)
- ✅ Server: PlayerRoll hub method + AutoRollBotOpponent
- ✅ Server: Bot-vs-bot auto-roll after 1s, human-vs-bot immediate, human-vs-human prompted
- ✅ Server: No timeout — humans roll when ready
- ✅ Server: ResolveCombat now sets LastDiceCount (fixed stuck move-in bug)
- ✅ Unity: SpawnDice event handler — two-phase dice spawning
- ✅ Unity: DiceRoller.SpawnSet() order-independent, WaitAndReadAll()
- ✅ Unity: Camera sweep once per attack (resets on capture dismiss + phase change)
- ✅ Unity: Blitz result gets camera sweep before placed dice
- ✅ Handset: RollPrompt listener at app level (useConnection hook, always mounted)
- ✅ Handset: Defender "Defend!" overlay with dice count choice + Roll button
- ✅ Handset: Vibrate on defend prompt
- ✅ Gated by IsUnityTVConnected — WebTV/handset flow unchanged without Unity

## Tech Debt / Polish Queue

- [ ] Dice physics tuning session — settle detection (add angular velocity check + "stable for N frames"), PhysicsMaterial tweaks, damping to prevent edge-balancing, possible slam-down force
- [ ] Dice face textures (pips visible on FBX model but tint overrides — consider texture-preserving tint)
- [ ] DicePanel frame/border (UI Image behind RawImage for TV-screen effect)
- [ ] Reinforcement pulse (brief pulse on army placement)
- [ ] Remove debug logging (DiceFace dot products, DiceRoller spawn/read logs)
- [ ] DicePanel positioning/sizing for TV layout
- [ ] Arena lighting and floor material polish
- [ ] GetRotationForFace verification (blitz placed dice may need Euler angle tweaks)

## Codebase Refactoring ✅ (2026-06-27)

- ✅ Converted all coroutines to `async Awaitable` (Unity 6 modern pattern)
- ✅ CancellationTokenSource for pulse animation cancellation
- ✅ `Application.runInBackground = true` (fixes focus-loss freezing)

## Next Up — Phase 2 Completion

- [ ] Dice result overlay text (who won each pair)
- [ ] Sound effects (dice rattle, bounce, result sting)
- [ ] Blitz summary text overlay (X rounds, attacker/defender losses)

## Phase 3 — Parity with Web Board (not started)

- [ ] Turn popup (whose turn + colour)
- [ ] Activity feed (attack/fortify/card trade log)
- [ ] Card trade alert
- [ ] Game over / winner announcement
- [ ] Fortify animation

## Phase 4 — Visual Polish (not started)

- [ ] Custom Blender tokens (replace cylinders)
- [ ] Territory tint fills
- [ ] Particle effects (capture explosion, win confetti)
- [ ] Camera pans to combat zone
- [ ] Ambient music + dynamic intensity
- [ ] Animated troop movement

## Key Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Project type | 3D URP | Need 3D for dice, camera perspective later |
| Camera | Orthographic (board), Perspective (dice) | Flat map view + dramatic dice |
| Dice approach | Picture-in-picture (Option B) | Map stays visible, spectator context |
| Blitz display | Summary only, no individual rolls | Blitz = fast, skip drama |
| Async pattern | `async Awaitable` (Unity 6) | Modern C#, matches day job |
| SignalR version | 8.0.x | 9.x+ incompatible with Unity runtime |
| Repo | Separate from Risk repo | Different commit cadence, heavy assets |
