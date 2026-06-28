# AI Player Design

## Overview

Server-driven AI players that participate like humans. Same SignalR events, same game rules. Other players shouldn't immediately know it's a bot (aside from the name).

## Architecture (same as Flutter)

- AI logic lives on the server — no client needed
- AI "takes turns" via the same GameService methods
- Delays injected to simulate thinking (no instant actions)
- TV/handsets see normal game events — AI is transparent to clients

## Tiers (progressive difficulty)

### Tier 1: Random (for testing)
- Places armies randomly on owned territories
- Attacks random adjacent enemies (if >2 armies)
- Random fortify or skip
- No strategy — just fills the seat

### Tier 2: Aggressive
- Concentrates armies on front-line territories
- Attacks weakest adjacent enemy (lowest army count)
- Always attacks when able
- Fortifies toward the front

### Tier 3: Strategic
- Territory clustering (prefers connected groups)
- Continent awareness (prioritises completing/denying continents)
- Threat assessment (avoids picking fights with strongest player)
- Card timing (trades when strategically advantageous)
- Fortifies to protect continent borders

### Tier 4: Personality-based
- Named characters with distinct styles (like Flutter):
  - Cautious Carl — turtles, builds up, only attacks when overwhelming
  - Aggressive Alice — attacks constantly, spreads thin
  - Continental Chris — laser-focused on completing continents
  - Opportunist Ollie — targets weakest player, steals cards
- Adaptive timing — mirrors human pace

## Quick Reference — Lobby Buttons

| Button | Play Style |
|---|---|
| 🤖 **Tier-1** | Random. Places/attacks/fortifies randomly. Zero strategy — just fills a seat. |
| ⚔️ **Tier-2** | Aggressive heuristic. Always attacks weakest neighbour, blitzes at 5+, reinforces frontline, fortifies rear→front. Predictable but active. |
| 🧠 **Tier-3** | Strategic + ML. Uses a trained model to judge attack odds. Scores targets by continent completion. Stops attacking after earning a card. Smart reinforce/fortify. |
| 🦊 **Tier-4** | Enhanced heuristics. Hunts eliminations for card steals, values chokepoints, continent denial, fast tempo. Single personality (Opportunist). |
| 🧬 **Tier-5 Opportunist** | Tier-4 brain + learns from your play data. Hunts weak players, fast tempo. |
| 🧬 **Tier-5 Cautious** | Learns from your data + only attacks at 4:1 ratio, hoards cards, turtles up, slow expansion. |
| 🧬 **Tier-5 Aggressive** | Learns from your data + attacks at 1.5:1, max expansion, doesn't preserve armies, fastest tempo. |
| 🧬 **Tier-5 Continental** | Learns from your data + prioritises continent completion, blocks opponents near completing theirs. |

---

## Key Decisions

- **When to attack:** threshold-based (army ratio vs neighbour)
- **Where to reinforce:** weight by frontier exposure, continent progress, threat level
- **Blitz vs single:** blitz when overwhelming (3:1+), single when probing
- **Card trading:** trade immediately (tier 1-2) or hold for territory bonus / strategic timing (tier 3+)
- **Fortify direction:** toward weakest border / continent gap

## Timing & Feel

- Delay before each action (1-3s, slightly randomised)
- Faster in early game, slower in late game (mimics human deliberation)
- Optional: typing-indicator style "thinking..." shown on handsets

## Implementation Plan

1. AI player type flag on Player model (IsAI)
2. AI turn runner — triggered when currentPlayer.IsAI after turn advance
3. Timer-based action queue (reinforce → attack → fortify with delays)
4. Tier 1 first — get the plumbing working
5. Progressively smarter tiers as the game matures

## Open Questions

- How many AI players max? (Performance shouldn't be an issue — it's just logic)
- Can humans play alongside AI? (Yes — same lobby, host adds AI before starting)
- AI names/avatars — predefined list or random?
- Should AI honour house rules (LockedAttackFront, missions)?
- Mission-aware AI (tier 3+) — does it reveal intent through behaviour?

## Mission Concealment (Tier 3+)

**Status: NOT IMPLEMENTED — noted for future**

Current AI pursues missions transparently. Observant humans can read the pattern.

Planned improvements:
- **Misdirect** — attack territories outside mission targets early to disguise intent
- **Delay commitment** — don't complete the second continent until you can take it in one turn
- **Spread pressure** — maintain presence on multiple fronts so opponents can't deduce the target
- **Sprint at the end** — once committed, go all-in before others react
- **Early game strategic awareness** — grab small continents (Australia, South America) regardless of mission for the bonus income

---

*Created: 2026-06-21*
