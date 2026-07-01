# Dice Physics Tuning — Discussion

## Current State

The dice work. They roll, they settle, they read faces correctly. But they feel like plastic cubes bouncing off laminate flooring — smooth, clicky, slidey. They need to feel like dice landing in a wooden box.

## Where All The Settings Live

| Setting | Location | How to Edit |
|---------|----------|-------------|
| **Rigidbody** (mass, damping) | `Assets/Prefabs/Red-die.prefab` → Rigidbody component | Inspector on prefab |
| **BoxCollider size** | `Assets/Prefabs/Red-die.prefab` → BoxCollider component | Inspector on prefab |
| **PhysicsMaterial** (friction, bounce) | `Assets/DiceBounce.physicMaterial` | Inspector (Project panel) |
| **Throw force & torque** | `Assets/Scripts/DiceRoller.cs` → `throwForce`, `throwTorque` fields | Inspector on DiceRoller (scene) or script defaults |
| **Settle detection** | `Assets/Scripts/DiceRoller.cs` → `settleThreshold`, `settleTimeout` | Inspector on DiceRoller |
| **Arena floor** | Scene: `DiceArena/Floor` → BoxCollider | Inspector in scene |
| **Arena walls** | Scene: `DiceArena/WallRight`, `WallLeft`, `WallFront`, `WallBack` | Inspector in scene |

### What's NOT configured

- **Floor has no PhysicsMaterial** — `m_Material: {fileID: 0}`. It's using Unity's default (friction 0.6, bounce 0). Same for all walls.
- **Dice collider size is 0.19** (FBX model units) at scale 3 = effective 0.57 unit cube.
- **No angular velocity in settle check** — dice can be called "settled" while still visibly spinning.
- **No "stable for N frames" check** — a single frame below threshold counts as settled.

## Current Values (Red-die prefab)

| Parameter | Value |
|-----------|-------|
| Mass | 1 |
| Linear Damping | 0 |
| Angular Damping | 0.05 |
| Collision Detection | Discrete |
| Interpolate | None |

### DiceBounce PhysicsMaterial (on dice only)

| Parameter | Value |
|-----------|-------|
| Dynamic Friction | 0.4 |
| Static Friction | 0.4 |
| Bounciness | 0.4 |
| Friction Combine | Maximum (3) |
| Bounce Combine | Average (0) |

### DiceRoller Script

| Parameter | Value |
|-----------|-------|
| Throw Force | 8 |
| Throw Torque | 10 |
| Settle Threshold | 0.1 |
| Settle Timeout | 4s |

## The Problem

The "laminate flooring" feel comes from a combination of:

1. **No damping** — dice slide across the floor endlessly after they stop bouncing. Linear damping 0 means zero energy loss from movement itself.
2. **Negligible angular damping (0.05)** — dice spin on the floor like a coin. They don't "grip" the surface.
3. **No floor material** — Unity default floor has 0.6 friction but 0 bounce. The dice PhysicsMaterial's Friction Combine is set to Maximum, so it takes the higher of the two (0.6). That's *okay* but not "wooden box with felt lining" territory.
4. **Mass 1 at that scale** — it's a 1kg 0.57-unit cube. Heavy things have more momentum and take longer to stop. Real dice are 3-5 grams.
5. **Settle detection only checks linear velocity** — a die can be spinning flat on one face (angular velocity high, linear velocity ~0) and get called "settled". Visually wrong.

## What "Good" Feels Like

Think about what happens when you throw 3 dice into a wooden board game box lid:

- They hit the wood with a **thud** (low bounce, moderate energy)
- They **tumble** 2-3 times (rotation dominant, not sliding)
- They hit the sides, lose energy immediately
- They settle within **1-2 seconds** — no spinning, no sliding
- The corners and edges **catch** on the surface (high friction when rotating)

The key insight: real dice lose energy primarily through **rotational friction** (edges catching on the surface), not linear deceleration.

## Tuning Suggestions

These aren't exact — they need testing in combination. Starting points for experimentation:

### Rigidbody (Red-die prefab)

| Parameter | Current | Try | Rationale |
|-----------|---------|-----|-----------|
| Mass | 1 | **0.03–0.1** | Lighter = less momentum = stops faster. But too light and gravity feels floaty. 0.05 is a good starting point. |
| Linear Damping | 0 | **1.0–3.0** | Simulates surface drag. Dice decelerate without needing friction alone. Start at 2. |
| Angular Damping | 0.05 | **2.0–5.0** | The big one. This kills the spin-on-floor. Start at 3. Higher = dies quicker. |
| Collision Detection | Discrete | **Continuous** | Prevents tunnelling at high speed (dice passing through walls). Minor perf cost for 5 dice. |
| Interpolate | None | **Interpolate** | Smoother visual motion between physics steps. Purely cosmetic. |

### DiceBounce PhysicsMaterial (on dice)

| Parameter | Current | Try | Rationale |
|-----------|---------|-----|-----------|
| Dynamic Friction | 0.4 | **0.6–0.8** | More grab when rolling/sliding. Corners bite more. |
| Static Friction | 0.4 | **0.7–1.0** | Stays put once stopped. Prevents slow creep. |
| Bounciness | 0.4 | **0.15–0.25** | Less ping-pong, more thud. Real dice on wood barely bounce. |
| Friction Combine | Maximum | Keep | Takes higher of two surfaces — sensible default. |
| Bounce Combine | Average | **Minimum** | Takes lower bounce of the pair — floor absorbs more. |

### Floor PhysicsMaterial (NEW — create `ArenaFloor.physicMaterial`)

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Dynamic Friction | 0.7 | Wood-like grip |
| Static Friction | 0.8 | Dice don't slide once energy drops |
| Bounciness | 0.05 | Floor absorbs — almost no bounce back |
| Friction Combine | Average | |
| Bounce Combine | Minimum | Floor always wins — it's the absorber |

Apply this to the Floor BoxCollider AND the wall BoxColliders.

### DiceRoller Script

Note: spawn point is low and the throw is **directional** (+Z into the box), not a gravity drop from above. `throwForce` is actual throw velocity, not drop height. Reducing it too much and dice won't reach the far end of the arena.

| Parameter | Current | Try | Rationale |
|-----------|---------|-----|-----------|
| Throw Force | 8 | **6–8** (keep or slight reduce) | It's a directional throw, not a drop. Needs enough to reach the box. Damping/friction handle the stopping. |
| Throw Torque | 10 | **15–25** | MORE spin, less translation. Energy goes into tumbling (looks dramatic) but friction kills it fast. |
| Settle Threshold | 0.1 | **0.05** | Tighter — only truly still counts. |

## Settle Detection Fix

Current code only checks `rb.linearVelocity.magnitude`. Should also check angular velocity (a die spinning flat on one face has low linear but high angular — it's not settled).

Add a helper:

```csharp
bool IsSettled(Rigidbody rb)
{
    return rb.linearVelocity.magnitude < settleThreshold 
        && rb.angularVelocity.magnitude < settleThreshold;
}
```

Replace in the `WaitForSettle()` loop:

```csharp
// Before:
if (rb.linearVelocity.magnitude > settleThreshold)

// After:
if (!IsSettled(rb))
```

And optionally, require N consecutive frames below threshold to prevent premature detection on a momentary pause mid-bounce:

```csharp
int stableFrames = 0;
const int requiredStableFrames = 5;

while (elapsed < settleTimeout)
{
    bool allBelowThreshold = true;
    foreach (var die in activeDice)
    {
        if (!IsSettled(die.GetComponent<Rigidbody>()))
        {
            allBelowThreshold = false;
            break;
        }
    }

    if (allBelowThreshold) stableFrames++;
    else stableFrames = 0;

    if (stableFrames >= requiredStableFrames) return; // truly settled

    elapsed += Time.deltaTime;
    await Awaitable.NextFrameAsync();
}
```

## Approach

This is all experimentation in the Editor. No right answer on paper — you'll need to:

1. Set starting values from the table above
2. Hit Play, trigger `/admin/testdice`
3. Watch, feel, adjust
4. Repeat until it looks and feels right

The biggest bang-for-buck changes (in order):
1. **Angular damping → 3** (kills the spin)
2. **Linear damping → 2** (kills the slide)
3. **Bounciness → 0.2** (kills the ping-pong)
4. **Floor PhysicsMaterial** (surface feels like wood, not glass)
5. **Throw torque up, force down** (tumble not slide)

## Cleanup

- Delete `Assets/Prefabs/Die.prefab` (old Unity cube placeholder)
- Confirm `DiceRoller`'s `dicePrefab` field references `Red-die` in the scene

---

*Discussion doc — no code changes until tested in Editor.*
