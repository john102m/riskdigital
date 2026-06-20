namespace Risk.Server.Models;

public record TerritoryData(List<TerritoryDef> Territories, List<ContinentDef> Continents);
public record TerritoryDef(int Id, string Name, string Continent, List<int> Adjacent);
public record ContinentDef(string Name, int Bonus, List<int> Territories);
