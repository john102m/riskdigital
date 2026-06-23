# AI Tier Selection — Design

## Two Modes (Host Toggle in Lobby)

### Mode A: "Choose AI" (Known)
- Host picks tier for each AI added (button per tier)
- Tier shown in lobby next to the bot name: "Bot Alice 🤖 Aggressive"
- TV lobby shows the same — all humans know what they're facing
- Strategy: humans can plan around AI behaviour ("Alice will attack everything, stay away from her border")

### Mode B: "Mystery AI" (Unknown)
- Host taps "Add AI" — tier is assigned randomly (weighted or pure random)
- Nobody knows what tier each bot is — not the host, not the other players
- Name gives nothing away: "Bot Alice 🤖" (no tier label)
- Discovery through gameplay: "Why is Bob turtling? He must be cautious..."
- Adds unpredictability — you can't plan around known AI behaviour
- Revealed at game end? Optional — could show tier in the Game Over summary

## Implementation

### Player Model
```csharp
public int AiTier { get; set; }         // 1-4
public bool AiTierVisible { get; set; }  // whether to show in lobby
```

### House Rule
```csharp
public bool MysteryAI { get; set; }  // toggle in lobby (default: false = known)
```

### Hub Methods
- **Known mode:** `AddAI(int tier)` — host picks tier, `AiTierVisible = true`
- **Mystery mode:** `AddAI()` — server picks random tier, `AiTierVisible = false`

### Lobby Display
- Known: "Bot Alice 🤖 Aggressive"
- Mystery: "Bot Alice 🤖 ???" or just "Bot Alice 🤖"

### During Game
- Both modes: just show name (no tier clutter during play)
- Players figure out behaviour by watching

### Game Over (optional reveal)
- "Bot Alice was Aggressive (Tier 2)"
- "Bot Bob was Strategic (Tier 3)"
- Fun debrief moment — "I KNEW Bob was the smart one!"

## Name Pool

Names should be neutral (not hint at personality):
- Bot Alice, Bot Bob, Bot Carol, Bot Dave, Bot Eve
- Or themed: "General North", "Admiral West" etc (stretch goal)

With Tier 4 personalities (Carl/Alice/Chris/Ollie), those names ARE the personality — so Mystery mode would NOT use personality names. It'd use neutral names and assign a hidden personality.

## UI — Lobby (Host)

### Known Mode
```
┌──────────────────────────────────┐
│ 🤖 Add AI:                       │
│  [Random] [Aggressive] [Strategic]│
└──────────────────────────────────┘
```

### Mystery Mode
```
┌──────────────────────────────────┐
│ 🤖 Add AI  (tier = surprise!)    │
└──────────────────────────────────┘
```

### Toggle
A switch/pill in lobby settings: `AI Mode: [Known | Mystery]`

## Priority

1. Implement Tier 2 (Aggressive) in AiService
2. Add `AiTier` to Player model
3. Add tier selector UI (Known mode)
4. Add Mystery mode toggle
5. Implement Tier 3 later (uses same infrastructure)

## Open Questions

- Should Mystery mode weight toward easier tiers early? (e.g. first game = more Tier 1-2, experienced = more Tier 3-4)
- Should the host be able to see tiers in Mystery mode via admin endpoint? (cheat peek for debugging)
- At game end, always reveal or only in Mystery mode?

---

*Created: 2026-06-23*
