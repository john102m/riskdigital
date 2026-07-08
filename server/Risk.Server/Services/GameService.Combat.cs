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
        var attackerDice = RollDice(diceCount, "attacker").OrderByDescending(d => d).ToArray();
        int defenderDiceCount = target.Armies >= 2 ? 2 : 1;
        var defenderDice = RollDice(defenderDiceCount, "defender").OrderByDescending(d => d).ToArray();

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
    /// Sequential flow: attacker spawns → attacker submits → defender spawns → defender submits → resolve.
    /// </summary>
    public async Task<(GameState State, CombatResult Result)> AttackWithDice(
        IHubContext<GameHub> hub, string gameCode, string connectionId, int sourceId, int targetId, int diceCount)
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

        // ─── Step 1: Spawn attacker dice ─────────────────────────────────────
        var attackerTvConn = GetTVForPlayer(_pending.AttackerPlayerIndex);
        var defenderTvConn = GetTVForPlayer(_pending.DefenderPlayerIndex);
        bool sameHousehold = attackerTvConn != null && attackerTvConn == defenderTvConn;

        if (sameHousehold)
        {
            // Same TV rolls both — send both SpawnDice to group (spectator TVs open arena too)
            System.Diagnostics.Debug.WriteLine($"[DICE] Same-household: both on {(_registeredTVs.FirstOrDefault(t => t.ConnectionId == attackerTvConn)?.HouseholdId ?? "?")}");
            await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("attacker", diceCount, sourceId, targetId, _pending.AttackerPlayerIndex));
            await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("defender", defenderDiceCount, sourceId, targetId, _pending.DefenderPlayerIndex));
            _pending.DefenderRoll.TrySetResult(defenderDiceCount);

            // Wait for combined result (15s timeout)
            var sameHouseDiceTask = _pending.DiceResult.Task;
            if (await Task.WhenAny(sameHouseDiceTask, Task.Delay(15000)) != sameHouseDiceTask || sameHouseDiceTask.IsCanceled)
            {
                System.Diagnostics.Debug.WriteLine($"[DICE] Same-household timeout — falling back to server roll");
                _pending = null;
                return Attack(connectionId, sourceId, targetId, diceCount);
            }

            var (shAttacker, shDefender) = await sameHouseDiceTask;
            // Broadcast to spectator TVs
            await hub.Clients.Group(gameCode).SendAsync("AttackerDiceResult", new { values = shAttacker, sourceId, targetId });
            await hub.Clients.Group(gameCode).SendAsync("DefenderDiceResult", shDefender);
            await Task.Delay(500);

            _pending = null;
            return ResolveCombat(connectionId, sourceId, targetId, shAttacker, shDefender);
        }

        // ─── Cross-household: parallel flow ─────────────────────────────────
        // Send both SpawnDice simultaneously — all arenas roll at once.
        // Server waits for both authoritative submissions in parallel.
        System.Diagnostics.Debug.WriteLine($"[DICE] Parallel spawn: attacker + defender → group");
        await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("attacker", diceCount, sourceId, targetId, _pending.AttackerPlayerIndex));

        if (defenderPlayer.IsAI)
        {
            // Bot: spawn defender dice immediately, auto-complete submission
            await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("defender", defenderDiceCount, sourceId, targetId, _pending.DefenderPlayerIndex));
            _pending.DefenderRoll.TrySetResult(defenderDiceCount);
        }
        else
        {
            // Human defender: send RollPrompt — their arena is already open from SpawnDice("attacker")
            // They tap Roll → PlayerRoll sends SpawnDice("defender") to group
            await hub.Clients.Group(gameCode).SendAsync("RollPrompt",
                new RollPrompt("defender", defenderDiceCount, defenderDiceCount, sourceId, targetId, defenderPlayer.Name));
            System.Diagnostics.Debug.WriteLine($"[DICE] RollPrompt sent to {defenderPlayer.Name}");
        }

        // ─── Wait for both to submit (15s timeout) ───────────────────────────
        var diceResultTask = _pending.DiceResult.Task;
        if (await Task.WhenAny(diceResultTask, Task.Delay(15000)) != diceResultTask || diceResultTask.IsCanceled)
        {
            System.Diagnostics.Debug.WriteLine($"[DICE] Dice submit timeout — falling back to server roll");
            _pending = null;
            return Attack(connectionId, sourceId, targetId, diceCount);
        }

        // ─── Both submitted — broadcast results and resolve ───────────────────
        var (finalAttacker, finalDefender) = await diceResultTask;
        await hub.Clients.Group(gameCode).SendAsync("AttackerDiceResult", new { values = finalAttacker, sourceId, targetId });
        await hub.Clients.Group(gameCode).SendAsync("DefenderDiceResult", finalDefender);
        System.Diagnostics.Debug.WriteLine($"[DICE] Both submitted — A[{string.Join(",", finalAttacker)}] D[{string.Join(",", finalDefender)}], resolving");

        await Task.Delay(500);
        _pending = null;
        return ResolveCombat(connectionId, sourceId, targetId, finalAttacker, finalDefender);
    }

    /// <summary>Human defender taps Roll — spawn defender dice on their TV.</summary>
    public async Task PlayerRoll(IHubContext<GameHub> hub, string gameCode, string connectionId, int diceCount)
    {
        if (_pending == null) return;

        var defenderConnId = _state!.Players[_pending.DefenderPlayerIndex].ConnectionId;

        if (connectionId == defenderConnId && !_pending.DefenderRoll.Task.IsCompleted)
        {
            int finalCount = Math.Min(diceCount, _pending.DefenderDiceCount);
            _pending.DefenderDiceCount = finalCount;
            _pending.DefenderRoll.TrySetResult(finalCount);
            System.Diagnostics.Debug.WriteLine($"[DICE] PlayerRoll defender (human): diceCount={finalCount} → broadcast to group");
            await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("defender", finalCount, _pending.SourceId, _pending.TargetId, _pending.DefenderPlayerIndex));
        }
    }

    // AutoRollBotOpponent removed — sequential flow handles bot dice in AttackWithDice directly

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
            var attackerDice = RollDice(lastDice, "attacker").OrderByDescending(d => d).ToArray();
            int defDice = target.Armies >= 2 ? 2 : 1;
            var defenderDice = RollDice(defDice, "defender").OrderByDescending(d => d).ToArray();

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

    public (GameState State, bool ForcedTradeRequired, int EliminatedPlayerIndex, bool MissionWon, List<int> MissionFallbackPlayers) MoveAfterCapture(string connectionId, int sourceId, int targetId, int armies)
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
        var fallbackPlayers = new List<int>();
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
                        {
                            m.FallenBackToWorldDomination = true;
                            fallbackPlayers.Add(p);
                        }
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
        return (_state, forcedTrade, defenderId, missionWon, fallbackPlayers);
    }
}
