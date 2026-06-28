# AI Tier 3 — Strategic

## Personality

Thinks multiple turns ahead. Understands continent control, threat assessment, and card timing. The first tier that plays like a competent human.

## Reinforce

- Prioritises continent completion: if 1–2 territories away from owning a continent, stack armies on the border territory that can take the gap
- Otherwise: reinforce front-line territories weighted by threat (enemy armies adjacent)
- Never wastes armies on safe interior

## Attack

- **Continent-driven:** If close to completing a continent, attacks the gap territories first
- **Opportunistic:** Attacks isolated weak enemies (1–2 armies) for card earning even without continent motive
- **Restraint:** Won't attack into a heavily defended territory unless overwhelming advantage (3:1+ ratio)
- **Threat-aware:** Avoids weakening a border that faces the strongest player
- **Stops early:** If the attacks aren't gaining continent progress, stops to preserve armies

## Move-In After Capture

- Context-dependent:
  - Capturing final continent territory → move max (protect the bonus)
  - Still pushing → move enough to continue attacking from new territory
  - End of attack chain → leave balanced armies on both sides

## Fortify

- Always fortifies strategically
- Moves armies toward continent borders that are exposed
- If continent complete: fortifies the chokepoints (fewest entry points)

## Card Trading

- Holds cards until forced (5) OR until a territory bonus is available on owned territory
- Prefers sets that include owned-territory cards (+2 bonus)
- Trades at start of a "push turn" where continent completion is planned

## Blitz

- Blitzes when 3:1+ advantage and target is strategically valuable
- Single attacks when probing or when outcome uncertain

## Continent Priority

Evaluates each continent by:
1. **Progress** — territories already owned / total
2. **Gap difficulty** — total enemy armies in remaining territories
3. **Border defensibility** — how many entry points to defend after capture
4. **Bonus value / size ratio** — Australia (2 bonus, 2 borders) > Asia (7 bonus, many borders)

Priority score = `(progress × 3) + (bonus / borders) - (gap_difficulty × 0.5)`

## Threat Assessment

- Tracks which player has most territories, most armies, continent bonuses
- Avoids picking fights with the leader unless directly competing for same continent
- Targets weakest player when looking for easy card farming

## Mission Awareness (if UseMissions)

- Knows its own mission and works toward it
- Does NOT reveal intent through behaviour (see Tier 4 for active concealment)
- Adjusts priorities: if mission is "18 territories with 2+", focuses on spreading rather than stacking

## Key Difference from Tier 2

Tier 2 attacks everything it can. Tier 3 asks "should I attack this?" — considers continent progress, threat level, army preservation, and card timing. First tier with a plan.

---

*Created: 2026-06-21*
