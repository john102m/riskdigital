import { useState } from "react";
import { HubConnection } from "@microsoft/signalr";
import { Card, GameState } from "../types/game";

const CARD_ICONS: Record<string, string> = { Infantry: "⚔️", Cavalry: "🐎", Artillery: "💣", Wild: "🌟" };

interface Props {
  connection: HubConnection;
  cards: Card[];
  gameState: GameState;
  onTraded?: () => void;
}

export function CardTradePanel({ connection, cards, gameState, onTraded }: Props) {
  const [selected, setSelected] = useState<number[]>([]);

  const toggle = (i: number) => {
    setSelected((s) => s.includes(i) ? s.filter((x) => x !== i) : s.length < 3 ? [...s, i] : s);
  };

  const trade = async () => {
    try {
      await connection.invoke("TradeCards", selected);
      setSelected([]);
      onTraded?.();
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="p-2 bg-gray-800 rounded">
      <div className="flex flex-wrap gap-1 mb-2">
        {cards.map((c, i) => {
          const territory = c.territoryId !== null ? gameState.territories[c.territoryId] : null;
          return (
            <button
              key={i}
              onClick={() => toggle(i)}
              className={`px-2 py-1 rounded text-xs border ${selected.includes(i) ? "border-amber-400 bg-amber-900/40" : "border-white/10 bg-gray-700"}`}
            >
              {CARD_ICONS[c.type]} {territory?.name ?? "Wild"}
            </button>
          );
        })}
      </div>
      <button
        onClick={trade}
        disabled={selected.length !== 3}
        className="w-full bg-amber-600 active:bg-amber-700 px-3 py-2 rounded font-bold text-sm disabled:opacity-30"
      >
        Trade {selected.length}/3
      </button>
    </div>
  );
}
