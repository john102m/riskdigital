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
