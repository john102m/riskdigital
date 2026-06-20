import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { GameState } from "../types/game";

const HUB_URL = import.meta.env.PROD
  ? "/gamehub"
  : "http://localhost:5000/gamehub";

export function useConnection() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [gameState, setGameState] = useState<GameState | null>(null);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .build();

    conn.on("GameStateUpdated", (state: GameState) => setGameState(state));

    conn.start().then(() => {
      connectionRef.current = conn;
      setConnection(conn);
    });

    return () => {
      conn.stop();
    };
  }, []);

  return { connection, gameState };
}
