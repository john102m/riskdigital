import { useState } from "react";
import { HubConnection } from "@microsoft/signalr";

interface Props {
  connection: HubConnection;
  onJoined: (name: string) => void;
}

export function ConnectScreen({ connection, onJoined }: Props) {
  const [name, setName] = useState(() => localStorage.getItem("risk_name") || "");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [showJoin, setShowJoin] = useState(false);

  const createGame = async () => {
    if (!name.trim()) { setError("Enter your name"); return; }
    try {
      localStorage.setItem("risk_name", name.trim());
      await connection.invoke("CreateGame", name.trim());
      onJoined(name.trim());
    } catch (e: any) {
      setError(e.message);
    }
  };

  const joinGame = async () => {
    if (!name.trim() || !code.trim()) { setError("Enter name and game code"); return; }
    try {
      localStorage.setItem("risk_name", name.trim());
      await connection.invoke("JoinGame", code.trim(), name.trim());
      onJoined(name.trim());
    } catch (e: any) {
      setError(e.message);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center justify-center p-6 gap-4">
      <div className="text-5xl">🎲</div>
      <h1 className="text-4xl font-bold tracking-tight">Risk</h1>
      <p className="text-gray-500 text-sm">Digital Board Game</p>

      {error && <p className="text-red-400 text-sm">{error}</p>}

      <input
        type="text"
        placeholder="Your name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        className="bg-gray-800 border border-gray-700 px-4 py-3 rounded-lg text-center text-lg w-56 focus:outline-none focus:border-red-500"
        maxLength={20}
      />

      {!showJoin ? (
        <div className="flex flex-col gap-3 w-56">
          <button onClick={createGame} className="bg-red-600 hover:bg-red-700 px-6 py-3 rounded-lg text-lg font-bold transition">
            Create Game
          </button>
          <button onClick={() => setShowJoin(true)} className="bg-gray-700 hover:bg-gray-600 px-6 py-3 rounded-lg text-lg font-bold transition">
            Join Game
          </button>
        </div>
      ) : (
        <div className="flex flex-col gap-3 w-56 items-center">
          <input
            type="text"
            placeholder="Game code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            className="bg-gray-800 border border-gray-700 px-4 py-3 rounded-lg text-center text-2xl tracking-[0.3em] w-48 focus:outline-none focus:border-amber-500"
            maxLength={4}
          />
          <button onClick={joinGame} className="bg-amber-600 hover:bg-amber-700 px-6 py-3 rounded-lg text-lg font-bold w-full transition">
            Join
          </button>
          <button onClick={() => setShowJoin(false)} className="text-gray-500 text-sm hover:text-gray-300">
            ← Back
          </button>
        </div>
      )}
    </div>
  );
}
