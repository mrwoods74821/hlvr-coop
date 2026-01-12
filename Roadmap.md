# HLVRCoop Development Roadmap

**Comprehensive, detailed roadmap for continuing development of Half-Life: Alyx Co-Op Mod**

*This document provides step-by-step guidance for implementing all incomplete features, fixing disabled functionality, and completing the project*

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Priority 1: Critical Functionality (Game-Breaking Issues)](#priority-1-critical-functionality-game-breaking-issues)
3. [Priority 2: Incomplete Core Features](#priority-2-incomplete-core-features)
4. [Priority 3: Quality of Life Improvements](#priority-3-quality-of-life-improvements)
5. [Priority 4: Code Quality & Technical Debt](#priority-4-code-quality--technical-debt)
6. [Priority 5: Future Enhancements & Addon Support](#priority-5-future-enhancements--addon-support)
7. [Implementation Guidelines](#implementation-guidelines)
8. [Testing Strategy](#testing-strategy)
9. [Dependencies & Prerequisites](#dependencies--prerequisites)

---

## ⚠️ IMPORTANT: Read This First

**This roadmap is based on analysis of the EXISTING codebase, not official Half-Life: Alyx documentation.**

**Before implementing anything:**
1. ✅ Check [Official Valve Developer Community documentation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)
2. ✅ Test incrementally - Small changes, verify, repeat
3. ✅ Ask the community - Discord, forums, GitHub issues

**What this roadmap is:**
- Analysis of incomplete code in the mod
- Suggestions based on existing patterns
- Guide to continue development

**What this roadmap is NOT:**
- Official Source 2 API documentation
- Guaranteed-to-work solutions
- Complete understanding of HL:A modding

**For verification and official resources, see:** [VERIFICATION_AND_RESOURCES.md](VERIFICATION_AND_RESOURCES.md)

---

## Executive Summary

### Project Status: Version 3.2.0

### Version Comparison

**Version 3.1.0 (Previous Release)**
- Core multiplayer synchronization
- PLYR packet handler (player body position tracking)
- Basic WebSocket client/server communication
- VConsole TCP communication
- Lua scripting system
- Error logging to Debug output only
- Version checking system (but version hardcoded to 0, non-functional)
- Manual reconnection required for both WebSocket and VConsole

**Version 3.2.0 (Current Release) - New Features**
- **VConsole Automatic Reconnection** - Automatic reconnection with exponential backoff (1s, 2s, 4s, 8s, 16s delays, max 5 attempts) when game connection is lost
- **WebSocket Client Automatic Reconnection** - Automatic reconnection enabled with re-authentication when server connection is lost
- **Centralized Error Handling System** - ErrorLogger class with UI error channel (ERR) for visible error reporting in production
- **Version System Implementation** - Version set to 1, version checking now functional to prevent compatibility issues
- **Server Disconnect Feature** - Players automatically disconnected and returned to main menu when server stops

**Key Improvements in 3.2.0**
- Improved reliability: No more manual reconnection required for connection losses
- Better debugging: Errors now visible in UI instead of Debug output only
- Better user experience: Automatic recovery from connection issues
- Version compatibility checking enabled

### Completed Features ✅
- **VConsole Reconnection** (Task 1.2) - Automatic reconnection with exponential backoff when game connection is lost
- **Error Handling System** (Task 1.4) - Centralized ErrorLogger with UI error channel for debugging
- **WebSocket Client Reconnection** (Task 3.1) - Automatic reconnection with re-authentication when server connection is lost
- **Version System** (Task 3.2) - Version checking implemented (version 1) to prevent compatibility issues
- **PLYR Packet Handler** - Player body position tracking implemented
- **Server Disconnect Feature** - Players automatically disconnected and returned to main menu when server stops

### Current State
- **Functional:** Core multiplayer synchronization works, all reconnection systems implemented, comprehensive error handling, version checking enabled
- **Broken:** Automatic level transitions desynchronize players, audio system breaks during level transitions, sound file path format issues
- **Incomplete:** 13 packet types defined but not implemented (14 originally, PLYR is implemented)
- **Disabled:** Automatic map switching (code exists but commented out)
- **Missing:** Dedicated server mode

### Planned Work (Priority Order)

**Priority 1: Critical Functionality (Game-Breaking Issues)**
1. **Fix Automatic Map Switching** (CRITICAL) - Enable commented code to synchronize all players during level transitions
2. **Fix Audio System During Level Transitions** (CRITICAL) - Fix intermittent audio issues when transitioning between levels
3. **Fix Command Buffer Full Handling** (MEDIUM) - Implement command queue for VConsole when buffer fills

**Priority 2: Incomplete Core Features**
4. **Fix Sound File Path Format** (MINOR) - Cosmetic issue with jingle sound file path
5. **Investigate and Implement Unimplemented Packet Types** (MEDIUM) - Implement remaining 13 packet types
6. **Implement Dedicated Server Mode** (MEDIUM) - Headless server without UI dependency

**Priority 3: Quality of Life Improvements**
7. **Complete Lua Documentation** (LOW) - Create comprehensive API documentation for Lua scripting

**Priority 4: Code Quality & Technical Debt**
8. **Remove Workarounds** (LOW) - Improve chat command system
9. **Code Cleanup** (LOW) - Remove unused code, improve organization

**Priority 5: Future Enhancements**
10. **Steam Workshop Addon Compatibility** (FUTURE) - Enhanced addon support

### Critical Path
1. **Fix automatic map switching** - Breaks cooperative gameplay
2. **Fix audio system during level transitions** - Breaks immersion and gameplay
3. ~~**Implement VConsole reconnection**~~ ✅ COMPLETED - Prevents connection loss issues
4. ~~**Fix error handling**~~ ✅ COMPLETED - Makes debugging possible
5. **Complete packet handlers** - Enables full feature set

---

## Priority 1: Critical Functionality (Game-Breaking Issues)

### Task 1.1: Fix Automatic Map Switching (CRITICAL)

**Status:** Code exists but commented out  
**Priority:** CRITICAL - Breaks cooperative gameplay  
**Risk Level:** Medium  
**Dependencies:** None

#### Problem Statement

When Half-Life: Alyx automatically transitions to the next level (e.g., when reaching the end of a chapter), only the transitioning player moves to the new map. Other players remain on the old map, causing complete desynchronization.

**Current Flow:**
1. Player A reaches end of level → Game auto-transitions to next map
2. Lua script detects map change → Sends `MAPN` packet
3. Server receives `MAPN` packet → Updates internal map tracking
4. **Other players are NOT told to switch maps** ← PROBLEM
5. Player A is on new map, Players B/C/D are on old map → Desynchronized

#### Technical Details

**Location:** `AlyxGamemode/AlyxGamemode.cs` lines 285-316

**Current Code (lines 289-303 are commented):**
```csharp
case PacketType.MapName:
    if (packet.args.Length >= 2)
    {
        string suffix = "";
        /*
        if (packet.args[0] != player.Client.Map)
        {
            Response mapOutput = new("command", "addon_play " + packet.args[0] + "; addon_tools_map " + packet.args[0]);
            foreach (IndexedClient broadcast in connections)
            {
                Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                {
                    broadcast.Session.Send(JsonConvert.SerializeObject(mapOutput));
                }
            }
            suffix = " - Telling all clients to switch...";
        }
        */
        // ... rest of code
    }
```

**What the commented code does:**
- Checks if the reported map differs from the client's stored map
- If different, broadcasts `addon_play` and `addon_tools_map` commands to all other clients
- This would synchronize all clients to the new map

#### Implementation Steps

**Step 1: Understand Why It Was Disabled**
- Check git history for commit messages
- Test if uncommenting causes issues
- Look for error logs or issues related to map switching
- **Action:** Search repository history, test in isolated environment

**Step 2: Uncomment and Test Basic Functionality**
```csharp
// In AlyxGamemode.cs, uncomment lines 289-303
if (packet.args[0] != player.Client.Map)
{
    Response mapOutput = new("command", "addon_play " + packet.args[0] + "; addon_tools_map " + packet.args[0]);
    foreach (IndexedClient broadcast in connections)
    {
        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
        {
            broadcast.Session.Send(JsonConvert.SerializeObject(mapOutput));
        }
    }
    suffix = " - Telling all clients to switch...";
}
```

**Step 3: Add Map Validation**
- Verify map exists before switching
- Check if map is available for all clients
- Handle missing map scenarios
- **Implementation:**
```csharp
// Add before broadcasting
bool mapExists = ValidateMapExists(packet.args[0]);
if (!mapExists)
{
    Response error = new("status", "Map " + packet.args[0] + " not found. Cannot switch maps.");
    socket.Send(JsonConvert.SerializeObject(error));
    return; // Don't broadcast
}
```

**Step 4: Add Error Handling**
- Handle map load failures
- Notify clients if map switch fails
- Implement retry logic if needed
- **Implementation:**
```csharp
// After broadcasting, track map switch state
// Add timeout to detect if map switch failed
// Notify clients if switch doesn't complete within timeout
```

**Step 5: Add Confirmation/Coordination**
- Consider if all players should confirm before switching
- Or switch immediately (current approach)
- Add server setting for behavior
- **Implementation:**
```csharp
// Option 1: Immediate switch (current)
// Option 2: Wait for all players to be ready
// Option 3: Server setting to choose behavior
```

**Step 6: Update Client Map Tracking**
- Ensure `player.Client.Map` is updated correctly
- Update `IndexedClient.Map` property
- Verify map tracking is accurate
- **Location:** `HLVRCoOpModCore/IndexedClient.cs`

**Step 7: Test Scenarios**
- Test with 2 players
- Test with 16 players (max)
- Test rapid map changes
- Test map that doesn't exist
- Test network interruption during map change
- Test client disconnection during map change

#### Success Criteria

- [ ] When one player triggers level transition, all players switch to new map
- [ ] Map switching works with 2-16 players
- [ ] Error handling prevents crashes on invalid maps
- [ ] Clients receive clear status messages about map changes
- [ ] No desynchronization after level transitions

#### Potential Issues & Solutions

**Issue:** Map load failures
- **Solution:** Validate map exists, handle errors gracefully

**Issue:** Timing - clients load at different speeds
- **Solution:** Add ready state tracking, wait for all clients

**Issue:** Map not available for all clients
- **Solution:** Check map availability, notify players

**Issue:** Infinite map switching loop
- **Solution:** Add debouncing, track last map change time

#### Testing Checklist

- [ ] Single player level transition works
- [ ] Two players: both transition together
- [ ] Multiple players: all transition together
- [ ] Invalid map name: error handling works
- [ ] Network interruption: recovery works
- [ ] Rapid transitions: no crashes
- [ ] Client disconnects during transition: no server crash

---

### Task 1.2: ~~Implement VConsole Reconnection~~ - COMPLETED

**Status:** ✅ IMPLEMENTED - Automatic reconnection with exponential backoff
**Priority:** HIGH - Connection loss requires manual intervention
**Risk Level:** Medium
**Dependencies:** None

#### Problem Statement

If the VConsole TCP connection is lost (game crash, network issue, etc.), there is no automatic reconnection. Users must manually restart the mod or reconnect, breaking the multiplayer session.

**Current Behavior:**
- VConsole connection lost → No automatic recovery
- User must close and reopen mod
- All players lose connection to that client

#### Technical Details

**Location:** `HLVRCoOpMod/VConsole.cs` lines 115-129

**Commented Code:**
```csharp
/*
public void Reconnect()
{
    killed = true;
    if (stream != null)
        stream.Close();
    if (client != null)
        client.Close();
    if (watcher != null)
        watcher.SetWorking(false);
    client = new TcpClient("127.0.0.1", Settings.Default.VconsolePort);
    stream = client.GetStream();
    Connect();
}
*/
```

**Current Connection Logic:**
- `Connect()` method establishes connection (lines 45-78)
- `StreamWatcher` monitors for messages (lines 65-111)
- On stream end, `Read()` loop exits (line 79, 95)
- No reconnection attempt is made

#### Implementation Steps

**Step 1: Restore Reconnect Method**
```csharp
public bool Reconnect()
{
    try
    {
        Disconnect(); // Clean up old connection
        
        // Wait a moment before reconnecting
        Thread.Sleep(1000);
        
        // Attempt to reconnect
        return Connect();
    }
    catch (Exception ex)
    {
        // Log error but don't throw
        System.Diagnostics.Debug.WriteLine($"VConsole reconnection failed: {ex.Message}");
        return false;
    }
}
```

**Step 2: Detect Connection Loss**
- Modify `StreamWatcher.Read()` to detect stream end
- Add event for connection loss
- Trigger reconnection attempt
- **Location:** `HLVRCoOpMod/StreamWatcher.cs` lines 78-80, 95-97

**Implementation:**
```csharp
// In StreamWatcher.cs
if (read == 0)
{
    // Stream ended - connection lost
    OnConnectionLost?.Invoke(this, EventArgs.Empty);
    break;
}
```

**Step 3: Add Reconnection Retry Logic**
- Implement exponential backoff
- Limit retry attempts
- Notify user of reconnection attempts
- **Implementation:**
```csharp
private int reconnectAttempts = 0;
private const int MAX_RECONNECT_ATTEMPTS = 5;
private const int INITIAL_RETRY_DELAY = 1000; // 1 second

public async Task<bool> ReconnectWithRetry()
{
    reconnectAttempts = 0;
    while (reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
    {
        int delay = INITIAL_RETRY_DELAY * (int)Math.Pow(2, reconnectAttempts);
        await Task.Delay(delay);
        
        if (Reconnect())
        {
            reconnectAttempts = 0;
            return true;
        }
        
        reconnectAttempts++;
    }
    
    // Failed after all attempts
    reconnectAttempts = 0;
    return false;
}
```

**Step 4: Add Connection State Tracking**
- Track connection state
- Expose connection status
- Update UI with connection status
- **Implementation:**
```csharp
public enum VConsoleConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed
}

private VConsoleConnectionState connectionState = VConsoleConnectionState.Disconnected;
public VConsoleConnectionState ConnectionState => connectionState;
public event EventHandler<VConsoleConnectionState>? ConnectionStateChanged;
```

**Step 5: Integrate with StreamWatcher**
- Add connection lost event to StreamWatcher
- Subscribe to event in VConsole
- Trigger reconnection on connection loss
- **Implementation:**
```csharp
// In VConsole.cs constructor or Connect()
watcher.ConnectionLost += async (sender, e) =>
{
    connectionState = VConsoleConnectionState.Reconnecting;
    ConnectionStateChanged?.Invoke(this, connectionState);
    
    bool success = await ReconnectWithRetry();
    if (success)
    {
        connectionState = VConsoleConnectionState.Connected;
    }
    else
    {
        connectionState = VConsoleConnectionState.Failed;
    }
    ConnectionStateChanged?.Invoke(this, connectionState);
};
```

**Step 6: Add UI Notifications**
- Show reconnection status in UI
- Notify user of connection loss
- Display reconnection progress
- **Location:** `HLVRCoOpMod/UserInterface.cs`

**Step 7: Handle Edge Cases**
- Game not running (port not listening)
- Game crashed
- Network issues
- Multiple rapid reconnection attempts

#### Success Criteria

- [ ] VConsole automatically reconnects after connection loss
- [ ] Reconnection attempts are limited (no infinite loops)
- [ ] User is notified of reconnection status
- [ ] Reconnection works after game restart
- [ ] No memory leaks from reconnection attempts

#### Testing Checklist

- [ ] Close game → Reconnection attempts made
- [ ] Restart game → Reconnection succeeds
- [ ] Network interruption → Reconnection works
- [ ] Multiple disconnections → Reconnection still works
- [ ] Game never starts → Reconnection stops after max attempts

---

### Task 1.3: Fix Command Buffer Full Handling

**Status:** Code commented out, messages filtered  
**Priority:** MEDIUM - May cause command loss  
**Risk Level:** Low  
**Dependencies:** Task 1.2 (VConsole Reconnection)

#### Problem Statement

When VConsole command buffer fills up, commands may be lost. The current implementation simply filters out "Command buffer full" messages, but doesn't handle the underlying issue.

**Current Behavior:**
- "Command buffer full" messages are filtered (line 90)
- No tracking of buffer state
- No recovery mechanism
- Commands may be lost silently

#### Technical Details

**Location:** `HLVRCoOpMod/VConsole.cs` lines 90-109

**Current Code:**
```csharp
if(newMessage != "" && !newMessage.Contains("Command buffer full"))
{
    // Messages with "Command buffer full" are simply ignored
    Response input = new("print", newMessage);
    ws.Send(JsonConvert.SerializeObject(input));
}
```

**Commented Code (what it was supposed to do):**
```csharp
/*
if (newMessage.Contains("Command buffer full"))
{
    vcAmount += 1;
    if (vcAmount >= 10)
    {
        //Reconnect();
        vcAmount = 0;
    }
}
*/
```

**Issues with commented code:**
- `vcAmount` variable doesn't exist
- Reconnection may cause issues
- No command queuing

#### Implementation Steps

**Step 1: Implement Command Queue**
- Queue commands when buffer is full
- Retry queued commands after buffer clears
- Limit queue size to prevent memory issues
- **Implementation:**
```csharp
private Queue<string> commandQueue = new Queue<string>();
private const int MAX_QUEUE_SIZE = 100;
private bool bufferFull = false;

public void WriteCommand(string command, bool urgent = false)
{
    if (bufferFull && !urgent)
    {
        // Queue command if buffer is full
        if (commandQueue.Count < MAX_QUEUE_SIZE)
        {
            commandQueue.Enqueue(command);
        }
        return;
    }
    
    // ... existing WriteCommand logic
}
```

**Step 2: Track Buffer State**
- Detect "Command buffer full" messages
- Set buffer state flag
- Clear flag when buffer is available
- **Implementation:**
```csharp
private int bufferFullCount = 0;
private const int BUFFER_FULL_THRESHOLD = 3;

if (newMessage.Contains("Command buffer full"))
{
    bufferFull = true;
    bufferFullCount++;
    
    // If buffer full persists, may need reconnection
    if (bufferFullCount >= BUFFER_FULL_THRESHOLD)
    {
        // Consider reconnection or pause commands
    }
}
else
{
    // Buffer cleared
    if (bufferFull)
    {
        bufferFull = false;
        bufferFullCount = 0;
        ProcessCommandQueue(); // Send queued commands
    }
}
```

**Step 3: Process Command Queue**
- Send queued commands when buffer clears
- Rate limit to prevent buffer fill
- Handle queue overflow
- **Implementation:**
```csharp
private async void ProcessCommandQueue()
{
    while (commandQueue.Count > 0 && !bufferFull && stream != null)
    {
        string command = commandQueue.Dequeue();
        WriteCommand(command);
        
        // Rate limit to prevent buffer fill
        await Task.Delay(50); // 20 commands per second max
    }
}
```

**Step 4: Add Buffer Monitoring**
- Track command send rate
- Warn if sending too fast
- Automatically throttle if needed
- **Implementation:**
```csharp
private DateTime lastCommandTime = DateTime.MinValue;
private const int MIN_COMMAND_INTERVAL_MS = 50; // 20 commands/second max

public void WriteCommand(string command, bool urgent = false)
{
    // Rate limiting
    if (!urgent)
    {
        TimeSpan timeSinceLastCommand = DateTime.Now - lastCommandTime;
        if (timeSinceLastCommand.TotalMilliseconds < MIN_COMMAND_INTERVAL_MS)
        {
            // Queue if sending too fast
            commandQueue.Enqueue(command);
            return;
        }
    }
    
    lastCommandTime = DateTime.Now;
    // ... existing WriteCommand logic
}
```

**Step 5: Add Reconnection as Last Resort**
- Only reconnect if buffer full persists
- Use exponential backoff
- Notify user of issue
- **Implementation:**
```csharp
if (bufferFullCount >= 10)
{
    // Buffer persistently full - may need reconnection
    Response status = new("status", "VConsole buffer persistently full. Attempting reconnection...");
    ws?.Send(JsonConvert.SerializeObject(status));
    
    // Reconnect (from Task 1.2)
    await ReconnectWithRetry();
    bufferFullCount = 0;
}
```

#### Success Criteria

- [ ] Commands are queued when buffer is full
- [ ] Queued commands are sent when buffer clears
- [ ] No commands are lost
- [ ] Buffer state is tracked accurately
- [ ] Rate limiting prevents buffer overflow

#### Testing Checklist

- [ ] Send many commands rapidly → Commands queued
- [ ] Buffer fills → Commands queued
- [ ] Buffer clears → Queued commands sent
- [ ] Queue overflow → Handled gracefully
- [ ] Persistent buffer full → Reconnection triggered

---

### Task 1.4: ~~Implement Proper Error Handling~~ - COMPLETED

**Status:** ✅ IMPLEMENTED - Centralized ErrorLogger with UI error channel logging
**Priority:** HIGH - Makes debugging impossible
**Risk Level:** Low
**Dependencies:** None

#### Problem Statement

Errors are either silently caught or only logged to Debug output (not visible in release builds). This makes it impossible to diagnose issues in production.

**Current Issues:**
1. `LuaEnvironment.cs` line 147: `catch { } // Give up` - All exceptions swallowed
2. `AlyxGamemode.cs` lines 431-435: Only `Debug.WriteLine()` - Not visible in release
3. `StreamWatcher.cs` lines 104-108: Only `Debug.WriteLine()` - Not visible in release

#### Implementation Steps

**Step 1: Create Error Logging System**
- Create centralized error logging
- Log to UI error channel
- Log to file (optional)
- **Implementation:**
```csharp
// In HLVRCoOpModCore or new file
public static class ErrorLogger
{
    private static readonly Channel errorChannel = new("ERR", "Errors", Color.Red);
    
    public static void LogError(string source, Exception ex)
    {
        string message = $"[{source}] {ex.Message}\n{ex.StackTrace}";
        
        // Log to UI
        if (Program.userInterface != null)
        {
            Program.userInterface.Invoke(() =>
            {
                Program.userInterface.LogToOutput(errorChannel, message);
            });
        }
        
        // Log to Debug (for development)
        System.Diagnostics.Debug.WriteLine(message);
        
        // Optional: Log to file
        // LogToFile(message);
    }
    
    public static void LogError(string source, string message)
    {
        string fullMessage = $"[{source}] {message}";
        
        if (Program.userInterface != null)
        {
            Program.userInterface.Invoke(() =>
            {
                Program.userInterface.LogToOutput(errorChannel, fullMessage);
            });
        }
        
        System.Diagnostics.Debug.WriteLine(fullMessage);
    }
}
```

**Step 2: Fix LuaEnvironment Error Handling**
**Location:** `HLVRCoOpMod/LuaEnvironment.cs` line 147

**Current:**
```csharp
catch { } // Give up
```

**Fixed:**
```csharp
catch (Exception e)
{
    ErrorLogger.LogError("LuaEnvironment.Handle", e);
    // Don't rethrow - continue processing other handlers
}
```

**Step 3: Fix AlyxGamemode Error Handling**
**Location:** `AlyxGamemode/AlyxGamemode.cs` lines 431-435

**Current:**
```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"AlyxGamemode error: {ex.Message}");
    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
}
```

**Fixed:**
```csharp
catch (Exception ex)
{
    ErrorLogger.LogError("AlyxGamemode", ex);
    // Optionally: Set State = HandleState.Handled to prevent further processing
    State = HandleState.Handled;
}
```

**Step 4: Fix StreamWatcher Error Handling**
**Location:** `HLVRCoOpMod/StreamWatcher.cs` lines 104-108

**Current:**
```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"StreamWatcher error: {ex.Message}");
    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
}
```

**Fixed:**
```csharp
catch (Exception ex)
{
    ErrorLogger.LogError("StreamWatcher", ex);
    // Connection may be lost - trigger reconnection
    OnConnectionLost?.Invoke(this, EventArgs.Empty);
}
```

**Step 5: Add Error Recovery**
- Determine if errors are recoverable
- Attempt recovery when possible
- Graceful degradation
- **Implementation:**
```csharp
public enum ErrorSeverity
{
    Info,      // Log and continue
    Warning,   // Log, may affect functionality
    Error,     // Log, functionality impaired
    Critical   // Log, may need restart
}

public static void LogError(string source, Exception ex, ErrorSeverity severity = ErrorSeverity.Error)
{
    // ... logging code
    
    // Recovery based on severity
    switch (severity)
    {
        case ErrorSeverity.Critical:
            // May need to disconnect client or restart component
            break;
        case ErrorSeverity.Error:
            // Functionality impaired but can continue
            break;
        // ... other cases
    }
}
```

#### Success Criteria

- [ ] All errors are logged to UI
- [ ] Errors are visible in release builds
- [ ] Error source is identifiable
- [ ] Stack traces are available
- [ ] No silent failures

#### Testing Checklist

- [ ] Lua script error → Logged to UI
- [ ] Packet parsing error → Logged to UI
- [ ] Network error → Logged to UI
- [ ] Release build → Errors visible
- [ ] Multiple errors → All logged

---

### Task 1.5: Fix Audio System During Level Transitions (CRITICAL)

**Status:** Reported issue - Audio system not resetting properly  
**Priority:** CRITICAL - Breaks immersion and gameplay  
**Risk Level:** Medium  
**Dependencies:** Task 1.1 (Fix Automatic Map Switching) - Should be fixed together

#### Problem Statement

When transitioning to a new level/map, the audio system **intermittently** fails to reset properly, causing:
- Sounds from the previous level continue playing in the new level
- New level sounds (footsteps, NPCs, ambient sounds) do not play
- Menu/UI sounds continue to work (indicating audio system is functional, but game audio is not reset)

**Observed Behavior:**
- Single player (host) level transition works visually
- Audio issue is **intermittent** - sometimes works correctly, sometimes doesn't
- When broken: Audio from previous level persists in new level, no game audio (footsteps, NPCs, ambient) in new level
- When working: All audio functions correctly after transition
- UI/menu sounds function normally in both cases

**Root Cause Hypothesis:**
The intermittent nature suggests a **race condition or timing issue** rather than a missing reset command. Possible causes:
- Audio system reset timing relative to map load
- Resource initialization order inconsistencies
- State not being cleared consistently
- Threading/timing dependencies in Source 2 audio system

#### Technical Details

**Location:** Likely in map transition code - `AlyxGamemode/AlyxGamemode.cs` (Task 1.1) and/or game-side Lua scripts

**Current Map Transition Flow:**
1. Player triggers level transition → Game loads new map
2. Lua script detects map change → Sends `MAPN` packet
3. Server receives `MAPN` packet → Updates map tracking
4. Audio system is NOT reset/cleared
5. New level loads but audio system still references old level's audio

**Potential Causes:**
- Audio system not being cleared/reset during map transition
- Sound events from previous level not being stopped
- Audio context not being reinitialized for new level
- Source 2 engine may require explicit audio reset commands

#### Implementation Steps

**Step 1: Understand the Intermittent Nature**
- Test multiple level transitions to reproduce the issue
- Document conditions when it fails vs. when it works
- Check if timing affects the issue (fast transitions vs. slow)
- Look for patterns (specific maps, transition types, etc.)
- **Action:** Create a test scenario to reliably reproduce the issue

**Step 2: Research Source 2 Audio Reset Commands**
- Check VConsole commands for audio/sound management
- Research Source 2 engine audio reset functionality
- Check if `snd_restart` or similar commands exist
- Check Half-Life: Alyx Workshop Tools documentation for audio commands
- **Action:** Test VConsole commands like `snd_restart`, `soundflush`, or similar
- **Important:** Since issue is intermittent, we need to **force** audio reset every time to ensure consistency

**Step 2: Investigate Game-Side Audio Reset**
- Check if Lua scripts need to stop/clear audio on map change
- Look for audio-related VScript functions (e.g., `StopSoundOn`, `StopSoundEvent`)
- Check `hlvrcoop_interval.lua` for audio-related code
- **Location:** `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/hlvrcoop_interval.lua`

**Step 3: Add Audio Reset to Map Transition (C# Side)**
- Send audio reset command when map changes
- **Force reset every time** to prevent intermittent failures
- Integrate with Task 1.1 map switching code
- **Location:** `AlyxGamemode/AlyxGamemode.cs` (map transition handling)
- **Implementation:**
```csharp
// When map changes (in Task 1.1 code)
// Force audio reset to prevent intermittent failures
Response audioReset = new("command", "snd_restart"); // Or appropriate command
socket.Send(JsonConvert.SerializeObject(audioReset));
// Then send map change commands
Response mapOutput = new("command", "addon_play " + packet.args[0] + "; addon_tools_map " + packet.args[0]);
```

**Note:** Since the issue is intermittent, we should **always** send the audio reset command, even when audio appears to work. This ensures consistency.

**Step 4: Add Audio Reset to Game-Side Lua (If Needed)**
- Stop all active sounds before map transition
- Clear audio state in Lua scripts
- **Location:** Workshop addon Lua scripts
- **Implementation (if needed):**
```lua
-- In hlvrcoop_interval.lua or map transition handler
-- Stop all active sounds before transition
-- May need to track active sounds and stop them
```

**Step 5: Test Audio Reset Commands**
- Test `snd_restart` or equivalent VConsole command
- Verify it clears audio without crashing
- Test timing (before/after map load)
- **Commands to test:**
  - `snd_restart` (if available)
  - `soundflush` (if available)
  - `snd_rebuildaudiocache` (if available)
  - Other audio-related VConsole commands

**Step 6: Coordinate with Map Transition**
- Ensure audio reset happens at correct time
- Audio reset → Map change → Audio reinitialization
- **Ensure deterministic timing** to prevent intermittent failures
- Consider adding a small delay or wait mechanism
- **Order:**
  1. Stop/reset audio (force reset every time)
  2. Transition to new map
  3. Allow new map to initialize audio
  4. Optionally: Verify audio is working (if possible)

**Timing Considerations:**
- May need to reset audio **before** map transition
- May need to reset audio **after** map loads (or both)
- Test different timing scenarios to find most reliable approach
- Consider if delay is needed between reset and map load

**Step 7: Handle Edge Cases**
- Rapid map transitions (don't reset multiple times unnecessarily)
- Map transition failures (audio should still reset)
- Client disconnection during transition
- Network delays

#### Success Criteria

- [ ] Audio from previous level stops when transitioning to new level
- [ ] New level audio (footsteps, NPCs, ambient) plays correctly
- [ ] No audio artifacts or hanging sounds from previous level
- [ ] UI/menu sounds continue to work
- [ ] Audio reset works with single player transitions
- [ ] Audio reset works with multiplayer map transitions (when Task 1.1 is fixed)
- [ ] No crashes or performance issues from audio reset

#### Potential Issues & Solutions

**Issue:** `snd_restart` command doesn't exist or doesn't work
- **Solution:** Research alternative commands, or implement Lua-side audio clearing

**Issue:** Intermittent failures due to race conditions
- **Solution:** Always force audio reset (never rely on automatic reset), ensure deterministic timing

**Issue:** Audio reset timing (too early/too late)
- **Solution:** Test different timings, may need to reset both before AND after map load, add delays if needed

**Issue:** Audio reset affects UI sounds
- **Solution:** Use more specific commands that only affect game audio, not UI audio

**Issue:** Audio reset causes performance issues
- **Solution:** Only reset when map actually changes, not on every MAPN packet

**Issue:** Multiplayer synchronization (each client needs to reset)
- **Solution:** Broadcast audio reset command to all clients during map transition

**Issue:** Timing race condition between audio system and map load
- **Solution:** Add explicit delays, reset at multiple points in transition, or wait for map load confirmation before resetting

#### Testing Checklist

- [ ] Reproduce the intermittent issue (test multiple transitions)
- [ ] Identify pattern of when it fails vs. when it works
- [ ] Single player level transition → Audio resets correctly **every time** (test 10+ transitions)
- [ ] New level audio plays (footsteps, NPCs, ambient)
- [ ] Previous level audio stops
- [ ] UI/menu sounds still work
- [ ] Rapid transitions → No audio issues (test edge case timing)
- [ ] Multiple level transitions → Audio works correctly **consistently** (no intermittent failures)
- [ ] Multiplayer transitions → All clients have correct audio (when Task 1.1 is fixed)
- [ ] Verify fix prevents intermittent failures (stress test with many transitions)

#### Related Tasks

- **Task 1.1:** Fix Automatic Map Switching - Audio reset should be integrated with map switching code
- This task should be implemented alongside or after Task 1.1

---

## Priority 2: Incomplete Core Features

### Task 2.1: Fix Sound File Path Format (MINOR)

**Status:** Sound file cannot be loaded  
**Priority:** LOW - Cosmetic issue (jingle sound doesn't play)  
**Risk Level:** Low  
**Dependencies:** None

#### Problem Statement

The jingle sound (`hlvrcoop/jingle_up2`) fails to load during map transitions. The error shows:
```
[VC] File hlvrcoop/jingle_up2.vsnd missing sounds dir. Fixing for now.
[VC] File hlvrcoop/jingle_up2.vsnd did not have sounds directory in path, appending path and loading, but path needs to be fixed in resource file.
[VC] Error loading resource file "sounds/hlvrcoop/jingle_up2.vsnd_c" (Error: ERROR_FILEOPEN)
```

**Current Behavior:**
- Code uses: `"play hlvrcoop/jingle_up2"`
- Engine expects full path format
- Sound file fails to load
- No jingle plays on map transition

#### Technical Details

**Location:** `AlyxGamemode/AlyxGamemode.cs` line 323

**Current Code:**
```csharp
Response jingle = new("command", "play hlvrcoop/jingle_up2");
```

**Comparison with Working Sounds:**
- Working: `"play sounds/ui/hint.vsnd"` (uses full path with `.vsnd` extension)
- Broken: `"play hlvrcoop/jingle_up2"` (uses short path without extension)

**Sound Event Definition:**
- Sound event is defined as `"Addon.HLVRCOOP_JingleUp2"` in `WorkshopAddon/content/hlvr_addons/hlvr_coop/soundevents/hlvr_coop_soundevents.vsndevts`
- References file: `"sounds/hlvrcoop/jingle_up2.vsnd"`

#### Implementation Steps

**Step 1: Test Sound Event Name Format**
- Try using sound event name: `"play Addon.HLVRCOOP_JingleUp2"`
- Test if sound events can be referenced by name

**Step 2: Test Full Path Format**
- Try using full path: `"play sounds/hlvrcoop/jingle_up2.vsnd"`
- Match the format used by working sounds

**Step 3: Verify File Exists**
- Check if `jingle_up2.vsnd` file actually exists in the soundevents directory
- Verify file structure matches other working sounds

**Step 4: Check Sound Event Configuration**
- Verify sound event is properly registered
- Check if addon resources are loaded correctly

#### Success Criteria

- [ ] Jingle sound plays on map transition
- [ ] No VConsole errors about missing sound files
- [ ] Sound path format is consistent with other sounds

#### Notes

- This is a minor cosmetic issue - map transitions work without the jingle
- May be related to addon resource loading or sound event registration
- Could also be that the sound file doesn't exist in the installed addon

---

### Task 2.2: ~~Implement Player Body Position (PLYR Packet)~~ - COMPLETED

**Status:** ✅ IMPLEMENTED - Handler exists and works correctly  
**Priority:** N/A - Already implemented  
**Risk Level:** N/A  
**Dependencies:** None

#### Status Update

**UPDATE:** The PLYR packet handler **IS implemented** in `AlyxGamemode.cs` lines 225-258. The roadmap was incorrect - the handler exists and processes the packet correctly.

**Current Implementation:**
- Lua sends: `print("PLYR x y z pitch yaw roll health HLVRCOOP")`
- Server receives and parses the packet
- Player body position and angles are tracked internally (stored in `player.Origin` and `player.Angles`)
- Player health is parsed (but not currently stored - could be added to Player class)
- Body position is NOT broadcasted to other clients (intentional - only head/hand positions are visible)

**Implementation Details:**
- **Location:** `AlyxGamemode/AlyxGamemode.cs` lines 225-258
- Packet is parsed correctly with error handling
- Data is stored in player state for internal use (respawn, checkpoints, etc.)
- Health parsing exists but health value is not stored (only parsed)

**Potential Enhancement (Optional):**
- Store player health in Player class for use in respawn/checkpoint logic
- This is not critical as the data is parsed but simply not persisted

#### Success Criteria (Already Met)

- [x] PLYR packets are parsed correctly
- [x] Player body position is tracked
- [ ] Player health is stored (parsed but not stored in Player class - minor enhancement)
- [x] Data available for other systems (respawn, checkpoints, etc.)

---

### Task 2.3: Investigate and Implement Unimplemented Packet Types

**Status:** 13 packet types defined but not implemented (14 originally, PLYR is implemented)  
**Priority:** MEDIUM - May be needed for full functionality  
**Risk Level:** Low  
**Dependencies:** None

#### Problem Statement

13 packet types are defined in the enum but have empty case statements. These may be needed for full functionality, or they may be legacy/unused code. (Note: PLYR packet handler is implemented - see Task 2.2)

#### Investigation Steps

**Step 1: Search Lua Scripts for Packet Usage**
- Search for packet codes in `hlvrcoop_interval.lua`
- Check if any of these packets are sent
- Document which are actually used
- **Command:** `grep -r "RHND\|LHND\|HSET\|TAGS\|NPCS\|PRFX\|ALIV\|DMGE\|PROP\|BUTN\|DOOR\|TRIG\|TRGD" WorkshopAddon/`

**Step 2: Determine Purpose of Each Packet**

Based on naming conventions:

1. **RightHandIndexes / LeftHandIndexes / HeadsetIndexes / TextIndexes**
   - Likely for entity index tracking
   - May be used to track which entities represent player parts
   - **Action:** Check if these are sent during initialization

2. **ColliderIndexes**
   - Likely for NPC collider tracking
   - May be used for collision detection
   - **Action:** Check NPC-related code

3. **Prefix**
   - Unknown purpose
   - **Action:** Search codebase for "PRFX" or "Prefix"

4. **Alive**
   - Likely for player alive/dead state
   - May be redundant with PLYR packet health
   - **Action:** Check if used for respawn logic

5. **ColliderDamage**
   - Likely for NPC damage tracking
   - May be more detailed than NPHP packet
   - **Action:** Check NPC damage handling

6. **PhysicsObjectIndexStartPos**
   - Initial position for physics objects
   - May be used for initial sync
   - **Action:** Check entity initialization code

7. **ButtonIndexStartPos / ButtonPressIndex**
   - Button tracking
   - **Action:** Check if buttons need special handling

8. **DoorIndexStartPos**
   - Door tracking
   - **Action:** Check door synchronization

9. **TriggerIndexStartPos / TriggerActivateIndex**
   - Trigger tracking
   - **Action:** Check trigger synchronization

**Step 3: Implement or Remove**

For each packet type:
- **If used:** Implement handler
- **If unused:** Remove from enum (or mark as deprecated)
- **If unclear:** Leave empty but document

**Step 4: Fix ButtonPressIndex String Mapping**
**Location:** `AlyxGamemode/Packet.cs` line 157

**Current Issue:**
```csharp
PacketType.ButtonPressIndex => "BUTN",  // Same as ButtonIndexStartPos!
```

**Fix:**
```csharp
// In ParseType method (line 100-135)
"BPRS" => PacketType.ButtonPressIndex,  // Changed from "BUTN"

// In ToString method (line 136-171)
PacketType.ButtonPressIndex => "BPRS",  // Changed from "BUTN"
```

#### Implementation Template

For each packet that needs implementation:

```csharp
case PacketType.YourPacketType:
    if (packet.args.Length >= expectedArgs)
    {
        // Parse arguments
        // Validate data
        // Process packet
        // Broadcast if needed
    }
    break;
```

#### Success Criteria

- [ ] All used packet types are implemented
- [ ] Unused packet types are removed or documented
- [ ] ButtonPressIndex string mapping is fixed
- [ ] Packet purposes are documented

---

### Task 2.4: Implement Dedicated Server Mode

**Status:** Explicitly documented as "not fully developed"  
**Priority:** MEDIUM - Useful for server hosting  
**Risk Level:** Medium  
**Dependencies:** None

#### Problem Statement

Currently, servers must run with a client instance (listen server). There is no way to run a headless dedicated server without the UI.

#### Architecture Design

**Current Architecture:**
```
HLVRCoOpMod.exe
├── UserInterface (Windows Forms)
├── ServerProgram (requires UI)
└── ClientProgram (optional)
```

**Target Architecture:**
```
HLVRCoOpMod.exe (with UI - listen server)
HLVRCoOpModServer.exe (headless - dedicated server)
```

#### Implementation Steps

**Step 1: Create Server Project**
- Create new console application project: `HLVRCoOpModServer`
- Reference `HLVRCoOpModCore`, `AlyxGamemode`, `DiscordRpcPlugin`
- Copy server logic from `ServerProgram.cs`

**Step 2: Separate Server Logic from UI**
- Extract UI-independent code from `ServerProgram.cs`
- Create `ServerCore.cs` with core server logic
- Keep `ServerProgram.cs` as UI wrapper
- **Implementation:**
```csharp
// New file: HLVRCoOpModCore/ServerCore.cs
public class ServerCore
{
    public WebSocketServer? wss;
    public List<IndexedClient> connections = new();
    // ... all server logic without UI dependencies
}

// Modified: HLVRCoOpMod/ServerProgram.cs
public class ServerProgram
{
    private ServerCore serverCore = new();
    // Wraps ServerCore with UI integration
}
```

**Step 3: Create Console Server Application**
- Create `HLVRCoOpModServer/Program.cs`
- Load configuration from file
- Start server without UI
- **Implementation:**
```csharp
// HLVRCoOpModServer/Program.cs
class Program
{
    static void Main(string[] args)
    {
        // Load config from file
        var config = LoadConfig("serverconfig.json");
        
        // Initialize server
        ServerCore server = new ServerCore();
        server.Start(typeof(AlyxGamemode), LoadPlugins());
        
        // Keep running
        Console.WriteLine("Server started. Press Ctrl+C to stop.");
        Console.CancelKeyPress += (s, e) => {
            server.Close();
            Environment.Exit(0);
        };
        
        // Wait indefinitely
        Thread.Sleep(Timeout.Infinite);
    }
}
```

**Step 4: Create Configuration System**
- JSON configuration file
- Command-line argument support
- Environment variable support
- **Implementation:**
```csharp
// serverconfig.json
{
    "port": 8080,
    "password": "",
    "map": "mp_kiwitest",
    "maxPlayers": 16,
    "gamemode": "AlyxGamemode"
}
```

**Step 5: Add Logging for Headless Server**
- File logging
- Console logging
- Log rotation
- **Implementation:**
```csharp
public class ServerLogger
{
    private static StreamWriter? logFile;
    
    public static void Log(string message)
    {
        // Console
        Console.WriteLine($"[{DateTime.Now}] {message}");
        
        // File
        if (logFile != null)
        {
            logFile.WriteLine($"[{DateTime.Now}] {message}");
            logFile.Flush();
        }
    }
}
```

**Step 6: Handle UI-Dependent Code**
- Remove or stub UI calls
- Use console/file logging instead
- Handle Invoke() calls (not needed in console)
- **Implementation:**
```csharp
// Create abstraction for logging
public interface ILogger
{
    void Log(string message);
}

// Console implementation
public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine(message);
}

// UI implementation (existing)
public class UILogger : ILogger
{
    public void Log(string message) => Program.userInterface.Invoke(...);
}
```

**Step 7: Add Service/Background Mode (Optional)**
- Windows Service support
- Systemd service (Linux)
- Background daemon mode
- **Implementation:**
```csharp
// Use Topshelf or similar for Windows Service
// Or implement manually with ServiceBase
```

#### Success Criteria

- [ ] Dedicated server runs without UI
- [ ] Configuration loaded from file
- [ ] Logging works (console/file)
- [ ] Server can run as background service
- [ ] All server functionality works without UI

#### Testing Checklist

- [ ] Server starts without UI
- [ ] Clients can connect
- [ ] All server features work
- [ ] Configuration file loads correctly
- [ ] Logging works
- [ ] Server runs as service (if implemented)

---

## Priority 3: Quality of Life Improvements

### Task 3.1: ~~Enable WebSocket Client Reconnection~~ - COMPLETED

**Status:** ✅ IMPLEMENTED - Automatic reconnection enabled with re-authentication  
**Priority:** MEDIUM - Improves user experience  
**Risk Level:** Low  
**Dependencies:** None

#### Problem Statement

WebSocket client reconnection is disabled (`IsReconnectionEnabled = false`). If connection is lost, user must manually reconnect.

#### Implementation Steps

**Step 1: Enable Reconnection**
**Location:** `HLVRCoOpMod/ClientProgram.cs` line 78

**Current:**
```csharp
IsReconnectionEnabled = false
```

**Change to:**
```csharp
IsReconnectionEnabled = true
ReconnectTimeout = TimeSpan.FromSeconds(30) // Already set, but verify
```

**Step 2: Add Reconnection Event Handling**
- Handle reconnection events
- Notify user of reconnection
- Re-authenticate after reconnection
- **Implementation:**
```csharp
ws.ReconnectionHappened.Subscribe(recinfo =>
{
    ui.Invoke(() => ui.LogToOutput(channel, "Reconnected to server"));
    
    // Re-authenticate
    Response auth = new("client")
    {
        clientUsername = Settings.Default.ClientUsername,
        password = Settings.Default.ClientPassword,
        timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds
    };
    ws.Send(JsonConvert.SerializeObject(auth));
    
    // Reconnect VConsole if needed
    ConnectVConsole(ws);
});
```

**Step 3: Add Reconnection Status UI**
- Show reconnection attempts
- Display connection status
- Notify on successful reconnection

**Step 4: Test**
- Test network interruption
- Test server restart
- Test rapid reconnections
- Test reconnection timeout

#### Success Criteria

- [ ] Client automatically reconnects on connection loss
- [ ] User is notified of reconnection status
- [ ] Re-authentication works after reconnection
- [ ] VConsole reconnects after WebSocket reconnection

---

### Task 3.2: ~~Implement Version System~~ - COMPLETED

**Status:** ✅ IMPLEMENTED - Version set to 1, version checking works  
**Priority:** LOW - Prevents version mismatches  
**Risk Level:** Low  
**Dependencies:** None

#### Problem Statement

Version fields are hardcoded to 0, so version checking doesn't work. This could cause compatibility issues between client and server versions.

#### Implementation Steps

**Step 1: Define Version Number**
- Choose versioning scheme (e.g., 1.0.0, or simple integer)
- Update version in both locations
- **Location 1:** `HLVRCoOpMod/ClientProgram.cs` line 40
- **Location 2:** `HLVRCoOpModCore/Response.cs` line 33

**Step 2: Increment on Protocol Changes**
- Document when to increment version
- Update version when protocol changes
- **Rules:**
  - Increment if Response format changes
  - Increment if packet format changes
  - Increment if WebSocket protocol changes
  - Increment if authentication changes

**Step 3: Add Version Checking**
- Check versions on connection
- Warn on version mismatch
- Optionally reject incompatible versions
- **Location:** `HLVRCoOpMod/ClientProgram.cs` lines 95-105 (already exists, but version is 0)

**Step 4: Document Version History**
- Create VERSION_HISTORY.md
- Document protocol changes
- List version compatibility

#### Success Criteria

- [ ] Version numbers are set correctly
- [ ] Version checking works
- [ ] Users are warned on mismatch
- [ ] Version history is documented

---

### Task 3.3: Complete Lua Documentation

**Status:** "Coming soon!" in README  
**Priority:** LOW - Documentation  
**Risk Level:** None  
**Dependencies:** None

#### Implementation Steps

**Step 1: Document All Lua Functions**
- List all available functions
- Document parameters
- Document return values
- Add examples

**Step 2: Document Event System**
- List all event types
- Document when events fire
- Document event parameters
- Add examples

**Step 3: Create API Reference**
- Function reference
- Object reference
- Type reference
- Examples

**Step 4: Create Tutorials**
- Getting started
- Creating plugins
- Handling events
- Best practices

**Step 5: Update README**
- Remove "Coming soon!"
- Link to documentation

---

## Priority 4: Code Quality & Technical Debt

### Task 4.1: Remove Workarounds

**Status:** Chat command workaround exists  
**Priority:** LOW - Code quality  
**Risk Level:** Low  
**Dependencies:** None

#### Problem Statement

Chat command handling uses a workaround (`lua_chat_handled` response type) instead of a proper event system.

#### Implementation Steps

**Step 1: Design Proper Chat Command System**
- Integrate with plugin/gamemode event system
- Use existing event hooks
- Remove special response type

**Step 2: Implement**
- Modify chat handling to use events
- Remove `lua_chat_handled` response type
- Update Lua scripts to use new system

**Step 3: Test**
- Verify chat commands work
- Verify Lua scripts can handle commands
- Verify no regressions

---

### Task 4.2: Code Cleanup

**Status:** Commented code, empty implementations  
**Priority:** LOW - Code quality  
**Risk Level:** Low  
**Dependencies:** Complete other tasks first

#### Tasks

1. **Remove Unused Commented Code**
   - After implementing features, remove commented code
   - Or document why code is disabled

2. **Clean Up Empty Implementations**
   - Remove unused packet types
   - Remove placeholder code
   - Clean up unused variables

3. **Add Documentation**
   - Document complex logic
   - Add XML comments
   - Document design decisions

---

## Implementation Guidelines

### Code Style

- Follow existing C# conventions
- Use meaningful variable names
- Add XML comments for public methods
- Handle exceptions properly
- Log errors appropriately

### Testing Requirements

- Test with 2 players minimum
- Test with 16 players (max)
- Test error scenarios
- Test network interruptions
- Test edge cases

### Version Control

- Create feature branches
- Write descriptive commit messages
- Test before merging
- Update documentation

### Documentation

- Update code comments
- Update README if needed
- Update Documentation.md
- Document breaking changes

---

## Testing Strategy

### Unit Testing (Future)

- Test packet parsing
- Test entity ownership
- Test map switching logic
- Test error handling

### Integration Testing

- Test client-server communication
- Test VConsole communication
- Test Lua script execution
- Test plugin system

### Manual Testing

- Test with multiple players
- Test level transitions
- Test connection loss/recovery
- Test error scenarios

---

## Dependencies & Prerequisites

### Required Knowledge

- C# (.NET)
- Lua scripting
- WebSocket protocols
- TCP networking
- Source 2 Engine (VScript)

### Required Tools

- Visual Studio 2022+
- Half-Life: Alyx
- Half-Life: Alyx Workshop Tools
- Git

### External Dependencies

- Fleck (WebSocket server)
- Websocket.Client (WebSocket client)
- NLua (Lua integration)
- Newtonsoft.Json (JSON)

---

## Priority 5: Future Enhancements & Addon Support

### Task 5.1: Steam Workshop Addon Compatibility

**Status:** Future enhancement  
**Priority:** LOW - Quality of life improvements  
**Risk Level:** Medium  
**Dependencies:** Priority 1-4 tasks should be completed first

#### Overview

Add support for popular Half-Life: Alyx Steam Workshop addons to enhance the multiplayer experience. These addons provide additional features that players may want to use in cooperative gameplay.

#### Target Addons for Support

##### 1. Player Body - ALYX / GORDON

**Steam Workshop:** [Player Body](https://steamcommunity.com/sharedfiles/filedetails/?id=3581426521)

**Description:**
- Adds full player body (Alyx Vance or Gordon Freeman) visible in VR
- Includes H.E.V. suit callouts
- Customized weapon attachment points
- Additional health pen and grenade slots on belt
- Leaves ragdoll upon death

**Integration Requirements:**
- Synchronize player body model selection across clients
- Ensure body visibility is consistent for all players
- Handle body ragdoll synchronization on death
- Sync belt item positions if possible
- May require additional packet types for body state

**Investigation Needed:**
- How the addon determines which body model to use
- Whether body state can be queried via VScript
- If body ragdolls can be synchronized
- Compatibility with existing player synchronization

---

##### 2. Unpin Ragdolls [ALL MAPS UPDATE!]

**Steam Workshop:** [Unpin Ragdolls](https://steamcommunity.com/sharedfiles/filedetails/?id=2173405711)

**Description:**
- Removes "pinning" effect that prevents picking up humanoid bodies
- Allows players to pick up and throw corpses
- Use corpses as shields or move them out of the way

**Integration Requirements:**
- Synchronize ragdoll physics state across clients
- Ensure unpinned ragdolls are interactable for all players
- Handle ragdoll position/rotation synchronization
- May require new entity type tracking for ragdolls

**Investigation Needed:**
- How the addon modifies ragdoll physics properties
- Whether ragdoll state can be queried/set via VScript
- If ragdoll positions can be synchronized like physics objects
- Compatibility with existing physics synchronization

---

##### 3. Scalable Init Support

**Steam Workshop:** [Scalable Init Support](https://steamcommunity.com/sharedfiles/filedetails/?id=2182586257)

**Description:**
- Mod-loader that enables multiple init-based VScript mods to work simultaneously
- Centralized code location for easy maintenance
- Required dependency for many other addons

**Integration Requirements:**
- Ensure mod's Workshop addon is compatible with Scalable Init Support
- Verify that mod's Lua scripts work alongside Scalable Init Support
- Test compatibility when both are enabled
- May require coordination with addon loading order

**Investigation Needed:**
- Current compatibility status (may already work)
- Any conflicts with mod's initialization system
- Whether mod needs to follow Scalable Init Support's file structure
- Testing with other Scalable Init Support-dependent addons

---

#### Implementation Strategy

**Phase 1: Investigation (Before Implementation)**
1. Install and test each addon individually with the mod
2. Identify what breaks or doesn't work
3. Determine what needs to be synchronized
4. Check if VScript APIs are available for required features
5. Document compatibility issues

**Phase 2: Compatibility Testing**
1. Test each addon with multiplayer sessions
2. Identify synchronization requirements
3. Test with multiple players
4. Document edge cases and issues

**Phase 3: Implementation (If Feasible)**
1. Add necessary packet types (if needed)
2. Implement synchronization logic
3. Add configuration options
4. Test thoroughly
5. Document usage

**Phase 4: Integration**
1. Ensure addons work together (if multiple enabled)
2. Test with various combinations
3. Handle conflicts gracefully
4. Provide user documentation

#### Considerations

**Technical Challenges:**
- Some addon features may not be synchronizable (e.g., client-side visual effects)
- Addon APIs may not expose necessary state information
- Performance impact of additional synchronization
- Compatibility with existing mod features

**User Experience:**
- Addons should be optional (opt-in)
- Clear documentation on what works and what doesn't
- Graceful degradation if addon not available
- Configuration options for addon features

**Maintenance:**
- Addons may update and break compatibility
- Need to test after game/addon updates
- May require ongoing maintenance

#### Success Criteria

- [ ] Each addon can be enabled alongside the mod
- [ ] Core addon features work in multiplayer
- [ ] Synchronization works correctly (where applicable)
- [ ] No conflicts with existing mod functionality
- [ ] Clear documentation for users
- [ ] Graceful handling when addons are disabled

#### Notes

- **Priority:** This is a future enhancement, not critical for core functionality
- **Dependencies:** Should complete Priority 1-4 tasks first
- **Community Interest:** These are popular addons, so support would be valuable
- **Complexity:** Varies by addon - some may be simple compatibility fixes, others may require significant work
- **Official Documentation:** Check [Valve Developer Community](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools) for addon development guidelines

---

## Quick Reference: Implementation Order

1. **Fix automatic map switching** (CRITICAL)
2. **Fix audio system during level transitions** (CRITICAL, should be done with Task 1)
3. ~~**Implement VConsole reconnection**~~ ✅ (COMPLETED - Task 1.2)
4. ~~**Fix error handling**~~ ✅ (COMPLETED - Task 1.4)
5. **Fix command buffer handling** (MEDIUM)
6. ~~**Implement PLYR packet handler**~~ ✅ (COMPLETED - Already implemented)
7. ~~**Implement VConsole Reconnection**~~ ✅ (COMPLETED - Task 1.2)
8. ~~**Enable WebSocket Client Reconnection**~~ ✅ (COMPLETED - Task 3.1)
9. ~~**Implement Version System**~~ ✅ (COMPLETED - Task 3.2)
10. **Fix sound file path format** (LOW)
11. **Investigate unimplemented packets** (MEDIUM)
12. **Complete Lua documentation** (LOW)
13. **Code cleanup** (LOW)
14. **Addon support** (FUTURE - TBD after Priority 1-4)

---

*Last Updated: Based on comprehensive codebase analysis*
*This roadmap should be updated as tasks are completed*
