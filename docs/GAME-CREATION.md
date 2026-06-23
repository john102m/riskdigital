# Discussion: Game Creation & Late Joiners

## Problem

Anyone who opens the handset can tap "Create Game" — even if a game is already in progress. This nukes the active game for everyone.

Real-world scenario: daughter opened the app during a live game and hit Create Game, resetting everything.

## Sub-problems

1. **Accidental reset** — Create Game shouldn't be available when a game is running
2. **Late arrivals** — people who arrive after game start need a smooth path in (currently they see Create/Join but don't know the code)
3. **Spectators** — should non-players be able to watch without joining?

## Options

### A. Server-side guard only
- `CreateGame` throws an error if a game already exists (not in GameOver state)
- Handset shows the error message — user realises and taps Join instead
- Minimal change, slightly clunky UX

### B. Lobby status check (currently implemented but unapproved)
- Handset asks server on connect: "is there a game running?"
- If yes: hide Create Game, show Join with code pre-filled
- If no: show both buttons as normal
- Smoother UX — late arrivals land on Join screen ready to go

### C. Auto-rejoin only (no Create on production)
- Remove Create Game entirely from the handset UI
- Only the host (or admin endpoint) can create games
- Everyone else always joins via code
- Simplest — but means you need another mechanism to start a game (admin page? host-only button?)

### D. Host PIN / confirmation
- Create Game requires a PIN or host password
- Overkill for a family game?

## Current State of Code

I already implemented Option B without approval. The changes are in:
- `server/Risk.Server/Services/GameService.cs` — `GetLobbyStatus()` + CreateGame guard
- `server/Risk.Server/Hubs/GameHub.cs` — `GetLobbyStatus` hub method
- `handset/src/components/ConnectScreen.tsx` — lobby status check, hide Create, auto-fill code

These can be kept, reverted, or modified. They compile and type-check clean.

## Questions

1. Is Option B (lobby status + hide Create) the right approach?
2. Should late joiners during an active game be able to join as players (if slots available) or spectator-only?
3. Should the `/admin/reset` endpoint also be protected somehow — or is it fine since only you know the URL?
4. Any other edge cases to consider?

---

*Created: 2026-06-22*
