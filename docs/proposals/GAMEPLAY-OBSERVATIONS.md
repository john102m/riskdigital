# Things I've Noticed During Gameplay

Running list of observations, niggles, and ideas from actual play sessions. Will round up into proper proposals when ready.

---

## 1. Defender Roll Timeout Is Awkward

**What happens:** Bot attacks me while I'm making tea. Server waits 60s for my defender roll, then silently falls back to random dice. I come back to lost territories with no explanation.

**Issues:**
- 60s is too short for a real break but too long for active players watching nothing happen
- Silent resolution — nobody knows random dice were used
- Attacker is stuck waiting with no way to push through

**Suggested fix:**
- Extend timeout to 10 minutes (it's a board game, not a trading floor)
- After 30s, give the attacker an "Auto-defend for [Name]" button
- If forced or timed out, roll max defender dice randomly and tell everyone via toast
- Defender can still roll normally at any point before the force

---



## 2. Bot Turn Camera Snaps Instead of Zooming

**What happens:** When it's the bot's turn, the TV camera is supposed to zoom to the action (attack source/target territories). Instead it snaps instantly — no smooth pan/zoom, just a jarring jump.

**Expected:** Smooth animated fly/zoom to the territories involved, so you can follow what the bot is doing on the map.

**Likely cause:** Camera transition duration is too short or missing entirely for bot actions. May be using `transform.position =` instead of a lerp/flypath.

---



## 3. Card Trade UI — Cards Feel Too Large

**What happens:** When looking at my card set to decide whether to trade, the cards feel oversized. Takes up too much screen space on the handset.

**Expected:** Cards should be compact enough to see all of them at a glance without scrolling, especially when you have 4-5 cards.

---

