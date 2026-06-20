import { useState } from "react";
import { useConnection } from "./hooks/useConnection";
import { ConnectScreen } from "./components/ConnectScreen";
import { LobbyScreen } from "./components/LobbyScreen";
import { PlacementScreen } from "./components/PlacementScreen";

export default function App() {
  const { connection, gameState } = useConnection();
  const [playerName, setPlayerName] = useState(() => localStorage.getItem("risk_name") || "");

  if (!connection) {
    return (
      <div className="min-h-screen bg-gray-900 flex items-center justify-center">
        <p className="text-gray-500 animate-pulse">Connecting...</p>
      </div>
    );
  }

  if (!gameState) {
    return <ConnectScreen connection={connection} onJoined={setPlayerName} />;
  }

  if (gameState.phase === "Lobby") {
    return <LobbyScreen connection={connection} gameState={gameState} playerName={playerName} />;
  }

  if (gameState.phase === "InitialPlacement") {
    return <PlacementScreen connection={connection} gameState={gameState} playerName={playerName} />;
  }

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center justify-center p-6">
      <p className="text-2xl font-bold">{gameState.phase}</p>
      <p className="text-gray-500 mt-2">Coming soon...</p>
    </div>
  );
}
