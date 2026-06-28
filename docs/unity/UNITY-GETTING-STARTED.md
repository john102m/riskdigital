# Unity — Getting Started Tutorial

Three progressive exercises: editor basics → physics (no code) → a C# scripted game.

---

## Part 1 — Editor Basics

### Install

1. Download **Unity Hub** from [unity.com/download](https://unity.com/download)
2. Sign in (free Personal license)
3. Hub → Installs → Install Editor → latest **LTS** (Unity 6 / 6000.x)
4. Modules: tick **Visual Studio Editor** (integration package — VS2026 Enterprise already installed with "Game Development with Unity" workload)

> **Our setup:** VS2026 Enterprise with the Unity workload is already installed on the Z440. Unity will auto-detect it as the external script editor. Verify in Unity: Edit → Preferences → External Tools → External Script Editor = "Microsoft Visual Studio 2026".

### Create a Project

Hub → New Project → **Universal 3D** (URP) → name it `LearnUnity` → Create.

### Editor Layout

| Panel | Purpose |
|-------|---------|
| Scene | 3D workspace — right-click drag to orbit, scroll to zoom, middle-click pan |
| Game | What the player sees (camera output) |
| Hierarchy | Tree of GameObjects in the scene |
| Inspector | Properties of selected object |
| Project | Your Assets folder on disk |
| Console | Debug.Log output, errors |

### Core Mental Model

- Everything in a scene is a **GameObject**
- GameObjects have **Components** (Transform, Renderer, Collider, your scripts)
- **Transform** = position, rotation, scale — every GameObject has one
- You build things by composing components onto empty GameObjects

### Try It

1. Hierarchy → right-click → 3D Object → **Cube**
2. Select it — Inspector shows Transform, MeshFilter, MeshRenderer, BoxCollider
3. Change Position Y to `1` — cube rises
4. Change Scale to `(2, 0.5, 2)` — now it's a flat platform
5. Add a **Sphere** (3D Object → Sphere), position it at `(0, 3, 0)`
6. Press **Play** — nothing happens (no physics yet)

### Materials & Colour

1. Project panel → right-click → Create → **Material** → name it `RedMat`
2. Inspector → Base Map colour → pick red
3. Drag `RedMat` onto the cube in Scene view — it turns red

You now know: GameObjects, Components, Transform, Materials, and the editor panels.

---

## Part 2 — Bouncing Cube (Physics, No Code)

### Setup

1. **Delete** the sphere from Part 1 (or start fresh)
2. Create a **Plane** — this is the floor (already has a MeshCollider)
3. Create a **Cube** — set Position Y to `8`

### Add Physics

1. Select the cube → Add Component → **Rigidbody**
2. Press Play — cube falls and stops on the plane. Gravity works.

### Make It Bounce

1. Project → right-click → Create → **Physics Material** → name it `Bouncy`
2. Select `Bouncy` in Inspector:
   - `Dynamic Friction`: `0.2`
   - `Static Friction`: `0.2`
   - `Bounciness`: `0.85`
   - `Bounce Combine`: `Maximum`
3. Select the Cube → Inspector → Box Collider → **Material** field → drag `Bouncy` in
4. Press Play — cube bounces repeatedly, losing a bit of height each time

### Experiment

- Set `Bounciness` to `1`, both frictions to `0` → bounces forever
- Add Rigidbody to the Plane too → both objects react
- Rotate the cube 45° on X and Z before play → chaotic tumbling bounces
- Stack multiple cubes at different heights → chain reaction

### What You Learned

- Rigidbody = object participates in physics
- Collider = object has a physical shape
- Physics Material = surface properties (bounce, friction)
- No code required for basic physical behaviour

---

## Part 3 — Spawn Shooter (C# Scripting)

A first-person shooter where you fire bouncing balls at a wall of cubes. Covers: MonoBehaviour lifecycle, input, prefabs, instantiation, collision, scoring, UI, and coroutines.

### 3.1 — Scene Setup

1. **Floor:** Plane at `(0, 0, 0)`, scale `(3, 1, 3)`
2. **Wall:** Create 12 cubes arranged in a 4×3 grid (position them manually or we'll script it in 3.3):
   - Row 1: `(−1.5, 0.5, 8)`, `(−0.5, 0.5, 8)`, `(0.5, 0.5, 8)`, `(1.5, 0.5, 8)`
   - Row 2: same X positions, Y = `1.5`
   - Row 3: same X positions, Y = `2.5`
3. **Camera:** Position at `(0, 2, −5)`, Rotation `(10, 0, 0)` — looking at the wall

### 3.2 — Projectile Prefab

1. Create a **Sphere** → name it `Projectile`
2. Scale to `(0.4, 0.4, 0.4)`
3. Add **Rigidbody** (Use Gravity: on)
4. Add the `Bouncy` physics material from Part 2 to its Sphere Collider
5. Create a blue material, apply it
6. **Drag** `Projectile` from Hierarchy into Project panel → creates a prefab
7. **Delete** the original from the scene

### 3.3 — Wall Spawner Script

Create `Scripts/WallSpawner.cs`:

```csharp
using UnityEngine;

public class WallSpawner : MonoBehaviour
{
    public GameObject brickPrefab;
    public int columns = 4;
    public int rows = 3;
    public float spacing = 1.1f;

    void Start()
    {
        float startX = -(columns - 1) * spacing / 2f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 pos = new Vector3(
                    startX + col * spacing,
                    0.5f + row * spacing,
                    8f
                );
                Instantiate(brickPrefab, pos, Quaternion.identity);
            }
        }
    }
}
```

1. Create an **empty GameObject** → name it `WallSpawner`
2. Attach the script
3. For `brickPrefab` — create a Cube prefab (with Rigidbody, mass = 0.5) and assign it
4. Delete the manual wall cubes if you placed them earlier
5. Play — wall spawns procedurally

### 3.4 — Shooter Script

Create `Scripts/Shooter.cs`:

```csharp
using UnityEngine;

public class Shooter : MonoBehaviour
{
    public GameObject projectilePrefab;
    public float fireForce = 20f;
    public Transform spawnPoint;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            Fire();
    }

    void Fire()
    {
        GameObject ball = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        rb.linearVelocity = spawnPoint.forward * fireForce;

        Destroy(ball, 5f); // cleanup after 5 seconds
    }
}
```

1. Attach to the **Main Camera**
2. Create an **empty child** of the camera at `(0, 0, 1)` → name it `SpawnPoint`
3. Assign `SpawnPoint` to the script's `spawnPoint` field
4. Assign the `Projectile` prefab
5. Play — left-click fires balls at the wall

### 3.5 — Scoring with Collision Detection

Create `Scripts/Brick.cs` — attach to the brick prefab:

```csharp
using UnityEngine;

public class Brick : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Projectile"))
        {
            ScoreManager.Instance.AddPoint();
            Destroy(gameObject);
        }
    }
}
```

**Tag the projectile:** Select Projectile prefab → Inspector → Tag dropdown → Add Tag → create `Projectile` → assign it.

### 3.6 — Score Manager (Singleton Pattern)

Create `Scripts/ScoreManager.cs`:

```csharp
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public TextMeshProUGUI scoreText;

    int score;

    void Awake()
    {
        Instance = this;
    }

    public void AddPoint()
    {
        score++;
        scoreText.text = $"Score: {score}";
    }
}
```

1. Create UI: Hierarchy → UI → **Text - TextMeshPro** (accept TMP import if prompted)
2. Position top-left, set default text to `Score: 0`
3. Create empty GO `ScoreManager`, attach the script, drag the TMP text to `scoreText`

### 3.7 — Auto-Destroy & Reset (Coroutine)

Add a reset to `WallSpawner` — press R to rebuild the wall:

```csharp
using System.Collections;
using UnityEngine;

public class WallSpawner : MonoBehaviour
{
    public GameObject brickPrefab;
    public int columns = 4;
    public int rows = 3;
    public float spacing = 1.1f;

    void Start()
    {
        SpawnWall();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            StartCoroutine(ResetWall());
    }

    IEnumerator ResetWall()
    {
        foreach (GameObject brick in GameObject.FindGameObjectsWithTag("Brick"))
            Destroy(brick);

        yield return new WaitForSeconds(0.5f); // brief pause before respawn
        SpawnWall();
        ScoreManager.Instance.ResetScore();
    }

    void SpawnWall()
    {
        float startX = -(columns - 1) * spacing / 2f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 pos = new Vector3(
                    startX + col * spacing,
                    0.5f + row * spacing,
                    8f
                );
                GameObject brick = Instantiate(brickPrefab, pos, Quaternion.identity);
                brick.tag = "Brick";
            }
        }
    }
}
```

Add `ResetScore()` to ScoreManager:

```csharp
public void ResetScore()
{
    score = 0;
    scoreText.text = "Score: 0";
}
```

**Create the `Brick` tag** the same way you created `Projectile`.

### 3.8 — Polish Ideas (Optional)

- Add `AudioSource.PlayOneShot()` on brick destruction
- Particle effect on impact (`Instantiate` a particle prefab at collision point)
- Limited ammo — track shots, display on UI
- Timer — `Time.time` countdown, game over when it expires
- Mouse aim — `Camera.main.ScreenPointToRay(Input.mousePosition)` for aimed shots

---

## Key Concepts Covered

| Concept | Where |
|---------|-------|
| Editor navigation | Part 1 |
| GameObjects & Components | Part 1 |
| Materials | Part 1 |
| Rigidbody & physics | Part 2 |
| Physics Materials | Part 2 |
| MonoBehaviour lifecycle | Part 3.3 |
| Input handling | Part 3.4 |
| Prefabs & Instantiate | Part 3.2, 3.4 |
| Destroy | Part 3.4, 3.5 |
| Collision detection | Part 3.5 |
| Tags | Part 3.5 |
| Singleton pattern | Part 3.6 |
| UI (TextMeshPro) | Part 3.6 |
| Coroutines | Part 3.7 |
| FindGameObjectsWithTag | Part 3.7 |

## What's Next

- **Character controller** — first-person movement with `CharacterController` component
- **Raycasting** — `Physics.Raycast` for hitscan weapons, line-of-sight
- **Animation** — Animator controller, state machines, blend trees
- **ScriptableObjects** — data containers for game config (weapon stats, level data)
- **Addressables/Asset Bundles** — async loading for larger projects
- **Multiplayer** — Netcode for GameObjects (Unity's networking), or Mirror (community)

---

*Unity uses C# but not .NET 8 — it's .NET Standard 2.1 / CoreCLR under the hood. Most language features work (pattern matching, records, nullable refs) but BCL APIs differ from what you'd use in ASP.NET Core.*

---

## Risk TV App — What You Actually Need

The tutorial above teaches the editor, physics, and 3D scripting. The Risk TV app is much simpler — it's a **2D display application**, not a game engine project. Here's what matters:

### Relevant Concepts (from the tutorial)

| Concept | Risk Usage |
|---------|-----------|
| GameObjects & Components | Each territory is a GO with SpriteRenderer + TextMeshPro |
| Prefabs & Instantiate | Territory prefab, dice prefab, army token |
| MonoBehaviour lifecycle | `Start()` to connect SignalR, `Update()` for animations |
| Singleton pattern | GameStateManager receives and distributes server state |
| Tags | Territory lookup by id |
| Coroutines | Animation sequencing (dice → result → troop move) |

### Things you WON'T need

- Physics / Rigidbody / Colliders (no player interaction on TV)
- Character controllers
- Raycasting / input handling (TV is read-only display)
- Navigation / pathfinding
- 3D anything

### Project Type

Create as **2D (URP)** not 3D. This gives you:
- Default orthographic camera (top-down, no perspective)
- 2D sprite workflow in the editor
- Simpler rendering pipeline for Fire Stick hardware

### The Map — Practical Approach

1. **Source art:** Find a public domain Risk-style SVG world map (Wikimedia Commons has several)
2. **Inkscape:** Separate each territory into its own layer/path, name them by territory id
3. **Export:** Individual PNGs per territory (transparent background), or a sprite atlas
4. **Unity import:** Import as sprites, position them to form the world map
5. **Runtime:** `SpriteRenderer.color = playerColour` to tint ownership

Each territory GameObject has:
- `SpriteRenderer` — the territory shape, tinted by owner colour
- `TextMeshPro` (child GO) — army count number, positioned at territory centroid
- A script component with the territory id for lookup

### SignalR in Unity — Setup

The `Microsoft.AspNetCore.SignalR.Client` NuGet works in Unity but has caveats for Android/IL2CPP builds:

**Option A — Official NuGet (recommended to start):**
```
// Install via NuGetForUnity package manager
Microsoft.AspNetCore.SignalR.Client 8.x
```

**Key gotchas:**
- Add a `link.xml` in Assets to prevent IL2CPP from stripping SignalR types
- Use **JSON protocol only** (not MessagePack — relies on reflection that IL2CPP breaks)
- Test on-device (Fire Stick) early — don't rely only on Play mode in editor
- Unity's main thread rule: SignalR callbacks arrive on background threads, use `UnityMainThreadDispatcher` or queue updates

**link.xml (required for Android build):**
```xml
<linker>
  <assembly fullname="Microsoft.AspNetCore.SignalR.Client" preserve="all"/>
  <assembly fullname="Microsoft.AspNetCore.SignalR.Client.Core" preserve="all"/>
  <assembly fullname="Microsoft.Extensions.Logging" preserve="all"/>
  <assembly fullname="System.Text.Json" preserve="all"/>
</linker>
```

**Option B — Community wrapper:**
[`unity-signalr`](https://github.com/nicknsy/unity-signalr) wraps the official client with Unity lifecycle awareness. Evaluate if Option A causes friction.

### Minimum Viable Spike

Before building the full map, prove the architecture with a tiny test:

1. New Unity 2D project → `RiskTV`
2. Create 4 coloured square sprites (representing 4 territories)
3. Add a TextMeshPro child to each (army count)
4. Write `SignalRClient.cs` — connect to your server's `/gamehub`, listen for `GameStateUpdated`
5. Write `MapController.cs` — on state update, set each territory's colour and army text
6. Build to Android → sideload to Fire Stick → confirm it connects and updates

If this works, you've validated the entire pipeline. Everything after is just more territories and nicer animations.

### Animation Sequencing Pattern

Same concept as the Flutter TV's `OverlayCardQueue` — but in Unity terms:

```csharp
// Coroutine-based sequencing
IEnumerator PlayCombatSequence(CombatResult result)
{
    yield return StartCoroutine(AnimateDiceRoll(result.AttackerDice, result.DefenderDice));
    yield return new WaitForSeconds(0.5f);
    yield return StartCoroutine(ShowResult(result.Losses));

    if (result.Captured)
    {
        yield return StartCoroutine(AnimateCapture(result.TerritoryId, result.NewOwnerId));
    }

    // Now safe to process next queued event
    ProcessNextEvent();
}
```

Buffer incoming SignalR events during animation, process them sequentially. Same pattern you already understand from the Kotlin TV app.

### Fire Stick Build Settings

- **Platform:** Android
- **Texture compression:** ASTC (Fire Stick 4K Max supports it)
- **Target API:** 34
- **Min API:** 21 (Fire OS is Android-based)
- **Scripting backend:** IL2CPP (required for ARM64)
- **Architecture:** ARM64
- **Resolution:** 1920×1080 fixed (no scaling needed — Fire Stick always outputs 1080p to TV)

### DOTween (Animation Library)

Unity's built-in animation system (Animator/AnimationController) is overkill for procedural UI animations. **DOTween** is the standard choice:

```csharp
// Move a troop token from one territory to another
transform.DOMove(targetPosition, 0.8f).SetEase(Ease.InOutQuad);

// Fade in a territory capture flash
spriteRenderer.DOColor(flashColour, 0.2f).SetLoops(3, LoopType.Yoyo);

// Dice spin
transform.DORotate(new Vector3(0, 0, 360), 0.5f, RotateMode.FastBeyond360)
    .SetLoops(6)
    .OnComplete(() => ShowFinalFace(value));
```

Install via Unity Package Manager (free version is fine). Much simpler than keyframing everything.

### Development Workflow

| Tool | Purpose |
|------|---------|
| Unity Editor | Scene layout, running the app, asset management |
| VS2022 | C# script editing + debugging (attach to Unity process) |
| ADB | Deploy to Fire Stick (`adb install -r risk-tv.apk`) |

Edit in VS2022 → save → Unity auto-recompiles → press Play to test. Same as described in the dev tooling section of RISK-DESIGN.md.

### .gitignore for Unity Project

```
Library/
Temp/
Logs/
obj/
Builds/
UserSettings/
*.csproj
*.sln
```

Commit: `Assets/`, `Packages/`, `ProjectSettings/` only.
