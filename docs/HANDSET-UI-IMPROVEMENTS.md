# Handset UI Improvements

Target device: Samsung Galaxy A25 5G (6.5" display, large system font).

## Problem

With large font scaling, effective viewport shrinks significantly. Accordion headers, badges, labels, and action buttons all compete for vertical space. The Attack screen is worst — up to 6 distinct UI sections visible simultaneously.

---

## General Principles

1. **Vertical space is king** — every pixel of non-interactive chrome is a cost
2. **One decision at a time** — show only what the player needs *right now*
3. **Touch targets stay ≥44px** — don't sacrifice accessibility for density
4. **Progressive disclosure** — reveal complexity only when relevant
5. **Scrollable content, fixed actions** — action buttons never scroll off-screen

---

## Per-Phase Analysis & Proposals

### Lobby

**Current:** Game code, player list, start button. Low density — fine as-is.

**Improvements:**
- None needed. Simplest screen.

---

### Initial Placement

**Current:** Phase badge + status line + "armies remaining" + continent accordion with territory buttons.

**Issues:**
- Header takes 3 lines before any territory is visible
- With large font, only 1 continent visible without scrolling

**Proposals:**
| # | Change | Impact |
|---|--------|--------|
| 1 | Merge phase badge + status into single compact line: `🏰 Place army · 8 left` | Saves ~32px |
| 2 | Remove "Your turn" / "Waiting for X" — grey out entire screen when not your turn (obvious without text) | Saves ~24px |
| 3 | Show count in continent header: `▼ Africa (3) · 12 armies` — total armies in that continent at a glance | Info without opening |

---

### Reinforce

**Current:** Phase badge + card pill + status + card trade hint + card panel (expanded) + continent accordion + Done button.

**Issues:**
- Card panel when open pushes territory list far down — you can't see where to place
- Must trade message + card panel + accordion = very crowded with large font
- "Done → Attack" button can scroll off-screen if card panel is open

**Proposals:**
| # | Change | Impact |
|---|--------|--------|
| 1 | Compact header: `🎯 Reinforce · 5 to place · 🃏3` — single line | Saves ~40px |
| 2 | Card panel as bottom sheet (slides up from bottom, overlays territory list) instead of inline | Prevents layout push |
| 3 | Pin "Done → Attack" button to bottom (already mt-auto, but card panel can push it) — use sticky positioning | Button always reachable |
| 4 | When must-trade: show ONLY the card panel (hide territory list entirely) — can't place anyway | Eliminates confusion |
| 5 | Card buttons slightly larger with grid layout (2-col) instead of flex-wrap — easier fat-finger tap | Better touch accuracy |

---

### Attack (most complex)

**Current layout when active:**
1. Phase badge
2. Last combat result
3. Idle hint
4. "ATTACK FROM:" label + continent accordion (source)
5. "ATTACK:" label + target pills
6. Dice selector row + Attack/Blitz buttons
7. "Done Attacking" button (mt-auto)

**Issues:**
- After choosing source + target, dice row + 2 buttons + done button + combat result = almost full screen on large font
- Source accordion stays open (by design for rapid re-attack) but steals space from target list
- idle hint takes a line even when not needed
- Labels ("ATTACK FROM:", "ATTACK:") take space

**Proposals:**
| # | Change | Impact |
|---|--------|--------|
| 1 | Collapse source accordion after source selected — show selected source as compact chip: `🟢 Brazil (8) ✕` (tap ✕ to reselect) | Reclaims all accordion space |
| 2 | Move combat result to a brief toast/banner that fades (3s), not persistent block | Saves ~40px between attacks |
| 3 | Dice selector: default to max dice (most common choice), show as small toggle only if player wants fewer. Most players always pick 3. | Saves a row on >90% of attacks |
| 4 | Merge dice row with attack buttons: `⚔️ Attack (3🎲)` / `⚡ Blitz` — tap-and-hold Attack for dice options | One row instead of two |
| 5 | Remove idle hint — context is obvious from what's highlighted | Saves ~24px |
| 6 | Remove text labels ("ATTACK FROM:", "ATTACK:") — use colour coding: green-bordered section = source, red-bordered = target | Saves ~20px each |
| 7 | "Done Attacking" → smaller amber text button at top-right rather than full-width bottom bar (freed by collapsing source) | More scroll space |
| 8 | After capture, move-in screen is fine (dedicated full-screen) — no change needed |

**Alternative: wizard flow**
Instead of showing source + target + dice simultaneously, step through:
1. Pick source (full screen accordion)
2. Pick target (full screen list — source shown as chip at top)
3. Dice + Attack/Blitz (large buttons, easy tap)
4. Result → move-in or back to step 1

Pro: maximum space per step. Con: more taps for experienced players doing rapid attacks. Could offer a "compact mode" toggle.

---

### Fortify

**Current:** Phase badge + source accordion + target pills + army stepper + Skip/Fortify buttons.

**Issues:**
- Moderate density — better than Attack but still cramped if source continent has many territories

**Proposals:**
| # | Change | Impact |
|---|--------|--------|
| 1 | Same chip pattern as Attack: collapse source accordion after selection | Reclaims space for stepper |
| 2 | Compact header: single line | Saves ~24px |
| 3 | Army stepper: add slider (range input) alongside +/- for quick large moves | Faster than tapping + 20 times |
| 4 | Pre-select "Max" as default (most fortifies move max from rear to front) | One fewer tap in common case |

---

### Game Over

**Current:** Fine — low density, just results.

---

## Cross-Cutting Improvements

| # | Change | Applies to | Impact |
|---|--------|-----------|--------|
| A | Compact mode toggle (saved to localStorage) — smaller font in territory lists, tighter padding | All phases | ~20% more visible at cost of readability |
| B | Swipe-to-advance between turns (instead of Done button tap) — swipe left = advance | Reinforce, Attack, Fortify | Frees bottom button space |
| C | Reduce accordion header height from py-1.5 to py-1, text-sm → text-xs | All phases | ~6px per continent header ×6 = 36px |
| D | Territory buttons: army count as superscript badge instead of `(8)` suffix — shorter text = less wrapping | Attack/Fortify targets | Fewer 2-line buttons |
| E | Current player indicator: thin coloured top border on screen (2px) instead of text "Waiting for X" | All phases | Saves entire text line |
| F | Vibration on turn start (navigator.vibrate) — player knows without looking at screen | All phases | Awareness without chrome |
| G | When not your turn: show minimal waiting screen (just player colour + name + phase) — no accordions, no buttons | All phases | Dramatic simplification |

---

## Priority Ranking

High value, low effort (do first):
1. **G** — minimal waiting screen (already partially done for Attack/Fortify)
2. **1 (Attack)** — collapse source to chip after selection  
3. **4 (Reinforce)** — must-trade shows only card panel
4. **3 (Attack)** — default to max dice
5. **E** — coloured top border replaces "Waiting for" text

Medium value:
6. **2 (Reinforce)** — card panel as bottom sheet
7. **4 (Attack)** — merge dice + attack buttons
8. **1 (Placement/Reinforce)** — compact single-line headers
9. **D** — army count as badge not suffix
10. **F** — vibration on turn

Lower priority / bigger effort:
11. Attack wizard flow (alternative to compact mode)
12. **A** — compact mode toggle
13. **B** — swipe-to-advance

---

## Decisions Needed

- [ ] Chip-collapse vs keep-source-open (Attack): rapid re-attack from same source is common — is one extra tap to reopen acceptable?
- [ ] Wizard flow vs compact simultaneous (Attack): preference?
- [ ] Default max dice: always 3 unless player overrides — OK?
- [ ] Bottom sheet for cards: worth the complexity vs just hiding territory list?
- [ ] Compact mode toggle: worth maintaining two layouts?

---

*Created: 2026-06-22*
