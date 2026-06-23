import { Mission } from "../types/game";

interface Props {
  mission: Mission;
  onDismiss: () => void;
}

export function MissionWelcome({ mission, onDismiss }: Props) {
  return (
    <div className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-6" onClick={onDismiss}>
      <div className="bg-gray-800 border border-gray-600 rounded-xl p-6 max-w-xs text-center space-y-4" onClick={(e) => e.stopPropagation()}>
        <div className="text-4xl">🎯</div>
        <h2 className="text-xl font-bold text-white">Your Mission</h2>
        <p className="text-lg text-amber-300">{mission.description}</p>
        <p className="text-xs text-gray-400">Tap the 🎯 icon (top left) anytime to check your mission.</p>
        <button onClick={onDismiss} className="mt-2 bg-gray-700 active:bg-gray-600 px-6 py-2 rounded-lg text-white font-medium">
          Got it
        </button>
      </div>
    </div>
  );
}
