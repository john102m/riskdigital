import { GameState } from "../types/game";

interface Props {
  gameState: GameState;
  playerName: string;
}

export function GameOverScreen({ gameState, playerName }: Props) {
  const winner = gameState.players[gameState.currentPlayerIndex];
  const isMe = winner.name === playerName;
  const isHost = gameState.players.find((p) => p.name === playerName)?.isHost;

  const newGame = async () => {
    const base = import.meta.env.VITE_SERVER_URL || "";
    await fetch(`${base}/admin/reset/${gameState.gameCode}`);
  };

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col items-center justify-center p-6 gap-4">
      <div className="text-6xl">{isMe ? "🏆" : "💀"}</div>
      <h1 className="text-3xl font-bold" style={{ color: winner.colour }}>
        {isMe ? "Victory!" : `${winner.name} wins!`}
      </h1>
      <p className="text-gray-400 text-center">
        {isMe ? "Mission complete." : `${winner.name} completed their mission.`}
      </p>
      <div className="mt-4 w-full max-w-xs space-y-2">
        {gameState.players.map((p, i) => {
          const territories = gameState.territories.filter((t) => t.ownerId === i).length;
          return (
            <div key={p.name} className="flex items-center gap-3 bg-gray-800 rounded-lg px-4 py-2">
              <span className="h-3 w-3 rounded-full" style={{ backgroundColor: p.colour }} />
              <span className="font-medium flex-1">{p.name}</span>
              <span className="text-sm text-gray-500">{territories}t</span>
              {p.isEliminated && <span className="text-xs text-red-400">☠️</span>}
            </div>
          );
        })}
      </div>
      {isHost && (
        <button onClick={newGame} className="mt-6 bg-red-600 active:bg-red-700 px-6 py-3 rounded-lg text-lg font-bold">
          New Game
        </button>
      )}
    </div>
  );
}
