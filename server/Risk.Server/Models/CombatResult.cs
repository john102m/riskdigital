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

/// <summary>Sent to a player's handset telling them to tap Roll.</summary>
public record RollPrompt(
    string Role,        // "attacker" or "defender"
    int DiceCount,      // how many dice they'll roll (attacker: chosen, defender: max available)
    int MaxDice,        // max dice available (defender can choose fewer)
    int SourceId,
    int TargetId,
    string PlayerName   // who should roll — handset filters on this
);

/// <summary>Sent to Unity TV to spawn one player's dice.</summary>
public record SpawnDice(
    string Role,        // "attacker" or "defender"
    int DiceCount,
    int SourceId,
    int TargetId,
    int PlayerIndex     // which player index owns this roll — TV uses this to self-determine ownership
);