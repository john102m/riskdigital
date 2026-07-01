# Roadmap

## Tier A — Stabilise (Current)

| Item | Status |
|------|--------|
| Playtest combat state machine refactor | 🔄 In progress |
| Merge `refactor/combat-state-machine` → main | Pending |
| Fix reconnect deadlock (player index vs connection ID) | ✅ Done |
| Fix blitz panel stomp (state guards) | ✅ Done |
| Dice physics tuning — settle detection, damping, realism | Queued |
| Test dice endpoint polish (longer dwell, smooth camera) | Queued |
| DiceFaceReader verification (visual vs reported match) | Queued |

## Tier B — Unity Polish

| Item | Notes |
|------|-------|
| Sound effects on Unity | War horn, dice rattle, capture fanfare, elimination sting. Sources listed in IDEAS.md |
| Dice face textures | Pips visible — current FBX has them but tint overrides. Texture-preserving material |
| Arena lighting & floor material | Wood box lid feel, directional light for shadow |
| DicePanel frame/border | UI Image behind RawImage for TV-screen effect |
| Territory reinforcement pulse | Brief glow/scale on army placement |
| Blitz final dice — verify GetRotationForFace | Euler angles may need tweaking |
| Remove debug logging | DiceFace dot products, DiceRoller spawn/read |

## Tier C — Gameplay Depth

| Item | Notes |
|------|-------|
| Connected-path fortify | Move to any owned territory connected by owned chain (not just adjacent) |
| Taunts/chat | Predefined messages from handset → big toasts on TV. Family-friendly, fun |
| Spectator mode | Join as viewer only, see full board state |
| Game timer | Per-turn time limit, warning sound at threshold |
| Fog of war (variant) | Hide army counts for non-adjacent territories. House rule toggle |

## Tier D — Technical Ambition

| Item | Notes |
|------|-------|
| Tier 5 AI learning from play | Pipeline exists. Needs more game data to be meaningful |
| NUnit tests for game logic | Combat resolution, card validation, mission checking, elimination |
| LLM-powered AI taunting | Contextual trash talk based on game state. Personality-flavoured |
| AI mission concealment | Higher tiers hide intent, misdirect with fake targets |

## Tier E — Distribution & Access

| Item | Notes |
|------|-------|
| PWA handset | Install to homescreen, offline-capable (did this for Flutter) |
| Remote play UX | Already works architecturally. Needs: invite link, connection status, latency indicator |
| Family onboarding | QR code to join (print/display), guide.html polish, first-time tips |
| Player profiles | Stats across sessions (wins, territories captured, best blitz). localStorage or server file |

---

## Completed (archive)

- ✅ Full game loop (lobby → placement → reinforce → attack → fortify → game over)
- ✅ Card system with escalation, forced trades, territory bonuses
- ✅ 14 mission cards with elimination fallback
- ✅ Blitz attack with ML-predicted odds
- ✅ 5-tier AI (random → heuristic → strategic → personality → learning pipeline)
- ✅ Web TV board (parchment theme, dots, glow, sounds, activity feed)
- ✅ Handset (continent accordions, haptics, card UI, mission badges)
- ✅ Unity TV board (map, tokens, SignalR, dice arena, camera flypath)
- ✅ TV-driven dice physics (server delegates to Unity)
- ✅ Player-rolled dice (defender prompt, two-phase spawn)
- ✅ Combat state machine refactor
- ✅ Live deployment on WHUK (risk.spooch.co.uk)
- ✅ ML.NET training pipeline + auto-retrain on game-over
- ✅ House rules (locked attack front, fixed card values, missions toggle)

---

*Living document. Tiers are loose priority bands, not strict sequence.*
