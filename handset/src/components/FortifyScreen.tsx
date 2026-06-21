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

  // Step 3: Army count + confirm
  if (sourceId !== null && targetId !== null) {
    const source = gameState.territories.find((t) => t.id === sourceId)!;
    const target = gameState.territories.find((t) => t.id === targetId)!;
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
        <div className="text-center mb-4">
          <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: me.colour }}>
            Fortify
          </span>
          <p className="text-sm text-gray-400 mt-2">{source.name} → {target.name}</p>
        </div>

        <div className="flex-1 flex flex-col items-center justify-center gap-4">
          <div className="flex items-center gap-4">
            <button onClick={() => setArmies(Math.max(1, armies - 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">−</button>
            <span className="text-3xl font-bold w-12 text-center">{armies}</span>
            <button onClick={() => setArmies(Math.min(maxArmies, armies + 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">+</button>
          </div>
          <p className="text-xs text-gray-500">Max {maxArmies}</p>
        </div>

        <div className="flex gap-2">
          <button onClick={() => setTargetId(null)} className="flex-1 bg-gray-700 active:bg-gray-600 px-4 py-3 rounded-lg text-lg font-bold">
            ← Back
          </button>
          <button onClick={fortify} className="flex-1 bg-green-600 active:bg-green-700 px-4 py-3 rounded-lg text-lg font-bold">
            Fortify & End
          </button>
        </div>
      </div>
    );
  }

  // Step 2: Target picker
  if (sourceId !== null) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
        <div className="text-center mb-3">
          <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: me.colour }}>
            Fortify
          </span>
          <p className="text-sm text-gray-400 mt-2">Move to where?</p>
        </div>

        <div className="flex-1 overflow-y-auto">
          <ContinentAccordion
            territories={targets}
            expanded={expanded}
            onToggle={(c) => setExpanded((e) => e === c ? null : c)}
            renderButton={(t) => (
              <button
                key={t.id}
                onClick={() => { setTargetId(t.id); setArmies(1); }}
                className="px-3 py-2 rounded text-sm bg-gray-700 active:bg-blue-600"
              >
                {t.name} ({t.armies})
              </button>
            )}
          />
        </div>

        <div className="flex gap-2 mt-2">
          <button onClick={() => setSourceId(null)} className="flex-1 bg-gray-700 active:bg-gray-600 px-4 py-3 rounded-lg text-lg font-bold">
            ← Back
          </button>
          <button onClick={skip} className="flex-1 bg-amber-600 active:bg-amber-700 px-4 py-3 rounded-lg text-lg font-bold">
            Skip
          </button>
        </div>
      </div>
    );
  }

  // Step 1: Source picker
  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-3">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: me.colour }}>
          Fortify
        </span>
        <p className="text-sm text-gray-400 mt-2">Move from where?</p>
      </div>

      <div className="flex-1 overflow-y-auto">
        <ContinentAccordion
          territories={sources}
          expanded={expanded}
          onToggle={(c) => setExpanded((e) => e === c ? null : c)}
          renderButton={(t) => (
            <button
              key={t.id}
              onClick={() => setSourceId(t.id)}
              className="px-3 py-2 rounded text-sm bg-gray-700 active:bg-green-600"
            >
              {t.name} ({t.armies})
            </button>
          )}
        />
      </div>

      <button onClick={skip} className="mt-2 bg-amber-600 active:bg-amber-700 px-4 py-3 rounded-lg text-lg font-bold w-full">
        Skip → End Turn
      </button>
    </div>
  );
}
