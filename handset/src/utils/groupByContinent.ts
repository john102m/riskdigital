import { Territory } from "../types/game";

const CONTINENT_ORDER = ["North America", "South America", "Europe", "Africa", "Asia", "Australia"];

export const CONTINENT_COLOURS: Record<string, string> = {
  "North America": "#facc15",
  "South America": "#ef4444",
  "Europe": "#60a5fa",
  "Africa": "#b8860b",
  "Asia": "#34d399",
  "Australia": "#a78bfa",
};

export function groupByContinent(territories: Territory[]): { continent: string; territories: Territory[] }[] {
  const map = new Map<string, Territory[]>();
  for (const t of territories) {
    const list = map.get(t.continent) ?? [];
    list.push(t);
    map.set(t.continent, list);
  }
  return CONTINENT_ORDER
    .filter((c) => map.has(c))
    .map((c) => ({ continent: c, territories: map.get(c)!.sort((a, b) => a.name.localeCompare(b.name)) }));
}
