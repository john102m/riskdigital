# AI Tier 1 — Implementation Plan

## Goal

Server-driven AI player that fills a seat for solo testing. Random actions, delayed to feel human-ish. No strategy.

## Changes

### 1. Player Model

Add `IsAI` bool to `Player`. Serialized to clients (so handset/TV can show a bot icon if desired).

### 2. AddAI Hub Method

- Host-only, lobby phase only
- Creates a Player with `IsAI = true`, no real ConnectionId (use `"ai-{index}"`)
- Picks from a name pool: "Bot Alice", "Bot Bob", etc.
- Assigns next colour, broadcasts updated state

### 3. AiService (new singleton)

- Injected with `GameService` + `IHubContext<GameHub>`
- Single public method: `TriggerAiTurn()`
- Uses `Task.Delay` with randomised gaps (1–2s between actions)
- Broadcasts state after each action via `IHubContext`
- Handles all phases: InitialPlacement, Reinforce, Attack, Fortify

### 4. Wiring — Who Calls TriggerAiTurn?

After any turn advance that results in an AI player being current:
- `AdvancePlacementTurn()` — if next player is AI
- `EndTurn()` / `StartGame()` — if next player is AI

Rather than polluting GameService with AI awareness, the **hub** checks after broadcasting: if currentPlayer.IsAI, fire-and-forget `AiService.TriggerAiTurn()`.

Also needed: after `MoveAfterCapture` (AI continues attacking), and when game transitions from InitialPlacement → Playing (first player might be AI).

### 5. Tier 1 Random Logic

**InitialPlacement:**
- Pick random owned territory, place 1 army
- Repeat until ReinforcementsRemaining == 0

**Reinforce:**
- Trade cards if 5+ (pick first valid set)
- Place armies randomly on owned territories one at a time
- EndReinforce

**Attack:**
- 50% chance to attack at all (keeps games from dragging)
- If attacking: pick random owned territory with >1 army and adjacent enemy
- Use max dice (min of 3, source-1)
- After capture: move minimum armies in
- Repeat 1–3 times then EndAttack

**Fortify:**
- 50% chance to skip
- Otherwise: pick random territory with >1 army, move random amount to random adjacent owned territory
- EndTurn

### 6. Timing

- 1–2s delay before each discrete action
- Slightly faster during placement (0.5–1s)
- No parallelism — sequential awaits

### 7. Edge Cases

- AI with 5+ cards: auto-trade first valid set at start of reinforce
- AI eliminated mid-game: skip (already handled by turn advance loop)
- AI wins: normal GameOver flow
- Multiple AI: each takes turn sequentially when triggered
- Pending move-in: AI always moves minimum

### 8. Files to Create/Modify

| File | Action |
|------|--------|
| `Models/GameState.cs` | Add `IsAI` to Player |
| `Services/AiService.cs` | New — turn runner |
| `Hubs/GameHub.cs` | Add `AddAI` method, add post-broadcast AI trigger |
| `Program.cs` | Register `AiService` as singleton |

### 9. Not in Scope (Tier 1)

- No strategy, weighting, or threat assessment
- No personality or adaptive timing
- No mission awareness
- No blitz (single attacks only — simpler to implement)
- No UI changes on handset (AI just appears as a player)

---

*Created: 2026-06-21*
