namespace Risk.Server.Training;

/// <summary>
/// Simulates thousands of blitz battles and writes results to CSV.
/// This generates the training data for our ML.NET blitz probability model.
/// 
/// Each row = one simulated blitz with:
///   - AttackerArmies: starting attacker count (2-30)
///   - DefenderArmies: starting defender count (1-20)
///   - Captured: 1 if attacker won, 0 if not
///   - AttackerLosses: armies lost by attacker
///   - DefenderLosses: armies lost by defender
/// </summary>
public static class BlitzSimulator
{
    public static void GenerateData(string outputPath, int samplesPerMatchup = 100)
    {
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("AttackerArmies,DefenderArmies,Captured,AttackerLosses,DefenderLosses");

        // Simulate all relevant army combinations
        for (int atk = 2; atk <= 30; atk++)
        {
            for (int def = 1; def <= 20; def++)
            {
                for (int s = 0; s < samplesPerMatchup; s++)
                {
                    var (captured, atkLoss, defLoss) = SimulateBlitz(atk, def);
                    writer.WriteLine($"{atk},{def},{(captured ? 1 : 0)},{atkLoss},{defLoss}");
                }
            }
        }
    }

    /// <summary>
    /// Simulates a single blitz battle using the same dice rules as GameService.
    /// Attacker rolls max dice each round until capture or source = 1.
    /// </summary>
    private static (bool Captured, int AttackerLosses, int DefenderLosses) SimulateBlitz(int attackerArmies, int defenderArmies)
    {
        int startAtk = attackerArmies;
        int startDef = defenderArmies;

        while (attackerArmies > 1 && defenderArmies > 0)
        {
            // Attacker dice: min(3, armies-1)
            int atkDice = Math.Min(3, attackerArmies - 1);
            // Defender dice: min(2, armies)
            int defDice = Math.Min(2, defenderArmies);

            // Roll dice
            var atkRolls = RollDice(atkDice);
            var defRolls = RollDice(defDice);

            // Sort descending
            Array.Sort(atkRolls); Array.Reverse(atkRolls);
            Array.Sort(defRolls); Array.Reverse(defRolls);

            // Compare pairs — defender wins ties
            int pairs = Math.Min(atkDice, defDice);
            for (int i = 0; i < pairs; i++)
            {
                if (atkRolls[i] > defRolls[i])
                    defenderArmies--;
                else
                    attackerArmies--;
            }
        }

        bool captured = defenderArmies == 0;
        return (captured, startAtk - attackerArmies, startDef - defenderArmies);
    }

    private static int[] RollDice(int count)
    {
        var rolls = new int[count];
        for (int i = 0; i < count; i++)
            rolls[i] = Random.Shared.Next(1, 7);
        return rolls;
    }
}
