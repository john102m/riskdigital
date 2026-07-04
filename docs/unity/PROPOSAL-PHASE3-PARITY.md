# Proposal — Phase 3: Parity with Web Board

## Overview

Add three missing features to bring the Unity TV board in line with `tv.html`:
1. Turn popup (central screen overlay)
2. Game over / winner announcement
3. Fortify token pulse animation

The activity feed, card trade, and elimination lines are already implemented in `UIOverlay.cs`.

---

## 1. Turn Popup

### What tv.html does
- On `TurnStarted` event: shows a centred popup with player name + colour dot
- Auto-dismisses after 2.5s with a fade-out
- Also triggers during InitialPlacement when player changes (detected via state diff)

### What's needed

**SignalRClient.cs** — add `TurnStarted` event:
```csharp
public event Action<int> OnTurnStarted;

// In Start(), add handler:
connection.On<int>("TurnStarted", (playerIndex) =>
{
    UnityMainThread.Enqueue(() => OnTurnStarted?.Invoke(playerIndex));
});
```

**UIOverlay.cs** — add central popup system:
```csharp
// New fields
TextMeshProUGUI popupText;
GameObject popupGO;
CancellationTokenSource popupCts;

// In Start(), subscribe:
signalR.OnTurnStarted += OnTurnStarted;

// New methods:
void OnTurnStarted(int playerIndex)
{
    var state = GameStateManager.Instance.State;
    if (state == null || playerIndex < 0 || playerIndex >= state.players.Count) return;
    var player = state.players[playerIndex];
    ShowPopup($"<color={player.colour}>\u25cf</color> {player.name}'s turn", 2.5f);
    // Clear activity feed on new turn (matches tv.html)
    activityLines.Clear();
    feedText.text = "";
}

async void ShowPopup(string text, float duration)
{
    // See 3D popup implementation in BuildUI section above
}
```

**BuildUI()** — add popup panel with 3D feel:

The popup uses a world-space Canvas with a slight Z-rotation and scale animation to feel like a physical card/plaque dropping in front of the camera, rather than flat HUD text.

```csharp
// Central popup — world-space canvas for 3D presence
popupCanvasGO = new GameObject("PopupCanvas");
popupCanvasGO.transform.SetParent(transform); // scene root, not HUD canvas
var popupCanvas = popupCanvasGO.AddComponent<Canvas>();
popupCanvas.renderMode = RenderMode.WorldSpace;
popupCanvas.sortingOrder = 200;
var popupCanvasRect = popupCanvasGO.GetComponent<RectTransform>();
popupCanvasRect.sizeDelta = new Vector2(600, 100);

// Position in front of main camera
var cam = Camera.main;
popupCanvasGO.transform.position = cam.transform.position + cam.transform.forward * 5f;
popupCanvasGO.transform.rotation = cam.transform.rotation;

// Background panel — dark with rounded-corner feel, slight bevel via shadow
popupGO = new GameObject("PopupPanel");
popupGO.transform.SetParent(popupCanvasGO.transform, false);
var popupRect = popupGO.AddComponent<RectTransform>();
popupRect.anchorMin = Vector2.zero;
popupRect.anchorMax = Vector2.one;
popupRect.offsetMin = Vector2.zero;
popupRect.offsetMax = Vector2.zero;
var popupBg = popupGO.AddComponent<Image>();
popupBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

// Drop shadow (slightly offset darker panel behind)
var shadowGO = new GameObject("Shadow");
shadowGO.transform.SetParent(popupCanvasGO.transform, false);
var shadowRect = shadowGO.AddComponent<RectTransform>();
shadowRect.anchorMin = Vector2.zero;
shadowRect.anchorMax = Vector2.one;
shadowRect.offsetMin = new Vector2(4, -4);
shadowRect.offsetMax = new Vector2(4, -4);
shadowGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
shadowGO.transform.SetSiblingIndex(0); // behind main panel

// Text
var popupTextGO = new GameObject("PopupText");
popupTextGO.transform.SetParent(popupGO.transform, false);
var ptRect = popupTextGO.AddComponent<RectTransform>();
ptRect.anchorMin = Vector2.zero;
ptRect.anchorMax = Vector2.one;
ptRect.offsetMin = new Vector2(16, 0);
ptRect.offsetMax = new Vector2(-16, 0);
popupText = popupTextGO.AddComponent<TextMeshProUGUI>();
popupText.fontSize = 36f;
popupText.alignment = TextAlignmentOptions.Center;
popupText.color = Color.white;
popupText.richText = true;

popupCanvasGO.SetActive(false);
```

**ShowPopup animation** — 3D entrance/exit:
```csharp
async void ShowPopup(string text, float duration)
{
    popupCts?.Cancel();
    popupCts = new CancellationTokenSource();
    var ct = popupCts.Token;

    // Position in front of camera
    var cam = Camera.main;
    popupCanvasGO.transform.position = cam.transform.position + cam.transform.forward * 5f;
    popupCanvasGO.transform.rotation = cam.transform.rotation;

    popupCanvasGO.SetActive(true);
    popupText.text = text;

    // Entrance: scale from 0.3 → 1.0 with slight overshoot, fade in
    float enterTime = 0.35f;
    float elapsed = 0f;
    while (elapsed < enterTime && !ct.IsCancellationRequested)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / enterTime;
        // Overshoot ease: goes to ~1.1 then settles to 1.0
        float scale = 1f + (1f - t) * -0.7f + Mathf.Sin(t * Mathf.PI) * 0.1f;
        popupCanvasGO.transform.localScale = Vector3.one * Mathf.Max(scale, 0f);
        popupText.alpha = Mathf.Clamp01(t * 2f);
        await Awaitable.NextFrameAsync();
    }
    if (ct.IsCancellationRequested) return;
    popupCanvasGO.transform.localScale = Vector3.one;
    popupText.alpha = 1f;

    // Hold
    await Awaitable.WaitForSecondsAsync(duration);
    if (ct.IsCancellationRequested) return;

    // Exit: scale down + fade out + slight Y rotation (like flipping away)
    float exitTime = 0.5f;
    elapsed = 0f;
    Quaternion startRot = popupCanvasGO.transform.rotation;
    while (elapsed < exitTime && !ct.IsCancellationRequested)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / exitTime;
        popupCanvasGO.transform.localScale = Vector3.one * (1f - t * 0.3f);
        popupCanvasGO.transform.rotation = startRot * Quaternion.Euler(t * 15f, 0, 0);
        popupText.alpha = 1f - t;
        await Awaitable.NextFrameAsync();
    }

    popupCanvasGO.SetActive(false);
}
```

This gives:
- **Entrance:** scales up from small with a slight bounce — feels like a card popping toward you
- **Hold:** static in front of camera
- **Exit:** shrinks slightly + tilts on X-axis (like flipping down/away) + fades

Lightweight, no extra meshes, but feels physical rather than flat HUD.

---

## 2. Game Over / Winner Announcement

### What tv.html does
- On `MissionComplete` event: stores winner info
- When state.phase == "GameOver": shows persistent overlay with winner name + mission description
- On domination (own all 42): detected via state

### What's needed

**SignalRClient.cs** — add `MissionComplete` event:
```csharp
public event Action<int, string> OnMissionComplete;

// In Start():
connection.On<int, string>("MissionComplete", (playerIndex, description) =>
{
    UnityMainThread.Enqueue(() => OnMissionComplete?.Invoke(playerIndex, description));
});
```

**UIOverlay.cs** — handle game over:
```csharp
// In Start():
signalR.OnMissionComplete += OnMissionComplete;

void OnMissionComplete(int playerIndex, string description)
{
    var state = GameStateManager.Instance.State;
    if (state == null || playerIndex < 0 || playerIndex >= state.players.Count) return;
    var player = state.players[playerIndex];
    string msg = string.IsNullOrEmpty(description)
        ? $"<color={player.colour}>{player.name}</color> wins!"
        : $"<color={player.colour}>{player.name}</color> wins!\n<size=24>{description}</size>";
    ShowPopup(msg, 30f); // persistent (30s = effectively permanent)
}
```

Also detect domination via `RefreshTopBar()`:
```csharp
if (state.phase == "GameOver" && popupGO != null && !popupGO.activeSelf)
{
    // Fallback: if we missed MissionComplete, show generic game over
    ShowPopup("Game Over", 30f);
}
```

---

## 3. Fortify Token Pulse Animation

### What tv.html does
- On `FortifyMoved`: pulse-shrink on source territory, pulse-grow on target territory
- On `TroopsMovedIn` (after capture): same pulse effect

### What's needed

**BoardRenderer.cs** — add public pulse method:
```csharp
/// <summary>Quick scale pulse on a territory token (grow or shrink then return).</summary>
public async Awaitable PulseToken(int territoryId, bool grow, float duration = 0.6f)
{
    if (territoryId < 0 || territoryId >= 42 || tokens[territoryId] == null) return;

    var token = tokens[territoryId];
    var normalScale = tokenPrefab.transform.localScale * tokenScale;
    float peakScale = grow ? 1.5f : 0.6f;

    // Animate: normal → peak → normal
    float halfDuration = duration / 2f;
    float elapsed = 0f;

    while (elapsed < halfDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / halfDuration;
        token.transform.localScale = normalScale * Mathf.Lerp(1f, peakScale, t);
        await Awaitable.NextFrameAsync();
    }

    elapsed = 0f;
    while (elapsed < halfDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / halfDuration;
        token.transform.localScale = normalScale * Mathf.Lerp(peakScale, 1f, t);
        await Awaitable.NextFrameAsync();
    }

    token.transform.localScale = normalScale;
}
```

**UIOverlay.cs** — trigger pulse on fortify (already has `OnFortify` handler):
```csharp
void OnFortify(int playerIndex, int sourceId, int targetId, int armies)
{
    AddActivity($"{PlayerColourTag(playerIndex)} moved {armies}: {TerritoryName(sourceId)} \u2192 {TerritoryName(targetId)}");

    // Pulse animation
    var board = FindAnyObjectByType<BoardRenderer>();
    if (board != null)
    {
        _ = board.PulseToken(sourceId, grow: false);  // shrink source
        _ = board.PulseToken(targetId, grow: true);   // grow target
    }
}
```

---

## Files Modified

| File | Changes |
|------|---------|
| `SignalRClient.cs` | Add `OnTurnStarted` event + handler, add `OnMissionComplete` event + handler |
| `UIOverlay.cs` | Add central popup system, turn popup, game over popup, fortify pulse trigger |
| `BoardRenderer.cs` | Add public `PulseToken()` method |

## No Server Changes

All events (`TurnStarted`, `MissionComplete`, `FortifyMoved`) are already broadcast by the server. Unity just isn't listening to `TurnStarted` and `MissionComplete` yet.

---

*Estimated effort: ~30 minutes implementation.*
