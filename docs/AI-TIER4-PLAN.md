# AI Tier 4 — Personality-Based

## Overview

Tier 3 intelligence with personality constraints. Same evaluation functions, different weight profiles. Each character makes deliberately suboptimal choices that match their personality — making them feel human and unpredictable.

## Characters

### Cautious Carl 🐢
- **Style:** Turtles, builds massive stacks, only attacks with overwhelming force
- **Reinforce:** All armies on 1–2 territories (creates mega-stacks)
- **Attack threshold:** Won't attack unless 4:1+ ratio
- **Blitz:** Always (when he does attack, he commits fully)
- **Fortify:** Pulls armies inward to consolidate
- **Card trading:** Holds until forced (5 cards) — hoards for the big push
- **Weakness:** Slow expansion, easy to box in, predictable stack locations
- **Feel:** Long pauses before attacks (3–4s), fast skips when not attacking

### Aggressive Alice ⚔️
- **Style:** Attacks constantly, spreads thin, high risk/high reward
- **Reinforce:** Spreads across all front-line territories (thin but wide)
- **Attack threshold:** Attacks at 2:1 or even 1.5:1
- **Blitz:** Uses blitz even at 2:1 (reckless)
- **Fortify:** Moves forward always (never retreats)
- **Card trading:** Trades immediately when able (wants the armies NOW)
- **Weakness:** Overextends, leaves territories with 1 army, vulnerable to counter-attack
- **Feel:** Fast actions (0.5–1s), feels impatient and chaotic

### Continental Chris 🗺️
- **Style:** Laser-focused on completing continents, ignores everything else
- **Reinforce:** 100% on the continent gap border
- **Attack:** ONLY attacks territories needed for continent completion (or adjacent threats to owned continents)
- **Blitz:** Blitzes continent gaps, single-attacks everything else
- **Fortify:** Moves to continent border chokepoints
- **Card trading:** Trades when it enables a continent push
- **Weakness:** Predictable target (everyone knows which continent), ignores opportunities outside the plan
- **Feel:** Deliberate, consistent timing (1.5–2s), methodical

### Opportunist Ollie 🦊
- **Style:** Targets the weakest player, steals cards, kingmaker behaviour
- **Reinforce:** Adjacent to the weakest player's territories
- **Attack:** Targets player with fewest territories (elimination = cards)
- **Blitz:** Blitzes weak players aggressively
- **Fortify:** Toward the weak player's remaining territories
- **Card trading:** Strategic — holds for territory bonus if close, otherwise trades when 4+ cards
- **Weakness:** Creates enemies, kingmaker moves can backfire, neglects own defence
- **Feel:** Variable timing — fast when smelling blood (0.8s), slow when calculating who's weakest (2–3s)

## Personality as Weight Modifiers

Each character uses the same Tier 3 evaluation functions but with different weights:

| Factor | Carl | Alice | Chris | Ollie |
|--------|------|-------|-------|-------|
| Attack ratio threshold | 4.0 | 1.5 | 2.5 | 2.0 |
| Continent priority weight | 0.3 | 0.2 | 1.0 | 0.1 |
| Weakest player targeting | 0.1 | 0.3 | 0.0 | 1.0 |
| Army preservation | 1.0 | 0.2 | 0.5 | 0.4 |
| Expansion speed | 0.2 | 1.0 | 0.5 | 0.7 |
| Card hoarding | 1.0 | 0.0 | 0.3 | 0.5 |

## Mission Concealment

All Tier 4 characters actively disguise their mission:

- **Misdirect:** Attack territories outside mission targets in early game
- **Delay commitment:** Don't complete the winning condition until one explosive turn
- **Spread pressure:** Maintain presence on multiple fronts to prevent deduction
- **Sprint at the end:** Once committed, go all-in before opponents react
- **Reinforcement misdirection:** Don't evenly spread 2-armies for territory-count missions early

Each personality applies this differently:
- Carl: naturally conceals by being passive (hard to tell what he's building toward)
- Alice: naturally conceals by attacking everywhere (can't tell what's intentional)
- Chris: worst at concealment (continent focus is obvious) — needs extra misdirection
- Ollie: uses elimination targeting as cover for actual mission

## Adaptive Timing

- Mirrors average human turn speed in the game (tracked by server)
- Each character has a multiplier: Carl × 1.3, Alice × 0.6, Chris × 1.0, Ollie × 0.9
- Adds random jitter (±20%) to feel natural
- Slows down in late game (more to consider)

## Implementation Notes

- Same `AiService` infrastructure as Tier 1
- Add `AiPersonality` enum + weight profile class
- Tier 3 becomes the base evaluation engine; Tier 4 applies personality weights on top
- Character selection: host picks from a list, or random assignment

---

*Created: 2026-06-21*
