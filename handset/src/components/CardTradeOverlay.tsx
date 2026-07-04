import { useState, useEffect } from "react";
import { HubConnection } from "@microsoft/signalr";
import { Card, CardType, GameState } from "../types/game";

const CARD_ICONS: Record<string, string> = { Infantry: "⚔️", Cavalry: "🐎", Artillery: "💣", Wild: "🌟" };

function getTradeValue(cards: Card[], selected: number[]): number | null {
  if (selected.length !== 3) return null;
  if (selected.some(i => i >= cards.length)) return null;
  const types = selected.map(i => cards[i].type);
  const wilds = types.filter(t => t === "Wild").length;
  const nonWild = types.filter(t => t !== "Wild") as CardType[];

  // One of each
  const unique = new Set(nonWild);
  const isOneOfEach = unique.size + wilds >= 3 && unique.size > 1;
  if (isOneOfEach) return 10;

  // All same type (with wild filling in)
  const effectiveType = nonWild.length > 0 ? nonWild[0] : "Infantry";
  if (effectiveType === "Artillery") return 8;
  if (effectiveType === "Cavalry") return 6;
  return 4;
}

interface Props {
  connection: HubConnection;
  cards: Card[];
  gameState: GameState;
  forced: boolean;
  forcedLabel?: string;
  onClose: () => void;
}

export function CardTradeOverlay({ connection, cards, gameState, forced, forcedLabel, onClose }: Props) {
  const [selected, setSelected] = useState<number[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Clear selection when cards change (after a trade, cards array shrinks)
  useEffect(() => {
    setSelected([]);
  }, [cards.length]);

  const toggle = (i: number) => {
    setSelected((s) => s.includes(i) ? s.filter((x) => x !== i) : s.length < 3 ? [...s, i] : s);
    setError(null);
  };

  const trade = async () => {
    try {
      await connection.invoke("TradeCards", selected);
      setSelected([]);
      setError(null);
      if (!forced || cards.length - 3 < 5) onClose();
    } catch (e: any) {
      setError(e.message);
    }
  };

  const tradeValue = getTradeValue(cards, selected);

  return (
    <div className="fixed inset-0 z-[60] bg-gray-900 text-white flex flex-col">
      {/* Header */}
      <div className="relative flex items-center justify-center px-4 pt-2 pb-3 min-h-[44px] border-b border-white/10">
        <h2 className="text-lg font-bold text-amber-400 text-center">
          {forcedLabel ?? "Trade Cards"}
        </h2>
        {!forced && (
          <button onClick={onClose} className="absolute right-4 w-10 h-10 flex items-center justify-center rounded-full bg-gray-800 text-gray-400 text-xl active:bg-gray-700">
            ✕
          </button>
        )}
      </div>

      {/* Card grid */}
      <div className="flex-1 overflow-y-auto p-4">
        <div className="grid grid-cols-2 gap-3">
          {cards.map((card, i) => {
            const territory = card.territoryId !== null ? gameState.territories[card.territoryId] : null;
            const isSelected = selected.includes(i);
            const ownedTerritory = territory && territory.ownerId === gameState.currentPlayerIndex;
            return (
              <button
                key={i}
                onClick={() => toggle(i)}
                className={`flex flex-col items-center justify-center p-4 rounded-xl border-2 transition-all active:scale-95 touch-manipulation min-h-[100px]
                  ${isSelected ? "border-amber-400 bg-amber-900/40" : "border-white/10 bg-gray-800"}`}
              >
                <span className="text-3xl">{CARD_ICONS[card.type]}</span>
                <span className="text-sm font-bold mt-1">{card.type}</span>
                {territory && (
                  <span className={`text-xs mt-0.5 ${ownedTerritory ? "text-green-400" : "text-gray-400"}`}>
                    {territory.name}{ownedTerritory ? " ★" : ""}
                  </span>
                )}
                {isSelected && <span className="text-amber-400 text-xs font-bold mt-1">✓</span>}
              </button>
            );
          })}
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="mx-4 mb-2 px-4 py-3 bg-red-900/80 text-white text-sm font-medium text-center rounded-lg">
          {error.replace(/^.*?HubException:\s*/i, "").replace(/^Error:\s*/i, "") || "Invalid selection"}
        </div>
      )}

      {/* Footer */}
      <div className="p-4 border-t border-white/10">
        <button
          onClick={trade}
          disabled={selected.length !== 3}
          className="w-full bg-amber-600 active:bg-amber-700 px-6 py-4 rounded-lg text-lg font-bold disabled:opacity-30 transition-all touch-manipulation"
        >
          {selected.length === 3 && tradeValue
            ? `Trade → +${tradeValue} armies`
            : `Select ${3 - selected.length} more`}
        </button>
      </div>
    </div>
  );
}
