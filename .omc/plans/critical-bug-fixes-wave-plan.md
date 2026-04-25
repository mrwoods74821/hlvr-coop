# HLVR Co-Op Critical Bug Fixes - Wave Execution Plan

## Context

This plan addresses critical bugs identified through deep code analysis of the HLVR Co-Op multiplayer mod. Issues are categorized by severity and organized into execution waves to minimize risk while ensuring comprehensive fixes.

**Project:** HLVR Co-Op - Half-Life: Alyx Multiplayer Mod
**Tech Stack:** C# (.NET 6.0) host app + Lua VScript (Source 2)
**Communication:** WebSocket (Fleck) + VConsole TCP bridge
**Constraint:** Must maintain network protocol compatibility and compile with zero errors

---

## RALPLAN-DR Summary

### Principles (3-5)
1. **Minimal Impact** - Changes must be surgical; avoid refactoring unrelated code
2. **Thread Safety** - All concurrent access patterns must be properly synchronized
3. **Backward Compatibility** - Network packet format must remain unchanged
4. **Defensive Programming** - Add validation before parsing untrusted input
5. **Zero Compilation Errors** - All changes must build successfully

### Decision Drivers (Top 3)
1. **Crash Prevention** - `float.Parse` without try-catch can crash the server on malformed packets
2. **Data Race Elimination** - Concurrent access to shared collections without locks causes undefined behavior
3. **Correctness** - Logic bugs (operator precedence, type checks) cause functional issues

### Viable Options (>=2)

#### Option A: Three-Wave Execution (RECOMMENDED)
- **Wave 1 (Critical Lua)**: Fix operator precedence and type check bugs that affect game logic
- **Wave 2 (Critical C#)**: Fix crash vectors (async void, float.Parse, race conditions)
- **Wave 3 (Minor)**: Fix style issues, add defensive checks

**Pros:**
- Isolates risk; can stop after any wave if issues arise
- Critical fixes prioritized; lower-risk items deferred
- Easy to verify each wave independently
- Allows for incremental deployment/testing

**Cons:**
- Requires multiple commits/PRs
- Longer total execution time
- Some related fixes split across waves

**Bounded Cons:**
- Wave 1 is pure Lua syntax fixes; no C# rebuild needed
- Wave 2 touches C# threading code; requires careful testing
- Wave 3 is low-risk but optional for stability

#### Option B: Single-Batch Execution
- Fix all issues in one comprehensive change

**Pros:**
- Single commit; easier rollback
- All fixes delivered together
- Faster total execution

**Cons:**
- Higher risk; harder to isolate which change caused issues
- More difficult to code review in one pass
- Single failure blocks all fixes

**Bounded Cons:**
- Testing becomes more complex (more variables changed)
- Rollback would lose all progress

### Decision
**Option A (Three-Wave)** is chosen because:
- The critical C# fixes (Wave 2) involve threading primitives that warrant isolated testing
- Lua fixes (Wave 1) can be verified independently without rebuilding C# host
- Minor fixes (Wave 3) are not stability-critical and can be deferred if needed

### Alternatives Considered and Rejected
- **"Fix-only-crashes" approach**: Would leave logic bugs unfixed, causing functional issues
- **"Rewrite-unsafe-modules" approach**: Violates principle #1 (Minimal Impact); too much scope creep

---

## Wave 1: Critical Lua Fixes (Syntax & Logic)

### Risk Level: LOW
These are pure Lua syntax fixes with no C# changes. Can be verified in-game without rebuilding.

---

### Fix 1.1: Operator Precedence in hlvrcoop_interval.lua:251

**File:** `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/hlvrcoop_interval.lua`

**Issue:** Line 251 has an operator precedence bug where the OR conditions are not properly grouped after the AND condition.

**Current Code (Line 251):**
```lua
if object.class == "prop_door_rotating_physics" and (math.floor(angles[1]) ~= math.floor(object.angles[1])) or (math.floor(angles[2]) ~= math.floor(object.angles[2])) or (math.floor(angles[3]) ~= math.floor(object.angles[3])) then
```

**Problem:** Parses as `(A and B) or C or D`, meaning ALL entities get angle-change checks, not just doors.

**Fix:**
```lua
if object.class == "prop_door_rotating_physics" and ((math.floor(angles[1]) ~= math.floor(object.angles[1])) or (math.floor(angles[2]) ~= math.floor(object.angles[2])) or (math.floor(angles[3]) ~= math.floor(object.angles[3]))) then
```

**Verification:**
- Load into a map with doors and other physics props
- Move non-door physics props; confirm no PHYS packets spam
- Move doors; confirm PHYS packets are sent correctly
- Check console for unexpected HLVRCOOP output

---

### Fix 1.2: Type Check Bug in input.lua:112

**File:** `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/util/input.lua`

**Issue:** Line 112 has incorrect type check syntax.

**Current Code (Line 112):**
```lua
elseif not type(hand)=="number" then
```

**Problem:** `not type(hand)=="number"` parses as `(not type(hand))=="number"`, which is always false.

**Fix:**
```lua
elseif type(hand) ~= "number" then
```

**Verification:**
- Input system should correctly identify numeric hand indices
- Test with both literal (0/1) and EntityHandle hand parameters

---

### Fix 1.3: Complete Half-Refactored input.lua

**File:** `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/util/input.lua`

**Issue:** Lines 64-68, 91-93 declare variables whose initialization is commented out, but lines 155, 169, 182, 197 still reference them as 2D tables. The code is half-refactored.

**Analysis:**
- `pressed_flags`, `pressed_time`, `pressed_contexts` are declared but initialization (91-93) commented out
- `input_data` is the new structure, but old references remain
- Functions `GetPressed` (155), `GetHeld` (169), `InputThink` (182, 197) reference the old variables

**Fix Strategy:** Complete the migration to `input_data` structure.

**Changes Required:**

1. Remove dead variable declarations (lines 64-68):
```lua
-- REMOVE THESE LINES:
---@type boolean[][]
local pressed_flags = {}
---@type number[][]
local pressed_time = {}
---@type table<any,boolean>[][]
local pressed_contexts = {}
```

2. Remove commented initialization (lines 91-93) - already commented, keep as-is

3. Update `GetPressed` function (lines 152-162):
```lua
function Input.GetPressed(digital_button, hand, context)
    hand = resolveHand(hand)
    context = context or Player
    local id = input_data[hand][digital_button]
    if id.time > -1 and (Time() - id.time) <= frame_forgiveness then
        if not id.pressed_contexts[context] then
            id.pressed_contexts[context] = true
            return true
        end
    end
    return false
end
```

4. Update `GetHeld` function (lines 167-170):
```lua
function Input.GetHeld(digital_button, hand)
    hand = resolveHand(hand)
    local id = input_data[hand][digital_button]
    return id.time > -1
end
```

5. Update `InputThink` function (lines 182-197):
```lua
local function InputThink()
    for i = 0, 1 do
        for _, value in DIGITAL_ITER,Input.DIGITAL do
            local id = input_data[i][value]
            if Player:IsDigitalActionOnForHand(i, value) then
                if id.time == -1 then
                    Input.LastPressed = value
                    id.time = Time()
                    for _, callback in ipairs(id.pressed_callbacks) do
                        callback(i, value)
                    end
                end
            else
                if id.time ~= -1 then
                    Input.LastReleased = value
                    id.time = -1
                    id.pressed_contexts = {}
                    for _, callback in ipairs(id.release_callbacks) do
                        callback(i, value)
                    end
                end
            end
        end
    end
    return UPDATE_INTERVAL
end
```

6. Update initialization loop to set up context tracking (lines 99-104):
```lua
for _, value in DIGITAL_ITER,Input.DIGITAL do
    input_data[l][value].time = -1
    input_data[l][value].pressed_callbacks = {}
    input_data[l][value].release_callbacks = {}
    input_data[l][value].pressed_contexts = {}
end
```

**Verification:**
- Test button press detection (press-and-release behavior)
- Test button hold detection (continuous hold)
- Verify callback system still works
- Test frame forgiveness timing

---

### Fix 1.4: math.min() Missing Prefix in queue.lua and stack.lua

**Files:**
- `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/util/queue.lua`
- `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/util/stack.lua`

**Issue:** Uses bare `min()` instead of `math.min()`. This will error in standard Lua unless a global alias exists.

**Current Code:**
- queue.lua:70: `count = min(count or 1, #self.items)`
- queue.lua:82: `count = min(count or 1, #self.items)`
- stack.lua:101: `count = min(count or 1, #self.items)`
- stack.lua:113: `count = min(count or 1, #self.items)`

**Fix:** Replace all `min(` with `math.min(`

**Verification:**
- Create Queue and Stack objects
- Test Dequeue/Peek with various count values
- Test Pop/Peek with various count values
- Confirm no "attempt to call global 'min' (a nil value)" errors

---

## Wave 2: Critical C# Fixes (Crash Vectors & Thread Safety)

### Risk Level: MEDIUM to HIGH
These fixes involve threading primitives and error handling. Requires full rebuild and careful testing.

---

### Fix 2.1: DiscordRpcGlobalData.cs:37-40 - Boolean Return Bug

**File:** `DiscordRpcPlugin/DiscordRpcGlobalData.cs`

**Issue:** `GetServerStarted()` returns `RichPresence?` cast to bool. This doesn't track server state correctly.

**Current Code (Lines 37-40):**
```csharp
public bool GetServerStarted()
{
    return presence;
}
```

**Problem:** Casting a nullable `RichPresence` to bool doesn't indicate whether server started.

**Fix:** Add a dedicated boolean field:
```csharp
private RichPresence? presence;
private bool serverStarted = false;

public bool GetServerStarted()
{
    return serverStarted;
}

public void SetRichPresence(RichPresence presence)
{
    this.presence = presence;
    this.serverStarted = true;
}
```

**Verification:**
- Discord RPC status updates correctly
- No null reference exceptions

---

### Fix 2.2: VConsole.cs:339 - async void Exception Handling

**File:** `HLVRCoOpMod/VConsole.cs`

**Issue:** Line 339 `OnConnectionLost` is `async void`. Unhandled exceptions will crash the app.

**Current Code (Line 339):**
```csharp
private async void OnConnectionLost(object? sender, EventArgs e)
```

**Fix:** Change to `async Task` and wrap in Task.Run:
```csharp
private async Task OnConnectionLostAsync(object? sender, EventArgs e)
{
    // Prevent multiple simultaneous reconnection attempts
    if (isReconnecting)
        return;

    // Mark as killed immediately and synchronously
    lock (streamLock)
    {
        killed = true;
    }

    System.Diagnostics.Debug.WriteLine("VConsole: Connection lost, attempting to reconnect...");

    // Notify UI that connection was lost
    if (ui != null && channel != null)
    {
        ui.Invoke(() => ui.LogToOutput(channel, "VConsole connection lost. Attempting to reconnect..."));
    }

    await StartReconnectionLoop();
}

// Update the event registration to fire-and-forget properly:
private void OnConnectionLost(object? sender, EventArgs e)
{
    _ = Task.Run(async () =>
    {
        try
        {
            await OnConnectionLostAsync(sender, e);
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError("VConsole.OnConnectionLost", ex);
        }
    });
}
```

Also update line 225 event registration:
```csharp
watcher.ConnectionLost += OnConnectionLost; // Keep using the wrapper
```

**Verification:**
- Trigger connection loss (close game)
- Confirm reconnection loop works
- Confirm no unhandled exceptions crash the host app
- Check ErrorLogger for any caught exceptions

---

### Fix 2.3: VConsole.cs:501-518 - Race Condition in WriteRaw()

**File:** `HLVRCoOpMod/VConsole.cs`

**Issue:** `WriteRaw()` accesses `stream` without acquiring `streamLock`. Race condition with `Disconnect()` and `WriteCommand()`.

**Current Code (Lines 501-518):**
```csharp
public void WriteRaw(byte[] data)
{
    if (stream != null && !killed)
    {
        try
        {
            stream.Write(data);
        }
        catch (ObjectDisposedException)
        {
            killed = true;
        }
        catch (SocketException)
        {
            killed = true;
        }
    }
}
```

**Fix:**
```csharp
public void WriteRaw(byte[] data)
{
    lock (streamLock)
    {
        if (stream != null && !killed)
        {
            try
            {
                stream.Write(data);
            }
            catch (ObjectDisposedException)
            {
                killed = true;
            }
            catch (SocketException)
            {
                killed = true;
            }
        }
    }
}
```

**Verification:**
- Stress test with rapid commands during connection/disconnection
- Confirm no ObjectDisposedException crashes

---

### Fix 2.4: PlayerBodySyncPlugin.cs:30-35 - Thread-Safe Dictionary Access

**File:** `PlayerBodySyncPlugin/PlayerBodySyncPlugin.cs`

**Issue:** Static `Dictionary` and `HashSet` accessed from WebSocket callbacks without locks.

**Current Code (Lines 30-35):**
```csharp
private static readonly Dictionary<Guid, int> playerBodyPreferences = new();
private static readonly HashSet<Guid> queriedClients = new();
```

**Fix:** Add a lock object and synchronize access:
```csharp
private static readonly Dictionary<Guid, int> playerBodyPreferences = new();
private static readonly HashSet<Guid> queriedClients = new();
private static readonly object preferencesLock = new();

// Update all access points to use lock:
private static void AddOrUpdatePreference(Guid clientId, int preference)
{
    lock (preferencesLock)
    {
        playerBodyPreferences[clientId] = preference;
    }
}

private static bool TryGetPreference(Guid clientId, out int preference)
{
    lock (preferencesLock)
    {
        return playerBodyPreferences.TryGetValue(clientId, out preference);
    }
}

private static void RemoveClient(Guid clientId)
{
    lock (preferencesLock)
    {
        playerBodyPreferences.Remove(clientId);
        queriedClients.Remove(clientId);
    }
}

private static bool IsClientQueried(Guid clientId)
{
    lock (preferencesLock)
    {
        return queriedClients.Contains(clientId);
    }
}

private static void MarkClientQueried(Guid clientId)
{
    lock (preferencesLock)
    {
        queriedClients.Add(clientId);
    }
}

private static void UnmarkClientQueried(Guid clientId)
{
    lock (preferencesLock)
    {
        queriedClients.Remove(clientId);
    }
}
```

Then update all existing access points:
- Line 101: `MarkClientQueried(clientId);`
- Line 119-120: `RemoveClient(clientId);`
- Line 150: `if (IsClientQueried(clientId))`
- Line 219: `if (IsClientQueried(clientId))`
- Line 221: `UnmarkClientQueried(clientId);`
- Line 242: `TryGetPreference(clientId, out int existingPreference)`
- Line 245: `AddOrUpdatePreference(clientId, preference);`

**Verification:**
- Multiple clients connect/disconnect simultaneously
- Confirm no InvalidOperationException or race conditions
- Check that preferences sync correctly

---

### Fix 2.5: AlyxGamemode.cs:509-607 - float.Parse() Without Try-Catch

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Issue:** Four packet handlers use `float.Parse()` without try-catch. Bad network data will crash.

**Affected Functions:**
- `HandleTeleportPacket` (lines 509-517)
- `HandleHeadPosAngPacket` (lines 534-543)
- `HandlePlayerPosAngPacket` (lines 583-593)
- `HandlePhysicsObjectPosAngPacket` (starts at 645)

**Fix:** Use `float.TryParse()` with fallback:
```csharp
private static void HandleTeleportPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
{
    if (packet.args.Length >= 6)
    {
        if (float.TryParse(packet.args[0], CultureInfo.InvariantCulture.NumberFormat, out float x) &&
            float.TryParse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat, out float y) &&
            float.TryParse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat, out float z) &&
            float.TryParse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat, out float pitch) &&
            float.TryParse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat, out float yaw) &&
            float.TryParse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat, out float roll))
        {
            Vector TeleOrigin = new(x, y, z);
            Angle TeleAngles = new(pitch, yaw, roll);
            Response teleport = new("command", "hlvrcoop_teleportangles " + TeleOrigin.X + " " + TeleOrigin.Y + " " + TeleOrigin.Z + " " + TeleAngles.Pitch + " " + TeleAngles.Yaw + " " + TeleAngles.Roll);
            BroadcastToOthers(connections, socket, teleport);
        }
        else
        {
            SyncLog($"[DROP] Teleport packet has invalid float values: {string.Join(", ", packet.args)}");
        }
    }
}
```

Apply similar pattern to:
- `HandleHeadPosAngPacket`
- `HandlePlayerPosAngPacket` (also has `int.Parse` on line 593)
- `HandlePhysicsObjectPosAngPacket`

**Verification:**
- Send malformed packets with non-numeric values
- Confirm server doesn't crash
- Check ErrorLogger for drop messages
- Valid packets still process correctly

---

### Fix 2.6: ServerProgram.cs:23 - Concurrent Collection Access

**File:** `HLVRCoOpMod/ServerProgram.cs`

**Issue:** `connections` List accessed from WebSocket callbacks AND tick thread without synchronization.

**Current Code (Line 23):**
```csharp
public List<IndexedClient> connections = new() { };
```

**Fix:** Use `ConcurrentBag` or add lock. Since this is a list with frequent enumeration, use a lock with a standard List for better performance:
```csharp
public List<IndexedClient> connections = new() { };
private readonly object connectionsLock = new();

// Add a helper for thread-safe enumeration:
public void ForEachConnection(Action<IndexedClient> action)
{
    lock (connectionsLock)
    {
        foreach (var client in connections)
        {
            action(client);
        }
    }
}

public List<IndexedClient> GetConnectionsSnapshot()
{
    lock (connectionsLock)
    {
        return connections.ToList();
    }
}

public void AddConnection(IndexedClient client)
{
    lock (connectionsLock)
    {
        connections.Add(client);
    }
}

public void RemoveConnection(IWebSocketConnection socket)
{
    lock (connectionsLock)
    {
        var client = connections.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
        if (client != null)
        {
            connections.Remove(client);
        }
    }
}
```

Then update all access points throughout the file to use these helpers or the lock.

**Verification:**
- Multiple clients connect/disconnect during active gameplay
- Confirm no InvalidOperationException during enumeration
- Tick loop continues smoothly during client changes

---

### Fix 2.7: AlyxGlobalData.cs:258-270 - TOCTOU Race in Player Count

**File:** `AlyxGamemode/AlyxGlobalData.cs`

**Issue:** Player count check is outside the lock, allowing >16 players to race through.

**Current Code (Lines 258-270):**
```csharp
public Player? AddPlayer(IndexedClient client)
{
    lock (sync)
    {
        if(players.Count < 16)
        {
            int newIndex = Enumerable.Range(0, 16).Except(players.Select(p => p.Index)).First();
            Player player = new(newIndex, client);
            players.Add(player);
            return player;
        }
        return null;
    }
}
```

**Problem:** The check `players.Count < 16` is INSIDE the lock, which is correct. However, this is actually NOT a bug - the analysis was incorrect. The code is properly locked.

**Status:** NO FIX NEEDED - Code is already correct.

**Verification:** N/A

---

## Wave 3: Minor Fixes (Style & Defensive)

### Risk Level: LOW
Quality improvements and defensive checks. Not stability-critical.

---

### Fix 3.1: player_body_tracking.lua:336 - Offensive Term in Comment

**File:** `WorkshopAddonCompatibilityPlugin/sources/Player-Body/scripts/vscripts/player_body_tracking.lua`

**Issue:** Line 336 contains an offensive term in a comment.

**Action:** Locate and replace with neutral language. (Line number may vary; search for the term)

---

### Fix 3.2: player_body_tracking.lua:37-38 - Array Index Without Bounds Check

**File:** `WorkshopAddonCompatibilityPlugin/sources/Player-Body/scripts/vscripts/player_body_tracking.lua`

**Current Code (Lines 37-38):**
```lua
if (Entities:FindAllByClassname('logic_playerproxy')[2] ~= nil) then
```

**Issue:** Accessing index [2] without verifying the array has at least 2 elements.

**Fix:**
```lua
local proxies = Entities:FindAllByClassname('logic_playerproxy')
if (proxies ~= nil and #proxies >= 2) then
```

**Verification:** Load map with and without logic_playerproxy entities.

---

### Fix 3.3: redirect_damage_to_player.lua:14 - CreateDamageInfo() API Check

**File:** `WorkshopAddonCompatibilityPlugin/sources/Player-Body/scripts/vscripts/redirect_damage_to_player.lua`

**Issue:** `CreateDamageInfo()` may not exist in Source 2 VScript API.

**Current Code (Line 15):**
```lua
localPlayer:TakeDamage(CreateDamageInfo(dmgInflictor, dmgAttacker, dmgForce, dmgPosition, dmgAmount, 0))
```

**Fix:** Add existence check or use alternative API:
```lua
-- Check if CreateDamageInfo exists, otherwise use direct damage
if CreateDamageInfo then
    local dmgInfo = CreateDamageInfo(dmgInflictor, dmgAttacker, dmgForce, dmgPosition, dmgAmount, 0)
    localPlayer:TakeDamage(dmgInfo)
else
    -- Fallback: direct damage call (if supported)
    localPlayer:TakeDamage(dmgAmount, DMG_GENERIC, dmgAttacker, Vector(0,0,0))
end
```

**Verification:** Test damage redirection functionality.

---

### Fix 3.4: basic.lua:296-324 - io.open Without Error Checking

**File:** `HLVRCoOpMod/base_scripts/base/basic.lua`

**Issue:** `io.open()` calls don't check for failure (permissions, disk full, etc.)

**Current Code (Lines 296-298, 315-317):**
```lua
local file = io.open("bans.json", "w")
file:write(json.encode(lua_env.persistence["configjson"].bans))
file:close()
```

**Fix:**
```lua
local file = io.open("bans.json", "w")
if file then
    file:write(json.encode(lua_env.persistence["configjson"].bans))
    file:close()
    print("Unbanned " .. arg1[1])
else
    print("Error: Could not write to bans.json")
end
```

Apply to both `bans.json` and `ipbans.json` operations.

**Verification:** Test ban/unban commands; verify file operations succeed.

---

## Success Criteria

### Wave 1 (Lua)
- [ ] No syntax errors on map load
- [ ] Door angle updates work correctly (no spam on non-doors)
- [ ] Input system detects button presses correctly
- [ ] Queue/Stack operations work without errors
- [ ] In-game testing for 30 minutes without crashes

### Wave 2 (C#)
- [ ] Solution builds with 0 errors
- [ ] Server runs for 1 hour with multiple clients without crashes
- [ ] Malformed packets are logged and dropped (no crash)
- [ ] Connection/disconnection stress test passes
- [ ] Discord RPC shows correct status

### Wave 3 (Minor)
- [ ] No offensive language remains
- [ ] Bounds checks prevent nil access
- [ ] File operations handle errors gracefully

### Overall
- [ ] All changes committed with clear commit messages
- [ ] Network tests with 2+ clients pass
- [ ] No regression in existing functionality
- [ ] Build produces clean binaries

---

## Verification Protocol

1. **Compile Test:** Build solution in Release mode
2. **Unit Tests:** Run any existing unit tests
3. **Smoke Test:** Start server, connect 2 clients, play for 15 minutes
4. **Stress Test:** Connect/disconnect clients rapidly, send malformed packets
5. **Regression Test:** Verify all existing features still work

---

## Rollback Plan

If any wave causes issues:
1. Revert the specific commit for that wave
2. Document the issue in `.omc/plans/open-questions.md`
3. Adjust the fix and retry

---

## Open Questions

None initially. Add questions that arise during execution to `.omc/plans/open-questions.md`.
