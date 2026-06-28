# Proposal: Attack Dice 2-Row Layout & Always-Visible Card Badge

## Problem 1 — Attack Dice Buttons Are Too Small

When a source and target are selected in `AttackScreen.tsx`, the action row is:

```
[ ⚔️ {effectiveDice}🎲 (flex-1) ] [ ⚡ Blitz (flex-1) ] [ 1 ][ 2 ]
```

The alternative dice buttons (1, 2, or 3 excluding the current `effectiveDice`) are `px-2 py-2 text-xs` — poor touch targets on a phone. They get squeezed into whatever space remains after the two primary buttons.

### Solution

Split into two rows:

**Row 1** — Primary actions (full width, generous touch targets):
```
[ ⚔️ Attack {effectiveDice}🎲  (flex-1) ] [ ⚡ Blitz  (flex-1) ]
```

**Row 2** — Dice choice (all options including current, equally sized):
```
[ 1🎲  (flex-1) ] [ 2🎲  (flex-1) ] [ 3🎲  (flex-1) ]
```

- Show all dice options 1 through `maxDice` (not just the non-selected ones).
- Highlight the currently selected dice count with a distinct background (e.g. `bg-amber-600` vs `bg-gray-700`).
- Each button is `flex-1 py-2 rounded font-bold text-sm` — much better touch target.
- Row 2 only renders when `maxDice > 1` (if you can only roll 1, there's no choice to make).

### Code Change — `AttackScreen.tsx`

Replace the current single-row attack buttons block (~line 181–193):

```tsx
{sourceId !== null && targetId !== null && (
  <div className="mb-3 flex flex-col gap-2">
    {/* Row 1: Attack + Blitz */}
    <div className="flex gap-2">
      <button onClick={attack} disabled={maxDice < 1} className="flex-1 bg-red-600 active:bg-red-700 px-4 py-3 rounded-lg font-bold disabled:opacity-30">
        ⚔️ {effectiveDice}🎲
      </button>
      <button onClick={blitz} disabled={maxDice < 1} className="flex-1 bg-purple-600 active:bg-purple-700 px-4 py-3 rounded-lg font-bold disabled:opacity-30">
        ⚡ Blitz
      </button>
    </div>
    {/* Row 2: Dice choice */}
    {maxDice > 1 && (
      <div className="flex gap-2">
        {[1, 2, 3].filter(d => d <= maxDice).map((d) => (
          <button key={d} onClick={() => setDiceCount(d)} className={`flex-1 py-2 rounded-lg font-bold text-sm ${d === effectiveDice ? "bg-amber-600" : "bg-gray-700"}`}>
            {d}🎲
          </button>
        ))}
      </div>
    )}
  </div>
)}
```

---

## Problem 2 — Card Badge Only Visible During Your Reinforce Phase

The 🃏 badge (showing card count, tappable to open CardTradePanel) only appears:
- In `ReinforceScreen` when `isMyTurn` is true.
- Never in the "not my turn" waiting views.
- Never during Attack or Fortify phases (even though `cards` is passed to `AttackScreen`).

Players can't view their collected cards while waiting or during non-reinforce phases.

### Solution

Add a **read-only card badge** to every "not my turn" waiting screen and to the active Attack/Fortify views. When tapped, it shows the cards in a view-only list (no trade button — trading is only valid during your reinforce phase).

Implementation approach — a small `CardBadge` component rendered in `App.tsx` as an overlay, visible whenever the player has cards and the game is in the Playing phase:

### New Component — `components/CardBadge.tsx`

```tsx
import { useState } from "react";
import { Card } from "../types/game";

interface Props {
  cards: Card[];
  canTrade?: boolean;
}

export function CardBadge({ cards, canTrade }: Props) {
  const [open, setOpen] = useState(false);

  if (cards.length === 0) return null;

  return (
    <>
      <button
        onClick={() => setOpen(!open)}
        className="fixed top-2 right-2 z-50 min-h-[36px] px-3 flex items-center justify-center rounded-full bg-gray-700 text-sm text-white shadow-lg"
      >
        🃏 {cards.length}
      </button>
      {open && (
        <div className="fixed inset-0 z-40 bg-black/70 flex items-center justify-center p-4" onClick={() => setOpen(false)}>
          <div className="bg-gray-800 rounded-lg p-4 max-w-sm w-full max-h-[60vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h3 className="text-white font-bold mb-3">Your Cards</h3>
            <div className="flex flex-col gap-2">
              {cards.map((c, i) => (
                <div key={i} className="flex items-center justify-between bg-gray-700 rounded px-3 py-2 text-sm text-white">
                  <span>{c.territoryName ?? `Territory ${c.territoryId}`}</span>
                  <span className="text-gray-400">{c.type}</span>
                </div>
              ))}
            </div>
            {!canTrade && (
              <p className="text-xs text-gray-500 mt-3 text-center">Trading available during your Reinforce phase</p>
            )}
          </div>
        </div>
      )}
    </>
  );
}
```

### Change in `App.tsx`

In the Playing phase block, render `CardBadge` alongside every phase screen (except where `ReinforceScreen` already has its own inline badge). The badge sits as a fixed overlay so it doesn't interfere with layout:

```tsx
import { CardBadge } from "./components/CardBadge";

// ... inside the Playing phase:

if (gameState.turnPhase === "Reinforce") {
  // ReinforceScreen already has its own inline card badge when it's your turn.
  // But show the overlay badge for not-my-turn (ReinforceScreen's waiting view has no badge).
  const myIndex = gameState.players.findIndex(p => p.name === playerName);
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  return <>
    <MissionBadge mission={mission} />
    <StatusBadge mission={mission} gameState={gameState} playerName={playerName} />
    {!isMyTurn && <CardBadge cards={cards} />}
    <ReinforceScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} />
  </>;
}

if (gameState.turnPhase === "Attack") {
  return <>
    <MissionBadge mission={mission} />
    <StatusBadge mission={mission} gameState={gameState} playerName={playerName} />
    <CardBadge cards={cards} />
    <AttackScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} forcedTrade={forcedTrade} clearForcedTrade={clearForcedTrade} />
  </>;
}

if (gameState.turnPhase === "Fortify") {
  return <>
    <MissionBadge mission={mission} />
    <StatusBadge mission={mission} gameState={gameState} playerName={playerName} />
    <CardBadge cards={cards} />
    <FortifyScreen connection={connection} gameState={gameState} playerName={playerName} />
  </>;
}
```

### Existing ReinforceScreen Badge

Keep the existing inline 🃏 badge inside `ReinforceScreen` for the active player — it integrates with the `CardTradePanel` for trading. The new fixed `CardBadge` is for read-only viewing in all other contexts.

To avoid showing two badges during your own reinforce turn, the `App.tsx` change only adds `CardBadge` when `!isMyTurn` for the Reinforce phase.

For the Attack phase when it IS your turn — `AttackScreen` currently doesn't show an inline card badge (it only has the forced-trade modal). The new fixed `CardBadge` overlay covers this gap. If you want to keep the option to trade during Attack (not currently supported by game rules), `canTrade` can be passed as `false` for view-only.

---

## Summary of Files Changed

| File | Change |
|------|--------|
| `handset/src/components/AttackScreen.tsx` | Replace single-row attack buttons with 2-row layout |
| `handset/src/components/CardBadge.tsx` | **New file** — fixed-position card badge + modal |
| `handset/src/App.tsx` | Import `CardBadge`, render in Playing phase blocks |

## No Server Changes

Both tweaks are purely client-side UI. No SignalR methods or game logic affected.
