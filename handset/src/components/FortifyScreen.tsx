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

export function FortifyScreen({ connection, gameState, playerName }: Props) {
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const me = gameState.players[myIndex];
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  const currentPlayer = gameState.players[gameState.currentPlayerIndex];

  const [sourceId, setSourceId] = useState<number | null>(null);
  const [targetId, setTargetId] = useState<number | null>(null);
  const [armies, setArmies] = useState(1);
  const [expanded, setExpanded] = useState<string | null>(() => {
    const s = gameState.territories.filter((t) => t.ownerId === myIndex && t.armies > 1);
    if (gameState.attackFrontIds?.length) {
      const lastFrontId = gameState.attackFrontIds[gameState.attackFrontIds.length - 1];
      const lastTerritory = gameState.territories.find(t => t.id === lastFrontId);
      if (lastTerritory && s.some(t => t.continent === lastTerritory.continent))
        return lastTerritory.continent;
    }
    return groupByContinent(s)[0]?.continent ?? null;
  });

  const sources = gameState.territories
    .filter((t) => t.ownerId === myIndex && t.armies > 1)
    .sort((a, b) => a.name.localeCompare(b.name));

  const targets = sourceId !== null
    ? gameState.territories.filter((t) => {
        const source = gameState.territories.find((s) => s.id === sourceId)!;
        return source.adjacent.includes(t.id) && t.ownerId === myIndex;
      }).sort((a, b) => a.name.localeCompare(b.name))
    : [];

  const selectedSource = gameState.territories.find((t) => t.id === sourceId);
  const maxArmies = selectedSource ? selectedSource.armies - 1 : 0;

  const fortify = async () => {
    if (sourceId === null || targetId === null) return;
    try {
      await connection.invoke("Fortify", sourceId, targetId, armies);
      await connection.invoke("EndTurn");
    } catch (e: any) {
      alert(e.message);
    }
  };

  const skip = async () => {
    try {
      await connection.invoke("EndTurn");
    } catch (e: any) {
      alert(e.message);
    }
  };

  if (!isMyTurn) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: currentPlayer.colour }}>
          Fortify
        </span>
        <p className="text-lg text-gray-400 mt-4">
          <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span> is fortifying
        </p>
      </div>
    );
  }

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-3">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: me.colour }}>
          Fortify
        </span>
      </div>

      <div className="flex-1 overflow-y-auto">
        {/* Source picker */}
        <p className="text-xs text-gray-400 uppercase mb-1 font-medium">Move from:</p>
        <div className="mb-3">
          <ContinentAccordion
            territories={sources}
            expanded={expanded}
            onToggle={(c) => setExpanded((e) => e === c ? null : c)}
            renderButton={(t) => (
              <button
                key={t.id}
                onClick={() => { setSourceId(sourceId === t.id ? null : t.id); setTargetId(null); setArmies(1); }}
                className={`px-3 py-2 rounded text-sm ${sourceId === t.id ? "bg-green-600" : "bg-gray-700"}`}
              >
                {t.name} ({t.armies})
              </button>
            )}
          />
        </div>

        {/* Target picker */}
        {sourceId !== null && (
          <>
            <p className="text-xs text-gray-400 uppercase mb-1 font-medium">Move to:</p>
            <div className="flex flex-wrap gap-1 mb-3">
              {targets.map((t) => (
                <button
                  key={t.id}
                  onClick={() => { setTargetId(t.id); setArmies(1); }}
                  className={`px-3 py-2 rounded text-sm ${targetId === t.id ? "bg-blue-600" : "bg-gray-700"}`}
                >
                  {t.name} ({t.armies})
                </button>
              ))}
            </div>
          </>
        )}

        {/* Army stepper */}
        {sourceId !== null && targetId !== null && (
          <div className="flex items-center gap-3 justify-center mb-3">
            <button onClick={() => setArmies(Math.max(1, armies - 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">−</button>
            <span className="text-3xl font-bold w-12 text-center">{armies}</span>
            <button onClick={() => setArmies(Math.min(maxArmies, armies + 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">+</button>
            <button onClick={() => setArmies(maxArmies)} className="bg-blue-600 active:bg-blue-700 px-4 py-2 rounded text-xl font-bold">Max</button>
          </div>
        )}
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 mt-2">
        <button onClick={skip} className="flex-1 bg-amber-600 active:bg-amber-700 px-4 py-3 rounded-lg text-lg font-bold">
          Skip → End Turn
        </button>
        {sourceId !== null && targetId !== null && (
          <button onClick={fortify} className="flex-1 bg-green-600 active:bg-green-700 px-4 py-3 rounded-lg text-lg font-bold">
            Fortify & End
          </button>
        )}
      </div>
    </div>
  );
}
