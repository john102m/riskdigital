# Proposal — Lobby & Join UX (All Surfaces)

*2026-07-08 — written post multi-household implementation*

---

## Overview

The lobby/join experience now spans three surfaces and two play scenarios.
The current UI was designed for single-screen single-game play and has not
been updated to reflect multi-household or multi-game realities.

---

## Play Scenarios

| Scenario | Setup | Who does what |
|----------|-------|---------------|
| **A — Local single-screen** | 1 TV, players on phones in same room | Handset lobby only. TV auto-joins. No household config needed. |
| **B — Local multi-screen** | 2+ TVs, players on phones in same room | Each TV joins same game. Household assignment needed. |
| **C — Remote multi-screen** | 2+ TVs, players on phones in different locations | As B but over internet. Latency irrelevant for turn-based. |
| **D — Multi-game** | 2+ separate games running simultaneously | TVs join different games. Independent lobbies. |

Scenario A is the original design target. Scenarios B/C are new. Scenario D
already works (multi-game server) but the join screen doesn't surface it well.

---

## Current State — Problems

### Unity TV Join Screen

- Game list shows code, player count, phase — functional but bare
- Household input is a `TMP_InputField` with a baked-in placeholder text bug
- Format is "england 0,2" — developer-only, not usable by real players
- No feedback on what household was registered as
- No indication of which players belong to this TV after joining
- Auto-join (`autoJoinGameCode` Inspector field) bypasses join screen entirely —
  useful for dev but means no household config applied
- Reconnect path re-registers without household config (loses routing)

### Handset Lobby

- Shows player list with index numbers (added recently for household debugging)
- No concept of household assignment visible to players
- No way for host to assign players to TVs from the handset
- Game code shown at top — good, but no copy button
- AI tier buttons — good, functional
- Placement mode toggle — good

### Cross-surface

- Players have no way to know which TV is "theirs"
- No visual confirmation that household routing is active
- No error/warning if TV joins without household config in multi-TV game

---

## Ideas & Options

### 1. Household Assignment

The core problem: how does a TV know which players belong to it?

**Option A — Inspector fields (current)**
Set `householdId` and `playerIndices` in Unity Inspector before build/run.
- ✅ Zero runtime UX needed
- ✅ Already works
- ❌ Requires separate builds per household
- ❌ Not usable by non-developers

**Option B — TV join screen picker**
After joining a game, TV shows the player list and lets user tap which players
are at this TV. Sets `householdId` and `playerIndices` at runtime.
- ✅ No separate builds needed
- ✅ Players can self-assign at their TV
- ❌ Requires UI work on Unity join screen
- ❌ Need to handle the "nobody assigned yet" state on handset

**Option C — Host assigns from handset**
Host sees all connected TVs listed in lobby. Drags/assigns players to TVs.
- ✅ Centralised control
- ✅ Host can see full picture
- ❌ Most complex — requires new server events + handset UI + TV display
- ❌ Host needs to know which TV is which

**Option D — Auto-detect by subnet**
Server compares handset IP to TV IP — same subnet = same household.
- ✅ Zero user effort for LAN play
- ❌ Breaks for remote play (different public IPs)
- ❌ NAT/VPN can confuse subnet matching

**Option E — QR code / join code per TV**
Each TV displays a short code or QR. Players scan/enter it on their handset
to associate themselves with that TV.
- ✅ Explicit and clear
- ✅ Works remote
- ❌ Extra step for players
- ❌ Requires QR generation or short code system

**Recommendation for now:** Option B (TV picker) for local play, Option A
(Inspector) for dev/test. Option E for future remote play.

---

### 2. Unity TV Join Screen

**Current:** Game list rows + code input + household text input.

**Proposed improvements:**

| # | Idea | Notes |
|---|------|-------|
| 1 | Remove household text input entirely — replace with player picker after joining | See Option B above |
| 2 | Show TV name / location on join screen ("This is the Scotland TV") | Set via Inspector, displayed prominently |
| 3 | After joining, show which players are assigned to this TV | Coloured player chips |
| 4 | Show server connection status indicator (connected / reconnecting) | Already have reconnect logic |
| 5 | Game list rows: show player names not just count | More useful than "3 players" |
| 6 | Auto-join: if only one game active, join automatically after 3s (with cancel option) | Reduces clicks for single-game setup |
| 7 | After game ends, show "Waiting for new game..." instead of returning to join screen immediately | Less jarring |
| 8 | Dark theme consistency — current row colours are parchment-ish, clashes with dark board | Match board dark theme |

---

### 3. Handset Lobby

**Current:** Player list + AI buttons + placement mode + start button.

**Proposed improvements:**

| # | Idea | Notes |
|---|------|-------|
| 1 | Remove player index numbers (added for debug, not player-facing) | Was useful for household config, no longer needed |
| 2 | Show which TV each player is assigned to (small TV icon or household name badge) | Needs household info from server |
| 3 | Copy game code button (tap code to copy) | Common friction point — "what's the code again?" |
| 4 | QR code for game join URL | Players scan instead of typing URL + code |
| 5 | Host: "Assign to TV" flow — tap player → choose TV | Option C implementation |
| 6 | Show connected TVs in lobby (TV icon + household name) | Host can confirm both TVs are connected before starting |
| 7 | House rules summary visible in lobby | Players can confirm settings before starting |

---

### 4. Server — New Events Needed (for some options)

| Event | Direction | Payload | Purpose |
|-------|-----------|---------|---------|
| `TVRegistered` | Server → All | `householdId, playerCount` | Handset lobby shows connected TVs |
| `HouseholdAssigned` | Server → TV | `playerIndices[]` | TV learns its assignment from host |
| `AssignToHousehold` | Handset → Server | `playerIndex, householdId` | Host assigns player to TV |

Only needed if going beyond Option A/B.

---

## Priority & Phasing

### Phase 1 — Fix current pain points (minimal effort)
- Remove player index numbers from handset lobby ← 1 line
- TV join screen: show registered household/players after join
- Fix reconnect to preserve household config (known bug from SESSION-2026-07-07)

### Phase 2 — TV player picker (Option B)
- After TV joins game, show player list
- Player taps their name on the TV to claim it
- TV sends `RegisterAsTVWithHousehold` with claimed player indices
- Removes need for Inspector fields in normal use

### Phase 3 — Handset lobby TV awareness
- Server broadcasts TV registrations
- Handset lobby shows connected TVs
- Players can see which TV they're assigned to

### Phase 4 — Remote play (Option E)
- QR code / short code per TV
- Players scan on handset to associate
- Works across internet

---

## Decisions Needed

- [ ] Single-screen local play: keep as-is (no household UI needed) or unify with multi-screen flow?
- [ ] TV player picker (Option B): tap-to-claim or host-assigns?
- [ ] Player index numbers in handset lobby: remove now or keep for household debugging?
- [ ] Auto-join on TV: useful or annoying?
- [ ] QR code: worth implementing for remote play or too much complexity?

---

*Created: 2026-07-08*
