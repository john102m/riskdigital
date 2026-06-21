import { useState } from "react";
import { Mission } from "../types/game";

export function MissionBadge({ mission }: { mission: Mission | null }) {
  const [show, setShow] = useState(false);
  if (!mission) return null;

  const desc = mission.fallenBackToWorldDomination
    ? "Own all 42 territories (original mission failed)"
    : mission.description;

  return (
    <>
      <button
        onClick={() => setShow(!show)}
        className="fixed top-2 left-2 z-50 px-2 py-1 rounded bg-gray-800 border border-gray-700 text-xs"
      >
        🎯
      </button>
      {show && (
        <div className="fixed top-10 left-2 z-50 bg-gray-800 border border-gray-600 rounded-lg p-3 max-w-60 shadow-lg">
          <p className="text-xs text-gray-400 uppercase tracking-wider mb-1">Your Mission</p>
          <p className="text-sm text-white">{desc}</p>
        </div>
      )}
    </>
  );
}
