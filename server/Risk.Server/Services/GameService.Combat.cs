using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Models;

namespace Risk.Server.Services;

/// <summary>
/// Combat & dice logic. Handles single attacks, blitz, Unity TV dice delegation,
/// player roll prompts, bot auto-roll, and move-after-capture.
/// When Unity TV is connected, dice are rolled physically and results sent back.
/// When not connected, server rolls randomly (no change to existing flow).
/// </summary>
public partial class GameService
{
    public GameState EndAttack(string connectionId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (player.EarnedCardThisTurn && _state.Deck.Count > 0)
        {
            player.Cards.Add(_state.Deck[^1]);
            _state.Deck.RemoveAt(_state.Deck.Count - 1);
        }

        _state.TurnPhase = TurnPhase.Fortify;
        return _state;
    }

    public (GameState State, CombatResult Result) Attack(string connectionId, int sourceId, int targetId, int diceCount)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.FirstOrDefault(t => t.Id == sourceId);
        var target = _state.Territories.FirstOrDefault(t => t.Id == targetId);

        if (source is null || source.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own the source territory.");

        if (target is null || target.OwnerId == _state.CurrentPlayerIndex)
            throw new HubException("Target must be an enemy territory.");

        if (!source.Adjacent.Contains(targetId))
            throw new HubException("Target is not adjacent to source.");

        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count > 0
            && !_state.AttackFrontIds.Contains(sourceId))
            throw new HubException("You must continue attacking from your current front.");

        if (diceCount < 1 || diceCount > 3)
            throw new HubException("Dice count must be 1-3.");

        if (source.Armies <= diceCount)
            throw new HubException("Not enough armies to attack with that many dice.");

        _state.LastDiceCount = diceCount;
        var attackerDice = RollDice(diceCount).OrderByDescending(d => d).ToArray();
        int defenderDiceCount = target.Armies >= 2 ? 2 : 1;
        var defenderDice = RollDice(defenderDiceCount).OrderByDescending(d => d).ToArray();

        int attackerLosses = 0, defenderLosses = 0;
        int comparisons = Math.Min(attackerDice.Length, defenderDice.Length);
        for (int i = 0; i < comparisons; i++)
        {
            if (attackerDice[i] > defenderDice[i]) defenderLosses++;
            else attackerLosses++;
        }

        source.Armies -= attackerLosses;
        target.Armies -= defenderLosses;
        bool captured = target.Armies <= 0;

        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count == 0)
            _state.AttackFrontIds.Add(sourceId);

        if (captured)
        {
            target.OwnerId = _state.CurrentPlayerIndex;
            target.Armies = 0;
            _state.PendingMoveSource = sourceId;
            _state.PendingMoveTarget = targetId;
            if (_state.HouseRules.LockedAttackFront)
                _state.AttackFrontIds.Add(targetId);
            if (!player.EarnedCardThisTurn)
                player.EarnedCardThisTurn = true;
        }

        return (_state, new CombatResult(attackerDice, defenderDice, attackerLosses, defenderLosses, captured, sourceId, targetId, source.Armies, target.Armies));
    }

    /// <summary>
    /// Attack with Unity dice delegation when connected, server-side fallback otherwise.
    /// </summary>
    public async Task<(GameState State, CombatResult Result)> AttackWithDice(
        IHubContext<GameHub> hub, string connectionId, int sourceId, int targetId, int diceCount)
    {
        if (!IsUnityTVConnected)
            return Attack(connectionId, sourceId, targetId, diceCount);

        if (_pending != null)
            throw new HubException("Combat in progress — wait for dice to resolve.");

        var source = _state!.Territories.First(t => t.Id == sourceId);
        var target = _state.Territories.First(t => t.Id == targetId);
        int defenderDiceCount = target.Armies >= 2 ? 2 : 1;
        var defenderPlayer = _state.Players[target.OwnerId];
        var attackerPlayer = _state.Players[_state.CurrentPlayerIndex];

        _pending = new PendingCombat
        {
            SourceId = sourceId,
            TargetId = targetId,
            AttackerDiceCount = diceCount,
            DefenderDiceCount = defenderDiceCount,
            AttackerPlayerIndex = _state.CurrentPlayerIndex,
            DefenderPlayerIndex = target.OwnerId
        };

        if (attackerPlayer.IsAI && defenderPlayer.IsAI)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                await PlayerRoll(hub, attackerPlayer.ConnectionId, diceCount);
                await PlayerRoll(hub, defenderPlayer.ConnectionId, defenderDiceCount);
            });
        }
        else if (defenderPlayer.IsAI)
        {
            await PlayerRoll(hub, attackerPlayer.ConnectionId, diceCount);
            await PlayerRoll(hub, defenderPlayer.ConnectionId, defenderDiceCount);
        }
        else
        {
            await PlayerRoll(hub, attackerPlayer.ConnectionId, diceCount);
            _ = Task.Run(() => hub.Clients.All.SendAsync("RollPrompt",
                new RollPrompt("defender", defenderDiceCount, defenderDiceCount, sourceId, targetId, defenderPlayer.Name)));
        }

        // Wait for both players to roll (30s timeout prevents infinite stuck if prompt lost)
        var rollsCompleted = Task.WhenAll(_pending.AttackerRoll.Task, _pending.DefenderRoll.Task);
        if (await Task.WhenAny(rollsCompleted, Task.Delay(30000)) != rollsCompleted)
        {
            _pending = null;
            return Attack(connectionId, sourceId, targetId, diceCount);
        }

        var completed = await Task.WhenAny(_pending.DiceResult.Task, Task.Delay(10000));
        if (completed == _pending.DiceResult.Task && !_pending.DiceResult.Task.IsCanceled)
        {
            var (attackerDice, defenderDice) = await _pending.DiceResult.Task;
            _pending = null;
            return ResolveCombat(connectionId, sourceId, targetId, attackerDice, defenderDice);
        }

        _pending = null;
        return Attack(connectionId, sourceId, targetId, diceCount);
    }

    public async Task PlayerRoll(IHubContext<GameHub> hub, string connectionId, int diceCount)
    {
        if (_pending == null) return;

        var attackerConnId = _state!.Players[_pending.AttackerPlayerIndex].ConnectionId;
        var defenderConnId = _state.Players[_pending.DefenderPlayerIndex].ConnectionId;

        if (connectionId == attackerConnId && !_pending.AttackerRoll.Task.IsCompleted)
        {
            _pending.AttackerRoll.TrySetResult(diceCount);
            await hub.Clients.All.SendAsync("SpawnDice", new SpawnDice("attacker", diceCount, _pending.SourceId, _pending.TargetId));
            await AutoRollBotOpponent(hub, "defender");
        }
        else if (connectionId == defenderConnId && !_pending.DefenderRoll.Task.IsCompleted)
        {
            int finalCount = Math.Min(diceCount, _pending.DefenderDiceCount);
            _pending.DefenderDiceCount = finalCount;
            _pending.DefenderRoll.TrySetResult(finalCount);
            await hub.Clients.All.SendAsync("SpawnDice", new SpawnDice("defender", finalCount, _pending.SourceId, _pending.TargetId));
            await AutoRollBotOpponent(hub, "attacker");
        }
    }

    private async Task AutoRollBotOpponent(IHubContext<GameHub> hub, string role)
    {
        if (_pending == null) return;

        if (role == "defender" && !_pending.DefenderRoll.Task.IsCompleted)
        {
            var defender = _state!.Players[_pending.DefenderPlayerIndex];
            if (defender.IsAI)
            {
                _pending.DefenderRoll.TrySetResult(_pending.DefenderDiceCount);
                await hub.Clients.All.SendAsync("SpawnDice", new SpawnDice("defender", _pending.DefenderDiceCount, _pending.SourceId, _pending.TargetId));
            }
        }
        else if (role == "attacker" && !_pending.AttackerRoll.Task.IsCompleted)
        {
            var attacker = _state!.Players[_pending.AttackerPlayerIndex];
            if (attacker.IsAI)
            {
                _pending.AttackerRoll.TrySetResult(_pending.AttackerDiceCount);
                await hub.Clients.All.SendAsync("SpawnDice", new SpawnDice("attacker", _pending.AttackerDiceCount, _pending.SourceId, _pending.TargetId));
            }
        }
    }

    public (GameState State, CombatResult Result) ResolveCombat(string connectionId, int sourceId, int targetId, int[] attackerDice, int[] defenderDice)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.First(t => t.Id == sourceId);
        var target = _state.Territories.First(t => t.Id == targetId);

        attackerDice = attackerDice.OrderByDescending(d => d).ToArray();
        defenderDice = defenderDice.OrderByDescending(d => d).ToArray();
        _state.LastDiceCount = attackerDice.Length;

        int attackerLosses = 0, defenderLosses = 0;
        int comparisons = Math.Min(attackerDice.Length, defenderDice.Length);
        for (int i = 0; i < comparisons; i++)
        {
            if (attackerDice[i] > defenderDice[i]) defenderLosses++;
            else attackerLosses++;
        }

        source.Armies -= attackerLosses;
        target.Armies -= defenderLosses;
        bool captured = target.Armies <= 0;

        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count == 0)
            _state.AttackFrontIds.Add(sourceId);

        if (captured)
        {
            target.OwnerId = _state.CurrentPlayerIndex;
            target.Armies = 0;
            _state.PendingMoveSource = sourceId;
            _state.PendingMoveTarget = targetId;
            if (_state.HouseRules.LockedAttackFront)
                _state.AttackFrontIds.Add(targetId);
            if (!player.EarnedCardThisTurn)
                player.EarnedCardThisTurn = true;
        }

        return (_state, new CombatResult(attackerDice, defenderDice, attackerLosses, defenderLosses, captured, sourceId, targetId, source.Armies, target.Armies));
    }

    public (GameState State, BlitzResult Result) Blitz(string connectionId, int sourceId, int targetId)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        if (IsUnityTVConnected && _pending != null)
            throw new HubException("Combat in progress — wait for dice to resolve.");

        var source = _state.Territories.FirstOrDefault(t => t.Id == sourceId);
        var target = _state.Territories.FirstOrDefault(t => t.Id == targetId);

        if (source is null || source.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You don't own the source territory.");
        if (target is null || target.OwnerId == _state.CurrentPlayerIndex)
            throw new HubException("Target must be an enemy territory.");
        if (!source.Adjacent.Contains(targetId))
            throw new HubException("Target is not adjacent to source.");
        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count > 0
            && !_state.AttackFrontIds.Contains(sourceId))
            throw new HubException("You must continue attacking from your current front.");
        if (source.Armies <= 1)
            throw new HubException("Not enough armies to attack.");

        if (_state.HouseRules.LockedAttackFront && _state.AttackFrontIds.Count == 0)
            _state.AttackFrontIds.Add(sourceId);

        int startSourceArmies = source.Armies;
        int startTargetArmies = target.Armies;
        int rounds = 0;
        int[] finalAttackerDice = [];
        int[] finalDefenderDice = [];

        int lastDice = 0;
        while (source.Armies > 1 && target.Armies > 0)
        {
            lastDice = Math.Min(3, source.Armies - 1);
            var attackerDice = RollDice(lastDice).OrderByDescending(d => d).ToArray();
            int defDice = target.Armies >= 2 ? 2 : 1;
            var defenderDice = RollDice(defDice).OrderByDescending(d => d).ToArray();

            finalAttackerDice = attackerDice;
            finalDefenderDice = defenderDice;

            int comparisons = Math.Min(attackerDice.Length, defenderDice.Length);
            for (int i = 0; i < comparisons; i++)
            {
                if (attackerDice[i] > defenderDice[i]) target.Armies--;
                else source.Armies--;
            }
            rounds++;
        }

        bool captured = target.Armies <= 0;
        _state.LastDiceCount = Math.Min(lastDice, source.Armies - 1);

        if (captured)
        {
            target.OwnerId = _state.CurrentPlayerIndex;
            target.Armies = 0;
            _state.PendingMoveSource = sourceId;
            _state.PendingMoveTarget = targetId;
            if (_state.HouseRules.LockedAttackFront)
                _state.AttackFrontIds.Add(targetId);
            if (!player.EarnedCardThisTurn)
                player.EarnedCardThisTurn = true;
        }

        return (_state, new BlitzResult(rounds, startSourceArmies - source.Armies, startTargetArmies - target.Armies, captured, sourceId, targetId, source.Armies, target.Armies, finalAttackerDice, finalDefenderDice));
    }

    public (GameState State, bool ForcedTradeRequired, int EliminatedPlayerIndex, bool MissionWon) MoveAfterCapture(string connectionId, int sourceId, int targetId, int armies)
    {
        if (_state is null || _state.Phase != GamePhase.Playing || _state.TurnPhase != TurnPhase.Attack)
            throw new HubException("Not in attack phase.");

        var player = _state.Players[_state.CurrentPlayerIndex];
        if (player.ConnectionId != connectionId)
            throw new HubException("Not your turn.");

        var source = _state.Territories.First(t => t.Id == sourceId);
        var target = _state.Territories.First(t => t.Id == targetId);

        if (source.OwnerId != _state.CurrentPlayerIndex || target.OwnerId != _state.CurrentPlayerIndex)
            throw new HubException("You must own both territories.");

        int minMove = Math.Min(_state.LastDiceCount, source.Armies - 1);
        if (armies < minMove || armies >= source.Armies)
            throw new HubException($"Must move between {minMove} and {source.Armies - 1} armies.");

        source.Armies -= armies;
        target.Armies += armies;
        _state.PendingMoveSource = null;
        _state.PendingMoveTarget = null;

        int defenderId = -1;
        for (int i = 0; i < _state.Players.Count; i++)
        {
            if (i != _state.CurrentPlayerIndex && !_state.Players[i].IsEliminated
                && !_state.Territories.Any(t => t.OwnerId == i))
            {
                _state.Players[i].IsEliminated = true;
                defenderId = i;
                player.Cards.AddRange(_state.Players[i].Cards);
                _state.Players[i].Cards.Clear();

                if (_state.HouseRules.UseMissions)
                {
                    for (int p = 0; p < _state.Players.Count; p++)
                    {
                        if (p == _state.CurrentPlayerIndex) continue;
                        var m = _state.Players[p].Mission;
                        if (m is { Type: MissionType.Elimination } && m.TargetPlayerIndex == i)
                            m.FallenBackToWorldDomination = true;
                    }
                }
            }
        }

        bool missionWon = false;
        if (_state.HouseRules.UseMissions && CheckMissionComplete(_state.CurrentPlayerIndex))
        {
            _state.Phase = GamePhase.GameOver;
            missionWon = true;
        }
        else if (_state.Territories.All(t => t.OwnerId == _state.CurrentPlayerIndex))
        {
            _state.Phase = GamePhase.GameOver;
        }

        bool forcedTrade = player.Cards.Count >= 5;
        return (_state, forcedTrade, defenderId, missionWon);
    }
}
