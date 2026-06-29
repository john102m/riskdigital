namespace Risk.Server.Models;

public record CombatResult(
    int[] AttackerDice,
    int[] DefenderDice,
    int AttackerLosses,
    int DefenderLosses,
    bool Captured,
    int SourceId,
    int TargetId,
    int SourceArmies,
    int TargetArmies
);

public record BlitzResult(
    int Rounds,
    int TotalAttackerLosses,
    int TotalDefenderLosses,
    bool Captured,
    int SourceId,
    int TargetId,
    int SourceArmies,
    int TargetArmies,
    int[] FinalAttackerDice,
    int[] FinalDefenderDice
);

public record CombatRollRequest(
    int SourceId,
    int TargetId,
    int AttackerDiceCount,
    int DefenderDiceCount
);