import { useEffect, useState } from "react";
import { HubConnection } from "@microsoft/signalr";

const COLOURS = ["#E53E3E", "#3182CE", "#38A169", "#D69E2E", "#805AD5", "#DD6B20"];
const SERVER = import.meta.env.VITE_SERVER_URL || "";
const AVATARS = ["female-1", "female-2", "female-3", "female-4", "female-5", "female-6", "male-1", "male-2", "male-3"];

interface Props {
  connection: HubConnection;
  onJoined: (name: string) => void;
  showToast?: (msg: string) => void;
}

interface LobbyStatus {
  gameExists: boolean;
  gameCode?: string;
  phase?: string;
  playerCount?: number;
}

export function ConnectScreen({ connection, onJoined }: Props) {
  const [name, setName] = useState(() => localStorage.getItem("risk_name") || "");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [showJoin, setShowJoin] = useState(false);
  const [lobbyStatus, setLobbyStatus] = useState<LobbyStatus | null>(null);
  const [colourIndex, setColourIndex] = useState(() => Number(localStorage.getItem("risk_colour") || "0"));
  const [avatarIndex, setAvatarIndex] = useState(() => Number(localStorage.getItem("risk_avatar") || "0"));

  useEffect(() => {
    connection.invoke("GetLobbyStatus").catch(() => {});
    connection.on("LobbyStatus", (status: LobbyStatus) => {
      setLobbyStatus(status);
      if (status.gameExists && status.gameCode) {
        setCode(status.gameCode);
        setShowJoin(true);
      }
    });
    return () => { connection.off("LobbyStatus"); };
  }, [connection]);

  const save = () => {
    localStorage.setItem("risk_name", name.trim());
    localStorage.setItem("risk_colour", String(colourIndex));
    localStorage.setItem("risk_avatar", String(avatarIndex));
  };

  const createGame = async () => {
    if (!name.trim()) { setError("Enter your name"); return; }
    try {
      save();
      await connection.invoke("CreateGame", name.trim(), colourIndex, avatarIndex);
      onJoined(name.trim());
    } catch (e: any) {
      setError(e.message);
    }
  };

  const joinGame = async () => {
    if (!name.trim() || !code.trim()) { setError("Enter name and game code"); return; }
    try {
      save();
      await connection.invoke("JoinGame", code.trim(), name.trim(), colourIndex, avatarIndex);
      onJoined(name.trim());
    } catch (e: any) {
      setError(e.message);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col items-center justify-center p-6 pt-4 gap-3">
      <div className="flex items-center gap-2">
        <span className="text-3xl">🎲</span>
        <h1 className="text-3xl font-bold tracking-tight">Risk</h1>
      </div>
      <p className="text-gray-500 text-xs">Digital Board Game</p>

      {error && <p className="text-red-400 text-sm">{error}</p>}

      <input
        type="text"
        placeholder="Your name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        className="bg-gray-800 border border-gray-700 px-4 py-2.5 rounded-lg text-center text-lg w-56 focus:outline-none focus:border-red-500"
        maxLength={20}
      />

      {/* Colour picker */}
      <div className="flex gap-2">
        {COLOURS.map((c, i) => (
          <button
            key={i}
            onClick={() => setColourIndex(i)}
            className={`w-9 h-9 rounded-full border-2 transition-all ${colourIndex === i ? "border-white scale-110" : "border-transparent opacity-60"}`}
            style={{ backgroundColor: c }}
          />
        ))}
      </div>

      {/* Avatar picker */}
      <div className="grid grid-cols-5 gap-2">
        {AVATARS.map((name, i) => (
          <button
            key={i}
            onClick={() => setAvatarIndex(i)}
            className={`w-11 h-11 rounded-full overflow-hidden border-2 transition-all ${avatarIndex === i ? "border-white scale-110" : "border-transparent opacity-50"}`}
          >
            <img src={`${SERVER}/avatars/${name}.png`} alt="" className="w-full h-full object-contain" />
          </button>
        ))}
      </div>

      {!showJoin ? (
        <div className="flex flex-col gap-3 w-56">
          {(!lobbyStatus || !lobbyStatus.gameExists) && (
            <button onClick={createGame} className="bg-red-600 hover:bg-red-700 px-6 py-3 rounded-lg text-lg font-bold transition">
              Create Game
            </button>
          )}
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
