# Blitz Popup — Layout Spec

## Current State

The popup uses a world-space canvas (450×130) at 70% scale, positioned above the dice arena. Left-aligned text, punch-in animation. Lightning sprite from TMP Sprite Asset.

## What's Wrong

- Sprite jammed against the "BLITZ!" text — no breathing room
- Box padding feels unbalanced (text cramped against edges)
- Overall layout doesn't feel intentional

## Proposed Layout

```
┌──────────────────────────────────────────┐
│                                          │
│     ⚡     BLITZ!                        │
│                                          │
│     5 rounds - Lost 3 - Killed 7         │
│     Captured!                            │
│                                          │
└──────────────────────────────────────────┘
```

### Spacing Rules

| Element | Value | Notes |
|---------|-------|-------|
| Box padding (all sides) | 16px | Breathing room inside panel |
| Sprite → "BLITZ!" gap | `<space=12>` or 3 spaces | Clear separation |
| Line 1 → Line 2 gap | Default line height or `<line-height=130%>` | Not cramped |
| Line 2 → Line 3 gap | Same as above | Consistent |

### Text Spec

| Line | Content | Size | Colour | Alignment |
|------|---------|------|--------|-----------|
| 1 | `<sprite=0>` + `BLITZ!` | 36 | Gold #FFD700 | Left |
| 2 | `{rounds} rounds - Lost {atkLoss} - Killed {defLoss}` | 22 | White | Left |
| 3 | `Captured!` or `Held!` | 26 | Green #4ADE80 / Red #EF4444 | Left |

### Box Spec

| Property | Value | Notes |
|----------|-------|-------|
| Width | 450 | Fits longest stats line without overflow |
| Height | 130 | Snug around 3 lines + padding |
| Background | Dark (0.05, 0.05, 0.1, 0.92) | Matches existing popup |
| Position | Above dice arena (cam.up * 2f) | Doesn't overlap dice |
| Scale | 0.007 (70% of normal) | Doesn't dominate screen |

### Popup Text Padding (inside box)

Currently the text RectTransform has `offsetMin = (16, 0)` and `offsetMax = (-16, 0)` — horizontal only. For the blitz popup we need vertical padding too.

**Options:**
1. Change offsets dynamically in ShowPopupPunch: `offsetMin = (16, 10)`, `offsetMax = (-16, -10)`
2. Use `<margin>` TMP tag in the text string: `<margin=0, 10, 0, 10>`
3. Increase box height slightly and accept the natural spacing

### Sprite Spacing Options

1. `<space=12>` tag between sprite and text: `<sprite=0><space=12>BLITZ!`
2. Multiple literal spaces: `<sprite=0>   BLITZ!`
3. Adjust sprite's advance width in the Sprite Asset (Glyph Table → Advance field)

## Questions

1. Does left-aligned feel right, or should it be centred with the sprite above the text?
2. Is 450 wide enough or does the stats line overflow?
3. Should we try the sprite on its own line (larger) above "BLITZ!" text?

## Alternative Layout — Stacked

```
┌──────────────────────────────┐
│                              │
│            ⚡                │
│          BLITZ!              │
│   5 rounds - Lost 3 - K. 7  │
│         Captured!            │
│                              │
└──────────────────────────────┘
```

Centre-aligned, sprite on its own line at bigger scale. More balanced visually. Box taller but narrower.

---

*Created: 2026-07-05*
