# Proposal — Blitz Popup (Punchy Turn Popup Variant)

## Idea

Reuse the existing world-space popup system for blitz results, but with more aggressive entrance animation and bold formatting. No new UI elements — just a `ShowBlitzPopup()` variant of the existing `ShowPopup()`.

## Current Popup Behaviour

```
ShowPopup(text, duration):
  - Scale: 0.3 → 1.0 with sine overshoot (0.1 extra)
  - Hold for duration
  - Exit: shrink + tilt + fade (0.5s)
```

Works well for turn announcements. Too gentle for a blitz moment.

## Proposed: ShowBlitzPopup()

### Entrance (more aggressive)
- Scale: 0.1 → 1.2 → 1.0 (bigger overshoot, snaps back — "punch in")
- Faster entrance: 0.25s (vs 0.35s)
- Optional: 2-3 frame horizontal shake after snap-back (±5px wobble)

### Text Formatting
```
<size=48><color=#FFD700>⚡ BLITZ!</color></size>
<size=28>5 rounds · Lost 3 · Killed 7</size>
<size=32><color=#4ADE80>Captured!</color></size>
```

Or on fail:
```
<size=48><color=#FFD700>⚡ BLITZ!</color></size>
<size=28>8 rounds · Lost 6 · Killed 4</size>
<size=32><color=#EF4444>Held!</color></size>
```

- "BLITZ!" in gold, large
- Stats in white, medium
- Outcome in green (capture) or red (held)

### Timing
- Appears after dice are placed in the arena (not during camera sweep)
- Holds for 2.5s (dice panel holds 3.5s — popup fades slightly before panel dismisses)
- Exit: same tilt-fade as normal popup

### Sound
- Already playing `blitzWinClip` or `blitzFailClip` from UIOverlay — no change needed

## Implementation

1. Add `totalAttackerLosses` and `totalDefenderLosses` to `BlitzResultDTO`:
```csharp
public int totalAttackerLosses;
public int totalDefenderLosses;
```

2. Add `ShowBlitzPopup()` to `UIOverlay.cs`:
```csharp
async void ShowBlitzPopup(int rounds, int atkLoss, int defLoss, bool captured)
{
    string outcome = captured 
        ? "<color=#4ADE80>Captured!</color>" 
        : "<color=#EF4444>Held!</color>";
    string text = $"<size=48><color=#FFD700>\u26a1 BLITZ!</color></size>\n" +
                  $"<size=28>{rounds} rounds \u00b7 Lost {atkLoss} \u00b7 Killed {defLoss}</size>\n" +
                  $"<size=32>{outcome}</size>";
    
    // Punch-in popup (reuse popup system with aggressive curve)
    ShowPopupPunch(text, 2.5f);
}
```

3. Add `ShowPopupPunch()` — same as `ShowPopup()` but:
   - Entrance scale: 0.1 → 1.2 (0.15s) → 1.0 (0.1s settle)
   - Optional 3-frame wobble after settle
   - Same exit as normal

4. Call from `OnBlitzResultCapture()` in UIOverlay (where sounds already fire):
```csharp
void OnBlitzResultCapture(string json)
{
    var result = JsonConvert.DeserializeObject<BlitzResultDTO>(json);
    // ... existing sound logic ...
    ShowBlitzPopup(result.rounds, result.totalAttackerLosses, result.totalDefenderLosses, result.captured);
}
```

## Timing Coordination with Dice Panel

- CombatTheatre shows dice panel for 3.5s
- Blitz popup appears ~0.5s after dice placed (let eyes find the dice first)
- Popup holds 2.5s, fades before panel dismisses
- No overlap conflict — popup is centred, panel is left/right/centre of board

If panel is centred (rare — only cross-map attacks), popup could shift up slightly. Or just let them coexist briefly — popup is semi-transparent background, won't fully obscure dice.

## What This Doesn't Do

- No comic sprite/starburst (can add later if text feels flat)
- No screen shake (could be distracting on TV)
- No particle burst (separate proposal in board polish doc)
- Doesn't change single-attack combat display (only blitz)

## Effort

Low. ~30 mins. All infrastructure exists — it's a formatting + animation curve tweak on existing code.

---

*Created: 2026-07-05*
