import { useState, useEffect } from "react";
import { HubConnection } from "@microsoft/signalr";
import { Card, GameState } from "../types/game";
import { groupByContinent } from "../utils/groupByContinent";
import { ContinentAccordion } from "./ContinentAccordion";
import { CardTradePanel } from "./CardTradePanel";

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

  useEffect(() => {
    if (isMyTurn && !mustTrade && hasTradeableSet(cards)) {
      setTradeHint(true);
      const t = setTimeout(() => setTradeHint(false), 4000);
      return () => clearTimeout(t);
    }
  }, []);

  const mustTrade = isMyTurn && cards.length >= 5;

  const reinforce = async (territoryId: number) => {
    try {
      await connection.invoke("Reinforce", territoryId);
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

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-3">
        <div className="flex items-center justify-center gap-2">
          <span className="px-3 py-1 rounded-full text-sm font-bold uppercase tracking-wider" style={{ backgroundColor: me.colour, color: "#fff" }}>
            Reinforce
          </span>
          {cards.length > 0 && (
            <button onClick={() => { setShowCards(!showCards); if (!showCards) setExpanded(null); }} className="min-h-[32px] px-3 flex items-center justify-center rounded-full bg-gray-700 text-sm">
              🃏 {cards.length}
            </button>
          )}
        </div>
        {isMyTurn ? (
          mustTrade
            ? <p className="text-lg font-bold text-amber-400 mt-2">Trade cards first (5+ held)</p>
            : <p className="text-lg font-bold text-green-400 mt-2">Place {me.reinforcementsRemaining} armies</p>
        ) : (
          <p className="text-lg text-gray-400 mt-2">
            <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span> is reinforcing
          </p>
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
            <button
              key={t.id}
              onClick={() => reinforce(t.id)}
              disabled={!isMyTurn || me.reinforcementsRemaining <= 0 || mustTrade}
              style={isMyTurn && !mustTrade ? { backgroundColor: me.colour + "33" } : {}}
              className={`w-full text-left px-2 py-2 rounded flex justify-between items-center border border-white/10
                ${isMyTurn && me.reinforcementsRemaining > 0 && !mustTrade ? "active:brightness-125" : "opacity-50"}`}
            >
              <span className="font-medium text-sm truncate">{t.name}</span>
              <span className="text-sm font-bold ml-1 w-5 text-right">{t.armies}</span>
            </button>
          )}
        />
      </div>

      {isMyTurn && me.reinforcementsRemaining === 0 && !mustTrade && (
        <button onClick={endReinforce} className="mt-2 bg-green-600 active:bg-green-700 px-6 py-3 rounded-lg text-lg font-bold w-full">
          Done → Attack
        </button>
      )}
    </div>
  );
}
