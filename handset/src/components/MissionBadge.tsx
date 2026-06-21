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
        className="fixed top-2 left-2 z-50 min-w-[44px] min-h-[44px] flex items-center justify-center rounded bg-gray-800 border border-gray-700 text-base"
      >
        🎯
      </button>
      {show && (
        <>
          <div className="fixed inset-0 z-[60]" onClick={() => setShow(false)} />
          <div className="fixed top-14 left-2 z-[70] bg-amber-50 border-2 border-amber-800/60 rounded-lg p-3 max-w-60 shadow-lg">
            <p className="text-sm text-amber-800 uppercase tracking-wider mb-1 font-bold">Your Mission</p>
            <p className="text-base text-amber-950">{desc}</p>
          </div>
        </>
      )}
    </>
  );
}
