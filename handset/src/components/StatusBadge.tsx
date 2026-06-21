import { useState } from "react";
import { GameState, Mission } from "../types/game";

interface Props {
  mission: Mission | null;
  gameState: GameState;
  playerName: string;
}

const CONTINENT_TERRITORIES: Record<string, number> = {
  "North America": 9, "South America": 4, "Europe": 7, "Africa": 6, "Asia": 12, "Australia": 4
};

export function StatusBadge({ mission, gameState, playerName }: Props) {
  const [show, setShow] = useState(false);
  const myIndex = gameState.players.findIndex((p) => p.name === playerName);
  const myTerritories = gameState.territories.filter((t) => t.ownerId === myIndex);
  const totalOwned = myTerritories.length;

  // Continent breakdown
  const byCont: Record<string, number> = {};
  myTerritories.forEach((t) => { byCont[t.continent] = (byCont[t.continent] || 0) + 1; });

  // Mission-specific progress
  let progress = "";
  if (mission && !mission.fallenBackToWorldDomination) {
    if (mission.type === "TerritoryCount") {
      const min = mission.description.includes("2 armies");
      const qualifying = min ? myTerritories.filter((t) => t.armies >= 2).length : totalOwned;
      const target = mission.description.includes("18") ? 18 : 24;
      progress = `${qualifying}/${target} territories${min ? " (2+ armies)" : ""}`;
    } else if (mission.type === "Elimination") {
      progress = "Target still alive";
      const targetDesc = mission.description.replace("Eliminate ", "");
      // Check if eliminated
      const colours = ["Red", "Blue", "Green", "Yellow", "Purple", "Orange"];
      const targetIdx = colours.indexOf(targetDesc);
      if (targetIdx >= 0 && targetIdx < gameState.players.length && gameState.players[targetIdx].isEliminated) {
        progress = "✅ Target eliminated!";
      }
    }
  } else {
    progress = `${totalOwned}/42 territories`;
  }

  return (
    <>
      <button
        onClick={() => setShow(!show)}
        className="fixed top-2 right-2 z-50 min-w-[44px] min-h-[44px] flex items-center justify-center rounded bg-gray-800 border border-gray-700 text-base"
      >
        📊
      </button>
      {show && (
        <>
          <div className="fixed inset-0 z-[60]" onClick={() => setShow(false)} />
          <div className="fixed top-14 right-2 z-[70] bg-amber-50 border-2 border-amber-800/60 rounded-lg p-3 max-w-64 shadow-lg">
          <p className="text-sm text-amber-800 uppercase tracking-wider mb-2 font-bold">Status</p>
          {progress && <p className="text-base text-amber-700 mb-2">{progress}</p>}
          <p className="text-sm text-amber-900 mb-1">{totalOwned} territories owned</p>
          <div className="space-y-1">
            {Object.entries(CONTINENT_TERRITORIES).map(([cont, total]) => {
              const owned = byCont[cont] || 0;
              const full = owned === total;
              return (
                <div key={cont} className="flex justify-between text-xs">
                  <span className={full ? "text-green-700" : "text-amber-900"}>{cont}</span>
                  <span className={full ? "text-green-700 font-bold" : "text-amber-700"}>{owned}/{total}</span>
                </div>
              );
            })}
          </div>
        </div>
        </>
      )}
    </>
  );
}
