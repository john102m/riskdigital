# Proposal: Mission Fallback Notification + Card Trade UI Overhaul

## 1. Mission Fallback Bug Fix

### Problem
When Player A has "Eliminate Blue" and Player B kills Blue instead, the server sets `FallenBackToWorldDomination = true` but never sends `MissionUpdated` to Player A. Their handset still shows the old mission.

### Fix

**GameService.Combat.cs** — return affected player indices:

```csharp
// Change return type
public (GameState State, bool ForcedTradeRequired, int EliminatedPlayerIndex, bool MissionWon, List<int> MissionFallbackPlayers) MoveAfterCapture(...)

// Inside the fallback loop, collect them:
var fallbackPlayers = new List<int>();
for (int p = 0; p < _state.Players.Count; p++)
{
    if (p == _state.CurrentPlayerIndex) continue;
    var m = _state.Players[p].Mission;
    if (m is { Type: MissionType.Elimination } && m.TargetPlayerIndex == i)
    {
        m.FallenBackToWorldDomination = true;
        fallbackPlayers.Add(p);
    }
}

// Return with new field
return (_state, forcedTrade, defenderId, missionWon, fallbackPlayers);
```

**GameHub.cs** — send `MissionUpdated` to affected players:

```csharp
public async Task MoveAfterCapture(int sourceId, int targetId, int armies)
{
    var (state, forcedTrade, eliminatedIndex, missionWon, fallbackPlayers) = _game.MoveAfterCapture(...);
    
    // ... existing broadcasts ...

    // Notify players whose mission fell back
    foreach (var pi in fallbackPlayers)
    {
        var p = state.Players[pi];
        if (!p.IsAI && p.ConnectionId is not null)
            await Clients.Client(p.ConnectionId).SendAsync("MissionUpdated", p.Mission);
    }

    await BroadcastState(state);
}
```

**Handset (useConnection.ts)** — already handles `MissionUpdated`. No change needed there.

**Handset notification** — show a brief toast when mission changes. In `App.tsx`, detect when the mission's `fallenBackToWorldDomination` flips from false to true:

```tsx
// In the MissionUpdated handler (useConnection.ts or App.tsx):
const [missionToast, setMissionToast] = useState<string | null>(null);

// When MissionUpdated arrives:
connection.on("MissionUpdated", (mission) => {
  if (mission.fallenBackToWorldDomination && !prevMission?.fallenBackToWorldDomination) {
    setMissionToast("Your target was eliminated — mission is now world domination");
    setTimeout(() => setMissionToast(null), 5000);
  }
  setMission(mission);
});
```

Display as a simple amber banner at the top (same pattern as the tradeable-set hint):

```tsx
{missionToast && (
  <div className="fixed top-4 left-4 right-4 bg-amber-900/90 text-amber-200 px-4 py-3 rounded-lg text-sm font-medium text-center z-50">
    {missionToast}
  </div>
)}
```

### Files Changed
- `server/Risk.Server/Services/GameService.Combat.cs` — return fallback player list
- `server/Risk.Server/Hubs/GameHub.cs` — send MissionUpdated to affected players
- `handset/src/hooks/useConnection.ts` — detect fallback change, show toast

---

## 2. Card Trade UI Overhaul

### Problem
The current card trade panel is a small inline section that slots in at the top of the Reinforce screen. Cards are tiny `px-2 py-1` pill buttons (`text-xs`) in a flex-wrap row. On a phone:
- Touch targets are too small (especially with 5+ cards)
- Easily missed — buried under a 🃏 badge tap
- No visual prominence — feels like a secondary feature when it's actually critical
- Territory names in the pills are often truncated to uselessness

### Current flow
1. Badge shows 🃏 count → tap to expand
2. Small cards row appears at top, squeezing the territory list
3. Tap 3 cards (tiny targets) → tap "Trade 3/3" button
4. Panel collapses back

### Proposed: Full-Screen Card Trade Overlay

Match the pattern already used for:
- Forced trade modal (full screen, centred, no other UI)
- Defend prompt (full screen overlay)
- "Not your turn" screens (full screen, player name centred)

**New behaviour:**
1. Tap 🃏 badge → full-screen overlay slides up (covers territory list entirely)
2. Cards displayed as large tappable tiles in a 2-column grid
3. Each tile: card icon (large), type name, territory name (if any) — full width, big touch target
4. Selected cards get a thick amber border + checkmark
5. "Trade" button at bottom (full width, same size as "Done → Attack")
6. "Cancel" / ✕ button to dismiss without trading
7. Same overlay used for forced trade (but without Cancel — must trade)

### Visual spec

```
┌────────────────────────────────┐
│  ✕                  Trade Cards │  ← header row
├────────────────────────────────┤
│                                 │
│  ┌─────────┐  ┌─────────┐     │
│  │  ⚔️     │  │  🐎     │     │  ← 2-col grid
│  │Infantry │  │ Cavalry  │     │
│  │ Brazil  │  │  Wild    │     │
│  │  [✓]    │  │          │     │
│  └─────────┘  └─────────┘     │
│                                 │
│  ┌─────────┐  ┌─────────┐     │
│  │  💣     │  │  🌟     │     │
│  │Artillery│  │  Wild    │     │
│  │ Congo   │  │          │     │
│  │  [✓]    │  │          │     │
│  └─────────┘  └─────────┘     │
│                                 │
│  ┌─────────┐                   │
│  │  ⚔️     │                   │
│  │Infantry │                   │
│  │ Alaska  │                   │
│  │  [✓]    │                   │
│  └─────────┘                   │
│                                 │
├────────────────────────────────┤
│  [    Trade 3/3 → +10 ⚔️     ]│  ← shows bonus preview
└────────────────────────────────┘
```

### Card tile component

```tsx
function CardTile({ card, territory, selected, onTap }: {...}) {
  return (
    <button
      onClick={onTap}
      className={`flex flex-col items-center justify-center p-4 rounded-xl border-2 
        ${selected ? "border-amber-400 bg-amber-900/40" : "border-white/10 bg-gray-800"}
        active:scale-95 transition-all touch-manipulation min-h-[100px]`}
    >
      <span className="text-3xl">{CARD_ICONS[card.type]}</span>
      <span className="text-sm font-bold mt-1">{card.type}</span>
      <span className="text-xs text-gray-400 mt-0.5">{territory?.name ?? ""}</span>
      {selected && <span className="text-amber-400 text-xs mt-1">✓ Selected</span>}
    </button>
  );
}
```

### Trade bonus preview

When 3 cards are selected, show what the trade is worth:
- "Trade 3/3 → +4 ⚔️" (Infantry set)
- "Trade 3/3 → +10 🃏" (One of each)
- Highlight any territory bonus: "+2 on Brazil (owned)"

### Forced trade uses same overlay

Remove the separate forced-trade return in ReinforceScreen and AttackScreen. Instead:
- `forcedTrade` triggers the same full-screen card overlay
- Header says "Must trade! (X cards held)"
- No Cancel button — overlay stays until they trade down below 5

### Implementation

| File | Change |
|------|--------|
| `CardTradePanel.tsx` | Replace inline panel with full-screen overlay. Add CardTile grid, bonus preview, cancel button. |
| `ReinforceScreen.tsx` | Remove inline card section. 🃏 badge opens the full-screen overlay. Remove `mustTrade` early return (use overlay instead). |
| `AttackScreen.tsx` | Remove separate forced-trade return block. Use same overlay. |
| (optional) `CardTradeOverlay.tsx` | Extract to own component if cleaner. |

### Behaviour summary

| Trigger | Overlay style |
|---------|--------------|
| Tap 🃏 badge (voluntary) | Full screen, Cancel button available |
| 5+ cards at reinforce start | Full screen, no Cancel, "Must trade!" header |
| Post-elimination forced trade | Full screen, no Cancel, "Captured cards — must trade!" header |
| Tradeable set hint (amber banner) | Tapping it opens the full-screen overlay |

---

## Priority

1. Mission fallback fix — 10 minutes, critical bug
2. Card trade overlay — 30–45 minutes, UX improvement for playtest
