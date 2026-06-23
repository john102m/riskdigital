# Block 1: Aggressive AI (Tier 2) — Implementation Plan

## Goal

AI that applies real pressure. Reinforce the front, always attack weakest neighbour, blitz when strong, push forward, fortify toward the fight.

## Changes

### 1. Player Model — `Models/GameState.cs`
```csharp
public int AiTier { get; set; } = 1;  // 1=Random, 2=Aggressive
```

### 2. AiService — Branch on Tier

Current structure: `RunPlacement`, `RunReinforce`, `RunAttack`, `RunFortify`

Each gets a tier check:
```csharp
if (player.AiTier == 2) await RunAggressiveAttack(state, player, connId);
else await RunRandomAttack(state, player, connId);  // existing Tier 1
```

### 3. Tier 2 Logic

**Reinforce:**
- Find front-line territories (owned, adjacent to enemy)
- Sort by: most adjacent enemies first, then lowest army count
- Place all armies on top 1-2 front-line territories (concentrate force)

**Attack:**
- Always attack (no skip)
- Pick source: owned territory with highest army count on the front
- Pick target: adjacent enemy with lowest army count (weakest)
- If source has 5+ armies → Blitz, else single attack with max dice
- After capture: move max armies in (aggressive push)
- Repeat until no valid source with >2 armies, or 6 attacks done

**Fortify:**
- Always fortify (no skip)
- Find safest inland territory (all neighbours owned) with most armies
- Move max armies toward the front (adjacent owned territory that has adjacent enemies)

### 4. Timing (faster than Tier 1)
- Reinforce placement: 1.5s between each
- Attack selection: 1.5–2s
- Between attacks: 2–3s
- Fortify: 2s

### 5. AddAiPlayer — Default to Tier 2
- Change default `AiTier = 2` in AddAiPlayer (upgrade from random)
- Tier 1 becomes the "easy mode" option later via chooser

## Files to Modify

| File | Change |
|------|--------|
| `Models/GameState.cs` | Add `AiTier` to Player |
| `Services/AiService.cs` | Add Tier 2 methods, branch on tier |

## Test Checklist

- [ ] Bot reinforces front-line only (not random inland)
- [ ] Bot always attacks (doesn't skip 50%)
- [ ] Bot targets weakest neighbour
- [ ] Bot uses blitz when 5+ armies at source
- [ ] Bot moves max on capture
- [ ] Bot fortifies from rear to front
- [ ] Bot doesn't crash on edge cases (no valid targets, 1 army everywhere)
- [ ] Feels noticeably harder than Tier 1

---

*Created: 2026-06-23*
