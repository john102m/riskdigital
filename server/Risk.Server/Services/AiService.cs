using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Models;

namespace Risk.Server.Services;

public class AiService(IHubContext<GameHub> hub, MlModels ml, ILogger<AiService> logger)
{
    private static readonly AsyncLocal<GameService> _game = new();
    private static readonly AsyncLocal<string> _gameCode = new();

    private GameService game { get => _game.Value!; set => _game.Value = value; }
    private string gameCode { get => _gameCode.Value!; set => _gameCode.Value = value; }
    private IClientProxy Group => hub.Clients.Group(gameCode);

    public void TriggerIfAi(GameService gameInstance, string code)
    {
        if (gameInstance.State is null) return;

        // FFA placement: trigger all bots in parallel
        if (gameInstance.State.Phase == GamePhase.InitialPlacement &&
            gameInstance.State.HouseRules.PlacementMode == PlacementMode.FreeForAll)
        {
            foreach (var p in gameInstance.State.Players.Where(p => p.IsAI && !p.IsEliminated && p.ReinforcementsRemaining > 0))
                _ = RunBotPlacement(p, gameInstance, code);
            return;
        }

        var player = gameInstance.State.Players[gameInstance.State.CurrentPlayerIndex];
        if (!player.IsAI || player.IsEliminated) return;
        _ = RunTurnAsync(gameInstance, code);
    }

    private IAiStrategy GetStrategy(Player player)
    {
        if (player.AiTier >= 5)
            return new AiMlStrategy(ml, game, player.Personality ?? AiPersonality.Opportunist, player.AiTier);
        return new AiEasyStrategy();
    }

    #region Turn Orchestration

    private async Task RunTurnAsync(GameService gameInstance, string code)
    {
        game = gameInstance;
        gameCode = code;
        try
        {
            await AiActions.Delay(2500, 3000); // let turn popup display on TV

            var state = game.State!;
            var player = state.Players[state.CurrentPlayerIndex];
            var connId = player.ConnectionId;

            if (state.Phase == GamePhase.InitialPlacement)
            {
                await RunPlacement(state, player, connId);
                return;
            }

            if (state.Phase == GamePhase.Playing)
            {
                var strategy = GetStrategy(player);
                var actions = new AiActions(hub, game, gameCode);

                await RunReinforce(strategy, actions, state, player, connId);
                if (state.Phase == GamePhase.GameOver) return;

                await RunAttack(strategy, actions, state, player, connId);
                if (state.Phase == GamePhase.GameOver) return;

                await RunFortify(strategy, actions, state, player, connId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI turn failed");
            await RecoverFromError();
        }
    }

    private async Task RunBotPlacement(Player player, GameService gameInstance, string code)
    {
        game = gameInstance;
        gameCode = code;
        var state = gameInstance.State!;
        var connId = player.ConnectionId;
        await AiActions.Delay(1000, 2000);
        await RunPlacement(state, player, connId);
    }

    #endregion

    #region Placement

    private async Task RunPlacement(GameState state, Player player, string connId)
    {
        var playerIndex = state.Players.IndexOf(player);
        while (player.ReinforcementsRemaining > 0 && state.Phase == GamePhase.InitialPlacement)
        {
            await AiActions.Delay(1500, 2000);
            if (state.Phase != GamePhase.InitialPlacement) break;

            var owned = state.Territories.Where(t => t.OwnerId == playerIndex).ToList();
            Territory target;

            // All tiers use front-line placement (simple and effective)
            var frontLine = owned.Where(t => t.Adjacent.Any(a => state.Territories[a].OwnerId != playerIndex)).ToList();
            target = (frontLine.Count > 0 ? frontLine : owned).OrderBy(t => t.Armies).First();

            game.PlaceArmy(connId, target.Id);
            await Group.SendAsync("ArmiesPlaced", playerIndex, target.Id, 1);
            await actions_Broadcast();
        }

        // In manual mode, trigger next AI if it's their turn
        if (state.HouseRules.PlacementMode == PlacementMode.Manual)
        {
            await AiActions.Delay(500);
            TriggerIfAi(game, gameCode);
        }
    }

    #endregion

    #region Reinforce

    private async Task RunReinforce(IAiStrategy strategy, AiActions actions, GameState state, Player player, string connId)
    {
        // Trade cards if forced (5+)
        while (player.Cards.Count >= 5)
        {
            await AiActions.Delay(2000, 3000);
            var set = FindValidSet(player.Cards);
            if (set is null) break;
            var (_, armies, _) = game.TradeCards(connId, set);
            await Group.SendAsync("CardTraded", state.CurrentPlayerIndex, armies);
            await actions.Broadcast();
        }

        // Voluntary trade based on strategy
        if (player.Cards.Count >= 3)
        {
            var set = FindValidSet(player.Cards);
            if (set is not null && strategy.ShouldTradeCards(player, state))
            {
                await AiActions.Delay((int)(2000 * strategy.TimingMultiplier), (int)(2500 * strategy.TimingMultiplier));
                var (_, armies, _) = game.TradeCards(connId, set);
                await Group.SendAsync("CardTraded", state.CurrentPlayerIndex, armies);
                await actions.Broadcast();
            }
        }

        // Place armies
        while (player.ReinforcementsRemaining > 0)
        {
            await AiActions.Delay((int)(1500 * strategy.TimingMultiplier), (int)(2000 * strategy.TimingMultiplier));
            var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
            var target = strategy.ChooseReinforceTarget(state, owned, state.CurrentPlayerIndex);
            game.Reinforce(connId, target.Id);
            await Group.SendAsync("ArmiesPlaced", state.CurrentPlayerIndex, target.Id, 1);
            await actions.Broadcast();
        }

        await AiActions.Delay((int)(1000 * strategy.TimingMultiplier), (int)(1500 * strategy.TimingMultiplier));
        game.EndReinforce(connId);
        await actions.Broadcast();
    }

    #endregion

    #region Attack

    private async Task RunAttack(IAiStrategy strategy, AiActions actions, GameState state, Player player, string connId)
    {
        int attackCount = 0;
        while (true)
        {
            if (strategy.ShouldStopAttacking(state, player, attackCount)) break;

            var choice = strategy.ChooseAttack(state, state.CurrentPlayerIndex);
            if (choice is null) break;

            var (source, target) = choice.Value;
            await actions.ShowSelection(source.Id, null, strategy.TimingMultiplier);
            await actions.ShowSelection(source.Id, target.Id, strategy.TimingMultiplier);

            bool gameOver;
            if (strategy.ShouldBlitz(source, target))
            {
                int moveTroops = strategy.CalculateMoveTroops(source, target, true);
                gameOver = await actions.DoBlitz(connId, source, target, moveTroops, strategy.TimingMultiplier);
            }
            else
            {
                int moveTroops = strategy.CalculateMoveTroops(source, target, false);
                gameOver = await actions.DoSingleAttack(connId, source, target, moveTroops, strategy.TimingMultiplier);
            }

            if (gameOver) return;
            attackCount++;

            await AiActions.Delay((int)(2000 * strategy.TimingMultiplier), (int)(3000 * strategy.TimingMultiplier));
        }

        await actions.ClearSelection();
        await actions.DoEndAttack(connId, strategy.TimingMultiplier);
    }

    #endregion

    #region Fortify

    private async Task RunFortify(IAiStrategy strategy, AiActions actions, GameState state, Player player, string connId)
    {
        await AiActions.Delay((int)(1500 * strategy.TimingMultiplier), (int)(2500 * strategy.TimingMultiplier));

        var fortifyChoice = strategy.ChooseFortify(state, state.CurrentPlayerIndex);
        if (fortifyChoice is not null)
        {
            var (source, target, armies) = fortifyChoice.Value;
            await actions.DoFortify(connId, source.Id, target.Id, armies, state.CurrentPlayerIndex);
        }

        await actions.DoEndTurn(connId, strategy.TimingMultiplier);
        TriggerIfAi(game, gameCode);
    }

    #endregion

    #region Error Recovery

    private async Task RecoverFromError()
    {
        try
        {
            game.ClearPending();
            var state = game.State;
            if (state is null || state.Phase != GamePhase.Playing) return;
            var player = state.Players[state.CurrentPlayerIndex];
            if (!player.IsAI) return;

            var connId = player.ConnectionId;

            // Force through remaining phases so game doesn't freeze
            if (state.TurnPhase == TurnPhase.Reinforce)
            {
                var owned = state.Territories.Where(t => t.OwnerId == state.CurrentPlayerIndex).ToList();
                while (player.ReinforcementsRemaining > 0 && owned.Count > 0)
                    game.Reinforce(connId, owned[Random.Shared.Next(owned.Count)].Id);
                game.EndReinforce(connId);
            }
            if (state.TurnPhase == TurnPhase.Attack)
                game.EndAttack(connId);
            if (state.TurnPhase == TurnPhase.Fortify)
                game.EndTurn(connId);

            await actions_Broadcast();
            await Group.SendAsync("TurnStarted", state.CurrentPlayerIndex);
            TriggerIfAi(game, gameCode);
        }
        catch { /* last resort — at least we tried */ }
    }

    #endregion

    #region Helpers

    private async Task actions_Broadcast()
    {
        await Group.SendAsync("GameStateUpdated", game.State);
    }

    private static int[]? FindValidSet(List<Card> cards)
    {
        for (int i = 0; i < cards.Count - 2; i++)
            for (int j = i + 1; j < cards.Count - 1; j++)
                for (int k = j + 1; k < cards.Count; k++)
                {
                    var types = new[] { cards[i].Type, cards[j].Type, cards[k].Type };
                    int wilds = types.Count(t => t == CardType.Wild);
                    if (wilds >= 2) return [i, j, k];
                    if (wilds == 1) return [i, j, k];
                    var nonWild = types.Where(t => t != CardType.Wild).ToArray();
                    if (nonWild.Distinct().Count() == 1 || nonWild.Distinct().Count() == 3)
                        return [i, j, k];
                }
        return null;
    }

    #endregion
}
