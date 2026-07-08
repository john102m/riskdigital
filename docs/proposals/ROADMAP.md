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
| Blitz physics dice | Remote/spectator TV starts tumbling dice immediately when `AttackerDiceResult`/`DefenderDiceResult` arrive — concurrent with the rolling TV, not after. At settle time, snap faces to the received values (lerp ~0.2s, imperceptible). Both TVs appear to roll simultaneously, result revealed at the same moment. Same pattern for same-household spectator (test #3): receive both results, tumble all dice, snap at settle. `GetRotationForFace` already exists. New `SpawnSetWithTargetFaces(role, count, targetValues)` needed in DiceRoller — runs normal physics then applies face correction post-settle |
| **Simultaneous arena sweep (Phase 1)** | Server broadcasts `SpawnDice("attacker")` and `SpawnDice("defender")` to ALL TVs simultaneously (not just the owning TV). Both arenas sweep in at the same moment. Each TV only physically rolls the dice for its own role — the other set is ignored visually for now (static placement still used for remote dice). Server only accepts submissions from the correct TV per role. One-line server change (`SendAsync` to group instead of single client) + Unity handler opens arena on `SpawnDice` for non-owning role without spawning dice. **Phase 2** (later): replace static remote placement with real physics on both TVs — each TV rolls all dice, server uses only the authoritative submission per role, other TV's physics is eye candy. No snapping needed. |
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
