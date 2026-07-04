# Replace JavaScript Alerts with Toast Notifications

## Problem
14 `alert()` calls in the handset codebase. These are native browser popups — ugly, block interaction, and feel broken on a phone. They fire when a hub invocation fails (e.g. "Not your turn", "Not enough armies").

## Current Usage

| File | Count | Context |
|------|-------|---------|
| `AttackScreen.tsx` | 5 | Attack, Blitz, RollDice, MoveAfterCapture, EndAttack |
| `LobbyScreen.tsx` | 3 | CreateGame, JoinGame, StartGame |
| `FortifyScreen.tsx` | 2 | Fortify, EndTurn |
| `ReinforceScreen.tsx` | 2 | Reinforce, EndReinforce |
| `CardTradePanel.tsx` | 1 | TradeCards |
| `PlacementScreen.tsx` | 1 | PlaceArmy |

All follow the same pattern:
```tsx
} catch (e: any) {
  alert(e.message);
}
```

## Proposed Solution

Replace with a simple toast component — auto-dismissing notification at the top or bottom of the screen.

### Toast Component
```tsx
// components/Toast.tsx
export function Toast({ message, onDismiss }: { message: string; onDismiss: () => void }) {
  useEffect(() => {
    const t = setTimeout(onDismiss, 3000);
    return () => clearTimeout(t);
  }, []);

  return (
    <div className="fixed top-4 left-4 right-4 bg-red-900/90 text-white px-4 py-3 rounded-lg text-sm font-medium text-center z-50 animate-pulse">
      {message}
    </div>
  );
}
```

### Hook or State
Add `toastMessage` state to each screen (or lift to App.tsx and pass a `showToast` function down):

```tsx
const [toast, setToast] = useState<string | null>(null);

// In catch blocks:
} catch (e: any) {
  setToast(e.message);
}

// In JSX:
{toast && <Toast message={toast} onDismiss={() => setToast(null)} />}
```

### Behaviour
- Appears at top of screen over content
- Red/dark background, white text
- Auto-dismisses after 3 seconds
- Tap to dismiss (optional)
- Non-blocking — player can still interact

## Scope
- Create `Toast.tsx` component
- Add toast state (App-level or per-screen)
- Replace all 14 `alert()` calls
- Replace the `confirm("Skip attack phase?")` in AttackScreen with a nicer inline confirmation
- No new dependencies needed (pure Tailwind + React)
