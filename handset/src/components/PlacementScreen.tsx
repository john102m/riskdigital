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
    .sort((a, b) => a.name.localeCompare(b.name));

  const placeArmy = async (territoryId: number) => {
    try {
      await connection.invoke("PlaceArmy", territoryId);
    } catch (e: any) {
      alert(e.message);
    }
  };

  return (
    <div className="h-dvh bg-gray-900 text-white flex flex-col p-4 pt-4">
      <div className="text-center mb-4">
        <div className="flex items-center justify-center">
          <span className="pb-2 px-3 py-1 rounded-full text-sm font-bold uppercase tracking-wider" style={{ backgroundColor: me.colour, color: '#fff' }}>
            Initial Placement
          </span>
        </div>
        {isMyTurn ? (
          <p className="text-lg font-bold text-green-400">Your turn — place an army</p>
        ) : (
          <p className="text-lg text-gray-400">
            Waiting for <span style={{ color: currentPlayer.colour }}>{currentPlayer.name}</span>
          </p>
        )}
        <p className="text-sm text-gray-500 mt-1">{me.reinforcementsRemaining} armies remaining</p>
      </div>

      <ul className="flex-1 grid grid-cols-2 gap-0.5 content-start pb-2">
        {myTerritories.map((t) => (
          <li key={t.id}>
            <button
              onClick={() => placeArmy(t.id)}
              disabled={!isMyTurn}
              style={isMyTurn ? { backgroundColor: me.colour + "33" } : {}}
              className={`w-full text-left px-1.5 py-1 rounded flex justify-between items-center border border-white/10
                ${isMyTurn ? "active:brightness-125" : "bg-gray-800/50 opacity-50"}`}
            >
              <div className="truncate">
                <span className="font-medium text-xs">{t.name}</span>
              </div>
              <span className="text-sm font-bold ml-1 w-5 text-right">{t.armies}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
