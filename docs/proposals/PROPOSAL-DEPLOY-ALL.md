# Proposal: Deploy All Button + Haptic Feedback ✅ IMPLEMENTED

## Status: Done (2026-06-23)

During Reinforce (and Initial Placement), you might want to dump 30 armies on one territory. Currently you must tap 30 times. Tedious.

## Solution

Split each territory button into two touch targets:

```
┌──────────────────────────────────┬───────────┐
│  Ukraine                    (5)  │  All (28) │
│  [tap = +1]                      │  [deploy] │
└──────────────────────────────────┴───────────┘
```

- **Left (main area):** tap = place 1 army (current behaviour)
- **Right ("All" pill):** tap = place ALL remaining armies on this territory

Both trigger haptic feedback — short pulse for +1, longer buzz for deploy-all.

## Applies To

- **ReinforceScreen** — place reinforcements (0–30+ remaining)
- **PlacementScreen** — place initial armies (same pattern)

## Server Change

Add `count` parameter to `Reinforce` hub method:

```csharp
// GameHub.cs
public async Task Reinforce(int territoryId, int count = 1)

// GameService.cs
public GameState Reinforce(string connectionId, int territoryId, int count = 1)
{
    // ... existing validation ...
    var actual = Math.Min(count, player.ReinforcementsRemaining);
    territory.Armies += actual;
    player.ReinforcementsRemaining -= actual;
    return _state;
}
```

Same for `PlaceArmy` — add optional count:
```csharp
public async Task PlaceArmy(int territoryId, int count = 1)
```

## Handset Change

Territory button becomes a flex row:

```tsx
<div className="flex items-center gap-1 w-full">
  <button onClick={() => place(t.id, 1)} className="flex-1 ...">
    {t.name} <span>{t.armies}</span>
  </button>
  {remaining > 1 && (
    <button onClick={() => place(t.id, remaining)} className="...">
      All ({remaining})
    </button>
  )}
</div>
```

## Haptic Feedback

```ts
// utils/vibrate.ts
export function tap() { navigator.vibrate?.(10); }        // +1
export function heavyTap() { navigator.vibrate?.(30); }   // deploy all
```

## Edge Cases

- "All" button only shows when remaining > 1 (if 1 left, main tap is sufficient)
- PlaceArmy server-side: clamp count to remaining, same validation otherwise
- AI callers already place one-at-a-time — no change needed for them

## Files to Modify

| File | Change |
|------|--------|
| `server/Risk.Server/Hubs/GameHub.cs` | Add count param to Reinforce + PlaceArmy |
| `server/Risk.Server/Services/GameService.cs` | Handle count in Reinforce + PlaceArmy |
| `handset/src/components/ReinforceScreen.tsx` | Split button, call with count |
| `handset/src/components/PlacementScreen.tsx` | Split button, call with count |
| `handset/src/utils/vibrate.ts` | Add heavyTap() |

---

*Created: 2026-06-23*
