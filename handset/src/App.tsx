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
import { Toast } from "./components/Toast";

export default function App() {
  const { connection, gameState, cards, mission, missionToast, forcedTrade, clearForcedTrade, rollPrompt, clearRollPrompt, combatInProgress } = useConnection();
  const [playerName, setPlayerName] = useState(() => localStorage.getItem("risk_name") || "");
  const [showMissionWelcome, setShowMissionWelcome] = useState(false);
  const [lastTurnIndex, setLastTurnIndex] = useState<number | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const showToast = (msg: string) => setToast(msg);

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
    return <>{toast && <Toast message={toast} onDismiss={() => setToast(null)} />}<ConnectScreen connection={connection} onJoined={setPlayerName} showToast={showToast} /></>;
  }

  if (gameState.phase === "Lobby") {
    return <>{toast && <Toast message={toast} onDismiss={() => setToast(null)} />}<LobbyScreen connection={connection} gameState={gameState} playerName={playerName} showToast={showToast} /></>;
  }

  if (gameState.phase === "InitialPlacement") {
    return <>
      {toast && <Toast message={toast} onDismiss={() => setToast(null)} />}
      <MissionBadge mission={mission} />
      <StatusBadge mission={mission} gameState={gameState} playerName={playerName} />
      {showMissionWelcome && mission && <MissionWelcome mission={mission} onDismiss={() => setShowMissionWelcome(false)} />}
      <PlacementScreen connection={connection} gameState={gameState} playerName={playerName} showToast={showToast} />
    </>;
  }

  if (gameState.phase === "Playing") {
    const missionToastEl = missionToast ? (
      <div className="fixed top-4 left-4 right-4 bg-amber-900/90 text-amber-200 px-4 py-3 rounded-lg text-sm font-medium text-center z-50 animate-pulse">
        {missionToast}
      </div>
    ) : null;

    if (gameState.turnPhase === "Reinforce") {
      return <>{toast && <Toast message={toast} onDismiss={() => setToast(null)} />}{missionToastEl}<MissionBadge mission={mission} /><StatusBadge mission={mission} gameState={gameState} playerName={playerName} /><ReinforceScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} showToast={showToast} /></>;
    }

    if (gameState.turnPhase === "Attack") {
      return <>{toast && <Toast message={toast} onDismiss={() => setToast(null)} />}{missionToastEl}<MissionBadge mission={mission} /><StatusBadge mission={mission} gameState={gameState} playerName={playerName} /><AttackScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} forcedTrade={forcedTrade} clearForcedTrade={clearForcedTrade} rollPrompt={rollPrompt} clearRollPrompt={clearRollPrompt} combatInProgress={combatInProgress} showToast={showToast} /></>;
    }

    if (gameState.turnPhase === "Fortify") {
      return <>{toast && <Toast message={toast} onDismiss={() => setToast(null)} />}{missionToastEl}<MissionBadge mission={mission} /><StatusBadge mission={mission} gameState={gameState} playerName={playerName} /><FortifyScreen connection={connection} gameState={gameState} playerName={playerName} cards={cards} showToast={showToast} /></>;
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
