# Session Notes — 2026-06-28

## What Was Built

| Feature | Status |
|---------|--------|
| Rectangular dice arena (12×7 box lid) | ✅ Working |
| Low-angle perspective camera | ✅ Working |
| Directional dice throw (+Z along box) | ✅ Working |
| Layer isolation (DiceArena) | ✅ Working |
| Catmull-Rom camera flypath | ✅ Working |
| Randomised fly (jitter, speed, reverse) | ✅ Working |
| Result position (circle around overhead) | ✅ Working |
| FBX dice model imported | ✅ Working |
| TV-driven dice physics (server delegates) | ✅ Working (face mapping TBD) |
| Wood material on arena | ✅ Working |

## Key Gotchas Discovered

### Inspector values override code defaults
- Once a field is serialized in the scene, changing the default in code does nothing
- Must change in Inspector (not play mode) for it to persist
- This caused the serverUrl staying on production despite code change

### FBX import issues
- Mesh is nested as child of root container
- Need `GetComponentsInChildren<Renderer>()` not `GetComponent<Renderer>()`
- Origin/pivot often offset from mesh centre
- Scale often wrong (too small) — adjust on prefab
- May come with unwanted cameras/lights — uncheck Import Cameras/Lights

### Layer assignment
- DiceArena layer needed on runtime-spawned dice (set in code)
- DiceCamera culling mask must exclude everything except DiceArena
- Main camera culling mask must exclude DiceArena
- Token prefab must stay on Default layer

### Catmull-Rom with 4 points
- Only interpolates between points 1 and 2 — points 0/3 are tangent influences
- Need 6 waypoint slots (duplicate first/last) to traverse all 4 positions
- Same behaviour as Three.js but Three hides it internally

### SendAsync vs InvokeAsync (SignalR)
- `InvokeAsync` expects server to return a result — failed with "Method does not exist" for void methods
- `SendAsync` is fire-and-forget — works for void hub methods

### DiceFaceReader axis mapping
- Rotation 90,0,0 around X-axis pushes +Z (forward) up, not -Z
- Must think carefully about which local axis points world-up at each rotation
- Current mapping: up=1, down=6, right=4, left=3, fwd=5, back=2
- Still needs runtime verification — visual vs reported values not yet confirmed matching

## Architecture Change: TV-Driven Dice

Server now delegates single-attack dice rolls to Unity when connected:
1. Player attacks → Server sends CombatRollRequest (dice counts only)
2. Unity spawns dice → physics simulate → reads faces naturally
3. Unity sends SubmitDiceResult back to server
4. Server resolves combat with those values → broadcasts CombatResult

Blitz stays server-side. No Unity connected = existing flow unchanged.

## Files Modified

### Server (D:\Development\RiskDigital\server\Risk.Server\)
- Models/CombatResult.cs — added CombatRollRequest record
- Services/GameService.cs — RegisterAsTV, UnregisterTV, CreateDiceRequest, SubmitDiceResult, ResolveCombat
- Hubs/GameHub.cs — Attack() branches, RegisterAsTV + SubmitDiceResult hub methods

### Unity (D:\Unity Projects\RiskDigitalBoard\Assets\Scripts\)
- SignalRClient.cs — RegisterAsTV, CombatRollRequest event, SendDiceResult
- DiceRoller.cs — rewritten: RollAndRead (no face correction)
- CombatTheatre.cs — rewritten: handles CombatRollRequest flow
- DiceFaceReader.cs — axis mapping updated (still being tuned)
- CameraFlypath.cs — new: Catmull-Rom spline with randomisation + result transition

## Tomorrow: Pick Up From
1. Verify DiceFaceReader mapping is correct (test single die rolls)
2. Investigate bot dice rolls not showing
3. Remove debug logging once confirmed
4. Polish: dice panel frame, arena lighting
