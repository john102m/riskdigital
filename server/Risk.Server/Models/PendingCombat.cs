namespace Risk.Server.Models
{
    /// <summary>
    /// Encapsulates all state for a single combat awaiting dice results from Unity TV.
    /// Uses player indices (not connection IDs) so reconnects don't break matching.
    /// Multi-household: attacker and defender dice may arrive from different TVs.
    /// Sequential flow: defender spawns only after attacker submits.
    /// </summary>
    public class PendingCombat
    {
        public int SourceId { get; init; }
        public int TargetId { get; init; }
        public int AttackerDiceCount { get; init; }
        public int DefenderDiceCount { get; set; }
        public int AttackerPlayerIndex { get; init; }
        public int DefenderPlayerIndex { get; init; }
        public TaskCompletionSource<int> AttackerRoll { get; } = new();
        public TaskCompletionSource<int> DefenderRoll { get; } = new();
        public TaskCompletionSource<(int[] AttackerDice, int[] DefenderDice)> DiceResult { get; } = new();

        /// <summary>Fires when attacker TV submits its dice values (triggers defender spawn).</summary>
        public TaskCompletionSource<int[]> AttackerSubmitted { get; } = new();

        private int[]? _attackerDice;
        private int[]? _defenderDice;

        /// <summary>Legacy: submit both at once (single TV, backward compat).</summary>
        public void SubmitDiceResult(int[] attackerDice, int[] defenderDice)
            => DiceResult.TrySetResult((attackerDice, defenderDice));

        /// <summary>Submit attacker dice only (multi-household: attacker's TV).</summary>
        public void SubmitAttackerDice(int[] dice)
        {
            _attackerDice = dice;
            AttackerSubmitted.TrySetResult(dice);
            TryComplete();
        }

        /// <summary>Submit defender dice only (multi-household: defender's TV).</summary>
        public void SubmitDefenderDice(int[] dice)
        {
            _defenderDice = dice;
            TryComplete();
        }

        private void TryComplete()
        {
            if (_attackerDice != null && _defenderDice != null)
                DiceResult.TrySetResult((_attackerDice, _defenderDice));
        }
    }

}
