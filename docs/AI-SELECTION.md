# AI Selection — Final Design

## Tier Structure (Player-Facing)

| Button | Name | Under the Hood |
|--------|------|----------------|
| 🤖 | Easy | Tier 1: random everything |
| ⚔️ | Aggressive | Tier 2: heuristic brute force |
| 🐢 | Carl | Tier 3 + cautious weights |
| 💥 | Alice | Tier 3 + reckless weights |
| 🗺️ | Chris | Tier 3 + continent-obsessed weights |
| 🦊 | Ollie | Tier 3 + opportunist weights |

No "generic Tier 3" exposed to players. The personalities ARE Tier 3 — each is just a different dial setting on the same ML + heuristic engine.

## Personality Weight Profiles

```csharp
public record AiPersonality(
    string Name,
    string Emoji,
    float AttackThreshold,      // min score to attack (higher = more cautious)
    float ContinentWeight,      // multiplier on continent completion bonus
    float WeakPlayerWeight,     // bonus for targeting weakest player
    float ArmyPreservation,     // how quickly to stop after earning card (0=never stop, 1=always stop)
    float CardHoarding          // min cards before trading (3=trade immediately, 5=hold max)
);
```

| Param | Carl 🐢 | Alice 💥 | Chris 🗺️ | Ollie 🦊 |
|-------|---------|----------|-----------|----------|
| AttackThreshold | 0.8 | 0.3 | 0.5 | 0.4 |
| ContinentWeight | 1.0 | 0.5 | 5.0 | 0.5 |
| WeakPlayerWeight | 0.0 | 0.5 | 0.0 | 5.0 |
| ArmyPreservation | 1.0 | 0.0 | 0.5 | 0.3 |
| CardHoarding | 5 | 3 | 4 | 4 |

### What each personality FEELS like:

**Carl 🐢 "The Turtle"**
- Builds massive stacks. Won't attack unless overwhelming odds (0.8+). Hoards cards until forced. Protects his borders. Boring to watch but hard to kill. Vulnerable to being boxed in.

**Alice 💥 "The Berserker"**  
- Attacks at terrible odds (0.3+). Trades cards immediately for armies. Spreads everywhere, leaves territories at 1. Exciting to watch, often self-destructs. Can snowball if lucky early.

**Chris 🗺️ "The Planner"**
- Only cares about continents. Massive scoring bonus for continent completion attacks. Ignores "good" attacks that don't advance continent goals. Predictable (everyone can see which continent) but powerful once locked in.

**Ollie 🦊 "The Vulture"**
- Targets weakest player specifically. Wants eliminations for card transfers. Kingmaker behaviour — will hand the game to someone else if it means killing the weakling. Unpredictable and dangerous to be near when weak.

## Lobby UI

### Known Mode (default)
```
┌─────────────────────────────────────────────────┐
│  Add AI:                                         │
│  [🤖 Easy] [⚔️ Aggro] [🐢 Carl] [💥 Alice] [🗺️ Chris] [🦊 Ollie] │
└─────────────────────────────────────────────────┘
```

Player list shows: "Bot Alice 💥" or "Bot Dave 🐢"

### Mystery Mode (toggle)
```
┌──────────────────────────────┐
│  [🎲 Add Mystery AI]          │
└──────────────────────────────┘
```

Random personality assigned. Player list shows: "Bot Alice 🤖" (no personality hint). Revealed at game end.

### Hidden Personality Mode (alternative)

A middle ground — you know the difficulty tier but not the character:

```
[🤖 Easy] [⚔️ Aggressive] [🧠 Personality]
```

Tap "🧠 Personality" → adds a Tier 3 bot with a random personality (Carl/Alice/Chris/Ollie). Lobby shows "Bot Alice 🧠" — you know it's strategic but not *which* strategy. Discover through behaviour. Revealed at game end.

This keeps difficulty transparent (you chose "smart AI") while preserving the fun of figuring out *what kind* of smart. "Why is that one turtling? Must be Carl." "Why is it hunting me? Ollie..."

Could be the default for Tier 3 — no reason to ever reveal the personality upfront unless you specifically want to practise against a known style.

## Implementation

### Model (GameState.cs)
```csharp
public int AiTier { get; set; } = 1;           // 1, 2, or 3
public string? AiPersonality { get; set; }      // "Carl", "Alice", "Chris", "Ollie" (null for Tier 1-2)
```

### AiService
- Personalities stored as static dictionary of `AiPersonality` records
- Tier 3 methods read personality weights from current player
- `ScoreAttack()` uses `personality.AttackThreshold` and `personality.ContinentWeight`
- `RunStrategicAttack()` uses `personality.ArmyPreservation` for card restraint
- `RunReinforce()` uses `personality.CardHoarding` for trade timing

### Hub
```csharp
public async Task AddAI(int tier = 2, string? personality = null)
```

### Name Assignment
- Tier 1-2: "Bot Alice", "Bot Bob" (neutral names)
- Tier 3 personalities: "Bot Alice 💥", "Bot Carl 🐢" — emoji in the name for instant recognition

Or: personality name IS the display suffix: "Alice (Berserker)", "Dave (Turtle)"

## Priority

1. ✅ Tier 1 (random) — done
2. ✅ Tier 2 (aggressive) — done
3. ✅ Tier 3 base (ML + heuristics) — done
4. **Next:** Add personality weight profiles → 4 distinct characters
5. **Then:** Mystery mode toggle

## Open Questions

- Should personality names be the bot names? ("Carl" is always the turtle) Or separate? ("Bot Dave" assigned turtle personality randomly)
- In a 6-player game with 4 AI, can you have duplicates? (two Alices?) Probably no — one of each max.
- Should the TV show personality hints during gameplay? (e.g. "Carl is building up..." as flavour text in activity feed)

---

## Threat Detection & Mission Inference (Planned)

### The Concept

Smart AI shouldn't just play for itself — it should notice when another player is about to win and disrupt them. Like an experienced human who says "don't let Blue take Africa this turn!"

### Observable Signals (AI can infer from board state)

| What AI sees | Probable mission |
|--------------|-----------------|
| Player owns 5/6 of a continent, reinforcing the gap | Continent conquest |
| Player spreading exactly 2 armies on many territories | 18 territories × 2+ armies |
| Player owns 23 territories | 24 territory count |
| Player aggressively targeting one specific colour | Elimination mission |
| Player ignoring good attacks to focus on one region | Continent-specific mission |

### Threat Level Calculation

Each turn, scan all opponents:
```
For each player:
  - Continent progress: any continent at 80%+ owned? → HIGH threat
  - Territory count: 16+ territories? → MEDIUM (could be 18×2 or 24 mission)
  - Elimination pattern: attacking one colour repeatedly? → flag it
  
If HIGH threat detected:
  - Boost attacks that disrupt (take a territory IN their nearly-complete continent)
  - Even sacrifice good position to block
```

### Per-Personality Response

| Personality | Threat response |
|-------------|----------------|
| Carl 🐢 | Notices threats late (focused on self). Only reacts when someone is 1 territory from winning. |
| Alice 💥 | Accidentally disrupts everyone by attacking randomly. No deliberate blocking. |
| Chris 🗺️ | Blocks rival continent completion specifically (continent-aware). Ignores territory-count threats. |
| Ollie 🦊 | **Primary disruptor.** Actively scans every turn. Will abandon own plan to block whoever is closest to winning. The table police. |

### Own Mission Pursuit

AI also steers toward its own mission (not yet implemented):

| Mission type | AI behaviour |
|--------------|-------------|
| Continent conquest | Boost attacks into required continents. Fortify their borders. |
| 18 territories × 2+ | Spread armies wider (min 2 per territory). Attack weak isolated targets for territory count. |
| 24 territories | Expand aggressively. Quantity over quality. |
| Elimination | Target that player specifically. Stack armies adjacent to them. |

### Implementation Priority

1. Own mission pursuit (straightforward: read `player.Mission`, adjust weights)
2. Continent threat detection (someone at 80%+ → boost disruption)
3. Territory count awareness (someone at 16+ → watch them)
4. Mission inference from behaviour (most complex, Ollie-only at first)

---

*Updated: 2026-06-23*
