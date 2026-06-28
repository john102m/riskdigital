# Handset Refactoring Notes

## Common Patterns Identified

### 1. Continent Accordion (extract component)

The collapsible continent pill + territory buttons pattern is repeated in **4 screens** (Placement, Reinforce, Attack source, Fortify source/target). Each has:
- `expanded` state (single string or null)
- Coloured pill button with ▶/▼ + continent name + count
- Conditional render of territory buttons inside

**Recommendation:** Extract `<ContinentAccordion>` component.

```tsx
interface ContinentAccordionProps {
  territories: Territory[];
  expanded: string | null;
  onToggle: (continent: string) => void;
  renderButton: (territory: Territory) => ReactNode;
}
```

This would eliminate ~20 lines of duplicated JSX per screen.

**Priority:** Medium. It works now, but 4 copies means 4 places to update if the style changes again.

---

### 2. Player context derivation (extract hook)

Every screen computes the same 4 values from props:
```tsx
const myIndex = gameState.players.findIndex((p) => p.name === playerName);
const me = gameState.players[myIndex];
const isMyTurn = gameState.currentPlayerIndex === myIndex;
const currentPlayer = gameState.players[gameState.currentPlayerIndex];
```

**Recommendation:** Extract `usePlayer(gameState, playerName)` hook returning `{ myIndex, me, isMyTurn, currentPlayer }`.

**Priority:** Low. It's 4 lines, but it's in every screen.

---

### 3. CARD_ICONS constant (duplicated)

Defined identically in both `ReinforceScreen.tsx` and `AttackScreen.tsx`:
```tsx
const CARD_ICONS: Record<string, string> = { Infantry: "⚔️", Cavalry: "🐎", Artillery: "💣", Wild: "🌟" };
```

**Recommendation:** Move to `utils/cardIcons.ts` or alongside `groupByContinent.ts`.

**Priority:** Low. Only 2 places, but easy win.

---

### 4. Card trade UI (duplicated)

The card selection + trade button UI appears in both ReinforceScreen (panel) and AttackScreen (forced trade modal). Same logic:
- `toggleCard` / `toggleTradeCard` — identical
- Card pill buttons with select highlighting
- "Trade N/3" button

**Recommendation:** Extract `<CardTradePanel cards={} onTrade={} />` component.

**Priority:** Medium. Keeps trade UI consistent and single place to style.

---

### 5. Hub invocation with error alert (repeated pattern)

Every hub call follows the same try/catch/alert pattern:
```tsx
try {
  await connection.invoke("MethodName", ...args);
} catch (e: any) {
  alert(e.message);
}
```

**Recommendation:** Utility function:
```tsx
async function invoke(connection: HubConnection, method: string, ...args: any[]) {
  try {
    await connection.invoke(method, ...args);
  } catch (e: any) {
    alert(e.message);
  }
}
```

**Priority:** Low. Reduces boilerplate but not critical.

---

### 6. PlacementScreen inconsistency

PlacementScreen still uses non-collapsible continent headers (plain `<div>` not tappable `<button>`). It's the only screen without accordion behaviour.

**Recommendation:** Either add accordion (same pattern) or leave it — placement has fewer territories since you only own ~14-21, so scrolling is less of an issue. Your call.

**Priority:** Low.

---

## Summary

| Refactor | Impact | Effort | Priority |
|----------|--------|--------|----------|
| ContinentAccordion component | High (4 screens) | ~30 min | Medium |
| CardTradePanel component | Medium (2 screens) | ~20 min | Medium |
| usePlayer hook | Low (cleanliness) | ~10 min | Low |
| CARD_ICONS to shared file | Low | ~2 min | Low |
| invoke() utility | Low | ~5 min | Low |
| PlacementScreen accordion | Consistency | ~10 min | Low |

## Recommendation

Do **ContinentAccordion** and **CardTradePanel** extractions if you plan more UI iteration. Skip if the screens are feature-complete and you're moving to other work (blitz, game over, TV). The current duplication is manageable at this scale.
