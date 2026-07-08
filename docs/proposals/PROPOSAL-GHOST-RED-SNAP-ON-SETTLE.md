# Proposal — Snap Ghost Red Dice on Settle (Defender TV)

*2026-07-08*

---

## Problem

When Bob (attacker, remote TV) attacks you (defender, Z440), your arena opens
and ghost red dice tumble with real physics. During the window while you read
the dice and decide whether to tap Defend, the ghost red faces show random
physics values — e.g. 6, 5, 4. You roll your blue dice. Then `AttackerDiceResult`
arrives and the red dice snap to 3, 5, 1 — the actual result.

You made your decision looking at the wrong numbers. Actively misleading UX.

---

## Root Cause

Ghost red dice are spawned via `SpawnSetGhost` — real physics, faces unknown.
The snap currently only fires when `AttackerDiceResult` arrives. In the human
defender flow, `AttackerDiceResult` arrives **after** `SpawnDice("defender")`
(which is triggered by the defender tapping Roll). So the snap happens after
you've already committed to defending.

The values are available earlier — `AttackerDiceResult` arrives while the
ghost red dice are still settling. But the snap-on-arrival code runs before
the ghost dice have finished tumbling, leaving them visually unsettled at the
snap point.

---

## Fix

Fire a settle-then-snap task when ghost red dice are spawned. After physics
settle:
- If `lastAttackerValues` is already populated → snap immediately
- If not yet → do nothing (`OnAttackerDiceResult` will snap on arrival)

Also update `OnAttackerDiceResult` for the `None` role path to only snap if
the ghost dice have already settled (i.e. the settle task hasn't snapped them
yet). If the settle task beat the event, the snap is already done.

---

## Changes

### `CombatTheatre.cs`

**1. New field — track whether ghost red dice have settled:**

```csharp
bool ghostRedSettled = false;
```

Reset to `false` in `ResetCombat()`.

**2. New async method — settle and snap ghost red:**

```csharp
async Awaitable WaitSettleGhostRed()
{
    var token = combatCts?.Token ?? default;
    await diceRoller.WaitForSettleGhostRed(token);
    if (token.IsCancellationRequested) return;

    ghostRedSettled = true;

    if (lastAttackerValues.Length > 0)
    {
        Debug.Log($"[Combat] Ghost red settled — snapping to [{string.Join(",", lastAttackerValues)}]");
        diceRoller.SnapFacesForRole("attacker", lastAttackerValues);
    }
    else
    {
        Debug.Log($"[Combat] Ghost red settled — values not yet arrived, will snap on AttackerDiceResult");
    }
}
```

**3. Fire the task when ghost red dice are spawned** — in `OnSpawnDice`,
`role == "attacker"`, `!isMine` branch:

```csharp
// Non-owning TV — ghost roll red dice, snap faces when AttackerDiceResult arrives
currentRole = MyRole.None;
state = CombatState.Rolling;
diceRoller.SpawnSetGhost(role, diceCount);
_ = WaitSettleGhostRed();   // ← new: settle and snap as soon as physics done
```

**4. Update `OnAttackerDiceResult` for `currentRole == None` path** — only
snap if ghost dice haven't already settled and been snapped:

```csharp
if (state == CombatState.Rolling && currentRole == MyRole.None)
{
    lastAttackerValues = dto.values;
    if (!ghostRedSettled)
    {
        // Dice still tumbling — snap now (settle task will see values when it finishes)
        diceRoller.SnapFacesForRole("attacker", dto.values);
    }
    else
    {
        // Already settled — snap now (settle task had no values yet)
        diceRoller.SnapFacesForRole("attacker", dto.values);
    }
}
```

Actually both branches do the same thing — simplify to:

```csharp
if (state == CombatState.Rolling && currentRole == MyRole.None)
{
    lastAttackerValues = dto.values;
    diceRoller.SnapFacesForRole("attacker", dto.values);
}
```

This is the existing code — no change needed here. The settle task and the
event handler both call `SnapFacesForRole` — whichever fires second is a
no-op because the dice are already kinematic and frozen after the first snap.

### `DiceRoller.cs`

**New public method — wait for attacker ghost dice to settle:**

```csharp
/// <summary>
/// Wait for attacker ghost dice only to settle (indices 0..attackerDiceCount-1).
/// Used by defender TV to know when to snap ghost red faces.
/// </summary>
public async Awaitable WaitForSettleGhostRed(CancellationToken token = default)
{
    await Awaitable.NextFrameAsync();
    float elapsed = 0f;
    while (elapsed < settleTimeout)
    {
        if (token.IsCancellationRequested) return;
        bool allSettled = true;
        for (int i = 0; i < attackerDiceCount && i < activeDice.Count; i++)
        {
            var die = activeDice[i];
            if (die == null) continue;
            if (!IsSettled(die.GetComponent<Rigidbody>()))
            {
                allSettled = false;
                break;
            }
        }
        if (allSettled) return;
        elapsed += Time.deltaTime;
        await Awaitable.NextFrameAsync();
    }
}
```

---

## Result

Ghost red dice tumble, settle, then snap to the correct values — before you
tap Defend. You see the real attacker result while deciding. When you roll blue,
the red dice are already showing the right faces.

If `AttackerDiceResult` arrives before the dice settle (fast network, slow
physics), the existing `SnapFacesForRole` call in `OnAttackerDiceResult` fires
first — the settle task then finds kinematic dice and `SnapFacesForRole` is
called again harmlessly (no visual change, dice already frozen).

---

## Files

| File | Change |
|------|--------|
| `Assets/Scripts/CombatTheatre.cs` | New `ghostRedSettled` field; new `WaitSettleGhostRed` method; fire it in ghost red spawn branch |
| `Assets/Scripts/DiceRoller.cs` | New `WaitForSettleGhostRed` method — waits for attacker-index dice only |

---

*Created: 2026-07-08*
