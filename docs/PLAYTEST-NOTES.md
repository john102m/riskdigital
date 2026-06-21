# Play Test Notes

Jot issues, tweaks, and ideas here during play. We'll batch them up after.

---

## Bugs

- Blitz button not visible on phone (off-screen) — visible on desktop browser. Likely the dice picker + Attack + Blitz row overflows on narrow viewport with larger font


## UI Tweaks

- ~~On game enter, show welcome modal with mission description + hint about 🎯 icon top right~~ ✅ (prev session)
- ~~Status panel (📊) — tap to see mission progress, territory count, continent breakdown~~ ✅ (prev session)
- ~~When opening card panel, auto-collapse the accordion to free up screen space~~ ✅
- ~~Hint/popup at start of reinforce if you have a tradeable set (easy to miss)~~ ✅
- ~~"ATTACK FROM" / "ATTACK:" etc instruction labels too faded (grey) — bump contrast slightly~~ ✅
- ~~All buttons/text: add `select-none` (no text selection on long-press)~~ ✅
- ~~Attack phase: don't collapse the source (attacker) accordion after selecting~~ ✅
- ~~Debug TV (and future Unity board): show dice roll results on combat~~ ✅
- ~~Move troops after capture: offer a "Max" button (all but one) for quick advance~~ ✅
- ~~Blitz button not visible on phone — split dice/buttons into two rows~~ ✅

## Ideas



- After move-in completes (capture), auto-open the accordion for the continent where the player's strongest attacking candidate is (most armies with adjacent enemies) — not necessarily the captured territory's continent if you only moved 1 in
- Idle hint: if no interaction for ~5s, show a subtle context hint (e.g. "Choose a target to attack", "Tap Attack or Blitz", "Move troops in") based on current sub-state — fades in, disappears on next tap
- Blitz move-in screen: show a summary of the blitz outcome (rounds fought, troops lost each side) so you know the cost before deciding how many to move in
- Fortify: now that accordions are in place, may be able to revert from multi-step (Source → Target → Stepper) back to a single screen — less tapping
- Fortify stepper: add Max button (same as move-in stepper)
- House rule option: fixed card trade values (classic UK rules) — Infantry=4, Cavalry=6, Artillery=8, One-of-each=10. No escalation. Toggle vs current escalating system. Probably yes.
- Web-based TV board: develop tv.html into a proper web board (not just debug) — could be shared via URL for remote play with family. Means the game works without Unity/Fire TV at all — just a browser tab on any screen. Unity version becomes the premium local experience, web board is the accessible/remote one.
- Taunts/chat: predefined taunt messages players can send from handset, displayed as big toasts on the TV board. Keeps it fun and family-friendly without typing. E.g. "😈 Coming for you!", "🏳️ Surrender!", "💀 You're next!"

## Blitz Decisions

- Blitz always uses max dice each round (up to 3) — no player choice
- Drops to fewer dice as source depletes (4→3dice, 3→2dice, 2→1die, 1→stops)
- Fights until capture or source = 1
- **Move-in minimum after blitz**: dice used on the *final capturing roll* (same rule as manual attack — you committed those dice, you must move at least that many in)
- If source barely survived (e.g. 2 armies left, final roll was 1 die) → min move = 1
- If source is strong (4+ left, final roll was 3 dice) → min move = 3
- Player cannot choose dice count for blitz — that's the trade-off vs manual Attack
- Possible future idea: "cautious blitz" with user-chosen max dice cap (non-standard, parked)

## TV Layout (Web Board)

- Map fills entire viewport, all UI overlaid on top ✅
- Info box (game code, phase, players) — bottom-left overlay in Pacific Ocean area ✅
- Dice results — bottom-right overlay, 5s fade ✅
- **Image**: using `risk-board-game-map-cropped.jpg` (map content only, no black borders)
- **Overlay sync**: JS `syncOverlay()` calculates actual rendered image area using `naturalWidth/naturalHeight` and positions dot overlay to match. Fires on load, resize, and when map becomes visible. Handles `object-fit: contain` pillarboxing/letterboxing correctly.
- **Dots hidden until positioned** — opacity 0 → fade in after sync, prevents snap-in flash
- **Circle scaling**: `2.5vw` diameter, `1vw` font — scales proportionally across screen sizes
- **Tested working**: Edge (desktop), Fire TV Stick Silk browser, portable TV
- **JVC built-in browser**: dots stretched on X axis — likely non-standard CSS rendering. Low priority edge case.
- **F11 fullscreen (Edge/Chrome)**: dots drift outward from centre — Egypt (near centre) is correct, but further territories are increasingly offset in ±X and ±Y. Classic overlay/image size mismatch scaling error. `window.resize` event may not fire on F11, or fires before layout updates. Dots are correct before F11 and on actual TV targets. Desktop-only dev issue — park for now.
- Mission badge (🎯) popup: dismiss by tapping anywhere outside it, not just the badge again