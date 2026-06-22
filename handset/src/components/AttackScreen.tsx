import { useState, useEffect } from "react";
import { HubConnection } from "@microsoft/signalr";
import { Card, GameState, CombatResult, BlitzResult } from "../types/game";
import { groupByContinent } from "../utils/groupByContinent";
import { ContinentAccordion } from "./ContinentAccordion";
import { CardTradePanel } from "./CardTradePanel";

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
  cards: Card[];
  forcedTrade: boolean;
  clearForcedTrade: () => void;
}

export function AttackScreen({ connection, gameState, playerName, cards, forcedTrade, clearForcedTrade }: Props) {
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
  const [blitzSummary, setBlitzSummary] = useState<{ rounds: number; atkLoss: number; defLoss: number } | null>(null);
  const [expanded, setExpanded] = useState<string | null>("__init__");
  const [idleHint, setIdleHint] = useState<string | null>(null);

  // Idle hint: show context-aware prompt after 5s inactivity
  useEffect(() => {
    if (!isMyTurn || awaitingMove) { setIdleHint(null); return; }
    const hint = sourceId === null ? "Choose a territory to attack from"
      : targetId === null ? "Choose a target to attack"
      : "Tap Attack or ⚡ Blitz";
    const t = setTimeout(() => setIdleHint(hint), 10000);
    return () => { clearTimeout(t); setIdleHint(null); };
  }, [isMyTurn, sourceId, targetId, awaitingMove, lastResult]);

  // Broadcast selection to TV
  useEffect(() => {
    if (isMyTurn) connection.invoke("SelectAttack", sourceId, targetId).catch(() => {});
  }, [sourceId, targetId]);

  useEffect(() => {
    const handler = (result: CombatResult) => {
      setLastResult(result);
      setBlitzSummary(null);
      if (result.captured && isMyTurn) {
        setAwaitingMove(true);
        setMoveArmies(result.attackerDice.length);
      }
    };
    const blitzHandler = (result: BlitzResult) => {
      setLastResult({ attackerDice: [], defenderDice: [], attackerLosses: result.totalAttackerLosses, defenderLosses: result.totalDefenderLosses, captured: result.captured, sourceId: result.sourceId, targetId: result.targetId, sourceArmies: result.sourceArmies, targetArmies: result.targetArmies } as CombatResult);
      if (result.captured && isMyTurn) {
        setAwaitingMove(true);
        setMoveArmies(Math.min(3, result.sourceArmies - 1) || 1);
        setBlitzSummary({ rounds: result.rounds, atkLoss: result.totalAttackerLosses, defLoss: result.totalDefenderLosses });
      }
    };
    connection.on("CombatResult", handler);
    connection.on("BlitzResult", blitzHandler);
    return () => { connection.off("CombatResult", handler); connection.off("BlitzResult", blitzHandler); };
  }, [connection, isMyTurn]);

  const sources = gameState.territories
    .filter((t) => {
      if (t.ownerId !== myIndex || t.armies <= 1) return false;
      if (gameState.attackFrontIds && gameState.attackFrontIds.length > 0)
        if (!gameState.attackFrontIds.includes(t.id)) return false;
      // Must have at least one adjacent enemy
      return t.adjacent.some((adjId) => {
        const adj = gameState.territories.find((x) => x.id === adjId);
        return adj && adj.ownerId !== myIndex;
      });
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

  if (expanded === "__init__" && sources.length > 0) {
    const firstContinent = groupByContinent(sources)[0]?.continent;
    if (firstContinent) setExpanded(firstContinent);
  }

  const attack = async () => {
    if (sourceId === null || targetId === null) return;
    try {
      await connection.invoke("Attack", sourceId, targetId, effectiveDice);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const blitz = async () => {
    if (sourceId === null || targetId === null) return;
    try {
      await connection.invoke("Blitz", sourceId, targetId);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const moveIn = async () => {
    const mSource = awaitingMove ? sourceId : gameState.pendingMoveSource;
    const mTarget = awaitingMove ? targetId : gameState.pendingMoveTarget;
    try {
      await connection.invoke("MoveAfterCapture", mSource, mTarget, Math.max(moveArmies, gameState.lastDiceCount || 1));
      setAwaitingMove(false);
      setLastResult(null);
      setBlitzSummary(null);
      setSourceId(null);
      setTargetId(null);
      // Re-open strongest source's continent for next attack pick
      const remaining = sources.filter(t => t.id !== mSource && t.armies > 1);
      const strongest = remaining.sort((a, b) => b.armies - a.armies)[0];
      setExpanded(strongest?.continent ?? "__init__");
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

  // Forced trade modal (after elimination)
  if (forcedTrade && cards.length >= 5) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4 gap-4">
        <p className="text-lg font-bold text-amber-400">Captured cards — must trade!</p>
        <p className="text-sm text-gray-400">You have {cards.length} cards. Trade until under 5.</p>
        <CardTradePanel connection={connection} cards={cards} gameState={gameState} onTraded={() => { if (cards.length - 3 < 5) clearForcedTrade(); }} />
      </div>
    );
  }

  if (!isMyTurn) {
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4" style={{ borderTop: `3px solid ${currentPlayer.colour}` }}>
        <span className="text-2xl font-bold" style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span>
        <span className="text-sm text-gray-400 mt-1 uppercase tracking-wider">Attacking</span>
      </div>
    );
  }

  // Move-in UI after capture (from event or from server state on refresh)
  const pendingSource = awaitingMove ? sourceId : gameState.pendingMoveSource;
  const pendingTarget = awaitingMove ? targetId : gameState.pendingMoveTarget;
  if (isMyTurn && pendingSource !== null && pendingTarget !== null) {
    const source = gameState.territories.find((t) => t.id === pendingSource)!;
    const minMove = lastResult?.attackerDice.length || Math.min(3, source.armies - 1) || 1;
    const maxMove = source.armies - 1;
    const effectiveMove = Math.max(minMove, Math.min(moveArmies, maxMove));
    return (
      <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-4 gap-4">
        <p className="text-lg font-bold text-green-400">{gameState.territories.find(t => t.id === pendingTarget)?.name} Captured!</p>
        <p className="text-sm text-gray-400">Move troops in</p>
        {blitzSummary && (
          <p className="text-xs text-gray-400">⚡ {blitzSummary.rounds} rounds · You lost {blitzSummary.atkLoss} · They lost {blitzSummary.defLoss}</p>
        )}
        <div className="flex items-center gap-4">
          <button onClick={() => setMoveArmies(Math.max(minMove, moveArmies - 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">−</button>
          <span className="text-3xl font-bold w-12 text-center">{effectiveMove}</span>
          <button onClick={() => setMoveArmies(Math.min(maxMove, moveArmies + 1))} className="bg-amber-600 active:bg-amber-700 px-4 py-2 rounded text-xl font-bold">+</button>
          <button onClick={() => setMoveArmies(maxMove)} className="bg-blue-600 active:bg-blue-700 px-4 py-2 rounded text-xl font-bold">Max</button>
        </div>
        <p className="text-xs text-gray-400">Min {minMove} · Max {maxMove}</p>
        <button onClick={moveIn} className="bg-green-600 active:bg-green-700 px-6 py-3 rounded-lg text-lg font-bold">
          Move Troops
        </button>
      </div>
    );
  }

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col px-4 pt-2 pb-4">
      <div className="flex items-center justify-center mb-2 min-h-[33px] mx-10">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase" style={{ backgroundColor: me.colour }}>
          Attack
        </span>
      </div>

      {/* Last result */}
      {lastResult && (
        <div className="text-center text-sm mb-2 p-2 bg-gray-800 rounded">
          {lastResult.attackerDice.length > 0 ? (
            <>🎲 <span className="text-green-400">{lastResult.attackerDice.join(", ")}</span> vs <span className="text-red-400">{lastResult.defenderDice.join(", ")}</span>{" · "}Lost: {lastResult.attackerLosses} / {lastResult.defenderLosses}</>
          ) : (
            <>⚡ Blitz · Lost: <span className="text-green-400">{lastResult.attackerLosses}</span> / <span className="text-red-400">{lastResult.defenderLosses}</span>{lastResult.captured && " — Captured!"}</>
          )}
        </div>
      )}

      {/* Idle hint */}
      {idleHint && (
        <p className="text-center text-sm text-amber-300/80 mb-2 animate-pulse">{idleHint}</p>
      )}

      {/* Source picker */}
      {sourceId === null ? (
        <>
          <p className="text-xs text-gray-400 uppercase mb-1 font-medium">Attack from:</p>
          <div className="mb-3 flex-1 overflow-y-auto">
            <ContinentAccordion
              territories={sources}
              expanded={expanded}
              onToggle={(c) => setExpanded((e) => e === c ? null : c)}
              renderButton={(t) => (
                <button
                  key={t.id}
                  onClick={() => { setSourceId(t.id); setTargetId(null); }}
                  className="px-3 py-2 rounded text-sm bg-gray-700"
                >
                  {t.name} ({t.armies})
                </button>
              )}
            />
          </div>
        </>
      ) : (
        <div className="flex items-center gap-2 mb-3">
          <span className="px-3 py-1.5 rounded-full text-sm font-bold bg-green-700 flex items-center gap-1">
            🟢 {selectedSource?.name} ({selectedSource?.armies})
            <button onClick={() => { setSourceId(null); setTargetId(null); }} className="ml-1 text-white/60 hover:text-white">✕</button>
          </span>
        </div>
      )}

      {/* Target picker */}
      {sourceId !== null && (
        <>
          <p className="text-xs text-gray-400 uppercase mb-1 font-medium">Attack:</p>
          <div className="flex flex-wrap gap-1 mb-3">
            {targets.map((t) => (
              <button
                key={t.id}
                onClick={() => setTargetId(t.id)}
                className={`px-3 py-2 rounded text-sm ${targetId === t.id ? "bg-red-600" : "bg-gray-700"}`}
              >
                {t.name} ({t.armies})
              </button>
            ))}
          </div>
        </>
      )}

      {/* Attack buttons */}
      {sourceId !== null && targetId !== null && (
        <div className="mb-3 flex gap-2">
          <button onClick={attack} disabled={maxDice < 1} className="flex-1 bg-red-600 active:bg-red-700 px-4 py-2 rounded-lg font-bold disabled:opacity-30">
            ⚔️ {effectiveDice}🎲
          </button>
          <button onClick={blitz} disabled={maxDice < 1} className="flex-1 bg-purple-600 active:bg-purple-700 px-4 py-2 rounded-lg font-bold disabled:opacity-30">
            ⚡ Blitz
          </button>
          {maxDice > 1 && (
            <div className="flex gap-1">
              {[1, 2, 3].filter(d => d <= maxDice && d !== effectiveDice).map((d) => (
                <button key={d} onClick={() => setDiceCount(d)} className="px-2 py-2 rounded bg-gray-700 text-xs font-bold">
                  {d}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      <button onClick={endAttack} className="mt-auto bg-amber-600 active:bg-amber-700 px-6 py-3 rounded-lg text-lg font-bold w-full">
        Done → Fortify
      </button>
    </div>
  );
}
