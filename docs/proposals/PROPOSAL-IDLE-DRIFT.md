# Proposal: Idle Drift Camera During Thinking Time

## Problem
When a player takes time to think (especially during Reinforce and Fortify), the Unity TV board sits completely static. Looks frozen. The post-game drift already proves gentle camera movement gives the board a premium "living" feel.

## Proposed Behaviour

After **15 seconds** of no game state change, the camera starts a slow drift across the board. Any action (reinforce, attack, fortify, turn change) immediately stops the drift and resets the timer.

## Rules

| Condition | Drift? | Reason |
|-----------|--------|--------|
| Zoomed out + idle 15s | ✅ Yes | Board is static, drift adds life |
| Zoomed in (combat) + idle | ❌ No | Camera is focused on action area |
| Post-game over | ✅ Already works | Existing `StartDrift()` |
| During dice roll | ❌ No | Camera on flypath |
| Lobby / placement (rapid actions) | ✅ If idle long enough | Same 15s rule applies |

## Implementation

### BoardCamera.cs

Add idle timer logic:

```csharp
[Header("Idle Drift")]
[Tooltip("Seconds of inactivity before drift begins")]
public float idleThreshold = 15f;

float idleTimer;
bool idleDrifting;

// Called externally when any game activity occurs
public void ResetIdle()
{
    idleTimer = 0f;
    if (idleDrifting)
    {
        idleDrifting = false;
        drifting = false;
        // Lerp back to default (existing targetPosition logic handles this)
        targetPosition = defaultPosition;
        targetSize = defaultSize;
    }
}

// In Update():
void Update()
{
    // Existing lerp logic...

    // Idle detection (only when zoomed out)
    if (!drifting && Mathf.Approximately(targetSize, defaultSize))
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleThreshold)
        {
            idleDrifting = true;
            StartDrift();
        }
    }
}
```

### GameStateManager.cs or UIOverlay.cs

On every `OnStateChanged`:

```csharp
boardCamera.ResetIdle();
```

This covers all game events — reinforce, attack, fortify, turn change, card trade. One line.

### Drift behaviour (already exists)

The current drift uses sine/cosine for organic movement:
```csharp
float offsetX = Mathf.Sin(driftTime) * driftAmplitudeX;
float offsetY = Mathf.Cos(driftTime * 0.7f) * driftAmplitudeY;
```

Same gentle motion reused. No new animation code needed.

## What changes

| File | Change |
|------|--------|
| `BoardCamera.cs` | Add `idleThreshold`, `idleTimer`, `idleDrifting`, `ResetIdle()`, idle check in `Update()` |
| `UIOverlay.cs` (or `GameStateManager.cs`) | Call `boardCamera.ResetIdle()` on state change |

## Inspector Fields

- **Idle Threshold** — default 15s. Tunable. 10s for impatient, 20s for relaxed.
- Existing drift amplitude/speed fields already exposed — no new tuning needed.

## Edge Cases

- **Rapid actions reset continuously** — timer never reaches threshold during active play. Correct.
- **Bot turns** — bots act within 2–3s, timer never triggers. Correct.
- **Human vs human defend prompt** — defender thinking for 15s+ triggers drift. Camera will smoothly snap back when they roll. Nice.
- **Drift during zoom** — guarded by `Mathf.Approximately(targetSize, defaultSize)` check. Won't drift while zoomed in.

## Scope
~15 lines of code total. No new dependencies. Inspector-tunable threshold.
