# Proposal — Simultaneous Arena Sweep (Phase 1)

## Goal
Both TVs sweep their dice arena in at the same moment when an attack starts,
regardless of which household owns the attacker or defender. Currently only the
owning TV opens its arena — the other waits silently until a result arrives.

## What Changes

### Server — `GameService.Combat.cs`

Cross-household path. Change both `SpawnDice` sends from single-client to group:

**Step 1 — Attacker spawn:**
```csharp
// Before:
await hub.Clients.Client(attackerTvConn).SendAsync("SpawnDice", new SpawnDice("attacker", diceCount, sourceId, targetId));

// After:
await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("attacker", diceCount, sourceId, targetId));
```

**Step 3 — Defender spawn (bot path):**
```csharp
// Before:
await hub.Clients.Client(defenderTvConn).SendAsync("SpawnDice", new SpawnDice("defender", defenderDiceCount, sourceId, targetId));

// After:
await hub.Clients.Group(gameCode).SendAsync("SpawnDice", new SpawnDice("defender", defenderDiceCount, sourceId, targetId));
```

The defender `SpawnDice` for the **human** path is already handled by `PlayerRoll` when
the defender taps Roll — that also needs the same change:

In `PlayerRoll`:
```csharp
// Before:
await hub.Clients.Client(tvConn).SendAsync("SpawnDice", ...)

// After:
await hub.Clients.Group(gameCode).SendAsync("SpawnDice", ...)
```

Same-household path is unaffected — it already sends to a single TV and that TV
owns both roles.

### Unity — `CombatTheatre.cs`

`OnSpawnDice` currently assumes "if I receive SpawnDice, I own this role". After the
server change, every TV receives every `SpawnDice`. Need to distinguish:

- **I own this role** → roll dice (current behaviour)
- **I don't own this role** → open arena, start camera sweep, wait for result to arrive

How does a TV know if it owns the role? It registered with `playerIndices`. The TV
knows its own household's player indices from `GameJoinScreen` / `SignalRClient`.

Add a helper to `SignalRClient` (or `CombatTheatre`) to check ownership:

```csharp
bool IMyRole(int playerIndex)
{
    // If no playerIndices configured (single-TV setup) — always own everything
    if (signalR.playerIndices == null || signalR.playerIndices.Length == 0) return true;
    return System.Array.IndexOf(signalR.playerIndices, playerIndex) >= 0;
}
```

The `SpawnDice` event already includes `sourceId`/`targetId` but not `playerIndex`.
Need to add the attacker/defender player index to the payload so the TV can check ownership.

**Updated `SpawnDice` record (server `Models/CombatResult.cs`):**
```csharp
public record SpawnDice(
    string Role,
    int DiceCount,
    int SourceId,
    int TargetId,
    int PlayerIndex   // ← new: which player index owns this roll
);
```

**`OnSpawnDice` in `CombatTheatre.cs`:**
```csharp
void OnSpawnDice(string role, int diceCount, int sourceId, int targetId, int playerIndex)
{
    bool iMine = IMyRole(playerIndex);

    if (role == "attacker")
    {
        ResetCombat();
        currentSourceId = sourceId;
        currentTargetId = targetId;
        PositionPanel(sourceId, targetId);
        ShowPanel(true);
        StartCameraSweep();

        if (iMine)
        {
            currentRole = MyRole.Attacker;
            state = CombatState.Rolling;
            diceRoller.SpawnSet(role, diceCount);
            _ = WaitSettleAttacker();
        }
        else
        {
            // Arena open, sweep running — wait for AttackerDiceResult to place dice
            currentRole = MyRole.None;
            state = CombatState.Rolling;
        }
    }
    else if (role == "defender")
    {
        currentSourceId = sourceId;
        currentTargetId = targetId;

        if (currentRole == MyRole.Attacker && iMine)
        {
            // Same household — roll both
            currentRole = MyRole.SameHousehold;
            diceRoller.SpawnSet(role, diceCount);
        }
        else if (iMine)
        {
            // I'm the defender TV
            currentRole = MyRole.Defender;
            if (!dicePanelUI.activeSelf) // arena may already be open from attacker SpawnDice
            {
                PositionPanel(sourceId, targetId);
                ShowPanel(true);
                StartCameraSweep();
            }
            diceRoller.PlaceAttackerDiceOnly(lastAttackerValues, GetArenaCentre());
            diceRoller.SpawnSet(role, diceCount);
            _ = WaitSettleDefender();
        }
        else
        {
            // I'm the attacker TV — defender rolling on their TV, arena already open
            // Static defender dice will arrive via DefenderDiceResult
        }
    }
}
```

### Also — `SignalRClient.cs`

`SpawnDice` handler needs to pass `playerIndex` through to the event. Update the
deserialisation and event signature to include it.

---

## Impact on Tests 1–4

| Test | Before | After |
|------|--------|-------|
| 1 — Human (Eng) → Bot (Sco) | Scotland arena opens when SpawnDice("defender") arrives | Both arenas open when SpawnDice("attacker") fires |
| 2 — Bot (Sco) → Human (Eng) | England arena opens when RollPrompt fires | Both arenas open when SpawnDice("attacker") fires |
| 3 — Same-household, Sco spectator | Scotland opens when AttackerDiceResult arrives | Scotland opens when SpawnDice("attacker") fires |
| 4 — Bot → Bot cross-household | Remote opens on SpawnDice for each role | Both open on SpawnDice("attacker") |

No test regresses. All four gain simultaneous sweep.

## Files

| File | Change |
|------|--------|
| `server/Risk.Server/Services/GameService.Combat.cs` | 3 `SendAsync` calls: attacker spawn, bot defender spawn → group; PlayerRoll defender spawn → group |
| `server/Risk.Server/Hubs/GameHub.cs` | `PlayerRoll` → pass `gameCode` to `game.PlayerRoll` if not already |
| `server/Risk.Server/Models/CombatResult.cs` | `SpawnDice` record gains `PlayerIndex` field |
| `Assets/Scripts/SignalRClient.cs` | `SpawnDice` handler passes `playerIndex`; event signature updated |
| `Assets/Scripts/CombatTheatre.cs` | `OnSpawnDice` split into owning/non-owning branches |

---

## Phase 2 (later)
Replace static placement on non-owning TV with real physics — each TV tumbles all
dice independently, server only uses the submission from the correct TV per role.
Non-owning TV's physics result is eye candy, never read by server.

---

*Created: 2026-07-07*
