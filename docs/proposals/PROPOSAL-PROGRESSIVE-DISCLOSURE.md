# Proposal: Progressive Disclosure / Newbie Onboarding

## Problem
The handset is necessarily busy — continent accordions, card badges, mission badges, dice selectors, blitz button, move-in stepper. For experienced Risk players this is fine. For newcomers it's overwhelming on first contact.

## Philosophy
Don't dumb it down. Make it **learnable** — reveal complexity as it becomes relevant, not all at once.

---

## Option A: First-Turn Tooltips (Lightest Touch)

Brief overlay hints on the first occurrence of each phase. One sentence, tap to dismiss, never shows again (localStorage flag).

| Phase | Hint |
|-------|------|
| Placement (first turn) | "Tap a territory to place one army. Tap All to place everything there." |
| Reinforce (first time) | "You get armies each turn. Place them on territories you own." |
| Attack (first time) | "Pick a territory to attack FROM, then pick an enemy NEXT TO it." |
| Blitz (first time appears) | "⚡ Blitz attacks repeatedly until you win or run out." |
| Fortify (first time) | "Move armies between two adjacent territories you own. Or skip." |
| Cards (first trade available) | "Trade 3 matching cards for bonus armies." |

**Effort:** Low. 6 small overlays, localStorage persistence.

---

## Option B: Contextual Micro-Hints (Medium)

Instead of a one-time tooltip, show brief context text *within* the UI when the player hasn't acted for 5+ seconds. Already partially exists (idle hint in AttackScreen).

Extend to all phases:
- Reinforce idle → "Tap a territory to place armies there"
- Fortify idle → "Pick a territory to move FROM — or tap Skip"
- Card panel open, no selection → "Tap 3 cards to select a set"

**Effort:** Low-medium. Reuse existing idle hint pattern.

---

## Option C: Progressive Complexity (Bigger)

Hide advanced features until the player has used the basics:

| Feature | Hidden until... |
|---------|----------------|
| Blitz button | Player has done 3+ manual attacks |
| Dice count override | Player has attacked 5+ times (always use max until then) |
| Card trade hint | Player first earns a card |
| Mission badge tap | Mission welcome modal dismissed |
| Status badge | First reinforcement phase |

Each unlocked feature gets a brief "NEW" pulse animation on first appearance.

**Effort:** Medium. Need a per-player progression state (localStorage).

---

## Option D: Guided First Game (Heaviest)

A "Tutorial" toggle in lobby. When on:
- Each phase gets a full-screen explanation before first action
- Forced pauses with "Got it" buttons
- Arrows pointing to relevant UI elements
- Skippable at any time

**Effort:** High. Probably overkill for a family game where someone explains the rules verbally.

---

## Recommendation

**A + B combined.** First-turn tooltips for the very first game, idle micro-hints ongoing. Low effort, non-intrusive, helps without patronising.

Option C is interesting but risks annoying experienced players who reset localStorage. Option D is too heavy — the verbal "here's how it works" is the tutorial.

---

## Implementation Notes

- Store `risk_hints_seen` in localStorage (object with phase keys)
- Tooltip component: fixed overlay, dark bg, white text, tap to dismiss
- Idle hints: reuse existing pattern from AttackScreen (5s timer, context text)
- Never block interaction — hints are overlays, not modals
- Respect returning players — if `risk_name` exists in localStorage, assume they've played before (skip tooltips)

---

## Scope
- 1 new component (`HintOverlay.tsx`)
- Extend idle hint pattern to Reinforce + Fortify
- localStorage flags
- ~1 hour implementation

*Not urgent. Playtest feedback item for when the game goes to non-Risk-players regularly.*
