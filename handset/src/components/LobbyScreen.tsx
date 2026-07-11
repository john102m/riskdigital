import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";
import { useState } from "react";

const SERVER = import.meta.env.VITE_SERVER_URL || "";
const AVATARS = ["female-1", "female-2", "female-3", "female-4", "female-5", "female-6", "male-1", "male-2", "male-3"];

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
  showToast: (msg: string) => void;
}

export function LobbyScreen({ connection, gameState, playerName, showToast }: Props) {
  const isHost = gameState.players.find((p) => p.name === playerName)?.isHost;
  const [showT5, setShowT5] = useState(false);

  // Smart default: all bots → Auto, any humans → Free
  const allBots = gameState.players.filter(p => !p.isHost).every(p => p.isAI);
  const [placementMode, setPlacementMode] = useState<"Auto" | "FreeForAll" | "Manual">(allBots ? "Auto" : "FreeForAll");

  const placementLabels = { Auto: "🚀 Auto", FreeForAll: "🤝 Free", Manual: "📋 Manual" };
  const cyclePlacement = () => {
    setPlacementMode(m => m === "Auto" ? "FreeForAll" : m === "FreeForAll" ? "Manual" : "Auto");
  };

  const startGame = async () => {
    try {
      await connection.invoke("StartGame", placementMode);
    } catch (e: any) {
      showToast(e.message);
    }
  };

  const addAI = async (tier: number, personality?: string) => {
    try {
      await connection.invoke("AddAI", tier, personality ?? null);
      setShowT5(false);
    } catch (e: any) {
      showToast(e.message);
    }
  };

  const removeAI = async (index: number) => {
    try {
      await connection.invoke("RemoveAI", index);
    } catch (e: any) {
      showToast(e.message);
    }
  };

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-3">
      {/* Header — game code */}
      <div className="text-center shrink-0">
        <p className="text-2xl font-bold tracking-[0.3em] text-amber-400">{gameState.gameCode}</p>
      </div>

      {/* Scrollable content */}
      <div className="flex-1 overflow-y-auto mt-3">
        <div className="w-full max-w-xs mx-auto">
          <ul className="space-y-1.5">
            {gameState.players.map((p, i) => (
              <li key={p.name} className="flex items-center gap-3 bg-gray-800 rounded-lg px-4 py-2">
                <span className="text-xs text-gray-500 font-mono w-4">{i}</span>
                <img src={`${SERVER}/avatars/${AVATARS[p.avatarIndex] || "female-1"}.png`} alt="" className="h-8 w-8 rounded-full shrink-0 border-2" style={{ borderColor: p.colour }} />
                <span className="font-medium">{p.name}</span>
                {p.isHost && <span className="ml-auto text-xs text-gray-500 uppercase">Host</span>}
                {p.isAI && !isHost && <span className="ml-auto text-xs text-gray-500">🤖 T{p.aiTier}</span>}
                {p.isAI && isHost && (
                  <button onClick={() => removeAI(i)} className="ml-auto text-red-400 text-sm font-bold px-2 py-1 rounded hover:bg-red-900/30">✕</button>
                )}
              </li>
            ))}
          </ul>

          <p className="mt-3 text-sm text-gray-500 text-center">
            {gameState.players.length < 2
              ? "Waiting for 1 more player..."
              : `${gameState.players.length} players — ready to conquer 🌍`}
          </p>

          {/* Placement mode toggle */}
          <div className="mt-3 flex justify-center">
            {isHost ? (
              <button onClick={cyclePlacement} className="px-4 py-2 rounded-full bg-gray-800 border border-white/10 text-sm font-medium active:bg-gray-700 touch-manipulation">
                {placementLabels[placementMode]}
              </button>
            ) : (
              <span className="px-4 py-2 rounded-full bg-gray-800/50 border border-white/10 text-sm text-gray-400">
                {placementLabels[placementMode]}
              </span>
            )}
          </div>

          {/* AI tier buttons */}
          {isHost && gameState.players.length < 6 && (
            <div className="flex flex-col gap-2 mt-3">
              <div className="flex gap-2">
                <button onClick={() => addAI(1)} className="flex-1 bg-blue-600 active:bg-blue-700 px-3 py-2 rounded-lg font-bold text-sm">
                  🤖 T1
                </button>
                <button onClick={() => addAI(2)} className="flex-1 bg-purple-600 active:bg-purple-700 px-3 py-2 rounded-lg font-bold text-sm">
                  ⚔️ T2
                </button>
                <button onClick={() => addAI(3)} className="flex-1 bg-green-600 active:bg-green-700 px-3 py-2 rounded-lg font-bold text-sm">
                  🧠 T3
                </button>
                <button onClick={() => addAI(4)} className="flex-1 bg-amber-600 active:bg-amber-700 px-3 py-2 rounded-lg font-bold text-sm">
                  🦊 T4
                </button>
                <button onClick={() => setShowT5(!showT5)} className="flex-1 bg-rose-600 active:bg-rose-700 px-3 py-2 rounded-lg font-bold text-sm">
                  🧬 T5
                </button>
              </div>
              {showT5 && (
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={() => addAI(5, "Opportunist")} className="bg-rose-700 active:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold">🦊 Opportunist</button>
                  <button onClick={() => addAI(5, "Cautious")} className="bg-rose-700 active:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold">🛡️ Cautious</button>
                  <button onClick={() => addAI(5, "Aggressive")} className="bg-rose-700 active:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold">🔥 Aggressive</button>
                  <button onClick={() => addAI(5, "Continental")} className="bg-rose-700 active:bg-rose-800 px-3 py-2 rounded-lg text-xs font-bold">🗺️ Continental</button>
                  <button onClick={() => addAI(5)} className="col-span-2 bg-rose-900 active:bg-rose-950 px-3 py-2 rounded-lg text-xs font-bold">🎲 Mystery</button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Pinned start button */}
      {isHost && (
        <div className="shrink-0 pt-3">
          <button onClick={startGame} className="w-full bg-green-600 active:bg-green-700 px-6 py-3 rounded-lg text-xl font-bold">
            Start Game
          </button>
        </div>
      )}
    </div>
  );
}
