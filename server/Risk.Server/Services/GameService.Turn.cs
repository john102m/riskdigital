using Microsoft.AspNetCore.SignalR;
using Risk.Server.Models;

namespace Risk.Server.Services;

/// <summary>
/// Turn phase actions — reinforce, card trading, fortify, end turn.
/// These methods are called by GameHub during a player's turn.
/// No combat logic here — see GameService.Combat.cs.
/// </summary>
public partial class GameService
{
    public (GameState State, int ArmiesGranted, List<int> TerritoryBonusIds) TradeCards(string connectionId, int[] cardIndices)
    {
        if (_state is null || _state.Phase != GamePhase.Playing)
            throw new HubException("Not in playing phase.");

        if (_state.TurnPhase != TurnPhase.Reinforce && _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Cannot trade cards in this phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (cardIndices.Length != 3 || cardIndices.Distinct().Count() != 3)
            throw new HubException("Must trade exactly 3 distinct cards.");

        if (cardIndices.Any(i => i < 0 || i >= player.Cards.Count))
            throw new HubException("Invalid card index.");

        var cards = cardIndices.Select(i => player.Cards[i]).ToArray();

        if (!IsValidSet(cards))
            throw new HubException("Invalid card set.");

        int armies;
        if (_state.HouseRules.FixedCardValues)
        {
            var types = cards.Select(c => c.Type).ToArray();
            int wilds = types.Count(t => t == CardType.Wild);
            var nonWild = types.Where(t => t != CardType.Wild).ToArray();
            bool isOneOfEach = nonWild.Distinct().Count() + wilds >= 3 && nonWild.Distinct().Count() > 1;

            if (isOneOfEach)
                armies = 10;
            else
            {
                var effectiveType = nonWild.Length > 0 ? nonWild[0] : CardType.Infantry;
                armies = effectiveType switch
                {
                    CardType.Artillery => 8,
                    CardType.Cavalry => 6,
                    _ => 4
                };
            }
        }
        else
        {
            _state.CardTradeCount++;
            armies = _state.CardTradeCount switch
            {
                1 => 4, 2 => 6, 3 => 8, 4 => 10, 5 => 12, 6 => 15,
                _ => 15 + (_state.CardTradeCount - 6) * 5
            };
        }

        var bonusIds = new List<int>();
        foreach (var card in cards)
        {
            if (card.TerritoryId is int tid && _state.Territories[tid].OwnerId == _state.CurrentPlayerIndex)
            {
                _state.Territories[tid].Armies += 2;
                bonusIds.Add(tid);
            }
        }

        player.ReinforcementsRemaining += armies;

        foreach (var i in cardIndices.OrderByDescending(x => x))
            player.Cards.RemoveAt(i);

        _state.Deck.AddRange(cards);
        ShuffleDeck();

        return (_state, armies, bonusIds);
    }

    private static bool IsValidSet(Card[] cards)
    {
        var types = cards.Select(c => c.Type).ToArray();
        int wilds = types.Count(t => t == CardType.Wild);
        if (wilds >= 2) return true;
        if (wilds == 1) return true;
        var nonWild = types.Where(t => t != CardType.Wild).ToArray();
        return nonWild.Distinct().Count() == 1 || nonWild.Distinct().Count() == 3;
    }

    public (GameState State, int Placed) Reinforce(string connectionId, int territoryId, int count = 1)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Reinforce)
            throw new HubException("Not in reinforce phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.Cards.Count >= 5)
            throw new HubException("You must trade cards first (5+ cards).");

        if (player.ReinforcementsRemaining <= 0)
            throw new HubException("No reinforcements remaining.");

        var territory = _state.Territories.FirstOrDefault(t => t.Id == territoryId);
        if (territory is null || territory.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own that territory.");

        var actual = Math.Min(Math.Max(1, count), player.ReinforcementsRemaining);
        territory.Armies += actual;
        player.ReinforcementsRemaining -= actual;

        return (_state, actual);
    }

    public GameState EndReinforce(string connectionId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Reinforce)
            throw new HubException("Not in reinforce phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.ReinforcementsRemaining > 0)
            throw new HubException("Place all reinforcements first.");

        _state.TurnPhase = TurnPhase.Attack;
        _state.AttackFrontId = null;
        _state.AttackFrontIds = [];
        return _state;
    }

    public GameState EndTurn(string connectionId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing)
            throw new HubException("Not in playing phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        player.EarnedCardThisTurn = false;

        do
        {
            _state.CurrentPlayerIndex = (_state.CurrentPlayerIndex + 1) % _state.Players.Count;
        }
        while (_state.Players[_state.CurrentPlayerIndex].IsEliminated);

        _state.TurnPhase = TurnPhase.Reinforce;
        CalculateReinforcements();

        return _state;
    }

    public GameState Fortify(string connectionId, int sourceId, int targetId, int armies)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Fortify)
            throw new HubException("Not in fortify phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.First(t => t.Id == sourceId);
        var target = _state.Territories.First(t => t.Id == targetId);

        if (source.OwnerId != _state.CurrentPlayerIndex || target.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You must own both territories.");

        if (!source.Adjacent.Contains(targetId))
            throw new HubException("Territories must be adjacent.");

        if (armies < 1 || armies >= source.Armies)
            throw new HubException($"Must move between 1 and {source.Armies - 1} armies.");

        source.Armies -= armies;
        target.Armies += armies;

        return _state;
    }
}
