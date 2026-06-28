# Unity — Spawn Shooter Tutorial

Picks up after Part 2 (Bouncing Cube). You already know: GameObjects, Components, Transform, Materials, Rigidbody, Physics Materials, and the editor panels.

This builds a first-person ball shooter that knocks down a wall of cubes — with scoring, reset, and the key scripting concepts you'll reuse in the Risk TV app.

---

## 1 — Scene Setup

1. **New scene** (or clear out your Part 2 objects)
2. **Floor:** Hierarchy → 3D Object → Plane. Position `(0, 0, 0)`, Scale `(3, 1, 3)`
3. **Camera:** Select Main Camera → Position `(0, 2, -5)`, Rotation `(10, 0, 0)`
4. **Lighting:** default directional light is fine

Don't place wall cubes manually — a script will spawn them.

---

## 2 — Create the Projectile Prefab

1. Hierarchy → 3D Object → **Sphere** → rename to `Projectile`
2. Inspector → Transform → Scale: `(0.4, 0.4, 0.4)`
3. Add Component → **Rigidbody** (Use Gravity: ✓)
4. Apply the `Bouncy` Physics Material from Part 2 to the Sphere Collider's Material field
   - (If you don't have it: Project → Create → Physics Material, Bounciness `0.85`, Bounce Combine `Maximum`)
5. Create a blue Material (Project → Create → Material → Base Map colour → blue) → drag onto the sphere
6. **Tag it:** Inspector → Tag dropdown → Add Tag → `+` → type `Projectile` → Save → select the sphere again → Tag → choose `Projectile`
7. **Make it a prefab:** Drag `Projectile` from Hierarchy into the Project panel (Assets folder or a `Prefabs` subfolder)
8. Delete the original from the scene (it only needs to exist as a prefab now)

---

## 3 — Create the Brick Prefab

1. Hierarchy → 3D Object → **Cube** → rename to `Brick`
2. Add Component → **Rigidbody** → set Mass to `0.5`
3. **Tag it:** Add Tag → `Brick` → assign it (same process as above)
4. Optionally give it a coloured material so it's not default white
5. Drag into Project panel to create the prefab
6. Delete the original from the scene

---

## 4 — Scripts

Create a `Scripts` folder in the Project panel (right-click → Create → Folder).

### 4.1 — WallSpawner.cs

Right-click Scripts folder → Create → C# Script → name it `WallSpawner`. Double-click to open in VS2022.

```csharp
using System.Collections;
using UnityEngine;

public class WallSpawner : MonoBehaviour
{
    public GameObject brickPrefab;
    public int columns = 4;
    public int rows = 3;
    public float spacing = 1.1f;

    void Start() => SpawnWall();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            StartCoroutine(ResetWall());
    }

    IEnumerator ResetWall()
    {
        foreach (var brick in GameObject.FindGameObjectsWithTag("Brick"))
            Destroy(brick);
        yield return new WaitForSeconds(0.5f);
        SpawnWall();
        ScoreManager.Instance.ResetScore();
    }

    void SpawnWall()
    {
        float startX = -(columns - 1) * spacing / 2f;
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                var pos = new Vector3(startX + col * spacing, 0.5f + row * spacing, 8f);
                Instantiate(brickPrefab, pos, Quaternion.identity).tag = "Brick";
            }
    }
}
```

**What it does:** Spawns a 4×3 grid of brick cubes at `z=8`. Press R to destroy them all and respawn after a brief pause (coroutine).

### 4.2 — Shooter.cs

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
        {
            var ball = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);
            ball.GetComponent<Rigidbody>().linearVelocity = spawnPoint.forward * fireForce;
            Destroy(ball, 5f);
        }
    }
}
```

**What it does:** Left-click spawns a projectile and launches it forward. Auto-destroys after 5 seconds to avoid clutter.

### 4.3 — Brick.cs

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

**What it does:** When a projectile hits a brick, increment score and destroy the brick.

### 4.4 — ScoreManager.cs

```csharp
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public TextMeshProUGUI scoreText;
    int score;

    void Awake() => Instance = this;

    public void AddPoint()
    {
        score++;
        scoreText.text = $"Score: {score}";
    }

    public void ResetScore()
    {
        score = 0;
        scoreText.text = "Score: 0";
    }
}
```

**What it does:** Singleton that any script can call to update the score. Drives a UI text element.

---

## 5 — Wire Everything Up

Save all scripts in VS2022 and switch back to Unity (it auto-compiles).

### 5.1 — WallSpawner

1. Hierarchy → Create Empty → rename to `WallSpawner`
2. Add Component → WallSpawner (your script)
3. Inspector → drag the **Brick prefab** from Project panel into the `Brick Prefab` field

### 5.2 — Shooter (on the Camera)

1. Select **Main Camera** in Hierarchy
2. Add Component → Shooter
3. Create an **empty child** of the camera: right-click Main Camera → Create Empty → rename to `SpawnPoint`
4. Set SpawnPoint position to `(0, 0, 1)` — one unit in front of camera
5. Back on Main Camera's Shooter component:
   - Drag `SpawnPoint` into the `Spawn Point` field
   - Drag the **Projectile prefab** into the `Projectile Prefab` field

### 5.3 — Brick Script on the Prefab

1. In Project panel, **double-click the Brick prefab** to open it in isolation
2. Add Component → Brick (your script)
3. Click the `←` arrow (top-left of Scene view) to exit prefab editing mode

### 5.4 — Score UI

1. Hierarchy → UI → **Text - TextMeshPro**
   - First time? Accept the "Import TMP Essentials" dialog
2. Select the new text object in Hierarchy (inside a Canvas)
3. Inspector:
   - Text: `Score: 0`
   - Font Size: `36`
   - Rect Transform → anchor top-left, position `(120, -40, 0)`
4. Rename it to `ScoreText`

### 5.5 — ScoreManager

1. Hierarchy → Create Empty → rename to `ScoreManager`
2. Add Component → ScoreManager (your script)
3. Drag `ScoreText` from Hierarchy into the `Score Text` field

---

## 6 — Play!

Press **Play** (▶ at the top). You should see:

- A 4×3 wall of cubes at the far end
- Left-click fires blue balls
- Balls that hit bricks → brick disappears, score increments
- Press **R** → wall resets, score resets

### Troubleshooting

| Problem | Fix |
|---------|-----|
| Balls fire but don't hit anything | Check SpawnPoint's Z is positive (firing forward, not backward) |
| Bricks don't disappear | Confirm the Projectile prefab has the `Projectile` tag assigned |
| Score doesn't update | Confirm ScoreManager GO exists and has the TMP text assigned |
| "NullReferenceException" on ScoreManager.Instance | ScoreManager GO must exist in the scene and have the script attached |
| Balls go through bricks | Both need colliders (Cube has BoxCollider by default, Sphere has SphereCollider by default — don't remove them) |

---

## 7 — Key Concepts You Just Learned

| Concept | How It Was Used |
|---------|-----------------|
| **Prefabs** | Projectile and Brick — template objects you Instantiate at runtime |
| **Instantiate** | Spawning balls and wall cubes from prefabs |
| **Destroy** | Removing bricks on hit, auto-cleaning projectiles after 5s |
| **Tags** | Identifying projectiles in collision checks, finding all bricks for reset |
| **MonoBehaviour lifecycle** | `Awake` (singleton init), `Start` (spawn wall), `Update` (input polling) |
| **Input** | `GetMouseButtonDown(0)` for fire, `GetKeyDown(KeyCode.R)` for reset |
| **Collision detection** | `OnCollisionEnter` — fires when two Rigidbody+Collider objects touch |
| **Coroutines** | `IEnumerator` + `yield return` for timed sequences (reset pause) |
| **Singleton pattern** | `ScoreManager.Instance` — global access point, one instance |
| **UI (TextMeshPro)** | Displaying score on-screen via Canvas system |
| **Rigidbody.linearVelocity** | Giving the projectile its initial speed and direction |

---

## 8 — Optional Polish

Ideas if you want to keep experimenting:

- **Sound:** Add an `AudioSource` to the Brick prefab. In `Brick.cs`, call `AudioSource.PlayClipAtPoint(clip, transform.position)` before destroying
- **Particles:** Create a particle system prefab → `Instantiate` it at the collision point on brick destruction
- **Limited ammo:** Track shot count in Shooter, display on UI, disable firing at zero
- **Timer:** Countdown using `Time.time`, game over when it expires
- **Aimed shots:** Replace `spawnPoint.forward` with `Camera.main.ScreenPointToRay(Input.mousePosition).direction` for mouse-aimed firing

---

## What's Next

This tutorial covered the scripting fundamentals. For the **Risk TV app**, you'll reuse:

- **Prefabs + Instantiate** → territory tokens, dice overlays
- **Singleton pattern** → GameStateManager receiving SignalR updates
- **Coroutines** → animation sequencing (dice roll → result → troop move)
- **UI (TextMeshPro)** → army counts on territories, info displays

You won't need: physics, input handling, collision detection, or Rigidbody (the TV app is a read-only display).

Next step: [UNITY-GETTING-STARTED.md → "Risk TV App — What You Actually Need"](UNITY-GETTING-STARTED.md) section for the SignalR spike.
