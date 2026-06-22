import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
}

export function LobbyScreen({ connection, gameState, playerName }: Props) {
  const isHost = gameState.players.find((p) => p.name === playerName)?.isHost;

  const startGame = async () => {
    try {
      await connection.invoke("StartGame");
    } catch (e: any) {
      alert(e.message);
    }
  };

  const addAI = async () => {
    try {
      await connection.invoke("AddAI");
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center p-4 pt-8">
      <p className="text-gray-500 text-sm uppercase tracking-wider">Game Code</p>
      <p className="text-3xl font-bold tracking-[0.3em] text-amber-400">{gameState.gameCode}</p>

      <div className="mt-4 w-full max-w-xs">
        <p className="text-sm font-semibold text-gray-500 uppercase tracking-wider mb-3">Players</p>
        <ul className="space-y-2">
          {gameState.players.map((p) => (
            <li key={p.name} className="flex items-center gap-3 bg-gray-800 rounded-lg px-4 py-3">
              <span className="h-4 w-4 rounded-full shrink-0" style={{ backgroundColor: p.colour }} />
              <span className="font-medium text-lg">{p.name}</span>
              {p.isHost && <span className="ml-auto text-xs text-gray-500 uppercase">Host</span>}
              {p.isAI && <span className="ml-auto text-xs text-gray-500">🤖</span>}
            </li>
          ))}
        </ul>
      </div>

      <p className="mt-4 text-sm text-gray-500">
        {gameState.players.length < 2
          ? "Waiting for 1 more player..."
          : `${gameState.players.length} players — ready to conquer 🌍`}
      </p>

      {isHost && (
        <div className="mt-4 flex flex-col gap-3 items-center">
          {gameState.players.length < 6 && (
            <button onClick={addAI} className="bg-purple-600 hover:bg-purple-700 px-6 py-3 rounded-lg font-bold transition">
              🤖 Add AI Player
            </button>
          )}

            <button onClick={startGame} className="bg-green-600 hover:bg-green-700 px-6 py-3 rounded-lg text-xl font-bold transition">
              Start Game
            </button>

        </div>
      )}
    </div>
  );
}
