# Lua API Reference

[← Lua Scripting System](Lua-Scripting-System) | [Plugin & Gamemode System →](Plugin-Gamemode-System)

---

## Overview

This document provides a complete reference for the Lua scripting API available in HLVRCoop. Lua scripts run server-side and can extend mod functionality through event handlers, chat commands, and C# integration.

**⚠️ Before using any API:**
- Check [Official VScript API Documentation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting) for game-side functions
- Verify functions exist in Half-Life: Alyx (not just Source 2)
- Test incrementally

---

## Table of Contents

1. [Global Functions](#global-functions)
2. [Global Objects](#global-objects)
3. [Event System](#event-system)
4. [C# Integration](#c-integration)
5. [Response Object](#response-object)
6. [Channel Object](#channel-object)
7. [Persistent Storage](#persistent-storage)
8. [Configuration](#configuration)
9. [Examples](#examples)
10. [Best Practices](#best-practices)

---

## Global Functions

### Printing & Logging

#### `print(...)`
Prints messages to the mod UI in the "LUA" channel.

**Parameters:**
- `...` - Variable arguments (strings, numbers, etc.)

**Example:**
```lua
print("Hello, world!")
print("Player count:", playerCount)
```

**Output:** Appears in mod UI under "LUA" channel (Cornflower Blue)

---

#### `printerr(...)`
Prints error messages to the mod UI in the "ERR" (Errors) channel.

**Parameters:**
- `...` - Variable arguments

**Example:**
```lua
printerr("Failed to process command:", errorMessage)
```

**Output:** Appears in mod UI under "ERR" channel (Red)

---

#### `printdebug(...)`
Writes to Debug output (visible in Visual Studio debugger, not in UI).

**Parameters:**
- `...` - Variable arguments

**Example:**
```lua
printdebug("Debug info:", variable)
```

**Note:** Only visible in debug builds with debugger attached.

---

### String Utilities

#### `split(str, delimiter)`
Splits a string into an array using the specified delimiter.

**Parameters:**
- `str` (string) - String to split
- `delimiter` (string) - Delimiter character(s)

**Returns:** Array of strings

**Example:**
```lua
local parts = split("HEAD 100 200 300 0 90 0 HLVRCOOP", " ")
-- parts[1] = "HEAD"
-- parts[2] = "100"
-- parts[3] = "200"
-- etc.
```

**Implementation:** Uses C# `StringSplit()` method, returns JSON-decoded array.

---

### Script Management

#### `refresh(script)`
Reloads a specific Lua script file.

**Parameters:**
- `script` (string) - Path to script file (relative to `scripts/` folder)

**Example:**
```lua
refresh("my_feature.lua")  -- Reloads scripts/my_feature.lua
```

**Note:** Script must be in `scripts/` folder. Path should not include `scripts/` prefix.

---

#### `refresh_all()`
Reloads all Lua scripts from the `scripts/` folder.

**Example:**
```lua
refresh_all()  -- Reloads all scripts
```

**Note:** Scripts are loaded in alphabetical order.

---

#### `luarun(code)`
Executes Lua code as a string.

**Parameters:**
- `code` (string) - Lua code to execute

**Example:**
```lua
luarun("print('Hello from string!')")
luarun("lua_env.persistence['test'] = 'value'")
```

**Use Cases:**
- Dynamic code execution
- Server commands that execute Lua
- Testing code snippets

---

### Event System

#### `handle(handleType, arg1, arg2, arg3, arg4)`
Internal function that dispatches events to all registered handlers. You typically don't call this directly - it's called automatically by the mod.

**Parameters:**
- `handleType` (string) - Event type (e.g., "Server_PreGamemode_PreResponse")
- `arg1`, `arg2`, `arg3`, `arg4` - Event-specific arguments

**Note:** This is the global dispatcher. Scripts register handlers in `lua_env.handlers` table.

---

## Global Objects

### `lua_env`
Main Lua environment table containing shared state.

**Structure:**
```lua
lua_env = {
    persistence = {},  -- Persistent storage (survives script reloads)
    handlers = {}      -- Registered event handlers
}
```

**Usage:**
```lua
-- Access persistence
lua_env.persistence["my_key"] = "value"

-- Register handler
lua_env.handlers[1] = function(handleType, arg1, arg2, arg3, arg4)
    -- Handler code
end
```

---

### `lua_config`
Configuration table loaded from `base/_config.lua`.

**Available Properties:**
- `lua_config.sub_gamemode` (string) - Current sub-gamemode name
- `lua_config.client_introduction_message` (table) - Welcome message lines
- `lua_config.gamemodes` (table) - Gamemode name mappings
- `lua_config.sub_gamemodes` (table) - Sub-gamemode name mappings
- `lua_config.client_helptable` (table) - Client `/help` command output
- `lua_config.server_helptable` (table) - Server `help` command output

**Example:**
```lua
-- Access config
local gamemode = lua_config.gamemodes["AlyxGamemode"]  -- "Half-Life: Alyx"

-- Modify help table
table.insert(lua_config.client_helptable, "/mycommand - My custom command")
```

**Note:** Modifications to `lua_config` are in-memory only. Edit `base/_config.lua` for permanent changes.

---

### `Program.userInterface`
C# UserInterface object for UI access.

**Available Methods:**
- `Program.userInterface:Invoke(function)` - Invoke function on UI thread
- `Program.userInterface:LogToOutput(channel, message)` - Log to output channel

**Example:**
```lua
Program.userInterface:Invoke(function()
    Program.userInterface:LogToOutput(channel, "Message")
end)
```

**Note:** Always use `Invoke()` when accessing UI from event handlers (may be on different thread).

---

### `LuaEnvironment.instance`
C# LuaEnvironment instance for advanced operations.

**Available Methods:**
- `LuaEnvironment.instance:StringSplit(str, delimiter)` - Split string (returns JSON)
- `LuaEnvironment.instance:RunFile(file)` - Execute Lua file
- `LuaEnvironment.instance:RunLua(code)` - Execute Lua code string

**Example:**
```lua
local json = LuaEnvironment.instance:StringSplit("a,b,c", ",")
-- json is a JSON string that can be decoded
```

---

### C# .NET Types

Lua scripts can access .NET types directly:

**DateTime:**
```lua
local now = DateTime.UtcNow
local timestamp = DateTime.UtcNow:Subtract(DateTime.UnixEpoch).TotalMilliseconds
```

**Color:**
```lua
local red = Color.Red
local custom = Color:FromArgb(255, 128, 64)
```

**System Types:**
- `System.String`
- `System.Int32`
- `System.Double`
- `System.Boolean`
- etc.

---

## Event System

### Registering Event Handlers

Scripts register handlers in the `lua_env.handlers` table:

```lua
-- Get or create handler index
if not lua_env.persistence["my_handler"] then
    lua_env.persistence["my_handler"] = #lua_env.handlers + 1
end

-- Register handler function
lua_env.handlers[lua_env.persistence["my_handler"]] = function(handleType, arg1, arg2, arg3, arg4)
    if handleType == "Server_PreGamemode_PreResponse" then
        -- Handle event
    end
end
```

**Important:** Use persistence to store handler index so it persists across script reloads.

---

### Event Types

#### Client Events

| Event | When Fired | Arguments |
|-------|------------|-----------|
| `Client_PreStart` | Before client connects | `ui` (UserInterface) |
| `Client_PostStart` | After client connects | `ui` (UserInterface), `ws` (WebsocketClient), `error` (bool) |
| `Client_PreClose` | Before client disconnects | `ui` (UserInterface), `ws` (WebsocketClient) |
| `Client_PostClose` | After client disconnects | `ui` (UserInterface), `ws` (WebsocketClient) |
| `Client_PreResponse` | Before processing server response | `response` (Response) |
| `Client_PostResponse` | After processing server response | `response` (Response) |

---

#### Server Events

| Event | When Fired | Arguments |
|-------|------------|-----------|
| `Server_PreGamemode_PreStart` | Before server starts | None |
| `Server_PostGamemode_PreStart` | After server initialization | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PreGamemode_PostStart` | Before server fully starts | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PostGamemode_PostStart` | After server fully starts | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PreGamemode_PreClose` | Before server closes | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PostGamemode_PreClose` | After server closing starts | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PreGamemode_PostClose` | Before server fully closes | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PostGamemode_PostClose` | After server fully closes | `type` (Type), `plugins` (List\<Type\>), `map` (string) |
| `Server_PreGamemode_PreResponse` | Before processing client message | `response` (Response), `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `map` (string) |
| `Server_PostGamemode_PreResponse` | After initial processing | `response` (Response), `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `map` (string) |
| `Server_PreGamemode_PostResponse` | Before final response | `response` (Response), `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `map` (string) |
| `Server_PostGamemode_PostResponse` | After final response | `response` (Response), `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `map` (string) |
| `Server_PreGamemode_ClientOpen` | When client connects | `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `username` (string) |
| `Server_PostGamemode_ClientOpen` | After client connects | `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `username` (string) |
| `Server_PreGamemode_ClientClose` | When client disconnects | `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `username` (string) |
| `Server_PostGamemode_ClientClose` | After client disconnects | `connections` (List\<IndexedClient\>), `socket` (IWebSocketConnection), `username` (string) |
| `Server_PreGamemode_Command` | Server operator command | `command` (List\<string\>), `connections` (List\<IndexedClient\>), `gamemodeType` (Type) |
| `Server_PostGamemode_Command` | After command processed | `command` (List\<string\>), `connections` (List\<IndexedClient\>), `gamemodeType` (Type) |
| `Server_PreGamemode_Think` | Server tick (every 66ms) | `tickrate` (int), `connections` (List\<IndexedClient\>), `map` (string) |
| `Server_PostGamemode_Think` | After server tick | `tickrate` (int), `connections` (List\<IndexedClient\>), `map` (string) |

---

### Common Event Patterns

#### Handling Chat Commands

```lua
if handleType == "Server_PreGamemode_PreResponse" then
    if arg1.type == "chat" and arg1.data then
        if arg1.data == "/mycommand" then
            -- Handle command
            local resp = Response("status", "Command executed!")
            arg3:Send(resp:ToString())
            
            -- Mark as handled to prevent default processing
            arg1.handled = true
        end
    end
end
```

**Arguments:**
- `arg1` = `Response` object (contains `type`, `data`, etc.)
- `arg2` = `List<IndexedClient>` (all connected clients)
- `arg3` = `IWebSocketConnection` (sender's connection)
- `arg4` = `string` (current map name)

---

#### Handling Server Commands

```lua
if handleType == "Server_PreGamemode_Command" then
    if arg1[0] == "mycommand" then
        if arg1.Count < 2 then
            print("Usage: 'mycommand <arg>'")
            return
        end
        print("Executed with arg:", arg1[1])
    end
end
```

**Arguments:**
- `arg1` = `List<string>` (command and arguments, `arg1[0]` is command name)
- `arg2` = `List<IndexedClient>` (all connected clients)
- `arg3` = `Type` (gamemode type)

---

#### Tracking Player Data

```lua
if handleType == "Server_PreGamemode_PreResponse" then
    if arg1.type == "print" and arg1.data then
        if string.find(arg1.data, "HLVRCOOP") then
            local packet = split(arg1.data, " ")
            if packet[1] == "PLYR" then
                -- Update player data
                for i = 0, arg2.Count - 1 do
                    if arg2[i].Session.ConnectionInfo.Id:ToString() == arg3.ConnectionInfo.Id:ToString() then
                        lua_env.persistence["players"][arg2[i].Username]["health"] = tonumber(packet[8])
                        lua_env.persistence["players"][arg2[i].Username]["origin"] = {
                            ["x"] = tonumber(packet[2]),
                            ["y"] = tonumber(packet[3]),
                            ["z"] = tonumber(packet[4])
                        }
                        break
                    end
                end
            end
        end
    end
end
```

---

## C# Integration

### Creating Response Objects

#### `Response(type, data)`
Creates a new Response object for sending messages to clients.

**Parameters:**
- `type` (string) - Response type: `"status"`, `"command"`, `"chat"`, etc.
- `data` (string) - Message content

**Returns:** Response object

**Example:**
```lua
local resp = Response("status", "Hello, player!")
local cmd = Response("command", "hlvrcoop_teleport 100 200 300")
```

**Response Types:**
- `"status"` - Status message (displayed in UI)
- `"command"` - VConsole command to execute
- `"chat"` - Chat message
- `"vconsole"` - VConsole output

**Sending Responses:**
```lua
-- Send to specific client
arg3:Send(resp:ToString())  -- arg3 is IWebSocketConnection

-- Send to all clients
for i = 0, arg2.Count - 1 do
    arg2[i].Session:Send(resp:ToString())
end
```

---

### Creating Channel Objects

#### `Channel(name, friendlyName, color)`
Creates a log channel for custom output.

**Parameters:**
- `name` (string) - Channel code (e.g., "MYCH")
- `friendlyName` (string) - Display name (e.g., "My Channel")
- `color` (Color) - Channel color

**Returns:** Channel object

**Example:**
```lua
local myChannel = Channel("MYCH", "My Channel", Color.Green)
Program.userInterface:Invoke(function()
    Program.userInterface:LogToOutput(myChannel, "Custom message")
end)
```

---

### Accessing C# Collections

Lua can access C# collections (List, Array, etc.):

**List\<IndexedClient\>:**
```lua
for i = 0, arg2.Count - 1 do
    local client = arg2[i]
    print("Client:", client.Username)
    print("Map:", client.Map)
    -- Send message
    client.Session:Send(resp:ToString())
end
```

**List\<string\> (Command arguments):**
```lua
if arg1.Count >= 2 then
    local command = arg1[0]  -- Command name
    local arg = arg1[1]      -- First argument
end
```

**Properties:**
- `.Count` - Number of items (use `for i = 0, list.Count - 1`)
- `[index]` - Access by index (0-based)

---

### Accessing Connection Info

**IWebSocketConnection:**
```lua
local connectionId = arg3.ConnectionInfo.Id:ToString()
local ipAddress = arg3.ConnectionInfo.ClientIpAddress
```

**IndexedClient:**
```lua
local username = client.Username
local map = client.Map
local session = client.Session  -- IWebSocketConnection
```

---

## Response Object

Response objects represent messages sent between client and server.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `type` | string | Response type (`"status"`, `"command"`, `"chat"`, etc.) |
| `data` | string | Message content |
| `map` | string | Current map name |
| `remoteClientUsername` | string | Sender's username (for chat) |
| `clientUsername` | string | Client username |
| `password` | string | Authentication password |
| `version` | int | Protocol version |
| `urgent` | bool | Urgent flag (for commands) |
| `timestamp` | long | Unix timestamp (milliseconds) |
| `handled` | bool | **NEW:** Indicates if response was handled by plugin/Lua. Set to `true` to prevent default processing. |

### Handling Responses

#### `handled` Property

The `handled` property allows scripts to signal that they have processed a response and default processing should be skipped.

**Usage:**
```lua
if arg1.type == "chat" and arg1.data == "/mycommand" then
    -- Process the command
    local resp = Response("status", "Command executed!")
    arg3:Send(resp:ToString())
    
    -- Mark as handled to prevent default chat processing
    arg1.handled = true
end
```

**When to use:**
- When handling chat commands in `Server_PreGamemode_PreResponse`
- When you want to prevent default server behavior
- When implementing custom command processing

**Default value:** `false` (response is not handled)

**Backward compatibility:** The legacy method of setting `arg1.type = "lua_chat_handled"` is still supported but deprecated. New scripts should use `arg1.handled = true`.

### Methods

#### `ToString()`
Serializes Response to JSON string for sending.

**Returns:** JSON string

**Example:**
```lua
local resp = Response("status", "Hello!")
local json = resp:ToString()
arg3:Send(json)
```

---

## Channel Object

Channel objects define log output channels in the mod UI.

### Creating Channels

```lua
local channel = Channel("CODE", "Display Name", Color.Red)
```

**Parameters:**
- Channel code (4 letters, e.g., "ERR", "LUA")
- Friendly display name
- Color object

### Using Channels

```lua
Program.userInterface:Invoke(function()
    Program.userInterface:LogToOutput(channel, "Message")
end)
```

**Note:** Always use `Invoke()` when accessing UI from event handlers.

---

## Persistent Storage

### Overview

`lua_env.persistence` is a table that persists across script reloads (but not server restarts).

### Usage

**Storing Data:**
```lua
lua_env.persistence["my_key"] = "value"
lua_env.persistence["players"] = {}
lua_env.persistence["players"]["username"] = {
    health = 100,
    origin = {x = 0, y = 0, z = 0}
}
```

**Retrieving Data:**
```lua
local value = lua_env.persistence["my_key"]
local player = lua_env.persistence["players"]["username"]
```

**Clearing Data:**
```lua
lua_env.persistence["my_key"] = nil
lua_env.persistence = {}  -- Clear all
```

### File Persistence

For data that survives server restarts, scripts must manually save/load:

**Saving:**
```lua
local file = io.open("config.json", "w")
file:write(json.encode(lua_env.persistence["configjson"]))
file:close()
```

**Loading:**
```lua
local file = io.open("config.json", "r")
if file then
    local data = file:read("*all")
    file:close()
    lua_env.persistence["configjson"] = json.decode(data)
end
```

---

## Configuration

### Config File

Edit `base/_config.lua` to configure server settings.

**Available Settings:**
- `lua_config.sub_gamemode` - Current sub-gamemode
- `lua_config.client_introduction_message` - Welcome message (supports placeholders)
- `lua_config.gamemodes` - Gamemode name mappings
- `lua_config.sub_gamemodes` - Sub-gamemode name mappings
- `lua_config.client_helptable` - Client `/help` output
- `lua_config.server_helptable` - Server `help` output

### Introduction Message Placeholders

- `~map` - Current map name
- `~areis` - "are" or "is" (based on player count)
- `~playercount` - Number of players
- `~plural` - "s" or "" (for pluralization)
- `~gamemode` - Gamemode name
- `~subgamemode` - Sub-gamemode name

**Example:**
```lua
"There ~areis ~playercount player~plural on ~map"
-- Becomes: "There are 3 players on mp_kiwitest"
```

---

## Examples

### Example 1: Simple Chat Command

```lua
-- Register handler
if not lua_env.persistence["my_handler"] then
    lua_env.persistence["my_handler"] = #lua_env.handlers + 1
end

lua_env.handlers[lua_env.persistence["my_handler"]] = function(handleType, arg1, arg2, arg3, arg4)
    if handleType == "Server_PreGamemode_PreResponse" then
        if arg1.type == "chat" and arg1.data == "/hello" then
            local resp = Response("status", "Hello, " .. arg3.ConnectionInfo.Id:ToString() .. "!")
            arg3:Send(resp:ToString())
            arg1.handled = true  -- Mark as handled to prevent default processing
        end
    end
end
```

---

### Example 2: Server Command

```lua
if handleType == "Server_PreGamemode_Command" then
    if arg1[0] == "broadcast" then
        if arg1.Count < 2 then
            print("Usage: 'broadcast <message>'")
            return
        end
        local message = arg1[1]
        for i = 2, arg1.Count - 1 do
            message = message .. " " .. arg1[i]
        end
        local resp = Response("status", message)
        for i = 0, arg2.Count - 1 do
            arg2[i].Session:Send(resp:ToString())
        end
        print("Broadcasted:", message)
    end
end
```

---

### Example 3: Player Tracking

```lua
if handleType == "Server_PreGamemode_PreResponse" then
    if arg1.type == "print" and arg1.data then
        if string.find(arg1.data, "PLYR") then
            local packet = split(arg1.data, " ")
            for i = 0, arg2.Count - 1 do
                if arg2[i].Session.ConnectionInfo.Id:ToString() == arg3.ConnectionInfo.Id:ToString() then
                    -- Update player data
                    if not lua_env.persistence["players"] then
                        lua_env.persistence["players"] = {}
                    end
                    if not lua_env.persistence["players"][arg2[i].Username] then
                        lua_env.persistence["players"][arg2[i].Username] = {}
                    end
                    lua_env.persistence["players"][arg2[i].Username]["health"] = tonumber(packet[8])
                    lua_env.persistence["players"][arg2[i].Username]["origin"] = {
                        x = tonumber(packet[2]),
                        y = tonumber(packet[3]),
                        z = tonumber(packet[4])
                    }
                    break
                end
            end
        end
    end
end
```

---

### Example 4: Custom Server Command with Persistence

```lua
if handleType == "Server_PreGamemode_Command" then
    if arg1[0] == "setmotd" then
        if arg1.Count < 2 then
            print("Usage: 'setmotd <message>'")
            return
        end
        local motd = arg1[1]
        for i = 2, arg1.Count - 1 do
            motd = motd .. " " .. arg1[i]
        end
        lua_env.persistence["motd"] = motd
        print("MOTD set to:", motd)
    elseif arg1[0] == "getmotd" then
        local motd = lua_env.persistence["motd"]
        if motd then
            print("MOTD:", motd)
        else
            print("No MOTD set")
        end
    end
end

-- Send MOTD when player joins
if handleType == "Server_PreGamemode_ClientOpen" then
    if lua_env.persistence["motd"] then
        local resp = Response("status", lua_env.persistence["motd"])
        arg2:Send(resp:ToString())
    end
end
```

---

## Best Practices

### 1. Handler Registration

Always use persistence to store handler index:

```lua
if not lua_env.persistence["my_handler"] then
    lua_env.persistence["my_handler"] = #lua_env.handlers + 1
end

lua_env.handlers[lua_env.persistence["my_handler"]] = function(...)
    -- Handler code
end
```

**Why:** Prevents duplicate handlers on script reload.

---

### 2. Null Checking

Always check for nil before accessing objects:

```lua
if arg1 and arg1.type and arg1.data then
    -- Safe to use
end

if arg2 and arg2.Count and arg2.Count > 0 then
    -- Safe to iterate
end
```

---

### 3. Error Handling

Wrap code in error handling:

```lua
local success, result = pcall(function()
    -- Risky code
    local value = arg1.data:sub(1, 10)
end)

if not success then
    printerr("Error:", result)
end
```

---

### 4. Thread Safety

Use `Invoke()` when accessing UI:

```lua
Program.userInterface:Invoke(function()
    Program.userInterface:LogToOutput(channel, "Message")
end)
```

**Why:** Event handlers may run on different threads.

---

### 5. Performance

- Avoid heavy operations in Think events (runs every 66ms)
- Cache frequently accessed data
- Use early returns to skip unnecessary processing

```lua
if handleType == "Server_PreGamemode_Think" then
    -- Keep this lightweight!
    if tickCount % 10 == 0 then  -- Only run every 10 ticks
        -- Heavy operation
    end
end
```

---

### 6. Chat Command Handling

Mark commands as handled to prevent default processing:

```lua
if arg1.data == "/mycommand" then
    -- Handle command
    arg1.handled = true  -- Important! Prevents default processing
end
```

**Note:** The legacy method of setting `arg1.type = "lua_chat_handled"` is still supported for backward compatibility, but using `arg1.handled = true` is the recommended approach.

---

### 7. String Operations

Use Lua string functions for pattern matching:

```lua
-- Check if string starts with pattern
if string.find(arg1.data, "^/echo") then
    -- Starts with "/echo"
end

-- Pattern replacement
local newStr = str:gsub("~placeholder", value)
```

---

### 8. JSON Operations

Use the `json` library for encoding/decoding:

```lua
-- Encode table to JSON
local jsonStr = json.encode(myTable)

-- Decode JSON to table
local myTable = json.decode(jsonStr)
```

---

## Common Patterns

### Pattern: Command with Arguments

```lua
if arg1[0] == "mycommand" then
    if arg1.Count < 2 then
        print("Usage: 'mycommand <arg>'")
        return
    end
    local arg = arg1[1]
    -- Process command
end
```

---

### Pattern: Iterate All Clients

```lua
for i = 0, arg2.Count - 1 do
    local client = arg2[i]
    -- Process client
    client.Session:Send(resp:ToString())
end
```

---

### Pattern: Find Client by Username

```lua
for i = 0, arg2.Count - 1 do
    if arg2[i].Username == targetUsername then
        -- Found client
        arg2[i].Session:Send(resp:ToString())
        return
    end
end
print("Player not found")
```

---

### Pattern: Find Current Client

```lua
for i = 0, arg2.Count - 1 do
    if arg2[i].Session.ConnectionInfo.Id:ToString() == arg3.ConnectionInfo.Id:ToString() then
        -- This is the current client
        local currentClient = arg2[i]
        break
    end
end
```

---

## Troubleshooting

### Script Not Loading

- Check file is in `scripts/` folder
- Verify file has `.lua` extension
- Check for syntax errors (use `printerr()` to see errors)
- Ensure script doesn't crash on load

### Handler Not Firing

- Verify handler is registered in `lua_env.handlers`
- Check handler index is stored in persistence
- Ensure event type matches exactly (case-sensitive)
- Add `print()` statements to debug

### C# Objects Not Accessible

- Verify `import()` statements in `__init.lua`
- Check object is not nil before accessing
- Use `Invoke()` for UI access
- Verify object type matches expected C# type

### Persistence Not Working

- Persistence survives script reloads, NOT server restarts
- Use file I/O for persistent data across restarts
- Check persistence key exists before accessing

---

## Related Documentation

- [Lua Scripting System](Lua-Scripting-System) - System overview
- [Development Guide](Development-Guide) - How to write scripts
- [Plugin & Gamemode System](Plugin-Gamemode-System) - Event system details
- [Packet System](Packet-System) - Packet types and formats
- [Player Management](Player-Management) - Player data structures

---

## Official Resources

- **[Half-Life: Alyx Lua Scripting (VScript API)](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)** - Official VScript API
- **[Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)** - Tutorial
- **[VScript API Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)** - Complete API reference

---

**Happy scripting! -Dayshower**
