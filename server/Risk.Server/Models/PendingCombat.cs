namespace Risk.Server.Models
{
    /// <summary>
    /// Encapsulates all state for a single combat awaiting dice results from Unity TV.
    /// Uses player indices (not connection IDs) so reconnects don't break matching.
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

        public void SubmitDiceResult(int[] attackerDice, int[] defenderDice)
            => DiceResult.TrySetResult((attackerDice, defenderDice));
    }

}
