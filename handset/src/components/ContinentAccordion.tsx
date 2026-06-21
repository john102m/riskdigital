import { ReactNode } from "react";
import { Territory } from "../types/game";
import { groupByContinent, CONTINENT_COLOURS } from "../utils/groupByContinent";

interface Props {
  territories: Territory[];
  expanded: string | null;
  onToggle: (continent: string) => void;
  renderButton: (territory: Territory) => ReactNode;
}

export function ContinentAccordion({ territories, expanded, onToggle, renderButton }: Props) {
  const groups = groupByContinent(territories);

  return (
    <>
      {groups.map((g) => (
        <div key={g.continent} className="mb-2">
          <button
            onClick={() => onToggle(g.continent)}
            className="px-3 py-1.5 mb-1 rounded-full text-sm font-bold uppercase tracking-wider flex items-center gap-1"
            style={{ backgroundColor: CONTINENT_COLOURS[g.continent] + "33", color: CONTINENT_COLOURS[g.continent] }}
          >
            <span>{expanded === g.continent ? "▼" : "▶"}</span>
            {g.continent} ({g.territories.length})
          </button>
          {expanded === g.continent && (
            <div className="flex flex-wrap gap-1">
              {g.territories.map((t) => renderButton(t))}
            </div>
          )}
        </div>
      ))}
    </>
  );
}
