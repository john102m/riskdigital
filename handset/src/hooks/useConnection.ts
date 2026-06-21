import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { Card, GameState } from "../types/game";

const HUB_URL = import.meta.env.VITE_SERVER_URL
  ? `${import.meta.env.VITE_SERVER_URL}/gamehub`
  : "/gamehub";

export function useConnection() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [gameState, setGameState] = useState<GameState | null>(null);
  const [cards, setCards] = useState<Card[]>([]);
  const [forcedTrade, setForcedTrade] = useState(false);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .build();

    conn.on("GameStateUpdated", (state: GameState) => setGameState(state));
    conn.on("CardsUpdated", (hand: Card[]) => setCards(hand));
    conn.on("ForcedTradeRequired", (hand: Card[]) => {
      setCards(hand);
      setForcedTrade(true);
    });

    conn.onreconnected(() => {
      const name = localStorage.getItem("risk_name");
      if (name) conn.invoke("Rejoin", name);
    });

    conn.start().then(() => {
      connectionRef.current = conn;
      setConnection(conn);
      // Rejoin if we were in a game (browser refresh)
      const name = localStorage.getItem("risk_name");
      if (name) conn.invoke("Rejoin", name);
    });

    // Reconnect when phone wakes up
    const handleVisibility = () => {
      if (document.visibilityState === "visible" && conn.state === "Disconnected") {
        conn.start().then(() => {
          const name = localStorage.getItem("risk_name");
          if (name) conn.invoke("Rejoin", name);
        });
      }
    };
    document.addEventListener("visibilitychange", handleVisibility);

    // Keep screen awake
    let wakeLock: WakeLockSentinel | null = null;
    navigator.wakeLock?.request("screen").then((wl) => { wakeLock = wl; }).catch(() => {});

    return () => {
      document.removeEventListener("visibilitychange", handleVisibility);
      wakeLock?.release();
      conn.stop();
    };
  }, []);

  return { connection, gameState, cards, forcedTrade, clearForcedTrade: () => setForcedTrade(false) };
}
