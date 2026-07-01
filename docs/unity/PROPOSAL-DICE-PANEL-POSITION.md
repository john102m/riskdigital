# Proposal — Dynamic Dice Panel Positioning

## Goal

Move the dice panel (RawImage) to avoid overlapping the attacking and defending territories. Three fixed positions: **left**, **right**, **centre**.

## Current Setup

- `DicePanel` is a RawImage under Canvas, currently full-screen width with transparent background.
- The dice arena + camera flypath renders through a RenderTexture into this RawImage.
- Territories have known screen positions (percentage-based COORDS in `BoardRenderer.cs`).

## Proposed Behaviour

| Source & Target Position | Panel Position |
|--------------------------|---------------|
| Both on the **right** half of map | **Left** |
| Both on the **left** half of map | **Right** |
| Spanning both sides (e.g. Kamchatka vs Alaska) | **Centre** |

"Half" = territory X coordinate above/below 50%.

## Implementation

### 1. Shrink the RawImage

Reduce from full-screen to roughly **40–50% width** and a height that frames the arena box. This needs manual tweaking after the camera FOV is tightened to fill the render texture with the arena.

### 2. Three anchor positions (Inspector fields)

On `CombatTheatre.cs`:

```csharp
[Header("Panel Positioning")]
[Tooltip("Panel anchored position when placed on the left")]
public Vector2 panelPosLeft = new Vector2(50f, -50f);   // offset from top-left

[Tooltip("Panel anchored position when placed on the right")]
public Vector2 panelPosRight = new Vector2(-50f, -50f); // offset from top-right

[Tooltip("Panel anchored position when centred")]
public Vector2 panelPosCentre = new Vector2(0f, -50f);  // offset from top-centre
```

These are RectTransform `anchoredPosition` values. Exact numbers TBD in Inspector — these are starting points.

### 3. Positioning logic

Add to `CombatTheatre.cs`:

```csharp
RectTransform panelRect;

void Start()
{
    panelRect = dicePanelUI.GetComponent<RectTransform>();
    // ... existing setup
}

void PositionPanel(int sourceId, int targetId)
{
    // Territory X coords are percentages (0–100) from BoardRenderer.COORDS
    float sourceX = BoardRenderer.GetTerritoryX(sourceId);
    float targetX = BoardRenderer.GetTerritoryX(targetId);

    bool sourceLeft = sourceX < 50f;
    bool targetLeft = targetX < 50f;

    if (sourceLeft && targetLeft)
    {
        // Action on left → panel right
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = panelPosRight;
    }
    else if (!sourceLeft && !targetLeft)
    {
        // Action on right → panel left
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = panelPosLeft;
    }
    else
    {
        // Spanning both halves → panel centre
        panelRect.anchorMin = new Vector2(0.5f, 1);
        panelRect.anchorMax = new Vector2(0.5f, 1);
        panelRect.pivot = new Vector2(0.5f, 1);
        panelRect.anchoredPosition = panelPosCentre;
    }
}
```

### 4. Expose territory X coords from BoardRenderer

Add a public static helper:

```csharp
// In BoardRenderer.cs
public static float GetTerritoryX(int id)
{
    if (id < 0 || id >= 42) return 50f;
    return COORDS[id].x;
}
```

### 5. Call PositionPanel when combat starts

In `CombatTheatre.cs`, call `PositionPanel(sourceId, targetId)` from:
- `OnSpawnDice` — when role is "attacker", use the current attack selection IDs
- `OnCombatRollRequest` — has sourceId/targetId directly
- `ShowBlitzDice` — has sourceId/targetId in the DTO

This means CombatTheatre needs to track the current sourceId/targetId from `OnAttackSelection`:

```csharp
int currentSourceId = -1;
int currentTargetId = -1;

void OnAttackSelection(int sourceId, int targetId)
{
    currentSourceId = sourceId;
    currentTargetId = targetId;
}
```

Then in `EnterWaitingForDice()`:

```csharp
void EnterWaitingForDice()
{
    hideCts?.Cancel();
    diceRoller.ClearDice();
    PositionPanel(currentSourceId, currentTargetId);
    ShowPanel(true);
    // ... rest unchanged
}
```

## Camera/Arena Considerations

Before implementing, you'll need to:
1. **Tighten the arena camera** — reduce FOV or move closer so the box fills the render texture. Currently with full-screen panel the framing is loose.
2. **Set RawImage size** — decide on the pixel dimensions (e.g. 700×400 on a 1920×1080 canvas).
3. **Test the flypath** — make sure the drone footage doesn't clip out of frame at the new tighter zoom.

The flypath waypoints may need pulling in slightly so the camera stays pointed at the box throughout.

## Steps

1. Tighten arena camera (FOV / position) so box fills render texture
2. Shrink RawImage to desired size
3. Verify flypath still looks good at new framing
4. Add `GetTerritoryX()` to BoardRenderer
5. Add panel positioning fields + logic to CombatTheatre
6. Test with attacks on left, right, and cross-map

---

*Proposal — discuss and tweak values in Inspector before finalising.*
