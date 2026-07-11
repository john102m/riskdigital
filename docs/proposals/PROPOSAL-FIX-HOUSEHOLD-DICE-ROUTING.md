# Proposal — Fix Multi-Household Dice Routing

## Symptom

Player 0 (England/Z440) attacks Bot 2 (Scotland/laptop). Z440 rolls **both** attacker and defender dice. Laptop shows the arena but no dice spawn. Laptop then displays the result statically (like a blitz courtesy display) — which is the spectator path, not the active-roller path.

## Root Causes

### 1. `GetTVForPlayer` fallback returns wrong TV

```csharp
public string? GetTVForPlayer(int playerIndex)
{
    if (_registeredTVs.Count == 1) return _registeredTVs[0].ConnectionId;
    if (_registeredTVs.Count == 0) return null;

    var match = _registeredTVs.FirstOrDefault(t => t.PlayerIndices?.Contains(playerIndex) == true);
    if (match != null) return match.ConnectionId;

    // BUG: Falls back to first TV (Z440) instead of null
    return _registeredTVs[0].ConnectionId;
}
```

When the laptop registers **without** `playerIndices` (see cause #2), no TV entry contains player index 2. The fallback returns `_registeredTVs[0]` — Z440. So `SpawnDice("defender")` goes to Z440, which dutifully spawns and rolls blue dice alongside its own red dice.

### 2. Laptop registers without household config

Several paths lead to a TV registering without household info:

**a) `householdInput` field not wired in the scene.**  
`ApplyHouseholdConfig` exits immediately if `householdInput == null`:
```csharp
void ApplyHouseholdConfig()
{
    if (householdInput == null) return;  // ← Silent exit
    var text = householdInput.text?.Trim();
    if (string.IsNullOrEmpty(text)) return;  // ← Silent exit
    ...
}
```
If the build's scene doesn't have the household TMP_InputField wired to `GameJoinScreen.householdInput`, the household is never applied. `JoinGame` then takes the `else` branch:
```csharp
if (!string.IsNullOrEmpty(householdId) && playerIndices.Length > 0)
    // RegisterAsTVWithHousehold  ← skipped
else
    await connection.InvokeAsync("RegisterAsTV", gameCode);  // ← no household
```

**b) Household text empty when user clicks join/row.**  
If the user clicks a game list row before typing household config, same result.

**c) Reconnect loses household config.**  
```csharp
connection.Reconnected += async id =>
{
    if (!string.IsNullOrEmpty(joinedGameCode))
    {
        await connection.InvokeAsync("RegisterAsTV", joinedGameCode);  // ← Always plain!
        ...
    }
};
```
On any reconnect (WiFi blip, sleep, SignalR transport switch), the laptop re-registers as a plain TV without household. From that point on, `GetTVForPlayer(2)` returns Z440.

### 3. No logging on registration state

There's no server-side log entry showing what `playerIndices` a TV registered with. Can't diagnose from `/admin/app-log` whether the laptop registered with or without household.

## Fix

### A. Server: `GetTVForPlayer` — remove dangerous fallback

```csharp
public string? GetTVForPlayer(int playerIndex)
{
    if (_registeredTVs.Count == 0) return null;
    if (_registeredTVs.Count == 1) return _registeredTVs[0].ConnectionId;

    // Multi-TV: find the TV that owns this player
    var match = _registeredTVs.FirstOrDefault(t => t.PlayerIndices?.Contains(playerIndex) == true);
    if (match != null) return match.ConnectionId;

    // No explicit owner — check if any TV has no playerIndices (legacy/unassigned)
    var unassigned = _registeredTVs.FirstOrDefault(t => t.PlayerIndices == null || t.PlayerIndices.Length == 0);
    if (unassigned != null) return unassigned.ConnectionId;

    // Truly no match — return null (caller should fall back to server-side roll)
    return null;
}
```

This way, if the laptop fails to register with household info, the fallback finds the "unassigned" TV (laptop with no playerIndices) rather than always picking the first one. If ALL TVs have playerIndices but none match, return null → server falls back to random dice (safe degradation).

### B. Server: Log TV registration details

In `RegisterAsTV`, add logging:

```csharp
public void RegisterAsTV(string connectionId, string? householdId = null, int[]? playerIndices = null)
{
    _registeredTVs.RemoveAll(t => t.ConnectionId == connectionId);
    _registeredTVs.Add(new TVRegistration(connectionId, householdId, playerIndices));
    // Log via ILogger passed through or static logger
    Console.WriteLine($"[TV] Registered: conn={connectionId[..8]}... household={householdId ?? "(none)"} players=[{(playerIndices != null ? string.Join(",", playerIndices) : "all")}] total={_registeredTVs.Count}");
}
```

Also log in `GetTVForPlayer` when the fallback fires:
```csharp
Console.WriteLine($"[TV] GetTVForPlayer({playerIndex}): no explicit match. TVs registered: {string.Join("; ", _registeredTVs.Select(t => $"{t.HouseholdId}=[{string.Join(",", t.PlayerIndices ?? Array.Empty<int>())}]"))}");
```

### C. Unity: Fix reconnect to re-register with household

```csharp
connection.Reconnected += async id =>
{
    if (!string.IsNullOrEmpty(joinedGameCode))
    {
        try
        {
            if (!string.IsNullOrEmpty(householdId) && playerIndices.Length > 0)
                await connection.InvokeAsync("RegisterAsTVWithHousehold", joinedGameCode, householdId, playerIndices);
            else
                await connection.InvokeAsync("RegisterAsTV", joinedGameCode);
            await connection.InvokeAsync("GetState");
        }
        catch { ... }
    }
};
```

### D. Unity: Warn if household not configured in multi-TV scenario

In `JoinGame`, after registration, log a warning if household is empty:

```csharp
public async Task JoinGame(string gameCode)
{
    ...
    if (string.IsNullOrEmpty(householdId) || playerIndices.Length == 0)
        Debug.LogWarning("[SignalR] Joined WITHOUT household config — dice routing will default to first TV. Set householdId + playerIndices for multi-TV.");
    ...
}
```

### E. (Optional) Server: Admin endpoint to show TV registrations

Add `/admin/tvs?gameCode=X` that returns the current `_registeredTVs` list for a game. Useful for debugging during testing:

```
GET /admin/tvs?gameCode=1234
[
  { "connectionId": "abc...", "householdId": "england", "playerIndices": [0, 1] },
  { "connectionId": "def...", "householdId": "scotland", "playerIndices": [2] }
]
```

## Files Changed

| File | Change |
|------|--------|
| `server/Risk.Server/Services/GameService.cs` | Fix `GetTVForPlayer` fallback, add logging to `RegisterAsTV` |
| `D:\Unity Projects\RiskDigitalBoard\Assets\Scripts\SignalRClient.cs` | Fix reconnect handler, add warning log |
| `server/Risk.Server/EndPointConfig/ManagementEndpoints.cs` | (Optional) Add `/admin/tvs` endpoint |

## How to Verify

After fix:
1. Start game with Z440 (`england 0,1`) and laptop (`scotland 2`)
2. Player 0 attacks Bot 2
3. **Z440:** rolls attacker dice (red) only, then receives `DefenderDiceResult` → places blue statically
4. **Laptop:** receives `SpawnDice("defender")` → rolls defender dice (blue) live, submits result
5. Combat resolves correctly

Also verify:
- Same-household combat (player 0 attacks bot 1) → Z440 rolls both (correct, both in england)
- After laptop WiFi reconnect → still routes correctly (reconnect fix)
- `/admin/app-log` shows registration entries with household/playerIndices

## Quick Diagnostic (Before Fix)

To confirm the diagnosis now, check `/admin/app-log` after both TVs join. You should see:
```
RegisterAsTV: success, code=1234, household=england    ← Z440
RegisterAsTV: success, code=1234, household=(none)     ← Laptop (BUG!)
```

If laptop shows `household=(none)`, the `householdInput` field isn't wired in the laptop's build scene, or the text was empty when join was clicked.

---

*Created: 2026-07-07*
