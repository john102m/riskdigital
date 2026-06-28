# AI Tier 2 — Aggressive

## Personality

Attacks whenever able, concentrates force on the front line, targets the weakest neighbour. Has instincts but no long-term plan.

## Reinforce

- All armies go to front-line territories (those with adjacent enemies)
- Weighted by: most adjacent enemies first, then lowest army count (shore up weak fronts)
- Never places inland

## Attack

- Always attacks (no 50% skip)
- Picks source with highest army count on the front
- Targets weakest adjacent enemy (lowest army count)
- Uses max dice always
- Keeps attacking until source ≤ 2 or no valid targets remain
- More attacks per turn than Tier 1 (3–6)

## Move-In After Capture

- Moves max armies into captured territory (aggressive push forward)

## Fortify

- Always fortifies (no skip)
- Moves armies from safest inland territory toward the front line
- "Safest" = owned territory with all adjacent also owned

## Card Trading

- Trades immediately when able (no holding)
- Picks first valid set

## Blitz

- Uses blitz when source has 5+ armies (why roll one at a time?)

## Timing

- Slightly faster than Tier 1 (0.8–1.5s between actions)
- Feels impatient

## Key Difference from Tier 1

Tier 1 is random. Tier 2 has simple heuristics: hit weak neighbours, reinforce the front, push forward. No continent awareness, no threat assessment, no card timing strategy.

---

*Created: 2026-06-21*
