# Regression Analysis: Newly Reported Issues

**Date:** Analysis based on codebase review  
**Status:** Pending Triage → Ready for Implementation

---

## Summary

This document provides detailed analysis of the 5 newly reported regressions, identifying root causes, affected code locations, and recommended fixes.

---

## Regression 1: Client Auto-Connect on Game Launch No Longer Works

### Problem Statement
Client fails to automatically join server when the game launches. Users must manually connect.

### Root Cause Analysis

**Location:** `HLVRCoOpMod/ClientProgram.cs`

**Current Flow:**
1. `ClientProgram.Start()` is called when user clicks "Start" in UI
2. WebSocket client connects to server (`ws.Start()`)
3. `ConnectVConsole()` is called immediately
4. Authentication is sent via `ReconnectionHappened` subscription (line 146-156)
5. **BUT:** There's no mechanism to detect when the game launches and automatically authenticate

**Issue:** The client connects to WebSocket immediately, but:
- If VConsole isn't ready (game not launched), connection fails
- Authentication is only sent on WebSocket reconnection, not on initial connection
- No automatic authentication when VConsole connects after game launch

**Code Locations:**
- `ClientProgram.cs:49-201` - `Start()` method
- `ClientProgram.cs:32-48` - `ConnectVConsole()` method
- `ClientProgram.cs:146-156` - `ReconnectionHappened` subscription (only fires on reconnection, not initial connection)

**Missing Logic:**
- No authentication sent on initial WebSocket connection
- No detection of game launch (VConsole connection success)
- No automatic authentication trigger when VConsole connects

### Recommended Fix

1. **Send authentication on initial WebSocket connection:**
   ```csharp
   // In ClientProgram.Start(), after ws.Start()
   ws.MessageReceived.Subscribe(msg => { ... }); // Existing handler
   
   // ADD: Send authentication immediately after connection
   Task.Run(async () =>
   {
       await Task.Delay(100); // Small delay to ensure connection is established
       if (ws != null && ws.IsStarted)
       {
           Response input = new("client")
           {
               clientUsername = Settings.Default.ClientUsername,
               password = Settings.Default.ClientPassword,
               timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds
           };
           ws.Send(JsonConvert.SerializeObject(input));
       }
   });
   ```

2. **Auto-authenticate when VConsole connects:**
   ```csharp
   // In VConsole.Reconnected event handler (already exists at line 161-184)
   // ADD: Also authenticate with server when VConsole first connects
   // This is already partially implemented, but needs to trigger on initial connect too
   ```

3. **Add logging for auto-connect attempts:**
   ```csharp
   // Log success/failure of authentication attempts
   ui.Invoke(() => ui.LogToOutput(channel, "Attempting to auto-connect to server..."));
   // After authentication response received, log success/failure
   ```

---

## Regression 2: Auto-Connect is Intermittent

### Problem Statement
Auto-connect sometimes works, sometimes doesn't. Needs reliability/pass/fail logging.

### Root Cause Analysis

**Location:** `HLVRCoOpMod/ClientProgram.cs`

**Issues:**
1. **No logging** - Can't tell if auto-connect is being attempted
2. **Race conditions** - VConsole connection timing vs WebSocket connection timing
3. **No retry logic** - If initial authentication fails, no retry
4. **Silent failures** - Errors are caught but not logged

**Code Locations:**
- `ClientProgram.cs:49-201` - `Start()` method (no logging)
- `ClientProgram.cs:77-145` - `MessageReceived` handler (no logging for auth failures)
- `VConsole.cs` - Connection timing issues

### Recommended Fix

1. **Add comprehensive logging:**
   ```csharp
   // In ClientProgram.Start()
   ui.Invoke(() => ui.LogToOutput(channel, "[AUTO-CONNECT] Starting auto-connect sequence..."));
   
   // After WebSocket connection
   ui.Invoke(() => ui.LogToOutput(channel, $"[AUTO-CONNECT] WebSocket connected: {ws.IsStarted}"));
   
   // After authentication attempt
   ui.Invoke(() => ui.LogToOutput(channel, "[AUTO-CONNECT] Authentication request sent"));
   
   // In MessageReceived handler, when "authenticated" response received
   ui.Invoke(() => ui.LogToOutput(channel, "[AUTO-CONNECT] ✓ Successfully authenticated"));
   
   // Add timeout detection
   Task.Run(async () =>
   {
       await Task.Delay(5000); // 5 second timeout
       if (map == "") // Not authenticated yet
       {
           ui.Invoke(() => ui.LogToOutput(channel, "[AUTO-CONNECT] ✗ Failed: Timeout waiting for authentication"));
       }
   });
   ```

2. **Add retry logic for authentication:**
   ```csharp
   private int authRetryCount = 0;
   private const int MAX_AUTH_RETRIES = 3;
   
   private void AttemptAuthentication()
   {
       if (ws != null && ws.IsStarted)
       {
           authRetryCount++;
           ui.Invoke(() => ui.LogToOutput(channel, $"[AUTO-CONNECT] Authentication attempt {authRetryCount}/{MAX_AUTH_RETRIES}"));
           // ... send authentication ...
       }
   }
   ```

3. **Handle authentication failures:**
   ```csharp
   // In MessageReceived handler
   case "status":
       if (response.data != null && response.data.Contains("Invalid") || response.data.Contains("password"))
       {
           ui.Invoke(() => ui.LogToOutput(channel, $"[AUTO-CONNECT] ✗ Failed: {response.data}"));
       }
       break;
   ```

---

## Regression 3: Workshop Addon Compatibility Plugin Not Auto-Enabling Addons

### Problem Statement
Workshop addon compatibility plugin does not auto-enable expected addons on startup.

### Root Cause Analysis

**Location:** `WorkshopAddonCompatibilityPlugin/WorkshopAddonCompatibilityPlugin.cs`

**Current Flow:**
1. Plugin initializes in `UserInterface_Initialized` (line 95-99)
2. `LoadEnabledAddons()` is called, which loads all supported addons (line 128)
3. `EnableAddonsForClient()` is called in `Server_PostGamemode_ClientOpen` (line 101-109)
4. **BUT:** `Server_PostGamemode_ClientOpen` only fires when a client connects via `OnOpen()` (line 293 in ServerProgram.cs)

**Issue:** 
- If client auto-connect doesn't work (Regression 1), `OnOpen()` never fires
- Even if client connects, addons are only enabled for NEW clients, not for the server host
- No mechanism to enable addons when server starts (only when clients connect)

**Code Locations:**
- `WorkshopAddonCompatibilityPlugin.cs:91-119` - Plugin handler
- `WorkshopAddonCompatibilityPlugin.cs:148-190` - `EnableAddonsForClient()` method
- `ServerProgram.cs:140-163` - `OnOpen()` method (triggers plugin)

**Missing Logic:**
- No addon enabling when server starts
- No addon enabling for listen server (host player)
- Addon enabling depends on client connection, which may fail

### Recommended Fix

1. **Enable addons when server starts:**
   ```csharp
   // In WorkshopAddonCompatibilityPlugin.cs
   case PluginHandleType.Server_PostGamemode_PostStart:
       // Enable addons for listen server (host player)
       if (vs != null && vs.Length >= 1)
       {
           // Get server's VConsole connection (if listen server)
           // This requires access to server's VConsole, which may need to be exposed
           // OR: Enable addons when first client connects (current behavior)
           // OR: Enable addons via a separate mechanism for listen server
       }
       break;
   ```

2. **Enable addons for listen server:**
   ```csharp
   // In ServerProgram.cs, after server starts
   // If this is a listen server (client also enabled), enable addons
   if (checkBoxClientEnabled.Checked)
   {
       // Enable addons for listen server
       // This requires sending commands to the host's VConsole
       // May need to expose VConsole access from ClientProgram to ServerProgram
   }
   ```

3. **Add logging:**
   ```csharp
   // In EnableAddonsForClient()
   System.Diagnostics.Debug.WriteLine($"[WorkshopAddon] Enabling {enabledAddons.Count} addons for client");
   // Log each addon being enabled
   foreach (var addonId in dependencyAddons)
   {
       System.Diagnostics.Debug.WriteLine($"[WorkshopAddon] Enabling dependency: {addonId}");
   }
   ```

**Alternative Approach:**
- Enable addons in game-side Lua scripts instead of C# side
- This would work regardless of client connection status

---

## Regression 4: StreamWatcher Socket Closure Errors on Game Exit

### Problem Statement
Exiting the game without stopping the server throws StreamWatcher socket closure errors. Error occurs at `StreamWatcher.cs:line 89`.

### Root Cause Analysis

**Location:** `HLVRCoOpMod/StreamWatcher.cs:50-122`

**Error Details:**
```
[ERR] [StreamWatcher] Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.
   at System.Net.Sockets.Socket.AwaitableSocketAsyncEventArgs.ThrowException(SocketError error, CancellationToken cancellationToken)
   at HLVRCoOpMod.StreamWatcher.Read() in StreamWatcher.cs:line 89
```

**Current Flow:**
1. `StreamWatcher.Read()` runs in a loop while `working == true` (line 52)
2. `stream.ReadAsync()` is called at line 89
3. When game exits, socket is closed by remote host
4. `ReadAsync()` throws `SocketException` or similar
5. Exception is caught at line 107, but error is logged even though it's expected

**Issue:**
- Exception is logged even when `working == false` (intentional disconnect)
- However, when game exits, `working` may still be `true` because VConsole hasn't been notified yet
- The check `if (working)` at line 111 prevents logging during intentional disconnects, but game exit is not an intentional disconnect from StreamWatcher's perspective

**Code Locations:**
- `StreamWatcher.cs:50-122` - `Read()` method
- `StreamWatcher.cs:107-119` - Exception handling
- `VConsole.cs:316-340` - `Disconnect()` method (sets `working = false`)

**Root Cause:**
- When game exits, the socket is closed by the game (remote host)
- StreamWatcher is still trying to read from the closed socket
- `working` flag is still `true` because VConsole hasn't detected the disconnect yet
- Exception is thrown and logged, even though it's expected behavior

### Recommended Fix

1. **Handle socket closure gracefully:**
   ```csharp
   // In StreamWatcher.Read(), line 89
   try
   {
       int read = await stream.ReadAsync(data, bytesRead, length - bytesRead);
       if (read == 0)
       {
           // Stream ended - connection lost
           OnConnectionLost();
           break;
       }
       bytesRead += read;
   }
   catch (System.Net.Sockets.SocketException ex)
   {
       // Socket was closed by remote host (game exited)
       // This is expected when game exits, don't log as error
       if (ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset ||
           ex.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown)
       {
           // Expected: Game closed the connection
           OnConnectionLost();
           break;
       }
       else
       {
           // Unexpected socket error
           if (working)
           {
               ErrorLogger.LogError("StreamWatcher", ex);
               OnConnectionLost();
           }
           break;
       }
   }
   catch (System.IO.IOException ex)
   {
       // Stream was closed (expected when game exits)
       // Check if it's a connection reset
       if (ex.InnerException is System.Net.Sockets.SocketException socketEx &&
           (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset ||
            socketEx.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown))
       {
           // Expected: Game closed the connection
           OnConnectionLost();
           break;
       }
       else
       {
           // Unexpected IO error
           if (working)
           {
               ErrorLogger.LogError("StreamWatcher", ex);
               OnConnectionLost();
           }
           break;
       }
   }
   ```

2. **Improve connection lost detection:**
   ```csharp
   // Check if stream is still readable before attempting to read
   if (stream != null && !stream.CanRead)
   {
       OnConnectionLost();
       break;
   }
   ```

3. **Add graceful shutdown flag:**
   ```csharp
   // In VConsole.Disconnect(), set a flag before closing
   // In StreamWatcher, check this flag before logging errors
   private bool gracefulShutdown = false;
   
   public void SetGracefulShutdown(bool value)
   {
       gracefulShutdown = value;
   }
   
   // In exception handler
   if (working && !gracefulShutdown)
   {
       ErrorLogger.LogError("StreamWatcher", ex);
   }
   ```

---

## Regression 5: Player Body Visibility Between Players Unverified

### Problem Statement
Player body visibility between players is unverified. Requires test to confirm models are visible.

### Root Cause Analysis

**Location:** `PlayerBodySyncPlugin/PlayerBodySyncPlugin.cs`

**Current Implementation:**
- Plugin exists and handles body preference synchronization
- Body preferences are queried on client connect (line 97)
- Preferences are broadcast to all clients (line 258-274)
- **BUT:** No verification that bodies are actually visible

**Code Locations:**
- `PlayerBodySyncPlugin.cs:89-109` - `HandleClientConnect()` - Queries preference
- `PlayerBodySyncPlugin.cs:236-275` - `UpdatePlayerPreference()` - Broadcasts preference
- `ClientProgram.cs:177-183` - Sends preference to game when VConsole reconnects

**Potential Issues:**
1. **Timing:** Body preference may be sent before game is ready to receive it
2. **Workshop Addon:** Requires "Player Body" addon to be enabled (WorkshopAddonCompatibilityPlugin)
3. **Convar:** Game must support `player_body_preference` convar
4. **Multiplayer:** Player Body addon may not support multiplayer visibility

### Recommended Fix

1. **Add verification logging:**
   ```csharp
   // In UpdatePlayerPreference(), after broadcasting
   System.Diagnostics.Debug.WriteLine($"[PlayerBodySync] Broadcasted preference {preference} for client {clientId} to {connections.Count - 1} other clients");
   ```

2. **Add test command:**
   ```csharp
   // Add server command to test body visibility
   // "test_body_visibility <username>" - Forces body preference change and logs result
   ```

3. **Verify addon is enabled:**
   ```csharp
   // Check if Player Body addon is enabled before syncing
   var enabledAddons = WorkshopAddonCompatibilityPlugin.GetEnabledAddons();
   if (!enabledAddons.Contains("3581426521")) // Player Body Workshop ID
   {
       System.Diagnostics.Debug.WriteLine("[PlayerBodySync] WARNING: Player Body addon not enabled!");
   }
   ```

4. **Add delay for game readiness:**
   ```csharp
   // In HandleClientConnect(), add delay before querying
   Task.Run(async () =>
   {
       await Task.Delay(2000); // Wait for game to be ready
       Response queryCommand = new("command", "convar_get player_body_preference");
       socket.Send(JsonConvert.SerializeObject(queryCommand));
   });
   ```

**Testing Required:**
- Test with 2+ players in multiplayer session
- Verify bodies are visible to other players
- Test with different body preferences (Gordon vs Alyx)
- Test with Player Body addon enabled/disabled
- Test with addon dependencies (Scalable Init Support, AlyxLib)

---

## Implementation Priority

1. **HIGH:** Regression 4 (StreamWatcher errors) - User-facing errors, easy fix
2. **HIGH:** Regression 1 (Auto-connect) - Core functionality broken
3. **MEDIUM:** Regression 2 (Intermittent auto-connect) - Related to #1, add logging
4. **MEDIUM:** Regression 3 (Workshop addons) - Depends on #1 being fixed
5. **LOW:** Regression 5 (Body visibility) - Testing/verification, may not be a code issue

---

## Testing Checklist

### Regression 1 & 2 (Auto-Connect)
- [ ] Start game with mod running
- [ ] Verify client automatically connects to server
- [ ] Check logs for auto-connect attempts
- [ ] Test with game already running vs game starting after mod
- [ ] Test with server on same machine vs remote server
- [ ] Test with password-protected server
- [ ] Test rapid connect/disconnect cycles

### Regression 3 (Workshop Addons)
- [ ] Start server with WorkshopAddonCompatibilityPlugin enabled
- [ ] Verify addons are enabled in game
- [ ] Test with listen server (host player)
- [ ] Test with dedicated server (no host player)
- [ ] Test with client connecting after server starts
- [ ] Verify addon dependencies are enabled in correct order

### Regression 4 (StreamWatcher Errors)
- [ ] Start mod and connect to game
- [ ] Exit game without stopping server
- [ ] Verify no error messages in log
- [ ] Verify graceful disconnect handling
- [ ] Test with game crash (force close)
- [ ] Test with network interruption

### Regression 5 (Body Visibility)
- [ ] Test with 2+ players in multiplayer
- [ ] Verify bodies are visible to other players
- [ ] Test different body preferences
- [ ] Test with Player Body addon enabled/disabled
- [ ] Verify synchronization across clients

---

## Notes

- All fixes should maintain backward compatibility
- Error logging should be informative but not spammy
- Consider adding configuration options for auto-connect behavior
- Workshop addon enabling may need to be moved to game-side Lua for better reliability
