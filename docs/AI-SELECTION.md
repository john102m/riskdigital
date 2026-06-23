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

*Updated: 2026-06-23*
