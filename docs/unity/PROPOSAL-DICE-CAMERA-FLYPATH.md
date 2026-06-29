# Proposal: Dice Camera Flypath (Catmull-Rom)

## What
Add a cinematic camera flypath during dice rolls — the DiceCamera follows a Catmull-Rom spline through waypoints while dice tumble, giving a drone-footage feel.

## Why
Currently the dice camera is static. A moving camera adds drama and spectacle — the whole point of the Unity board over the web board.

## Where
- **New script:** `Assets/Scripts/CameraFlypath.cs`
- **Modified:** `CombatTheatre.cs` — fire the flypath alongside the dice roll
- **Scene:** 4–6 empty GameObjects as waypoints under/near DiceArena

## How It Works

### Catmull-Rom Spline
Same maths as Three.js `CatmullRomCurve3`. The path passes through all waypoints (unlike Bézier where control points are off-path). Provides smooth continuous curves with no sharp transitions.

### CameraFlypath.cs

```csharp
using System.Threading;
using UnityEngine;

/// <summary>
/// Flies a camera along a Catmull-Rom spline defined by waypoints.
/// Designed to run concurrently with dice physics simulation.
/// </summary>
public class CameraFlypath : MonoBehaviour
{
    [Tooltip("Ordered waypoints — camera passes through each one")]
    public Transform[] waypoints;

    [Tooltip("What the camera looks at during flight")]
    public Transform lookTarget;

    [Tooltip("Total flight duration in seconds")]
    public float duration = 2.5f;

    /// <summary>
    /// Fly the given camera transform along the spline. Cancellable.
    /// </summary>
    public async Awaitable Fly(Transform cam, CancellationToken ct)
    {
        int count = waypoints.Length;
        if (count < 4) return;

        float elapsed = 0f;
        while (elapsed < duration && !ct.IsCancellationRequested)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // smoothstep ease-in-out

            // Map t (0–1) to spline segment
            float scaled = t * (count - 3);
            int seg = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, count - 4);
            float segT = scaled - seg;

            cam.position = CatmullRom(
                waypoints[seg].position,
                waypoints[seg + 1].position,
                waypoints[seg + 2].position,
                waypoints[seg + 3].position,
                segT);

            if (lookTarget != null)
                cam.LookAt(lookTarget);

            elapsed += Time.deltaTime;
            await Awaitable.NextFrameAsync();
        }
    }

    /// <summary>
    /// Catmull-Rom interpolation between p1 and p2, using p0/p3 as tangent influences.
    /// </summary>
    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}
```

### CombatTheatre Integration

In `PlayCombatSequence`, fire the flypath alongside the dice roll:

```csharp
[SerializeField] CameraFlypath cameraFlypath;

// Inside PlayCombatSequence, after showing dice panel:
var flyCts = new CancellationTokenSource();
_ = cameraFlypath.Fly(diceCamera.transform, flyCts.Token); // runs in parallel
await diceRoller.RollDice(attackerValues, defenderValues);
flyCts.Cancel(); // stop fly when dice settle + result shown
```

### Waypoint Setup (Scene)

Create empty GameObjects positioned in the scene. Example path for a side-sweep:

```
CamPath_0 — Behind throw (near spawn point, low angle, z ≈ -6)
CamPath_1 — Sweep out to the left/right (x offset, rising slightly)
CamPath_2 — Arc toward where dice settle (centre of box, higher)
CamPath_3 — Close-up overhead for result (above centre, looking down)
CamPath_4 — (Optional) slight pull-back for the 1.5s result hold
```

Drag them in Scene view until the preview looks right. The spline flows smoothly through all points.

### LookTarget

An empty GameObject positioned at the centre of the arena floor. The camera always faces this point during flight, keeping the action framed regardless of camera position.

## Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Spline type | Catmull-Rom | Passes through points (intuitive), familiar from Three.js |
| Easing | Smoothstep | Cinematic accel/decel without extra dependencies |
| LookAt | Fixed target | Keeps dice in frame regardless of camera path |
| Cancellation | CancellationToken | Clean stop when dice settle, matches existing async pattern |
| Separate script | CameraFlypath.cs | Reusable, keeps CombatTheatre clean |

## Future Enhancements
- Multiple paths (random selection per roll for variety)
- Rotation waypoints (instead of LookAt — for specific dramatic angles)
- Speed curve (slow during throw, fast during bounce, slow for settle)
- Shake/handheld wobble overlay for realism

## Implementation Steps
1. Create `CameraFlypath.cs` in `Assets/Scripts/`
2. Add 4–5 empty waypoint GameObjects in the scene near DiceArena
3. Create a LookTarget empty at the arena floor centre
4. Add CameraFlypath component to DiceArena (or a dedicated object)
5. Wire waypoints + lookTarget in Inspector
6. Add `[SerializeField] CameraFlypath cameraFlypath` to CombatTheatre
7. Fire `Fly()` in parallel with `RollDice()`, cancel after
8. Tweak waypoint positions and duration until it feels good
