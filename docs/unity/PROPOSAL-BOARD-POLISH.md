# Proposal — Unity Board Graphic Polish

The game is playable and sounds great with the phased soundtrack. The board surface looks good (normal map relief, distinct continent colours). This is the final visual polish pass — making what happens *on* the board feel as good as the board itself looks.

---

## Problem: 30 Armies Looks Like 1

The single biggest visual gap. Every territory has an identical cylinder regardless of army count. You can't glance at the board and see where power is concentrated. The number label does the work, but the *feel* is flat.

---

## Options for Army Representation

### Option A — Height Scaling

Scale the cylinder's Y axis with army count. Tall = strong, short = weak.

- **Formula:** `height = baseHeight + log2(count) * scaleFactor` (logarithmic so 30 isn't a skyscraper)
- **Pros:** Instant visual weight. Simple to implement. One mesh.
- **Cons:** Tall stacks could obscure neighbours in dense areas (Europe). Shadow changes add visual noise.
- **Mitigation:** Cap max height. Use logarithmic curve so 1→5 is dramatic but 20→30 is subtle.

### Option B — Radius Scaling

Scale X/Z (wider footprint for bigger armies). Token becomes a fat disc when powerful.

- **Formula:** `radius = baseRadius + log2(count) * scaleFactor`
- **Pros:** No occlusion issues. Immediate "weight" feel.
- **Cons:** Could overlap neighbours on tight territories (Europe/Asia border). Less dramatic than height.
- **Mitigation:** Logarithmic curve + max cap. Tight territories naturally have small bases anyway.

### Option C — Tiered Models (Classic Risk)

Replace single cylinder with infantry/cavalry/artillery meshes at count thresholds.

- 1 army = single small piece (infantry)
- 5 armies = medium piece (cavalry) replaces 5 infantry
- 10 armies = large piece (artillery) replaces 2 cavalry

30 armies = 3 artillery arranged in a tight cluster.

- **Pros:** Most thematic. Visual variety. Classic Risk language.
- **Cons:** Most work (3 model variants, cluster positioning logic). Potentially cluttered on small territories.
- **Could start simple:** Use differently-sized cylinders or geometric shapes as stand-ins before investing in proper models.

### Option D — Aura / Glow Intensity

Token stays the same size but gains a glowing ring or point light that intensifies with army count.

- 1 army = dim/no aura
- 10+ = visible coloured glow on the normal map surface
- 20+ = bright, pulsing slightly

- **Pros:** Dramatic. Fits the lit normal map aesthetic. No occlusion. Leverages existing point light system.
- **Cons:** Might get overwhelming with many large armies (whole map glowing). Harder to judge exact relative strength.
- **Mitigation:** Logarithmic intensity. Only visible above a threshold (say 5+).

### Option E — Combined: Height + Aura

Scale height for immediate visual weight, add subtle aura glow above a threshold for drama.

- 1–4: short cylinder, no glow
- 5–14: medium height, faint glow
- 15+: tall, visible glow interacting with normal map

Best of both — readable AND dramatic. Slightly more complex but each part is simple.

---

## Recommendation

**Option E (Height + Aura)** for maximum impact, or **Option A (Height only)** for simplicity-first with aura added later.

Height scaling alone solves 80% of the problem with minimal code. Aura adds the cinematic layer.

---

## Other Polish Candidates

### Capture Effects
- Brief particle burst (smoke/sparks) when a territory changes hands
- Token colour transition (old colour fades to new) rather than instant swap
- Satisfying audio already exists (capture fanfare) — visual should match

### Reinforcement Animation
- Troops "drop in" — token grows/bounces when armies are placed
- Scale punch (overshoot then settle) on the cylinder
- Heavier sound already triggers at high counts — visual could match

### Elimination Ceremony
- Eliminated player's territories cascade-change colour to the attacker
- Brief ripple/wave effect spreading from the kill territory outward
- Dramatic pause (camera holds, lights dim momentarily)

### Fortify Animation
- Visible "march" — token at source shrinks, token at target grows
- Optional: brief line/trail between territories during the move
- Already have pulse shrink/grow — could make it more pronounced

### Phase Lighting Shifts
- Reinforce: warm golden light (building up)
- Attack: cooler, slightly red-tinted (tension)
- Fortify: neutral/calm
- Subtle — 10-15% colour temperature shift on the directional light. Felt more than seen.

### Turn Transition
- Brief dim/brighten cycle when turn changes
- Active player's territories get a subtle luminance boost (their colour slightly brighter)
- Fades back to neutral after 2-3s

---

## Implementation Priority

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| 1 | Army height scaling | Low | High — solves the core problem |
| 2 | Reinforcement bounce/punch | Low | Medium — placement feels alive |
| 3 | Capture particle effect | Medium | Medium — combat payoff |
| 4 | Aura glow (army strength) | Medium | Medium — cinematic layer |
| 5 | Phase lighting shifts | Low | Low-medium — subtle mood |
| 6 | Elimination cascade | Medium | Low (rare event) — but dramatic when it happens |
| 7 | Fortify trail/march | Medium | Low — minor phase |
| 8 | Turn transition dim/brighten | Low | Low — subtle |

---

## Constraints

- Fire TV Stick 4K Max is the target — effects must run at solid FPS on mobile GPU
- Keep token readable at all times — army count label must stay clear
- Don't fight the normal map aesthetic — effects should complement, not overpower
- Logarithmic scaling everywhere — differences matter most at low counts (1 vs 5 is more important than 25 vs 30)

---

## Questions to Decide

1. Height scaling, radius scaling, or tiered models? (Recommend: height)
2. Add aura glow now or save for a second pass?
3. Capture effect: particles, colour fade, or both?
4. Phase lighting: yes/no? (Low effort, subtle reward)
5. Any of the "Other Polish" items feel wrong for the aesthetic?

---

*Created: 2026-07-05*
