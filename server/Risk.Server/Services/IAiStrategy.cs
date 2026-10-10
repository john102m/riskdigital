using Risk.Server.Models;

namespace Risk.Server.Services;

public interface IAiStrategy
{
    (Territory Source, Territory Target)? ChooseAttack(GameState state, int myIndex);
    (Territory Source, Territory Target, int Armies)? ChooseFortify(GameState state, int myIndex);
    Territory ChooseReinforceTarget(GameState state, List<Territory> owned, int myIndex);
    bool ShouldBlitz(Territory source, Territory target);
    bool ShouldStopAttacking(GameState state, Player player, int attackCount);
    int CalculateMoveTroops(Territory source, Territory target, bool capturedViaBlitz);
    bool ShouldTradeCards(Player player, GameState state);
    float TimingMultiplier { get; }
}
