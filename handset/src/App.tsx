import { useEffect, useState } from "react";
import { useConnection } from "./hooks/useConnection";
import { ConnectScreen } from "./components/ConnectScreen";
import { LobbyScreen } from "./components/LobbyScreen";
import { PlacementScreen } from "./components/PlacementScreen";
import { ReinforceScreen } from "./components/ReinforceScreen";
import { AttackScreen } from "./components/AttackScreen";
import { FortifyScreen } from "./components/FortifyScreen";
import { GameOverScreen } from "./components/GameOverScreen";
import { MissionBadge } from "./components/MissionBadge";
import { MissionWelcome } from "./components/MissionWelcome";
import { StatusBadge } from "./components/StatusBadge";

export default function App() {
  const { connection, gameState, cards, mission, forcedTrade, clearForcedTrade, rollPrompt, clearRollPrompt, combatInProgress } = useConnection();
  const [playerName, setPlayerName] = useState(() => localStorage.getItem("risk_name") || "");
  const [showMissionWelcome, setShowMissionWelcome] = useState(false);
  const [lastTurnIndex, setLastTurnIndex] = useState<number | null>(null);

  useEffect(() => {
    if (mission) setShowMissionWelcome(true);
  }, [mission]);

  // Vibrate when it becomes your turn
  useEffect(() => {
    if (!gameState) return;
    const myIndex = gameState.players.findIndex(p => p.name === playerName);
    const current = gameState.currentPlayerIndex;
    if (current !== lastTurnIndex) {
      setLastTurnIndex(current);
      if (myIndex >= 0 && current === myIndex && navigator.vibrate) {
        navigator.vibrate(100);
      }
    }
  }, [gameState?.currentPlayerIndex]);

  if (!connection) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <p className="text-gray-500 animate-pulse">Connecting...</p>
      </div>
    );
  }

  const inGame = gameState?.players.some((p) => p.name === playerName);

  if (!gameState || !inGame) {
    return <ConnectScreen connection={connection} onJoined={setPlayerName} />;
  }

  if (gameState.phase === "Lobby") {
    return <LobbyScreen connection={connection} gameState={gameState} playerName={playerName} />;
  }

  if (gameState.phase === "InitialPlacement") {
    return <>
      <MissionBadge mission={mission} />
      <StatusBadge mission={mission} gameState={gameState} playerName={playerName} />
      {showMissionWelcome && mission && <MissionWelcome mission={mission} onDismiss={() => setShowMissionWelcome(false)} />}
      <PlacementScreen connection={connection} gameState={gameState} playerName={playerName} />
    </>;
  }

  if (gameState.phase === "Playing") {
    if (gameState.turnPhase === "Reinforce") {
      return <><MissionBadge mission={mission} /><StatusBadge mission={mission} gameState={gameState} playerName={playerName} /><ReinforceScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} /></>;
    }

    if (gameState.turnPhase === "Attack") {
      return <><MissionBadge mission={mission} /><StatusBadge mission={mission} gameState={gameState} playerName={playerName} /><AttackScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} forcedTrade={forcedTrade} clearForcedTrade={clearForcedTrade} rollPrompt={rollPrompt} clearRollPrompt={clearRollPrompt} combatInProgress={combatInProgress} /></>;
    }

    if (gameState.turnPhase === "Fortify") {
      return <><MissionBadge mission={mission} /><StatusBadge mission={mission} gameState={gameState} playerName={playerName} /><FortifyScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} /></>;
    }

    return (
      <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center justify-center p-6">
        <p className="text-2xl font-bold">{gameState.turnPhase}</p>
        <p className="text-gray-500 mt-2">Coming soon...</p>
      </div>
    );
  }

  if (gameState.phase === "GameOver") {
    return <GameOverScreen gameState={gameState} playerName={playerName} />;
  }

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center justify-center p-6">
      <p className="text-2xl font-bold">{gameState.phase}</p>
      <p className="text-gray-500 mt-2">Coming soon...</p>
    </div>
  );
}
