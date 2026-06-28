# Phase 2 — Combat Theatre

## Goal
When an attack happens, the Unity board plays a cinematic dice sequence instead of just updating numbers. This is the primary visual differentiator from the web board.

## Server Events

```csharp
// Single attack
CombatResult { AttackerDice[], DefenderDice[], AttackerLosses, DefenderLosses,
               Captured, SourceId, TargetId, SourceArmies, TargetArmies }

// Blitz (repeated combat)
BlitzResult { Rounds, TotalAttackerLosses, TotalDefenderLosses,
              Captured, SourceId, TargetId, SourceArmies, TargetArmies }
```

## Combat Sequence (single attack)

1. **Announce** — brief text flash: "Alaska → Kamchatka" with player colours
2. **Camera cut** — switch to dice arena camera
3. **Dice roll** — spawn attacker dice (red, 1–3) and defender dice (white, 1–2) with physics
4. **Settle** — wait for dice to stop moving
5. **Read faces** — determine top face of each die (should match server values)
6. **Display result** — highlight winning/losing pairs, show losses
7. **Camera return** — cut back to board camera
8. **Update board** — token colours/counts already handled by GameStateUpdated

Total sequence: ~3–4 seconds per attack.

## Blitz Handling

Blitz sends one `BlitzResult` for potentially dozens of rounds. Options:
- **A)** Show a single dramatic roll with final totals (simple, fast)
- **B)** Rapid-fire dice montage (flashy but complex)
- **Start with A** — show attacker/defender loss totals, skip individual rounds.

## Architecture

### Dice Arena
- Position: off-map (e.g. world position `(50, 0, 0)` — far from the board)
- Floor plane with walls (invisible or dark themed) to contain dice
- Lit separately from the map (own light)
- Dice arena camera renders only when active

### Cameras
| Camera | Purpose | Culling |
|--------|---------|---------|
| Main Camera | Board overview (orthographic) | Everything except Dice layer |
| Dice Camera | Dice arena (perspective, angled down) | Dice layer only |

Use **layers** to separate: dice objects on "Dice" layer, arena on "Dice" layer. Main camera ignores Dice layer. Dice camera only sees Dice layer.

Switch between them by enabling/disabling camera GameObjects (or adjusting depth/priority).

### Scripts

| Script | Role |
|--------|------|
| `DiceRoller.cs` | Spawns dice prefabs, applies force/torque, detects when settled, reads top face |
| `CombatTheatre.cs` | Receives CombatResult from SignalRClient, orchestrates the full sequence via coroutine |
| `DiceFaceReader.cs` | Component on each die — determines which face is up when stationary |

### Dice Prefab
- **Mesh:** Default Unity cube (or bevelled cube from Blender later)
- **Material:** Red for attacker, white for defender
- **Faces:** TextMeshPro on each face (1–6) OR a texture atlas with dot pips
- **Physics:** Rigidbody + BoxCollider + bouncy PhysicsMaterial
- **Layer:** "Dice"

### Face Reading
Each die has 6 face normals. When settled (Rigidbody velocity ≈ 0):
- Check which face's outward normal is most aligned with `Vector3.up`
- That face's value is the result

```csharp
// Face mapping for default Unity cube
// +Y = top, -Y = bottom, +X = right, -X = left, +Z = front, -Z = back
Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.right, 
                          Vector3.left, Vector3.forward, Vector3.back };
int[] faceValues =      { 1, 6, 2, 5, 3, 4 }; // standard die opposite faces sum to 7
```

### PhysicsMaterial (Dice)
- Dynamic Friction: `0.4`
- Static Friction: `0.4`
- Bounciness: `0.3`
- Bounce Combine: `Average`

Gives a satisfying tumble without excessive bouncing.

## Sound Effects (stretch for Phase 2, required for Phase 3)

| Event | Sound |
|-------|-------|
| Dice throw | Rattle/shake |
| Dice bounce | Wood tap (per collision) |
| Dice settle | Brief silence |
| Attacker wins | Short triumphant sting |
| Defender wins | Thud/block sound |
| Territory captured | Fanfare |

## Event Queue

Combat events may arrive while a dice sequence is playing. Buffer them:

```csharp
Queue<CombatResult> pendingCombats = new();

void OnCombatResult(CombatResult result) {
    pendingCombats.Enqueue(result);
    if (!isPlaying) StartCoroutine(ProcessQueue());
}

IEnumerator ProcessQueue() {
    isPlaying = true;
    while (pendingCombats.Count > 0) {
        yield return StartCoroutine(PlayCombatSequence(pendingCombats.Dequeue()));
    }
    isPlaying = false;
}
```

## Implementation Order

1. **Dice prefab** — cube with face values, rigidbody, physics material
2. **Dice arena** — floor + walls + dedicated camera + lighting
3. **DiceRoller.cs** — spawn, throw, detect settle, read face
4. **Test standalone** — press a key to roll dice, verify values read correctly
5. **CombatTheatre.cs** — wire to SignalR events, camera switching, full sequence
6. **Polish** — timing, camera angle, result display overlay

## Inspector-Exposed Settings

```csharp
public float throwForce = 5f;
public float throwTorque = 10f;
public float settleThreshold = 0.01f;
public float settleTimeout = 3f;
public float resultDisplayTime = 1.5f;
public Color attackerDiceColour = Color.red;
public Color defenderDiceColour = Color.white;
```

## Open Questions

- Dice face textures: pips vs numbers? (Pips are more authentic, numbers easier to implement)
- Camera transition: hard cut vs brief lerp/fade?
- Dice arena aesthetic: dark felt table? Wooden surface? Transparent/floating?

## Deferred Items

- **Reinforcement pulse:** When armies are placed/reinforced, briefly pulse that territory (1–2 beats). Same pulse logic as attack glow but with a short timeout. Compare army counts between state updates to detect which territories changed.

---

*Phase 2 starts after Phase 1 is stable and committed.*
