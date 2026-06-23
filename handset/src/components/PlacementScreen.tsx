import { useState } from "react";
import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";
import { groupByContinent } from "../utils/groupByContinent";
import { ContinentAccordion } from "./ContinentAccordion";
import { tap, heavyTap } from "../utils/vibrate";

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
}

export function PlacementScreen({ connection, gameState, playerName }: Props) {
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const me = gameState.players[myIndex];
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  const currentPlayer = gameState.players[gameState.currentPlayerIndex];
  const myTerritories = gameState.territories.filter((t) => t.ownerId === myIndex);

  const [expanded, setExpanded] = useState<string | null>(() => groupByContinent(myTerritories)[0]?.continent ?? null);

  if (!isMyTurn) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4" style={{ borderTop: `3px solid ${currentPlayer.colour}` }}>
        <span className="text-2xl font-bold" style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span>
        <span className="text-sm text-gray-400 mt-1 uppercase tracking-wider">Placing armies</span>
      </div>
    );
  }

  const placeArmy = async (territoryId: number, count: number = 1) => {
    try {
      await connection.invoke("PlaceArmy", territoryId, count);
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col px-4 pt-2 pb-4">
      <div className="text-center mb-2 min-h-[33px] flex items-center justify-center">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase tracking-wider" style={{ backgroundColor: me.colour, color: '#fff' }}>
          Place army · {me.reinforcementsRemaining} left
        </span>
      </div>

      <div className="flex-1 overflow-y-auto pb-2">
        <ContinentAccordion
          territories={myTerritories}
          expanded={expanded}
          onToggle={(c) => setExpanded((e) => e === c ? null : c)}
          renderButton={(t) => (
            <div key={t.id} className="flex items-center gap-1 w-full">
              <button
                onClick={() => { tap(); placeArmy(t.id, 1); }}
                onContextMenu={(e) => e.preventDefault()}
                style={{ backgroundColor: me.colour + "33" }}
                className="w-[70%] px-3 py-2 rounded-l text-sm flex justify-between items-center border border-white/10 active:scale-95 active:brightness-150 transition-all touch-manipulation"
              >
                <span className="font-medium truncate">{t.name}</span>
                <span className="font-bold ml-1">{t.armies}</span>
              </button>
              <button
                onClick={() => { heavyTap(); placeArmy(t.id, me.reinforcementsRemaining); }}
                onContextMenu={(e) => e.preventDefault()}
                disabled
                className="w-[30%] px-2 py-2 rounded-r bg-white/10 border border-white/10 text-xs font-bold text-amber-300 transition-all touch-manipulation opacity-30"
              >
                All
              </button>
            </div>
          )}
        />
      </div>
    </div>
  );
}
