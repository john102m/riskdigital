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

## Tech Debt / Polish Queue

- [ ] Emission changes base colour (orange → yellow) — need subtler glow approach
- [ ] Dice face textures (pips or numbers) — currently plain cubes
- [ ] Reinforcement pulse (brief pulse on army placement)
- [ ] Remove debug logs before next release
- [ ] DicePanel positioning/sizing for TV layout
- [ ] Camera angle and lighting in dice arena

## Codebase Refactoring ✅ (2026-06-27)

- ✅ Converted all coroutines to `async Awaitable` (Unity 6 modern pattern)
- ✅ CancellationTokenSource for pulse animation cancellation
- ✅ `Application.runInBackground = true` (fixes focus-loss freezing)

## Next Up — Phase 2 Completion

- [ ] Blitz result display (summary overlay, no individual rolls)
- [ ] Dice result overlay text (who won each pair)
- [ ] Sound effects (dice rattle, bounce, result sting)

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
