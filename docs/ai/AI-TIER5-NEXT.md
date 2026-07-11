# AI Tier 5+ — Next Steps (Post-Refactor)

Incremental improvements to make the top-tier AI genuinely harder to beat. Ordered by effort vs impact. All assume the AI refactor (PROPOSAL-AI-REFACTOR.md) is done first — clean strategy separation makes each step straightforward.

---

## Phase 1: Opponent Modelling (Heuristic, No ML)

**Impact:** High — this is what makes humans feel "seen"
**Effort:** Low — data already available, just scoring adjustments

The AI currently scores each attack in isolation. It doesn't ask "what is this player doing?" Add per-turn opponent tracking:

### What to track (per player, recalculated each turn):
- Continent progress: which continents are they building toward? (already in `GetContinentFor`)
- Threat level: territories × total armies × continent bonus potential
- Aggression toward me: how many times have they attacked me in last N turns?
- Card count: are they about to trade and get a massive reinforcement?

### Scoring adjustments:
- **Block continent completion:** If opponent owns N-1 of a continent, attack that last territory scores +3.0 × continent bonus (currently `ContinentDenial` does some of this but only reacts when they're 1-2 away — make it more aggressive)
- **Don't provoke the strong:** If a player has 2× my armies and hasn't attacked me recently, reduce score for attacking their territories (implicit non-aggression)
- **Pile on the leader:** If one player owns >40% of territories, all AI personalities get a bonus for attacking them regardless of personality weights
- **Card timing awareness:** If an opponent has 4-5 cards and you can eliminate them, that elimination is worth more (you get their cards → forced trade → massive reinforcement)

### Implementation:
- New `OpponentProfile` class: tracks per-player stats, refreshed at start of each AI turn
- Add `ScoreOpponentContext()` to the scoring pipeline
- ~100 lines total

---

## Phase 2: Adaptive Personality (Heuristic, No ML)

**Impact:** Medium — makes games less predictable, AI "wakes up" when losing
**Effort:** Low — just recalculate weights conditionally

Currently `PersonalityWeights` are fixed at game start. Make them shift based on board position:

### Rules:
- **Losing (< 8 territories):** Increase `ArmyPreservation`, decrease `ExpansionSpeed`. Turtle up, wait for opportunity.
- **Dominant (> 15 territories):** Increase `ExpansionSpeed`, decrease `ArmyPreservation`. Press the advantage.
- **Close to mission:** Temporarily override personality — Continental bot becomes Aggressive when 1 territory from mission completion.
- **Just got a big card trade:** Increase aggression for this turn only (mirror human behaviour — you just got 10 armies, you're going to use them).
- **Being ganged up on:** If 2+ players attacked me last turn, shift toward Cautious regardless of base personality.

### Implementation:
- `AdjustWeights(PersonalityWeights base, GameState state, int myIndex)` method
- Returns modified weights per turn, doesn't mutate the base
- ~60 lines

---

## Phase 3: Retaliation Memory (Simple History)

**Impact:** Medium — makes the AI feel less like a pushover
**Effort:** Medium — needs per-game state that persists across turns

Currently the AI has no memory. If you attack it every turn, it doesn't care. Add a short memory:

### What to track:
- `Dictionary<int, List<int>>` — who attacked whom, last 5 turns
- "Grudge" score: if player X has attacked me 3 turns running, boost score for attacking them back

### Behaviour:
- Retaliatory attacks score +0.5 per recent attack against me
- Capped — don't suicide into a strong player just for revenge
- Personality modulates: Aggressive retaliates more, Cautious less, Opportunist only if it's also strategically sound

### Implementation:
- `AttackHistory` class on `GameService` or `AiService` (per-game)
- Updated after each combat resolution
- Fed into `ScoreTier4Attack` as a bonus
- ~40 lines

---

## Phase 4: Multi-Turn Planning (Bigger Lift)

**Impact:** High — "I'll save up, then push in 2 turns"
**Effort:** High — needs goal-setting and state projection

This is where it stops being simple scoring and starts being actual strategy:

### Concept:
- At turn start, AI sets a **goal** for this turn (or multi-turn goal)
- Goals: "Complete Africa", "Eliminate Player 3", "Turtle and trade cards", "Deny Europe"
- All decisions (reinforce, attack, fortify) serve the goal
- Goal persists across turns unless board state invalidates it

### Why it's hard:
- Need to evaluate "can I achieve this goal in N turns?"
- Need to simulate forward (what if I reinforce here, attack there, will I have enough?)
- Risk has too much randomness for deep lookahead — but 1-2 turns is feasible

### Possible approach:
- Monte Carlo simulation: try 50 random futures from current position, score outcomes
- Pick the goal that leads to best average outcome
- Or simpler: just evaluate "continent completion distance" and commit to the closest one

### Implementation:
- `IAiGoal` interface with `Evaluate()`, `IsStillViable()`, `ScoreAttack()` methods
- `GoalSelector` picks best goal at turn start
- Goal biases all scoring for that turn
- ~200-300 lines
- **Do after Phases 1-3 are bedded in**

---

## Phase 5: Learned Human Behaviour (ML.NET Improvement)

**Impact:** Medium — better 70/30 blend
**Effort:** Medium — needs more training data and model iteration

Current state: `PredictHumanAttack` uses a single regression model trained on attack-log.csv. Improvements:

### Better features:
- Add "turn number" as feature (humans play differently early vs late game)
- Add "card count" (humans attack more aggressively when they have cards to trade)
- Add "was attacked last turn" (humans retaliate — teach the model this)
- Add "territory count ratio" (humans push harder when they're winning)

### Better training:
- Log "non-attacks" — territories you *could* have attacked but didn't (negative examples)
- Currently only logs `DidAttack=1`. Need `DidAttack=0` rows for the model to learn restraint.
- Requires logging at end of attack phase: for each valid source→target pair not attacked, log a row with `DidAttack=0`

### Adaptive blend:
- Start at 90/10 (heuristic/ML) with < 20 games of data
- Shift to 70/30 at 50+ games
- Shift to 50/50 at 200+ games
- Could even go 30/70 if the model proves accurate

### Implementation:
- Feature expansion: ~20 lines in `ActionLogger`
- Non-attack logging: ~30 lines at end of `RunAttack`
- Adaptive blend: ~10 lines in `ScoreTier4Attack`
- Retrain pipeline already exists

---

## Summary

| Phase | What | Effort | Impact | Prereq |
|-------|------|--------|--------|--------|
| 1 | Opponent modelling | Low (~100 lines) | High | AI refactor |
| 2 | Adaptive personality | Low (~60 lines) | Medium | AI refactor |
| 3 | Retaliation memory | Medium (~40 lines) | Medium | Phase 1 |
| 4 | Multi-turn planning | High (~300 lines) | High | Phases 1-3 |
| 5 | Better ML training | Medium (~60 lines) | Medium | More game data |

Phases 1-3 are pure heuristics — no ML, no external dependencies, just smarter scoring. They'd make Tier 5 noticeably harder without any model retraining. Phase 4 is the "rainy weekend" project. Phase 5 improves naturally as more games are played.

---

*Created: 2026-07-11*
