import { useState, useEffect } from "react";
import { HubConnection } from "@microsoft/signalr";
import { Card, GameState } from "../types/game";
import { groupByContinent } from "../utils/groupByContinent";
import { ContinentAccordion } from "./ContinentAccordion";
import { CardTradePanel } from "./CardTradePanel";
import { tap, heavyTap } from "../utils/vibrate";

function hasTradeableSet(cards: Card[]): boolean {
  if (cards.length < 3) return false;
  const wilds = cards.filter(c => c.type === "Wild").length;
  if (wilds >= 2) return true; // 2 wilds + anything
  const types = cards.filter(c => c.type !== "Wild").map(c => c.type);
  // All same
  for (const t of ["Infantry", "Cavalry", "Artillery"]) {
    if (types.filter(x => x === t).length + wilds >= 3) return true;
  }
  // All different
  const unique = new Set(types);
  if (unique.size >= 3) return true;
  if (unique.size >= 2 && wilds >= 1) return true;
  return false;
}

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
  cards: Card[];
}

export function ReinforceScreen({ connection, gameState, playerName, cards }: Props) {
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const me = gameState.players[myIndex];
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  const currentPlayer = gameState.players[gameState.currentPlayerIndex];
  const myTerritories = gameState.territories.filter((t) => t.ownerId === myIndex);

  const [showCards, setShowCards] = useState(cards.length >= 5);
  const [expanded, setExpanded] = useState<string | null>(() => groupByContinent(myTerritories)[0]?.continent ?? null);
  const [tradeHint, setTradeHint] = useState(false);

  const mustTrade = isMyTurn && cards.length >= 5;

  useEffect(() => {
    if (isMyTurn && !mustTrade && hasTradeableSet(cards)) {
      setTradeHint(true);
      const t = setTimeout(() => setTradeHint(false), 4000);
      return () => clearTimeout(t);
    }
  }, []);

  if (!isMyTurn) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4" style={{ borderTop: `3px solid ${currentPlayer.colour}` }}>
        <span className="text-2xl font-bold" style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span>
        <span className="text-sm text-gray-400 mt-1 uppercase tracking-wider">Reinforcing</span>
      </div>
    );
  }

  const reinforce = async (territoryId: number, count: number = 1) => {
    try {
      await connection.invoke("Reinforce", territoryId, count);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const endReinforce = async () => {
    try {
      await connection.invoke("EndReinforce");
    } catch (e: any) {
      alert(e.message);
    }
  };

  if (mustTrade) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4 gap-4">
        <p className="text-lg font-bold text-amber-400">Trade cards first ({cards.length} held)</p>
        <CardTradePanel connection={connection} cards={cards} gameState={gameState} onTraded={() => { }} />
      </div>
    );
  }

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col px-4 pt-2 pb-4">
      <div className="flex items-center justify-center gap-2 mb-2 min-h-[33px]">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase tracking-wider" style={{ backgroundColor: me.colour, color: "#fff" }}>
          Reinforce · {me.reinforcementsRemaining}
        </span>
        {cards.length > 0 && (
          <button onClick={() => { setShowCards(!showCards); if (!showCards) setExpanded(null); }} className="min-h-[32px] px-3 flex items-center justify-center rounded-full bg-gray-700 text-sm">
            🃏 {cards.length}
          </button>
        )}
      </div>

      {/* Card trade panel */}
      {tradeHint && !showCards && (
        <button onClick={() => { setShowCards(true); setExpanded(null); setTradeHint(false); }} className="mb-2 px-3 py-2 bg-amber-800/60 border border-amber-500/50 rounded text-sm text-amber-300 text-center animate-pulse">
          🃏 You have a tradeable set — tap to open cards
        </button>
      )}
      {showCards && (
        <div className="mb-3">
          <CardTradePanel connection={connection} cards={cards} gameState={gameState} onTraded={() => setShowCards(false)} />
        </div>
      )}

      {/* Territory grid */}
      <div className="flex-1 overflow-y-auto">
        <ContinentAccordion
          territories={myTerritories}
          expanded={expanded}
          onToggle={(c) => setExpanded((e) => e === c ? null : c)}
          renderButton={(t) => (
            <div key={t.id} className="flex items-center gap-1 w-full">
              <span
                onClick={() => { if (me.reinforcementsRemaining > 0) { heavyTap(); reinforce(t.id, 1); } }}
                style={{ backgroundColor: me.colour + "33" }}
                className={`font-medium min-w-0 flex-1 px-2 py-2 rounded-l text-sm flex justify-between items-center border border-white/10 active:scale-95 active:brightness-150 transition-all touch-manipulation cursor-pointer ${me.reinforcementsRemaining <= 0 ? "opacity-50 pointer-events-none" : ""}`}
              ><span className="truncate">{t.name}</span><span className="font-bold ml-2 shrink-0 text-white/70">{t.armies}</span></span>
              <button
                onClick={() => { heavyTap(); reinforce(t.id, me.reinforcementsRemaining); }}
                disabled={me.reinforcementsRemaining <= 0 ? true : false}
                onContextMenu={(e) => e.preventDefault()}
                className={`w-12 shrink-0 px-2 py-2 rounded-r bg-white/10 border border-white/10 text-xs font-bold text-amber-300 active:scale-95 active:brightness-150 transition-all touch-manipulation
                 ${me.reinforcementsRemaining <= 0 ? "opacity-50" : ""}`}
              >
                All
              </button>
              <button
                onClick={() => { heavyTap(); reinforce(t.id, 1); }}
                disabled={me.reinforcementsRemaining <= 0 ? true : false}
                onContextMenu={(e) => e.preventDefault()}
                className={`w-12 shrink-0 px-2 py-2 rounded-r bg-white/10 border border-white/10 text-xs font-bold text-amber-300 active:scale-95 active:brightness-150 transition-all touch-manipulation
                 ${me.reinforcementsRemaining <= 0 ? "opacity-50" : ""}`}
              >
                +1
              </button>

            </div>
          )}
        />
      </div>

      {me.reinforcementsRemaining === 0 && (
        <button onClick={endReinforce} className="mt-2 bg-green-600 active:bg-green-700 px-6 py-3 rounded-lg text-lg font-bold w-full">
          Done → Attack
        </button>
      )}
    </div>
  );
}
