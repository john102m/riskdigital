# Proposal — Blitz Result Flash on Unity Board

## Problem

When a blitz completes, the attacker sees a summary on their handset (rounds, losses, captured/held). The Unity board shows the final dice in the arena + plays a sound, but there's no text summary visible. You have to infer the outcome from the activity feed line or the dice faces.

A quick flash of the blitz stats would make the moment land — especially for spectators watching the TV who aren't holding a phone.

## Current Behaviour

1. Blitz completes server-side → `BlitzResult` broadcast
2. **Handset:** shows "5 rounds · You lost 3 · They lost 7 · Captured!" on the move-in screen
3. **Unity CombatTheatre:** camera sweep → final dice placed statically → hold 3.5s → dismiss
4. **Unity UIOverlay:** activity feed says "● captured Brazil" or "● defends Brazil" + plays win/fail sound

The dice arena shows *what* the final roll was, but not the *story* (how many rounds, how bloody).

## Proposal

Show a brief text overlay on the Unity board during the blitz dice display — same position as the turn popup (world-space canvas, centred) or overlaid near the dice panel.

### Option A — World-Space Popup (like turn popup)

Reuse the existing `ShowPopup()` system. Brief centred text:

```
⚡ Blitz: 5 rounds
Attacker lost 3 · Defender lost 7
Captured!
```

- Appears after dice are placed (not during camera sweep)
- Holds for the 3.5s dice display, fades with panel dismiss
- Green text for capture, red for held
- Small font (24-28) so it doesn't compete with dice

**Pros:** Already built (ShowPopup). Simple.  
**Cons:** World-space popup might overlap dice panel depending on camera angle. Two visual elements competing.

### Option B — Text Overlay on Dice Panel

Add a TextMeshPro label *inside* the dice arena RawImage area (or just above/below it). Part of the combat theatre display.

```
┌─────────────────┐
│  [dice]  [dice] │
│  [dice]  [dice] │
│                 │
│ 5 rounds · -3/+7│
│    Captured!    │
└─────────────────┘
```

- Rendered on the dice panel camera's canvas (or screen-space overlay anchored to panel position)
- Same lifecycle as the dice display — appears with dice, disappears with panel

**Pros:** Co-located with the dice. Single focal point. No overlap issue.  
**Cons:** Needs a new text element on the panel. Slightly more work.

### Option C — Activity Feed Enhancement

Just make the blitz activity feed line richer:

```
● 5 rounds: lost 3, killed 7 → captured Brazil
```

Instead of the current simple "● captured Brazil".

**Pros:** Zero new UI. Just a string change.  
**Cons:** Feed is bottom-left, small. Misses the moment. Not visible during dice display if you're looking at the arena.

## Recommendation

**Option B** — text below the dice in the panel area. It's where the eye already is. The blitz summary becomes part of the dice display rather than a separate popup competing for attention.

Fallback: **Option A** if positioning on the panel proves awkward. The popup system already works.

## Data Available

From `BlitzResultDTO`:
- `rounds` — number of attack rounds
- `captured` — win/loss
- `sourceId` / `targetId` — territories involved
- `finalAttackerDice` / `finalDefenderDice` — last roll values

From server `BlitzResult` (not currently sent to Unity but could be added):
- `totalAttackerLosses` / `totalDefenderLosses` — total casualties

**Gap:** The Unity `BlitzResultDTO` doesn't currently include `totalAttackerLosses` / `totalDefenderLosses`. Either:
1. Add those fields to the existing broadcast (server already sends them — Unity just doesn't deserialise them), or
2. Calculate from army counts (less reliable)

Option 1 is trivial — just add the fields to the DTO class.

## Implementation

1. Add `totalAttackerLosses` and `totalDefenderLosses` to `BlitzResultDTO` in `CombatTheatre.cs`
2. Create a TextMeshPro label in the dice panel area (or screen-space overlay anchored to panel)
3. In `ShowBlitzDice()`, after placing dice, set the label text:
   - `"⚡ {rounds} rounds · -{atkLoss}/+{defLoss}"` (or similar compact format)
   - Colour: green if captured, red if held
4. Clear label when panel dismisses

## Open Questions

1. Option A, B, or C?
2. Text format — compact one-liner or multi-line?
3. Include territory names or just stats?
4. Should single-attack captures also get a brief flash? (Currently only dice shown)

---

*Created: 2026-07-05*
