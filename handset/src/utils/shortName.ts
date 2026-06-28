const shortNames: Record<string, string> = {
  "Eastern United States": "East. US",
  "Western United States": "West. US",
  "Central America": "C. America",
  "North Africa": "N. Africa",
  "South Africa": "S. Africa",
  "East Africa": "E. Africa",
  "Western Europe": "W. Europe",
  "Northern Europe": "N. Europe",
  "Southern Europe": "S. Europe",
  "Southeast Asia": "SE. Asia",
  "Middle East": "Mid. East",
  "New Guinea": "N. Guinea",
  "Eastern Australia": "E. Australia",
  "Western Australia": "W. Australia",
  "Northwest Territory": "NW. Terr.",
  "Great Britain": "Gr. Britain",
};

export function shortName(name: string): string {
  return shortNames[name] || name;
}
