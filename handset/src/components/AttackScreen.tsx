import { useState, useEffect } from "react";
import { HubConnection } from "@microsoft/signalr";
import { GameState, CombatResult } from "../types/game";

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
}

export function AttackScreen({ connection, gameState, playerName }: Props) {
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const me = gameState.players[myIndex];
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  const currentPlayer = gameState.players[gameState.currentPlayerIndex];

  const [sourceId, setSourceId] = useState<number | null>(null);
  const [targetId, setTargetId] = useState<number | null>(null);
  const [diceCount, setDiceCount] = useState(3);
  const [lastResult, setLastResult] = useState<CombatResult | null>(null);
  const [awaitingMove, setAwaitingMove] = useState(false);
  const [moveArmies, setMoveArmies] = useState(1);

  useEffect(() => {
    const handler = (result: CombatResult) => {
      setLastResult(result);
      if (result.captured && isMyTurn) {
        setAwaitingMove(true);
        setMoveArmies(1);
      }
    };
    connection.on("CombatResult", handler);
    return () => { connection.off("CombatResult", handler); };
  }, [connection, isMyTurn]);

  const sources = gameState.territories
    .filter((t) => {
      if (t.ownerId !== myIndex || t.armies <= 1) return false;
      if (gameState.attackFrontIds && gameState.attackFrontIds.length > 0)
        return gameState.attackFrontIds.includes(t.id);
      return true;
    })
    .sort((a, b) => a.name.localeCompare(b.name));

  const targets = sourceId !== null
    ? gameState.territories.filter((t) => {
        const source = gameState.territories.find((s) => s.id === sourceId)!;
        return source.adjacent.includes(t.id) && t.ownerId !== myIndex;
      }).sort((a, b) => a.name.localeCompare(b.name))
    : [];

  const selectedSource = gameState.territories.find((t) => t.id === sourceId);
  const maxDice = selectedSource ? Math.min(3, selectedSource.armies - 1) : 0;
  const effectiveDice = Math.max(1, Math.min(diceCount, maxDice));

  const attack = async () => {
    if (sourceId === null || targetId === null) return;
    try {
      await connection.invoke("Attack", sourceId, targetId, effectiveDice);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const moveIn = async () => {
    try {
      await connection.invoke("MoveAfterCapture", sourceId, targetId, moveArmies);
      setAwaitingMove(false);
      setLastResult(null);
      setSourceId(null);
      setTargetId(null);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const endAttack = async () => {
    try {
      await connection.invoke("EndAttack");
    } catch (e: any) {
      alert(e.message);
    }
  };

  if (!isMyTurn) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: currentPlayer.colour }}>
          Attack
        </span>
        <p className="text-lg text-gray-400 mt-4">
          <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span> is attacking
        </p>
        {lastResult && (
          <div className="mt-4 text-center text-sm text-gray-400">
            🎲 {lastResult.attackerDice.join(", ")} vs {lastResult.defenderDice.join(", ")}
            {lastResult.captured && " — Captured!"}
          </div>
        )}
      </div>
    );
  }

  // Move-in UI after capture
  if (awaitingMove && sourceId !== null && targetId !== null) {
    const source = gameState.territories.find((t) => t.id === sourceId)!;
    const maxMove = source.armies - 1;
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4 gap-4">
        <p className="text-lg font-bold text-green-400">Territory Captured! 🎉</p>
        <p className="text-sm text-gray-400">Move troops into {gameState.territories.find(t => t.id === targetId)?.name}</p>
        <div className="flex items-center gap-4">
          <button onClick={() => setMoveArmies(Math.max(1, moveArmies - 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">−</button>
          <span className="text-3xl font-bold w-12 text-center">{moveArmies}</span>
          <button onClick={() => setMoveArmies(Math.min(maxMove, moveArmies + 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">+</button>
        </div>
        <p className="text-xs text-gray-500">Min 1 · Max {maxMove}</p>
        <button onClick={moveIn} className="bg-green-600 active:bg-green-700 px-6 py-3 rounded-lg text-lg font-bold">
          Move Troops
        </button>
      </div>
    );
  }

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-3">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: me.colour }}>
          Attack
        </span>
      </div>

      {/* Last result */}
      {lastResult && (
        <div className="text-center text-sm mb-2 p-2 bg-gray-800 rounded">
          🎲 <span className="text-green-400">{lastResult.attackerDice.join(", ")}</span> vs <span className="text-red-400">{lastResult.defenderDice.join(", ")}</span>
          {" · "}Lost: {lastResult.attackerLosses} / {lastResult.defenderLosses}
        </div>
      )}

      {/* Source picker */}
      <p className="text-xs text-gray-500 uppercase mb-1">Attack from:</p>
      <div className="flex flex-wrap gap-1 mb-3">
        {sources.map((t) => (
          <button
            key={t.id}
            onClick={() => { setSourceId(sourceId === t.id ? null : t.id); setTargetId(null); }}
            className={`px-2 py-1 rounded text-xs ${sourceId === t.id ? "bg-green-600" : "bg-gray-700"}`}
          >
            {t.name} ({t.armies})
          </button>
        ))}
      </div>

      {/* Target picker */}
      {sourceId !== null && (
        <>
          <p className="text-xs text-gray-500 uppercase mb-1">Attack:</p>
          <div className="flex flex-wrap gap-1 mb-3">
            {targets.map((t) => (
              <button
                key={t.id}
                onClick={() => setTargetId(t.id)}
                className={`px-2 py-1 rounded text-xs ${targetId === t.id ? "bg-red-600" : "bg-gray-700"}`}
              >
                {t.name} ({t.armies})
              </button>
            ))}
          </div>
        </>
      )}

      {/* Dice picker + Attack button */}
      {sourceId !== null && targetId !== null && (
        <div className="flex items-center gap-3 mb-3">
          <p className="text-xs text-gray-500">Dice:</p>
          {[1, 2, 3].map((d) => (
            <button
              key={d}
              disabled={d > maxDice}
              onClick={() => setDiceCount(d)}
              className={`px-3 py-2 rounded font-bold ${d === diceCount ? "bg-amber-600" : "bg-gray-700"} ${d > maxDice ? "opacity-30" : ""}`}
            >
              {d}
            </button>
          ))}
          <button onClick={attack} disabled={maxDice < 1} className="ml-auto bg-red-600 active:bg-red-700 px-4 py-2 rounded-lg font-bold disabled:opacity-30">
            ⚔️ Attack
          </button>
        </div>
      )}

      {/* Done button */}
      <button onClick={endAttack} className="mt-auto bg-amber-600 active:bg-amber-700 px-6 py-3 rounded-lg text-lg font-bold w-full">
        Done Attacking → Fortify
      </button>
    </div>
  );
}
