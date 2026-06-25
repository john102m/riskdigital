import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";
import { useState } from "react";

const SERVER = import.meta.env.VITE_SERVER_URL || "";
const AVATARS = ["female-1", "female-2", "female-3", "female-4", "female-5", "female-6", "male-1", "male-2", "male-3"];

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
}

export function LobbyScreen({ connection, gameState, playerName }: Props) {
  const isHost = gameState.players.find((p) => p.name === playerName)?.isHost;
  const [showT5, setShowT5] = useState(false);

  const startGame = async () => {
    try {
      await connection.invoke("StartGame");
    } catch (e: any) {
      alert(e.message);
    }
  };

  const addAI = async (tier: number, personality?: string) => {
    try {
      await connection.invoke("AddAI", tier, personality ?? null);
      setShowT5(false);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const removeAI = async (index: number) => {
    try {
      await connection.invoke("RemoveAI", index);
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center p-4 pt-6">
      <p className="text-gray-500 text-xs uppercase tracking-wider">Game Code</p>
      <p className="text-2xl font-bold tracking-[0.3em] text-amber-400">{gameState.gameCode}</p>

      <div className="mt-3 w-full max-w-xs">
        <ul className="space-y-1.5">
          {gameState.players.map((p, i) => (
            <li key={p.name} className="flex items-center gap-3 bg-gray-800 rounded-lg px-4 py-2">
              <img src={`${SERVER}/avatars/${AVATARS[p.avatarIndex] || "female-1"}.png`} alt="" className="h-8 w-8 rounded-full shrink-0 border-2" style={{ borderColor: p.colour }} />
              <span className="font-medium">{p.name}</span>
              {p.isHost && <span className="ml-auto text-xs text-gray-500 uppercase">Host</span>}
              {p.isAI && !isHost && <span className="ml-auto text-xs text-gray-500">🤖 Tier-{p.aiTier}</span>}
              {p.isAI && isHost && (
                <button onClick={() => removeAI(i)} className="ml-auto text-red-400 text-sm font-bold px-2 py-1 rounded hover:bg-red-900/30">✕</button>
              )}
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
            <div className="flex flex-col gap-2 w-full max-w-xs">
              <div className="flex gap-2">
                <button onClick={() => addAI(1)} className="flex-1 bg-blue-600 hover:bg-blue-700 px-4 py-2 rounded-lg font-bold text-sm transition">
                  🤖 Tier-1
                </button>
                <button onClick={() => addAI(2)} className="flex-1 bg-purple-600 hover:bg-purple-700 px-4 py-2 rounded-lg font-bold text-sm transition">
                  ⚔️ Tier-2
                </button>
              </div>
              <div className="flex gap-2">
                <button onClick={() => addAI(3)} className="flex-1 bg-green-600 hover:bg-green-700 px-4 py-2 rounded-lg font-bold text-sm transition">
                  🧠 Tier-3
                </button>
                <button onClick={() => addAI(4)} className="flex-1 bg-amber-600 hover:bg-amber-700 px-4 py-2 rounded-lg font-bold text-sm transition">
                  🦊 Tier-4
                </button>
                <button onClick={() => setShowT5(!showT5)} className="flex-1 bg-rose-600 hover:bg-rose-700 px-4 py-2 rounded-lg font-bold text-sm transition">
                  🧬 Tier-5
                </button>
              </div>
              {showT5 && (
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={() => addAI(5, "Opportunist")} className="bg-rose-700 hover:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold transition">🦊 Opportunist</button>
                  <button onClick={() => addAI(5, "Cautious")} className="bg-rose-700 hover:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold transition">🛡️ Cautious</button>
                  <button onClick={() => addAI(5, "Aggressive")} className="bg-rose-700 hover:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold transition">🔥 Aggressive</button>
                  <button onClick={() => addAI(5, "Continental")} className="bg-rose-700 hover:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold transition">🗺️ Continental</button>
                  <button onClick={() => addAI(5)} className="col-span-2 bg-rose-900 hover:bg-rose-950 px-3 py-2 rounded-lg text-xs font-bold transition">🎲 Mystery</button>
                </div>
              )}
            </div>
          )}

            <button onClick={startGame} className="bg-green-600 hover:bg-green-700 px-6 py-3 rounded-lg text-xl font-bold transition">
              Start Game
            </button>

        </div>
      )}
    </div>
  );
}
