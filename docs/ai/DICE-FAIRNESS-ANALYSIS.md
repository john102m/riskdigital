# Dice Fairness Analysis

## Overview

Two dice sources exist in the game:

| Source | When used | Method |
|--------|-----------|--------|
| **Server** | Blitz (all rounds), single attacks without Unity connected | `Random.Shared.Next(1, 7)` — .NET PRNG |
| **Unity** | Single attacks when Unity TV board is connected | 3D physics simulation, face read via dot product |

Both are logged to `dice-audit.csv` for long-term comparison.

## Data Location

| Environment | Path |
|-------------|------|
| Local dev | `server/Risk.Server/Data/logs/dice-audit.csv` |
| WHUK prod | `D:\Inetpub\vhosts\spooch.co.uk\tmp\risk-logs\dice-audit.csv` (or `/admin/logs-download`) |

## CSV Format

```csv
Timestamp,Source,Role,Value
2026-07-04T20:15:03.123Z,server,attacker,4
2026-07-04T20:15:03.123Z,server,attacker,6
2026-07-04T20:15:03.124Z,server,defender,3
2026-07-04T20:15:10.456Z,unity,attacker,2
2026-07-04T20:15:10.457Z,unity,attacker,5
2026-07-04T20:15:12.789Z,unity,defender,1
```

## What to Analyse

### 1. Face Distribution (fairness check)

Each face (1–6) should appear ~16.7% of the time. Significant deviation indicates bias.

**PowerShell quick check:**
```powershell
$data = Import-Csv "server/Risk.Server/Data/logs/dice-audit.csv"
$data | Group-Object Value | Sort-Object Name | Select-Object Name, Count, @{N="Pct";E={[math]::Round($_.Count/$data.Count*100,1)}}
```

**Split by source:**
```powershell
$data | Where-Object { $_.Source -eq "server" } | Group-Object Value | Sort-Object Name | Select-Object Name, Count
$data | Where-Object { $_.Source -eq "unity" } | Group-Object Value | Sort-Object Name | Select-Object Name, Count
```

### 2. Attacker vs Defender Win Rate

In standard Risk, the attacker has a statistical advantage (~60% win rate per die comparison with 3v2). Deviations from expected rates per source indicate bias.

```powershell
# This requires combat-level analysis (not just individual dice)
# Future enhancement: log combat outcomes per source in the same file
```

### 3. Unity Physics Bias Detection

Physics dice can be biased by:
- Spawn rotation (always starting from same orientation)
- Arena geometry (walls/floor favouring certain bounce patterns)
- Throw direction (always same angle)
- Settle detection timing (reading face before fully stable)

**Signs of bias:**
- One face appears >20% (should be ~16.7%)
- Opposite faces correlated (e.g. if 6 is high, 1 should be low — opposite side)
- Attacker/defender showing different distributions (same physics, shouldn't differ)

### 4. Sample Size

| Rolls | Confidence |
|-------|-----------|
| <100 | Too few — noise dominates |
| 100–500 | Trends visible but not conclusive |
| 500–2000 | Good statistical power |
| 2000+ | Definitive — any bias >2% detectable |

Rule of thumb: need ~500 rolls per source before drawing conclusions.

### 5. Chi-Square Goodness of Fit

For rigorous testing (is this die fair?):

```powershell
# Expected: each face appears count/6 times
$data = Import-Csv "dice-audit.csv" | Where-Object { $_.Source -eq "unity" }
$n = $data.Count
$expected = $n / 6
$groups = $data | Group-Object Value
$chiSq = ($groups | ForEach-Object { [math]::Pow($_.Count - $expected, 2) / $expected } | Measure-Object -Sum).Sum
Write-Output "Chi-square: $([math]::Round($chiSq, 2)) (critical value at p=0.05, df=5: 11.07)"
# If chi-sq > 11.07, the die is likely biased
```

## Admin Endpoints

| Endpoint | Returns |
|----------|---------|
| `/admin/logs-status` | Shows all log files including dice-audit.csv |
| `/admin/logs-download` | Zip of all CSVs (includes dice audit) |

## When to Check

- After every 5–10 games, glance at face distribution
- If a player complains "the dice hate me" — pull the data
- After any Unity dice physics tuning (spawn, throw, friction changes) — check the next 100+ rolls for shifts

## Future Enhancements

- Add combat outcome per source (win/loss per comparison, not just face values)
- In-game admin endpoint: `/admin/dice-stats` returning live distribution summary
- Auto-detect bias: log a warning if any face exceeds 20% after 500+ rolls
