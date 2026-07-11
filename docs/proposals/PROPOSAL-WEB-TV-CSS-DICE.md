# Proposal: Web TV Board Parity with Unity Desktop

## Summary

Bring `tv.html` closer to feature parity with the Unity desktop TV board. The Unity
build remains the primary TV target (real physics dice, camera flypath, 3D arena).
The web board is the zero-install alternative — works on any device with a browser
(Fire Stick Silk, laptop, mini PC, phone) when the Unity machine isn't available.

## Motivation

- Unity desktop build is the premium experience but requires a dedicated Windows machine
- `tv.html` is the fallback for casual use, remote play, or when no PC is near the TV
- Currently missing dice visuals entirely — the biggest experiential gap
- Closing the gap makes the web board a viable game night option, not just a status display

## Gap Analysis

### ✅ Already in tv.html (parity achieved)

| Feature | Notes |
|---------|-------|
| Map with territory dots (colour + army count) | Positioned overlays on full-viewport map |
| Attack selection glow (source green, target red) | Pulse animation |
| Sound effects | dice, capture, elimination, turn, card, fortify, victory, place, alert, fail |
| Turn popup | Central popup with player name + colour + avatar |
| Activity feed | Attack/fortify/card/blitz log, sliding in, max 4 lines |
| Card trade popup | Central popup |
| Elimination popup | Central popup |
| Game over / winner overlay | Full-screen → shrinks to corner badge after 10s |
| Lobby screen | Player list with avatars, game code |
| Reinforce pulse | Territory dot grows on placement |
| Fortify animation | Source shrinks, target grows |
| Mute toggle | Sound on/off |
| Wake lock | Screen stays on |
| Reconnect resilience | SignalR auto-reconnect + GetState recovery |

### ❌ Missing from tv.html (the gap)

| Feature | Unity Implementation | Proposed tv.html Approach | Priority |
|---------|---------------------|---------------------------|----------|
| **Dice visual** | 3D physics dice in arena, camera flypath | CSS 3D cubes with spin → land animation | P1 |
| **Blitz dice display** | Static placed dice (correct faces, scattered) | CSS dice in panel, no spin (placed immediately) | P1 |
| **Blitz popup** | Scroll unroll with rounds/losses/outcome | Styled overlay matching existing popup system | P1 |
| **Camera zoom on combat** | Orthographic zoom to midpoint of source/target | CSS transform scale + translate on map container | P2 |
| **Idle drift** | Slow camera wander after 15s inactivity | Subtle CSS translate drift animation | P3 |
| **Dice camera flypath** | Catmull-Rom spline, randomised per roll | Not applicable — dice panel is 2D overlay | — |
| **Phase-based soundtrack** | Crossfade between lobby/placement/attack/fortify/victory tracks | Background audio with phase-based track switching | P3 |
| **Defender roll awareness** | Arena opens, ghost red dice visible while waiting | "Waiting for defender..." indicator in dice panel | P2 |
| **Dice rattle sound** | Single rattle clip played on combat start | Same — play dice sound when panel appears | P1 |
| **Capture hold** | Dice held 4s on capture before dismiss | Dice panel stays 4s on capture | P1 |
| **Territory labels** | Always visible below tokens | Already present (always visible below dots) | — |
| **Post-game drift** | Camera slowly wanders | CSS drift animation on game over | P3 |

### Not applicable to tv.html (Unity-specific)

| Feature | Reason |
|---------|--------|
| Physics dice simulation | Browser doesn't need real physics — CSS animation achieves same visual result |
| DiceFaceReader / axis mapping | Dice values come from server — just display them |
| Multi-household dice routing | tv.html is display-only, doesn't submit dice |
| Dice arena (3D box) | 2D overlay instead |
| Camera flypath (Catmull-Rom spline) | Not needed — dice panel is picture-in-picture |
| Unity serialized Inspector values | N/A |

---

## P1 Features — Detailed Design

### 1. CSS 3D Dice

Each die is a CSS 3D cube (~50-60px) with pip faces drawn via CSS (no images):

```css
.die {
    width: 56px; height: 56px;
    position: relative;
    transform-style: preserve-3d;
    transition: transform 0.8s cubic-bezier(0.2, 0.8, 0.3, 1);
}
.die.spinning {
    animation: diceSpin 0.4s linear infinite;
}
.die .face {
    position: absolute; inset: 0;
    backface-visibility: hidden;
    border-radius: 6px;
    display: grid;
    padding: 8px;
}
```

Face rotation mapping (value → transform):
```
1: rotateX(0)     rotateY(0)       — front
2: rotateX(0)     rotateY(-90deg)  — right
3: rotateX(-90deg) rotateY(0)      — top
4: rotateX(90deg)  rotateY(0)      — bottom
5: rotateX(0)     rotateY(90deg)   — left
6: rotateX(180deg) rotateY(0)      — back
```

Attacker dice: red background, white pips.
Defender dice: blue background, white pips.

### 2. Dice Panel

Floating panel that appears during combat:

- Position: fixed, bottom-right (above info panel), or centred — adjustable
- Background: `rgba(0,0,0,0.85)` with subtle border
- Layout: attacker dice on left, gap/divider, defender dice on right
- Appears on combat start, dismisses after result hold

### 3. Animation Flow

**Single attack (Unity TV connected — delegated dice):**
1. `SpawnDice("attacker")` → panel appears, red dice spinning
2. `AttackerDiceResult` → red dice transition to correct faces
3. `SpawnDice("defender")` or `RollPrompt` → blue dice spinning (or "Waiting..." text)
4. `DefenderDiceResult` → blue dice transition to correct faces
5. `CombatResult` → hold (2.5s normal, 4s capture) → fade out

**Single attack (no Unity TV — server instant roll):**
1. `CombatResult` → panel appears, all dice spin briefly (0.6s) then land on values
2. Hold → fade out

**Blitz:**
1. `BlitzResult` → panel appears with final dice already showing (no spin)
2. Blitz popup overlay: "{rounds} rounds — Lost {atkLoss} — Won {defLoss}"
3. Hold 4s → fade out

### 4. New SignalR Listeners

```javascript
connection.on('SpawnDice', (data) => { /* show panel, start spinning */ });
connection.on('AttackerDiceResult', (data) => { /* land red dice on values */ });
connection.on('DefenderDiceResult', (values) => { /* land blue dice on values */ });
connection.on('RollPrompt', (data) => { /* show "Waiting for {name}..." */ });
```

No server changes needed — these events are already broadcast to all clients in the group.

### 5. Blitz Popup

Styled overlay on top of the dice panel:
```
⚡ BLITZ — 8 rounds
Lost 3 ⚔️ · Won 12 🛡️ · CAPTURED!
```
Same styling as existing central popup but positioned over/near the dice panel.

### 6. Dismiss Logic

- `CombatResult` with `captured: true` → hold 4s
- `CombatResult` with `captured: false` → hold 2.5s
- Next `SpawnDice("attacker")` → immediately clear and restart
- Phase change away from Attack → dismiss immediately
- `BlitzResult` → hold 4s

---

## P2 Features — Combat Zoom

Smooth CSS zoom on the map container during combat:

```javascript
function zoomToTerritory(sourceId, targetId) {
    const [sx, sy] = COORDS[sourceId];
    const [tx, ty] = COORDS[targetId];
    const cx = (sx + tx) / 2;
    const cy = (sy + ty) / 2;
    mapContainer.style.transform = `scale(1.8) translate(${50 - cx}%, ${50 - cy}%)`;
    mapContainer.style.transition = 'transform 1.5s ease-in-out';
}
function zoomOut() {
    mapContainer.style.transform = 'scale(1) translate(0, 0)';
}
```

Triggered on `AttackSelection` (zoom in), cleared on phase change or new attack (zoom out + re-zoom).

---

## P3 Features — Polish

### Idle Drift
After 15s of no activity, slowly pan the map with a CSS animation:
```css
@keyframes idleDrift {
    0% { transform: translate(0, 0); }
    25% { transform: translate(-1%, 0.5%); }
    50% { transform: translate(0.5%, -0.5%); }
    75% { transform: translate(-0.5%, -1%); }
    100% { transform: translate(0, 0); }
}
```

### Phase-Based Soundtrack
Add a background `<audio>` element that crossfades between tracks based on game phase.
Tracks can be the same MP3s from the Unity project (or royalty-free alternatives).
Gated behind the existing mute button.

### Always-On Territory Labels
Toggle option (query param?) to show short territory names always, not just on hover.

---

## Implementation Plan

All changes in `server/wwwroot/tv.html` — single file, no build step.

| Step | Scope | Estimate |
|------|-------|----------|
| CSS 3D dice (faces, spin, land) | ~100 lines CSS, ~30 lines JS | Small |
| Dice panel (show/hide, layout) | ~40 lines CSS, ~60 lines JS | Small |
| Event wiring (SpawnDice, results) | ~80 lines JS | Small |
| Blitz dice display (static placement) | ~30 lines JS | Tiny |
| Blitz popup overlay | ~20 lines CSS, ~20 lines JS | Tiny |
| Combat zoom (P2) | ~40 lines JS | Tiny |
| Idle drift (P3) | ~20 lines CSS/JS | Tiny |
| Soundtrack (P3) | ~60 lines JS | Small |

Total: ~500 lines added to tv.html.

## Hybrid Scenario: Unity TV + Web Board (Cross-Household)

When one household has the Unity desktop build and the other has only `tv.html`, dice
delegation needs a mixed mode. The principle: **if a player has a Unity TV, their dice
are physics-rolled. If not, the server rolls for them and broadcasts the result.**

### Scenario A: Unity player attacks → Web player defends

1. Server sends `SpawnDice("attacker")` to group
2. Unity TV (attacker's) rolls red dice physically, submits `SubmitRolledDice("attacker")`
3. Server broadcasts `AttackerDiceResult` → web board CSS red dice land on those values
4. Server sends `RollPrompt` to human defender's handset
5. Defender sees red dice result on web board while pondering
6. Defender taps Roll → `PlayerRoll` fires
7. Server sees **no Unity TV for defender** → generates random values, completes `_pending.SubmitDefenderDice(values)` internally
8. Server broadcasts `DefenderDiceResult` → both boards show blue dice landing
9. Server resolves combat, broadcasts `CombatResult`

Defender experience: tap Roll, see blue dice land on values. Same ceremony, server-random instead of physics.

### Scenario B: Web player attacks → Unity player defends

1. Server sends `SpawnDice("attacker")` to group
2. Server sees **no Unity TV for attacker** → generates random attacker values, completes `_pending.SubmitAttackerDice(values)` internally
3. Server broadcasts `AttackerDiceResult` → web board CSS red dice land, Unity TV snaps ghost red
4. If defender is human: `RollPrompt` sent → defender taps Roll → `PlayerRoll` fires
5. Server sends `SpawnDice("defender")` to group
6. Unity TV (defender's) rolls blue dice physically, submits `SubmitRolledDice("defender")`
7. Server broadcasts `DefenderDiceResult` → both boards show blue dice
8. Server resolves combat

Attacker experience: see red dice spin and land (CSS animation, server-random values). Still feels like a roll.

### Scenario C: Both on web boards (no Unity TV anywhere)

1. `IsUnityTVConnected` = false → existing path: server rolls both instantly
2. `CombatResult` broadcast with all values
3. Both web boards show quick spin → land animation from `CombatResult` values

No change needed — this already works today (just no dice visual yet).

### Server Change Required

In `AttackWithDice`, after determining the TV for each player:

```
var attackerTvConn = GetTVForPlayer(attackerPlayerIndex);
var defenderTvConn = GetTVForPlayer(defenderPlayerIndex);
```

If `GetTVForPlayer` returns a connection that belongs to a **Unity TV** (registered via `RegisterAsTV`), delegate to physics. If the player has **no Unity TV** (web board only, or no TV at all), the server generates random values and self-submits on their behalf.

This requires distinguishing "registered Unity TV" from "web board watching". Options:

1. **Web board does NOT call `RegisterAsTV`** — simplest. `GetTVForPlayer` only returns Unity TVs. If no Unity TV covers a player, server rolls for them.
2. Web board registers with a flag (`isPhysicsCapable: false`) — more explicit but more complex.

Option 1 is cleanest. Web board just listens to group broadcasts. Never registers. Server logic: "if I can't find a Unity TV for this player, I roll for them."

Current `GetTVForPlayer` falls back to the first registered TV if no match — that fallback needs to be removed (or only return TVs that explicitly claim the player). If no TV claims them, return null → server self-rolls.

### Timing Consideration

When the server self-rolls for a player (no physics), it should still:
- Broadcast `SpawnDice` (so web board starts the CSS spin animation)
- Wait a beat (~1s delay) before broadcasting the result (so the spin has time to look good)
- Then broadcast `AttackerDiceResult` or `DefenderDiceResult`

This gives the CSS dice time to animate rather than instantly showing values.

---

## Out of Scope

- Three.js / WebGL physics dice (future stretch goal if CSS feels too flat)
- Multi-household TV registration (tv.html is display-only — doesn't own/submit dice)
- Interactive elements (tv.html is read-only, no player input)

## Success Criteria

After P1, the web TV board should feel like a complete game night experience:
- You see dice roll and land during single attacks
- Blitz shows final dice + outcome popup
- Sound accompanies every action
- Any browser on any device is all you need
