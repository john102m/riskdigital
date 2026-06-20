import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
}

export function ReinforceScreen({ connection, gameState, playerName }: Props) {
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const me = gameState.players[myIndex];
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  const currentPlayer = gameState.players[gameState.currentPlayerIndex];
  const myTerritories = gameState.territories
    .filter((t) => t.ownerId === myIndex)
    .sort((a, b) => a.name.localeCompare(b.name));

  const reinforce = async (territoryId: number) => {
    try {
      await connection.invoke("Reinforce", territoryId);
    } catch (e: any) {
      alert(e.message);
    }
  };

  const endReinforce = async () => {
    try {
      await connection.invoke("EndReinforce");
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-3">
        <span className="px-3 py-1 rounded-full text-sm font-bold uppercase tracking-wider" style={{ backgroundColor: me.colour, color: "#fff" }}>
          Reinforce
        </span>
        {isMyTurn ? (
          <p className="text-lg font-bold text-green-400 mt-2">Place {me.reinforcementsRemaining} armies</p>
        ) : (
          <p className="text-lg text-gray-400 mt-2">
            <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span> is reinforcing
          </p>
        )}
      </div>

      <ul className="flex-1 grid grid-cols-2 gap-0.5 content-start">
        {myTerritories.map((t) => (
          <li key={t.id}>
            <button
              onClick={() => reinforce(t.id)}
              disabled={!isMyTurn || me.reinforcementsRemaining <= 0}
              style={isMyTurn ? { backgroundColor: me.colour + "33" } : {}}
              className={`w-full text-left px-1.5 py-1 rounded flex justify-between items-center border border-white/10
                ${isMyTurn && me.reinforcementsRemaining > 0 ? "active:brightness-125" : "opacity-50"}`}
            >
              <span className="font-medium text-xs truncate">{t.name}</span>
              <span className="text-sm font-bold ml-1 w-5 text-right">{t.armies}</span>
            </button>
          </li>
        ))}
      </ul>

      {isMyTurn && me.reinforcementsRemaining === 0 && (
        <button onClick={endReinforce} className="mt-2 bg-green-600 active:bg-green-700 px-6 py-3 rounded-lg text-lg font-bold w-full">
          Done → Attack
        </button>
      )}
    </div>
  );
}
