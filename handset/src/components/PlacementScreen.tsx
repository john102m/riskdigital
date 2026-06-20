import { HubConnection } from "@microsoft/signalr";
import { GameState } from "../types/game";

interface Props {
  connection: HubConnection;
  gameState: GameState;
  playerName: string;
}

export function PlacementScreen({ connection, gameState, playerName }: Props) {
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const me = gameState.players[myIndex];
  const isMyTurn = gameState.currentPlayerIndex === myIndex;
  const currentPlayer = gameState.players[gameState.currentPlayerIndex];
  const myTerritories = gameState.territories
    .filter((t) => t.ownerId === myIndex)
    .sort((a, b) => a.continent.localeCompare(b.continent) || a.name.localeCompare(b.name));

  const placeArmy = async (territoryId: number) => {
    try {
      await connection.invoke("PlaceArmy", territoryId);
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 text-white flex flex-col p-4">
      <div className="text-center mb-4">
        <p className="text-sm text-gray-500 uppercase tracking-wider">Initial Placement</p>
        {isMyTurn ? (
          <p className="text-lg font-bold text-green-400">Your turn — place an army</p>
        ) : (
          <p className="text-lg text-gray-400">
            Waiting for <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span>
          </p>
        )}
        <p className="text-sm text-gray-500 mt-1">{me.reinforcementsRemaining} armies remaining</p>
      </div>

      <ul className="flex-1 overflow-y-auto space-y-1">
        {myTerritories.map((t) => (
          <li key={t.id}>
            <button
              onClick={() => placeArmy(t.id)}
              disabled={!isMyTurn}
              className={`w-full text-left px-4 py-3 rounded-lg flex justify-between items-center
                ${isMyTurn ? "bg-gray-800 active:bg-gray-700" : "bg-gray-800/50 opacity-50"}`}
            >
              <div>
                <span className="font-medium">{t.name}</span>
                <span className="ml-2 text-xs text-gray-500">{t.continent}</span>
              </div>
              <span className="text-lg font-bold">{t.armies}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
