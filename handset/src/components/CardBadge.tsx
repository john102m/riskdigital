import { useState } from "react";
import { Card, Territory } from "../types/game";
import { shortName } from "../utils/shortName";

const typeIcon: Record<string, string> = { Infantry: "🥾", Cavalry: "🐴", Artillery: "💣", Wild: "🌟" };

interface Props {
  cards: Card[];
  territories: Territory[];
}

export function CardBadge({ cards, territories }: Props) {
  const [open, setOpen] = useState(false);

  if (cards.length === 0) return null;

  return (
    <>
      <button onClick={() => setOpen(!open)} className="min-h-[32px] px-3 flex items-center justify-center rounded-full bg-gray-700 text-sm">
        🃏 {cards.length}
      </button>
      {open && (
        <div className="fixed inset-0 z-[60] bg-black/70 flex items-center justify-center p-4" onClick={() => setOpen(false)}>
          <div className="bg-gray-800 border border-gray-600 rounded-lg p-4 max-w-sm w-full max-h-[60vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h3 className="text-white font-bold mb-3">Your Cards</h3>
            <div className="flex flex-col gap-2">
              {cards.map((c, i) => (
                <div key={i} className="flex items-center justify-between bg-gray-700 rounded px-3 py-2 text-sm text-white">
                  <span>{c.territoryId !== null ? shortName(territories.find(t => t.id === c.territoryId)?.name ?? "Unknown") : "Wild"}</span>
                  <span className="text-gray-400 ml-2">{typeIcon[c.type] ?? ""} {c.type}</span>
                </div>
              ))}
            </div>
            <p className="text-xs text-gray-500 mt-3 text-center">Trading available during your Reinforce phase</p>
          </div>
        </div>
      )}
    </>
  );
}
