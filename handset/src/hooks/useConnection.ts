import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { Card, GameState, Mission, RollPrompt } from "../types/game";

const HUB_URL = import.meta.env.VITE_SERVER_URL
  ? `${import.meta.env.VITE_SERVER_URL}/gamehub`
  : "/gamehub";

export function useConnection() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [gameState, setGameState] = useState<GameState | null>(null);
  const [cards, setCards] = useState<Card[]>([]);
  const [forcedTrade, setForcedTrade] = useState(false);
  const [mission, setMission] = useState<Mission | null>(null);
  const [missionToast, setMissionToast] = useState<string | null>(null);
  const [rollPrompt, setRollPrompt] = useState<RollPrompt | null>(null);
  const [combatInProgress, setCombatInProgress] = useState(false);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .build();

    conn.on("GameStateUpdated", (state: GameState) => {
      setGameState(state);
      if (state?.phase === "Lobby") { setCards([]); setMission(null); setForcedTrade(false); }
    });
    conn.on("CardsUpdated", (hand: Card[]) => setCards(hand));
    conn.on("MissionUpdated", (m: Mission) => {
      if (m.fallenBackToWorldDomination && !mission) {
        // First time receiving fallback — show toast
        setMissionToast("Your target was eliminated — mission is now world domination");
        setTimeout(() => setMissionToast(null), 5000);
      }
      setMission(m);
    });
    conn.on("ForcedTradeRequired", (hand: Card[]) => {
      setCards(hand);
      setForcedTrade(true);
    });
    conn.on("RollPrompt", (prompt: RollPrompt) => { setRollPrompt(prompt); });
    conn.on("CombatResult", () => setRollPrompt(null));
    conn.on("BlitzResult", () => setRollPrompt(null));
    conn.on("CombatStarted", () => setCombatInProgress(true));
    conn.on("CombatResolved", () => setCombatInProgress(false));

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

  return { connection, gameState, cards, mission, missionToast, forcedTrade, clearForcedTrade: () => setForcedTrade(false), rollPrompt, clearRollPrompt: () => setRollPrompt(null), combatInProgress };
}
