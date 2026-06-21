import { useState } from "react";
import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";
import { groupByContinent } from "../utils/groupByContinent";
import { ContinentAccordion } from "./ContinentAccordion";

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

  const placeArmy = async (territoryId: number) => {
    try {
      await connection.invoke("PlaceArmy", territoryId);
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-4">
        <div className="flex items-center justify-center">
          <span className="pb-2 px-3 py-1 rounded-full text-sm font-bold uppercase tracking-wider" style={{ backgroundColor: me.colour, color: '#fff' }}>
            Initial Placement
          </span>
        </div>
        {isMyTurn ? (
          <p className="text-lg font-bold text-green-400">Your turn — place an army</p>
        ) : (
          <p className="text-lg text-gray-400">
            Waiting for <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span>
          </p>
        )}
        <p className="text-sm text-gray-500 mt-1">{me.reinforcementsRemaining} armies remaining</p>
      </div>

      <div className="flex-1 overflow-y-auto pb-2">
        <ContinentAccordion
          territories={myTerritories}
          expanded={expanded}
          onToggle={(c) => setExpanded((e) => e === c ? null : c)}
          renderButton={(t) => (
            <button
              key={t.id}
              onClick={() => placeArmy(t.id)}
              disabled={!isMyTurn}
              style={isMyTurn ? { backgroundColor: me.colour + "33" } : {}}
              className={`px-3 py-2 rounded text-sm flex justify-between items-center border border-white/10 w-full
                ${isMyTurn ? "active:brightness-125" : "bg-gray-800/50 opacity-50"}`}
            >
              <span className="font-medium truncate">{t.name}</span>
              <span className="font-bold ml-1">{t.armies}</span>
            </button>
          )}
        />
      </div>
    </div>
  );
}
