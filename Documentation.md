# Comprehensive Guide: Half-Life: Alyx Co-Op Mod (Source 2 Engine)

**Complete documentation for understanding, using, and developing the HLVR Co-Op Mod**

---

## ⚠️ Important: Official Documentation & Verification

**Before making changes to this mod, verify your code against official Half-Life: Alyx and Source 2 documentation:**

### Essential Official Resources

- **[Half-Life: Alyx Lua Scripting (VScript API)](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)** - Official VScript API documentation
- **[Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)** - Step-by-step guide
- **[Half-Life: Alyx Workshop Tools Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)** - Complete API reference
- **[Source 2 Engine Documentation](https://developer.valvesoftware.com/wiki/Source_2)** - General Source 2 information
- **[Valve Console (VConsole)](https://developer.valvesoftware.com/wiki/Valve_Console)** - VConsole information

**⚠️ Note:** This documentation describes how the mod currently works. Some implementation details may be reverse-engineered rather than officially documented. Always verify against official sources when making changes.

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Architecture Overview](#2-architecture-overview)
3. [System Components](#3-system-components)
4. [How Everything Works Together](#4-how-everything-works-together)
5. [VConsole Communication](#5-vconsole-communication)
6. [Workshop Addon System](#6-workshop-addon-system)
7. [Networking Architecture](#7-networking-architecture)
8. [Packet System](#8-packet-system)
9. [Entity Synchronization](#9-entity-synchronization)
10. [Player Management](#10-player-management)
11. [Lua Scripting System](#11-lua-scripting-system)
12. [Plugin & Gamemode System](#12-plugin--gamemode-system)
13. [Half-Life: Alyx Integration](#13-half-life-alyx-integration)
14. [Development Guide](#14-development-guide)
15. [Troubleshooting](#15-troubleshooting)

---

## 1. Introduction

### What is This Mod?

The Half-Life: Alyx Co-Op Mod (HLVRCoop) is a multiplayer modification that enables cooperative gameplay in Half-Life: Alyx, allowing up to 16 players to play together simultaneously. The mod synchronizes:

- **Player positions and rotations** (head, hands, body)
- **Physics objects** (props, items, doors)
- **Entity outputs** (triggers, buttons, logic entities)
- **NPC states** (health, death)
- **Game events** (entity removal, prop breaking)

### Key Features

- **Up to 16 simultaneous players**
- **Real-time VR hand synchronization**
- **Physics object networking**
- **Trigger output synchronization**
- **Custom addon (gamemode/plugin) support**
- **Lua scripting for server-side logic**
- **Steam Workshop map support**
- **Discord Rich Presence integration**
- **Player name tags and visual representation**

### Technical Overview

The mod uses a **hybrid architecture**:

1. **C# Application** - Windows Forms desktop application handling networking and game communication
2. **Workshop Addon** - Lua/VScript addon running inside Half-Life: Alyx
3. **VConsole Protocol** - TCP-based communication between C# and the game
4. **WebSocket Networking** - Client-server architecture for multiplayer
5. **Lua Scripting** - Server-side scriptable logic

---

## 2. Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Half-Life: Alyx                         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │        Workshop Addon (hlvr_coop)                    │   │
│  │  ┌────────────────────────────────────────────────┐  │   │
│  │  │  Lua Scripts (hlvrcoop_interval.lua)               │  │   │
│  │  │  - Tracks player/entity positions              │  │   │
│  │  │  - Monitors entity outputs                     │  │   │
│  │  │  - Sends data via print() with "HLVRCOOP" tag      │  │   │
│  │  └────────────────────────────────────────────────┘  │   │
│  │              ↕ VConsole (TCP)                        │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│              HLVRCoOpMod (C# Application)                   │
│  ┌──────────────────────┐  ┌──────────────────────────┐     │
│  │   VConsole Client    │  │   WebSocket Server       │     │
│  │  - Connects to game  │  │  - Manages connections   │     │
│  │  - Sends commands    │  │  - Processes messages    │     │
│  │  - Receives output   │  │  - Broadcasts updates    │     │
│  └──────────────────────┘  └──────────────────────────┘     │
│  ┌──────────────────────┐  ┌──────────────────────────┐     │
│  │   AlyxGamemode       │  │   Lua Environment        │     │
│  │  - Packet processing │  │  - Script execution      │     │
│  │  - Entity management │  │  - Event handling        │     │
│  └──────────────────────┘  └──────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│              Client Applications (Multiple)                 │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  WebSocket Client                                    │   │
│  │  - Connects to server                                │   │
│  │  - Sends player data                                 │   │
│  │  - Receives updates                                  │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Game → C#**: Lua scripts in game send data via `print()` → VConsole captures → Parsed as packets
2. **C# → Game**: C# sends VConsole commands → Executed in game → Affects game state
3. **Client ↔ Server**: WebSocket messages → JSON responses → Synchronized state

---

## 3. System Components

### 3.1 Core Projects

#### HLVRCoOpMod (Main Application)
- **Type**: Windows Forms desktop application
- **Purpose**: Main user interface and orchestration
- **Key Classes**:
  - `Program.cs` - Entry point
  - `UserInterface.cs` - Windows Forms UI
  - `ClientProgram.cs` - Client connection logic
  - `ServerProgram.cs` - Server hosting logic
  - `VConsole.cs` - Game communication
  - `LuaEnvironment.cs` - Lua script execution

#### HLVRCoOpModCore (Core Library)
- **Type**: Class library (shared code)
- **Purpose**: Shared interfaces and base classes
- **Key Classes**:
  - `IGamemode` - Gamemode interface
  - `IPlugin` - Plugin interface
  - `Response` - Message format
  - `IndexedClient` - Client representation
  - `Vector`, `Angle`, `Location` - Math types
  - `PluginHandler`, `GamemodeHandler` - Event dispatchers

#### AlyxGamemode (Game-Specific Logic)
- **Type**: Class library (gamemode implementation)
- **Purpose**: Half-Life: Alyx specific synchronization
- **Key Classes**:
  - `AlyxGamemode.cs` - Main gamemode logic
  - `AlyxGlobalData.cs` - Global state management
  - `Player.cs` - Player representation
  - `Packet.cs` - Packet type definitions
  - `Entity.cs`, `Moveable.cs` - Entity tracking

#### DiscordRpcPlugin (Discord Integration)
- **Type**: Class library (plugin)
- **Purpose**: Discord Rich Presence
- **Key Classes**:
  - `DiscordRpcPlugin.cs` - Discord integration
  - `DiscordRpcGlobalData.cs` - State management

### 3.2 Workshop Addon

**Location**: `WorkshopAddon/game/hlvr_addons/hlvr_coop/`

**Structure**:
```
hlvr_coop/
├── addoninfo.txt          # Addon metadata
├── scripts/
│   ├── vlua_globals.lua   # Global definitions
│   └── vscripts/
│       ├── hlvrcoop_interval.lua  # Main game-side script
│       └── util/              # Utility scripts
├── content/               # Assets (sounds, models)
└── maps/                  # Custom maps
```

### 3.3 Base Scripts

**Location**: `HLVRCoOpMod/base_scripts/`

Server-side Lua scripts loaded automatically:
- `base/__init.lua` - Core functionality
- `base/basic.lua` - Basic commands and features
- `gamemodes/campaign/` - Campaign-specific logic

---

## 4. How Everything Works Together

### 4.1 Startup Sequence

1. **User launches HLVRCoOpMod.exe**
   - Windows Forms UI initializes
   - Plugins and gamemodes loaded from `plugins/` and `gamemodes/` folders
   - Lua scripts loaded from `scripts/` folder
   - Settings loaded from `.settings` files

2. **User starts Half-Life: Alyx**
   - Game launches with VConsole enabled (`-console -vconsole`)
   - VConsole listens on TCP port (default: 29000)
   - Workshop addon `hlvr_coop` must be enabled

3. **User connects client or starts server**
   - **Client**: Connects to WebSocket server, connects VConsole
   - **Server**: Starts WebSocket server, listens for clients

4. **Client connects to game**
   - VConsole connects via TCP to `localhost:29000`
   - Sends `addon_enable hlvr_coop` to enable Workshop addon
   - Game-side Lua scripts initialize

5. **Game-side initialization**
   - `hlvrcoop_interval.lua` creates player representation entities
   - Registers VConsole commands
   - Begins tracking entities and sending updates

### 4.2 Runtime Data Flow

#### Player Movement Synchronization

```
1. Player moves in VR
   ↓
2. Game: Lua script (every 5 frames) reads player position
   ↓
3. Game: print("HEAD x y z pitch yaw roll HLVRCOOP")
   ↓
4. VConsole: Captures print output via TCP
   ↓
5. C#: VConsole.MessageAvailable receives "PRNT" message
   ↓
6. C#: Response("print", message) sent via WebSocket to server
   ↓
7. Server: AlyxGamemode parses packet
   ↓
8. Server: Creates/updates Player object
   ↓
9. Server: Broadcasts to other clients
   ↓
10. Clients: Receive Response("command", "hlvrcoop_setlocation_nonuuid...")
   ↓
11. Clients: VConsole sends command to game
   ↓
12. Game: Moves other players' visual representations
```

#### Entity Synchronization

```
1. Player picks up/moves physics object
   ↓
2. Game: Lua monitors entity cache for position changes
   ↓
3. Game: print("PHYS entityName x y z pitch yaw roll HLVRCOOP")
   ↓
4. C#: Receives packet, checks entity ownership
   ↓
5. Server: If client owns entity, broadcasts to others
   ↓
6. Other clients: Receive command to move entity
   ↓
7. Game: Entity position synchronized
```

#### Trigger Output Synchronization

```
1. Player activates trigger (e.g., walks through trigger_multiple)
   ↓
2. Game: Entity output fires (OnStartTouch)
   ↓
3. Game: Output redirected to HLVRCOOP_OnStartTouch function
   ↓
4. Game: print("FIRE entityName OnStartTouch HLVRCOOP")
   ↓
5. Server: Parses packet, broadcasts to all clients
   ↓
6. All clients: Execute entity output
   ↓
7. Game: Trigger effects occur for all players
```

---

## 5. VConsole Communication

### 5.1 What is VConsole?

VConsole is Half-Life: Alyx's developer console accessible via TCP. The mod uses it for bidirectional communication with the game.

**Official Documentation:**
- [Valve Console (VConsole)](https://developer.valvesoftware.com/wiki/Valve_Console) - General VConsole information
- **Note:** The VConsole protocol format used by this mod may be reverse-engineered. There may not be official documentation for the TCP protocol format.

**⚠️ Verification:** The protocol implementation in `VConsole.cs` was discovered through reverse engineering. When modifying VConsole communication, test thoroughly as the protocol may not be officially documented.

### 5.2 VConsole Protocol

The mod implements a custom protocol over TCP:

#### Command Format (C# → Game)

```
[4 bytes: "CMND" reversed] [Protocol byte] [Length byte] [Command string] [0x00]
```

Example:
```
Bytes: [0x44 0x4E 0x4D 0x43] [0x01] [0x15] [0x61 0x64 0x64 0x6F 0x6E...] [0x00]
       ("CMND" reversed)      (protocol)  (length)  ("addon_enable...")   (null)
```

#### Print Format (Game → C#)

Game sends via `print()` function:
```
PRNT [Message data]
```

VConsole captures "PRNT" messages and extracts the data.

### 5.3 VConsole Commands

The mod sends various commands to the game:

**⚠️ Verification:** These commands are used by the mod. Verify command availability and syntax in the game before relying on them. Some commands may be undocumented or game-specific.

#### Game Control
- `addon_enable <addon_name_or_workshop_id>` - Enable Workshop addon (use folder name for local addons, or Workshop ID for subscribed addons)
  - **Verified:** Used successfully in `AlyxGamemode.cs` line 51
- `addon_play <map_name>` - Load/addon map
  - **Verified:** Used successfully in `ServerProgram.ChangeMap()` line 122
- `addon_tools_map <map_name>` - Load map in editor mode
  - **Verified:** Used successfully in `ServerProgram.ChangeMap()` line 122
- `buddha 1` - Enable god mode (prevents death)
  - **Verified:** Used in `AlyxGamemode.cs` line 250
- `unpause` - Unpause game
  - **Verified:** Used in `AlyxGamemode.cs` line 250
- `sv_cheats 1` - Enable cheats
  - **Verified:** Used in `AlyxGamemode.cs` line 81

#### Entity Management
- `ent_create <classname> {targetname "name" origin "x y z"}` - Create entity
- `ent_remove <targetname>` - Remove entity
- `ent_fire <targetname> <input> <value>` - Fire entity input

#### Custom HLVRCOOP Commands (Registered in Workshop Addon)
- `hlvrcoop_teleport <x> <y> <z>` - Teleport player
- `hlvrcoop_teleportangles <x> <y> <z> <pitch> <yaw> <roll>` - Teleport with angles
- `hlvrcoop_setlocation <entityName> <x> <y> <z> <pitch> <yaw> <roll>` - Move entity (UUID)
- `hlvrcoop_setlocation_nonuuid <entityName> <x> <y> <z> <pitch> <yaw> <roll>` - Move entity (by name)
- `hlvrcoop_fireoutput <entityName> <outputName>` - Fire entity output
- `hlvrcoop_grace <entityName>` - Re-enable entity (after ownership timeout)
- `hlvrcoop_npc_sethealth <entityName> <health>` - Set NPC health
- `hlvrcoop_break <entityName>` - Break prop
- `hlvrcoop_sethealth <health>` - Set player health
- `hlvrcoop_cache_entity <entityName>` - Cache entity for sync
- `hlvrcoop_cache_entity_nonuuid <entityName>` - Cache entity (by name)

### 5.4 Connection Process

1. **Check if port is listening**
   ```csharp
   IPEndPoint[] endPoints = IPGlobalProperties.GetActiveTcpListeners();
   // Check if VConsole port (29000) is in list
   ```

2. **Connect via TCP**
   ```csharp
   TcpClient client = new TcpClient("127.0.0.1", Settings.Default.VconsolePort);
   NetworkStream stream = client.GetStream();
   ```

3. **Set up stream watcher**
   - `StreamWatcher` monitors stream for incoming messages
   - Parses "PRNT" messages and fires events

4. **Send window focus**
   - Sends "VFCS" command to indicate focus state

5. **Queue commands**
   - Commands can be queued if connection not ready
   - Sent once connection established

### 5.5 Message Parsing

VConsole messages are parsed in `AlyxGamemode.cs`:

```csharp
// Game output captured as "print" response type
if (response.data.Contains("HLVRCOOP"))
{
    // Split into packet parts
    List<string> packetList = response.data.Split(" ").ToList();
    string packetType = packetList[0];  // e.g., "HEAD", "HAND", "PHYS"
    packetList.Remove(packetType);
    
    // Create packet
    Packet packet = new(packetType, packetList.ToArray());
    
    // Process based on packet type
    switch (packet.type)
    {
        case PacketType.HeadPosAng:
            // Handle head position
            break;
        // ...
    }
}
```

---

## 6. Workshop Addon System

### 6.1 What is a Workshop Addon?

Half-Life: Alyx supports Steam Workshop addons that can add scripts, assets, and maps. The mod uses the Workshop addon `hlvr_coop`.

**Official Documentation:**
- [Half-Life: Alyx Workshop Tools](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools) - Complete Workshop addon documentation
- [Workshop Addon Structure](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Addon_Structure) - How to structure addons
- [Workshop Tools Installation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Installation) - Installing Workshop Tools DLC

### 6.2 Addon Structure

```
WorkshopAddon/
├── content/                           # Game assets
│   └── hlvr_addons/
│       └── hlvr_coop/
│           ├── maps/                  # Custom maps
│           ├── resourcemanifests/     # Resource definitions
│           ├── soundevents/           # Sound definitions
│           └── sounds/                # Audio files
└── game/                              # Game scripts
    └── hlvr_addons/
        └── hlvr_coop/
            ├── addoninfo.txt          # Addon metadata
            └── scripts/
                ├── vlua_globals.lua   # Global definitions
                └── vscripts/
                    ├── hlvrcoop_interval.lua  # Main script
                    └── util/              # Utility libraries
```

### 6.3 Main Script: hlvrcoop_interval.lua

This script runs inside Half-Life: Alyx and handles:

1. **Player Tracking**
   - Reads player head, hand, and body positions every 5 frames
   - Sends data via `print()` with "HLVRCOOP" tag

2. **Entity Tracking**
   - Maintains cache of synchronized entities
   - Monitors position/angle changes
   - Sends updates when entities move

3. **Output Redirection**
   - Redirects entity outputs (OnTrigger, OnStartTouch, etc.) to HLVRCOOP functions
   - Captures output events and sends to server

4. **Player Representation**
   - Creates visual entities for other players:
     - `hlvrcoop_head_<index>` - prop_dynamic (headset model)
     - `hlvrcoop_lefthand_<index>` - prop_dynamic (left glove)
     - `hlvrcoop_righthand_<index>` - prop_dynamic (right glove)
     - `hlvrcoop_text_<index>` - point_worldtext (name tag)

5. **VConsole Commands**
   - Registers custom commands via `Convars:RegisterCommand()`

### 6.4 Entity Tracking System

The script maintains `HLVRCOOP_ENTCACHE`, a table of tracked entities:

```lua
HLVRCOOP_ENTCACHE = {
    ["uuid1"] = {
        name = "entity_name",
        origin = {x, y, z},
        angles = {pitch, yaw, roll},
        class = "prop_physics",
        model = "models/...",
        entity = entity_handle,
        health = 100  -- For NPCs
    },
    -- ...
}
```

**UUID Generation**:
```lua
function uuid(name, x, y, z, classname)
    return classname.."+"..name.."+"..math.floor(x).."+"..math.floor(y).."+"..math.floor(z)
end
```

### 6.5 Synchronized Entity Types

The script tracks these entity classes:

**Triggers**:
- `trigger_multiple`, `trigger_once`, `trigger_look`, `trigger_proximity`
- `trigger_teleport`, `trigger_detect_bullet_fire`, `trigger_detect_explosion`
- Various blind zombie triggers, foliage interaction, gravity

**Logic Entities**:
- `logic_relay`, `logic_timer`, `math_counter`

**Buttons**:
- `func_button`, `func_physical_button`

**Physics Objects**:
- `prop_physics`, `prop_physics_interactive`, `prop_door_rotating_physics`
- `prop_ragdoll`, `prop_animating_breakable`, `prop_dry_erase_marker`

**Items**:
- All `item_*` classes (health vials, ammo, grenades, resin, etc.)

**NPCs**:
- `npc_headcrab_*`, `npc_zombie*`, `npc_combine_s`, `npc_antlion`, `npc_strider`, etc.

### 6.6 Output Events

All entity outputs are redirected to HLVRCOOP functions:

```lua
-- Example: OnTrigger output
HLVRCOOP_OnTrigger = function(params)
    fireit(params, "OnTrigger")
end

function fireit(params, output)
    local ent = params.caller
    local name = ent:GetName()
    local class = ent:GetClassname()
    local origin = ent:GetOrigin()
    local uu = uuid(name, origin[1], origin[2], origin[3], class)
    print("FIRE "..uu.." "..output.." HLVRCOOP")
end
```

### 6.7 Development Workflow

1. **Edit scripts** in `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/`
2. **Use symlinks** (see README.md) to link to game directory, OR
3. **Copy files** to game's `hlvr_addons/hlvr_coop/` folder
4. **Enable addon** in game: `addon_enable hlvr_coop`
5. **Test changes** - scripts reload on map change

---

## 7. Networking Architecture

### 7.1 WebSocket-Based Communication

The mod uses **WebSocket** protocol for client-server communication:

- **Server**: Fleck library (C#)
- **Client**: WebsocketClient library (C#)
- **Protocol**: JSON over WebSocket
- **Port**: Configurable (default in settings)

### 7.2 Server Architecture

```csharp
WebSocketServer wss = new WebSocketServer("ws://[::]:" + port);
wss.Start(socket =>
{
    socket.OnClose = () => OnClose(socket);
    socket.OnMessage = message => OnMessage(message, socket);
});
```

**Server manages**:
- List of connected clients (`List<IndexedClient>`)
- Player state (`AlyxGlobalData.instance`)
- Entity ownership (`Moveable` tracking)
- Message routing and broadcasting

### 7.3 Client Architecture

```csharp
WebsocketClient ws = new WebsocketClient(new Uri("ws://" + ip + ":" + port));
ws.Start();
ws.MessageReceived.Subscribe(msg =>
{
    Response response = JsonConvert.DeserializeObject<Response>(msg.Text);
    // Process response
});
```

**Client handles**:
- Connection to server
- Sending player data
- Receiving updates from server
- VConsole communication

### 7.4 Message Format: Response Class

All messages use the `Response` class (serialized as JSON):

```csharp
public class Response
{
    public string? type;              // "command", "chat", "status", "print", etc.
    public string? data;              // Message content
    public string? map;               // Current map
    public string? remoteClientUsername;  // Sender username
    public string? clientUsername;    // Client username
    public string? password;          // Authentication
    public int? version;              // Protocol version
    public bool urgent;               // Urgent flag
    public long? timestamp;           // Unix timestamp
}
```

**Response Types**:
- `"authenticated"` - Connection successful
- `"chat"` - Chat message
- `"command"` - VConsole command to execute
- `"status"` - Status message (displayed in UI)
- `"print"` - VConsole output (from game)
- `"vconsole"` - VConsole output (processed)
- `"client"` - Client authentication request
- `"lua_chat_handled"` - Chat command handled by Lua

### 7.5 Connection Flow

#### Client Connection

1. Client connects to WebSocket server
2. Server receives connection, socket created
3. Client sends `Response("client")` with username/password
4. Server validates:
   - Password matches (if set)
   - Username not already in use
   - Username valid (no special chars, max 32 chars)
5. Server creates `IndexedClient` and adds to connections
6. Server sends `Response("authenticated")` with map name
7. Server triggers `OnOpen` events
8. Client receives authentication, connects VConsole
9. Client sends `Response("print", "INIT HLVRCOOP")` to initialize

#### Server Initialization

1. Server starts WebSocket server
2. Server begins tick loop (every 66ms)
3. Server loads gamemode and plugins
4. Server waits for client connections

### 7.6 Message Broadcasting

When a client sends data, server broadcasts to other clients:

```csharp
// Example: Broadcast head position
Response movement = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_head_" + playerIndex + " " + origin + " " + angles);
foreach (IndexedClient client in connections)
{
    Player? otherPlayer = AlyxGlobalData.instance.GetPlayer(client.Session.ConnectionInfo.Id);
    if (otherPlayer != null && client.Session.ConnectionInfo.Id != senderSocket.ConnectionInfo.Id)
    {
        client.Session.Send(JsonConvert.SerializeObject(movement));
    }
}
```

### 7.7 Authentication & Security

- **Password protection**: Server can require password
- **Username validation**: Max 32 chars, no `;` or `"`
- **Duplicate prevention**: Username must be unique
- **IP-based banning**: Support for IP bans
- **Version checking**: Protocol version validation

---

## 8. Packet System

### 8.1 Packet Types

Packets are 4-letter codes sent from game via `print()`:

| Code | Type | Description |
|------|------|-------------|
| `PLYR` | PlayerPosAng | Player body position/angles |
| `HEAD` | HeadPosAng | Headset position/angles |
| `HAND` | HandPosAng | Left/right hand positions |
| `INIT` | Initialization | Client initialization request |
| `IENT` | InitializedEntities | Entities initialized |
| `PHYS` | PhysicsObjectPosAng | Physics object position/angles |
| `PROP` | PhysicsObjectIndexStartPos | Physics object initial position |
| `MAPN` | MapName | Map name detection |
| `FIRE` | EntityFired | Entity output fired |
| `NPHP` | NPCHealth | NPC health update |
| `BRAK` | BrokenProp | Prop broken |
| `EREM` | EntityRemoved | Entity removed |
| `TELE` | Teleport | Teleport command |
| `SPWN` | Spawn | Entity spawn |
| `TENT` | TemplateEntity | Template entity cache |
| `CMND` | HLVRCOOPCommand | Custom command |

### 8.2 Packet Format

Packets are space-separated strings:

```
<CODE> <arg1> <arg2> ... <argN> HLVRCOOP
```

Example:
```
HEAD 1234.5 5678.9 9012.3 0.0 90.0 0.0 HLVRCOOP
FIRE entity_uuid OnStartTouch HLVRCOOP
PHYS prop_physics_001 100.0 200.0 300.0 0.0 0.0 0.0 HLVRCOOP
```

### 8.3 Packet Processing

1. **Game sends**: `print("HEAD x y z pitch yaw roll HLVRCOOP")`
2. **VConsole captures**: Raw TCP message
3. **C# receives**: `Response("print", "HEAD x y z pitch yaw roll HLVRCOOP")`
4. **AlyxGamemode parses**:
   ```csharp
   if (response.data.Contains("HLVRCOOP"))
   {
       List<string> parts = response.data.Split(" ").ToList();
       string code = parts[0];  // "HEAD"
       parts.RemoveAt(0);
       parts.RemoveAt(parts.Count - 1);  // Remove "HLVRCOOP"
       Packet packet = new(code, parts.ToArray());
   }
   ```
5. **Gamemode handles**: Switch statement processes packet type
6. **Server broadcasts**: Sends commands to other clients

### 8.4 Adding New Packet Types

See [Development Guide - Adding New Packet Types](#adding-new-packet-types)

---

## 9. Entity Synchronization

### 9.1 Entity Ownership System

To prevent conflicts when multiple players interact with the same entity:

1. **First-come-first-served**: First client to manipulate entity "owns" it
2. **Ownership timeout**: Ownership expires after 5 seconds of inactivity
3. **Ownership transfer**: When timeout occurs, next client can take ownership
4. **Re-enable on timeout**: Entity re-enabled for other clients after timeout

**Implementation**:
```csharp
Moveable class tracks:
- Entity reference
- Owning client GUID
- Timer (5 seconds)
- On timeout: Sends "hlvrcoop_grace" command to re-enable entity
```

### 9.2 Entity Cache System (Game-Side)

The Workshop addon maintains `HLVRCOOP_ENTCACHE`:

```lua
HLVRCOOP_ENTCACHE = {
    [uuid] = {
        name = "entity_name",
        origin = {x, y, z},
        angles = {pitch, yaw, roll},
        class = "prop_physics",
        entity = entity_handle,
        health = 100  -- For NPCs
    }
}
```

**Sync Process**:
1. Entity cached when first detected
2. Position/angles monitored every frame
3. Changes detected (position delta > 1 unit or angle change)
4. Update sent via `print("PHYS ... HLVRCOOP")`
5. Other clients receive update and move entity

### 9.3 Entity Tracking Flow

```
1. Entity spawns/created
   ↓
2. Game: HLVRCOOP_EntitySyncSpecific() called
   ↓
3. Game: Entity added to HLVRCOOP_ENTCACHE
   ↓
4. Game: Entity outputs redirected to HLVRCOOP functions
   ↓
5. Game: Position monitored every frame
   ↓
6. Player interacts → Position changes
   ↓
7. Game: Detects change, sends PHYS packet
   ↓
8. Server: Checks ownership
   ↓
9. Server: If owned by sender, broadcasts update
   ↓
10. Other clients: Receive command, move entity
```

### 9.4 Entity Output Synchronization

Entity outputs (OnTrigger, OnStartTouch, etc.) are synchronized:

1. **Output redirection**: All outputs redirected to HLVRCOOP functions
2. **Output capture**: When output fires, HLVRCOOP function called
3. **Packet sent**: `print("FIRE entity_uuid outputName HLVRCOOP")`
4. **Server broadcasts**: All clients receive output
5. **Clients execute**: `hlvrcoop_fireoutput` command sent to game
6. **Game fires output**: Entity output executed for all players

### 9.5 Supported Entity Classes

See [Workshop Addon - Synchronized Entity Types](#65-synchronized-entity-types)

---

## 10. Player Management

### 10.1 Player Representation

#### Server-Side (C#)
```csharp
public class Player : Location
{
    public int Index;              // 0-15 (player slot)
    public IndexedClient Client;   // WebSocket client reference
}
```

#### Client-Side (Game)
- `hlvrcoop_head_<index>` - Headset model (prop_dynamic)
- `hlvrcoop_lefthand_<index>` - Left hand model (prop_dynamic)
- `hlvrcoop_righthand_<index>` - Right hand model (prop_dynamic)
- `hlvrcoop_text_<index>` - Name tag (point_worldtext)

### 10.2 Player Limit

- **Maximum**: 16 players
- **Index assignment**: Sequential (0, 1, 2, ...)
- **Full server**: Returns `null` on `AddPlayer()` if 16 players

### 10.3 Player Join Flow

1. Client connects to server
2. Server authenticates client
3. Server creates `IndexedClient`
4. Server calls `AlyxGlobalData.instance.AddPlayer(client)`
5. Server assigns index (players.Count)
6. Server broadcasts join message
7. Server enables Workshop addon for client
8. Client initializes game-side entities
9. Client sends `INIT` packet
10. Server responds with initialization commands
11. Client sends `IENT` packet
12. Server enables game (buddha mode, unpause)

### 10.4 Player Leave Flow

1. Client disconnects (or server kicks)
2. Server calls `OnClose(socket)`
3. Server removes player from `AlyxGlobalData`
4. Server broadcasts leave message
5. Other clients: Player visual entities remain (not removed)
6. Server frees player index (slot available for new player)

### 10.5 Player Data Synchronization

#### Position Updates
- **Frequency**: Every 5 frames (~12 times per second at 60 FPS)
- **Data sent**: Head position, hand positions, body position
- **Packet types**: `HEAD`, `HAND`, `PLYR`

#### Health Tracking
- Tracked in Lua scripts (`lua_env.persistence["players"][username]["health"]`)
- Updated when `PLYR` packet received

### 10.6 Player State Management

**AlyxGlobalData** manages:
- List of active players
- Player lookup by GUID
- Player lookup by index
- Entity ownership tracking

---

## 11. Lua Scripting System

### 11.1 NLua Integration

The mod uses **NLua** library to execute Lua scripts from C#:

```csharp
Lua lua = new Lua();
lua.LoadCLRPackage();  // Enable C# integration
lua.DoFile("script.lua");
```

### 11.2 Script Loading

1. **Auto-load**: All `.lua` files in `scripts/` folder loaded on startup
2. **Alphabetical order**: Files sorted before loading
3. **Subdirectories**: Recursive search (`SearchOption.AllDirectories`)
4. **Reloadable**: Scripts can be reloaded via `script_refresh` command

### 11.3 C# Integration

Lua scripts can:
- **Import C# types**: `import('HLVRCoOpModCore')`
- **Call C# methods**: `LuaEnvironment.instance:RunLua(code)`
- **Access C# objects**: `Program.userInterface:LogToOutput(...)`
- **Use .NET classes**: `DateTime.UtcNow`, `System.Drawing.Color`, etc.

### 11.4 Event Handling

Scripts register handlers:

```lua
lua_env.handlers[index] = function(handleType, arg1, arg2, arg3, arg4)
    if handleType == "Server_PreGamemode_PreResponse" then
        -- Handle event
    end
end
```

**Global handler dispatcher**:
```lua
handle = function(handleType, arg1, arg2, arg3, arg4)
    for _, func in pairs(lua_env.handlers) do
        func(handleType, arg1, arg2, arg3, arg4)
    end
end
```

### 11.5 Persistent Storage

Data survives server restarts:

```lua
lua_env.persistence = {
    ["key"] = "value",
    ["players"] = {
        ["username"] = {health = 100, ...}
    },
    ["configjson"] = {...}
}
```

**Note**: Persistence is in-memory. For file persistence, scripts must manually save/load (see `basic.lua` for `config.json` example).

### 11.6 Available Functions

**Global functions** (from `__init.lua`):
- `print(...)` - Print to mod UI
- `printerr(...)` - Print error to mod UI
- `printdebug(...)` - Debug.WriteLine
- `split(str, delimiter)` - Split string to array
- `refresh(script)` - Reload script file
- `refresh_all()` - Reload all scripts
- `luarun(code)` - Execute Lua code string
- `handle(handleType, ...)` - Dispatch event to handlers

**C# objects**:
- `Program.userInterface` - UI access
- `LuaEnvironment.instance` - Lua environment
- `Response(type, data)` - Create response object
- `Channel(name, displayName, color)` - Log channel
- `DateTime`, `Color`, etc. - .NET types

### 11.7 Base Scripts

#### base/__init.lua
- Sets up global functions
- Imports C# libraries
- Initializes Lua environment

#### base/basic.lua
- Basic chat commands (`/help`, `/ping`, `/echo`, `/list`)
- Server commands (`kick`, `ban`, `tp`, `lua`, etc.)
- Player data tracking
- Configuration management
- Ban system

### 11.8 Writing Lua Scripts

See [Development Guide - Lua Scripts](#lua-scripts)

---

## 12. Plugin & Gamemode System

### 12.1 Plugin System

**Purpose**: Extend mod functionality without modifying core code

**Interface**:
```csharp
public interface IPlugin
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
}
```

**Implementation**:
- Inherit from `BasePlugin`
- Constructor sets metadata
- Parameterized constructor handles events

**Loading**:
- DLLs in `plugins/` folder auto-loaded
- Classes implementing `IPlugin` registered
- Appears in Plugins menu

**Event Handling**:
- Plugin constructor called with `PluginHandleType` and parameters
- Plugin can return early (no further processing) or continue
- Plugins processed in load order

### 12.2 Gamemode System

**Purpose**: Game-specific synchronization logic

**Interface**:
```csharp
public interface IGamemode
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public HandleState? State { get; set; }
}
```

**AlyxGamemode**:
- Handles Half-Life: Alyx specific packets
- Manages player state
- Processes entity synchronization
- Broadcasts updates to clients

**Handle States**:
- `HandleState.Continue` - Continue processing
- `HandleState.Handled` - Stop processing (handled)

### 12.3 Event Flow

```
Event occurs
  ↓
Server_PreGamemode_PreResponse (Plugins)
  ↓
Server_PreGamemode_PreResponse (Lua)
  ↓
Gamemode PreResponse
  ↓
[If Continue] Server_PostGamemode_PreResponse (Plugins)
  ↓
[If Continue] Server_PostGamemode_PreResponse (Lua)
  ↓
Default handling (ServerProgram)
  ↓
Server_PreGamemode_PostResponse (Plugins)
  ↓
Server_PreGamemode_PostResponse (Lua)
  ↓
Gamemode PostResponse
  ↓
Server_PostGamemode_PostResponse (Plugins)
  ↓
Server_PostGamemode_PostResponse (Lua)
```

### 12.4 Available Events

See [Development Guide - Event Types](#event-types)

---

## 13. Half-Life: Alyx Integration

### 13.1 Source 2 Engine

Half-Life: Alyx runs on **Source 2** engine, which supports:
- **VScript**: Lua-based scripting
- **VConsole**: Developer console via TCP
- **Workshop**: Steam Workshop addon support
- **Entity I/O**: Input/output system for entities

**Official Documentation:**
- [Source 2 Engine](https://developer.valvesoftware.com/wiki/Source_2) - General Source 2 information
- [Half-Life: Alyx Workshop Tools](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools) - HL:A specific tools and APIs

### 13.2 VScript API

**⚠️ CRITICAL: Always verify VScript functions against official documentation before using them.**

**Official Documentation:**
- **[Half-Life: Alyx Lua Scripting (VScript API)](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)** - Complete VScript API reference
- **[Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)** - Step-by-step guide
- **[VScript API Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)** - Complete function reference

**⚠️ Important Limitations:**
- VScript in Half-Life: Alyx is **limited** compared to other Source 2 games
- Many game systems may not be accessible
- Not all Source 2 VScript functions work in HL:A
- Always check official documentation for availability

The Workshop addon uses Source 2 VScript functions. **Functions listed below are used by this mod, but verify availability in official documentation before using in new code:**

**Entity Functions** (used in `hlvrcoop_interval.lua`):
- `Entities:GetLocalPlayer()` - Get player entity
  - **Verified:** Used in `hlvrcoop_interval.lua` line 3
  - **Official Docs:** [Entities:GetLocalPlayer()](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)
- `Entities:FindAllByClassname(classname)` - Find entities
  - **Verified:** Used in mod (check `hlvrcoop_interval.lua` for entity finding patterns)
- `entity:GetName()` - Get entity name
  - **Verified:** Used in `hlvrcoop_interval.lua` line 127
- `entity:GetOrigin()` - Get position
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 22, 65, 108
- `entity:GetAnglesAsVector()` - Get angles
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 24, 38, 66, 109
- `entity:GetClassname()` - Get entity class
  - **Verified:** Used in `hlvrcoop_interval.lua` line 130
- `entity:GetHealth()` - Get health
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 25, 77
- `entity:SetHealth(value)` - Set health
  - **Note:** Verify availability in official docs before using
- `entity:FireOutput(output, activator, caller, params, delay)` - Fire output
  - **Note:** Verify availability in official docs before using
- `entity:RedirectOutput(output, targetInput, targetEntity)` - Redirect output
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 136, 145

**VR Functions** (used in `hlvrcoop_interval.lua`):
- `Player:GetHMDAvatar()` - Get VR avatar
  - **Verified:** Used in `hlvrcoop_interval.lua` line 26
- `avatar:GetVRHand(index)` - Get hand controller
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 29-30
- `hand:GetAbsOrigin()` - Get hand position
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 36-37
- `hand:GetAnglesAsVector()` - Get hand angles
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 38-39

**Game Functions** (used in mod):
- `GetMapName()` - Get current map
  - **Verified:** Used in mod (check Lua scripts for map name retrieval)
- `SpawnEntityFromTableSynchronous(table)` - Spawn entity
  - **Note:** Verify availability in official docs before using
- `DoEntFireByInstanceHandle(entity, input, value, delay, activator, caller)` - Fire input
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 140, 145
- `ListenToGameEvent(eventName, callback, context)` - Listen to events
  - **Note:** Verify availability in official docs before using
- `Convars:RegisterCommand(name, callback, help, flags)` - Register command
  - **Note:** Verify availability in official docs before using
- `print(...)` - Print to console
  - **Verified:** Used extensively throughout `hlvrcoop_interval.lua` (lines 41, 42, 47, 68, etc.)
- `StartSoundEvent(event, entity)` - Play sound
  - **Note:** Verify availability in official docs before using

**⚠️ For functions marked "Note:", verify in [official VScript API documentation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting) before using.**

### 13.3 Entity System

**Official Documentation:**
- [Entity System](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Entity_System) - Entity documentation
- [Entity I/O System](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Entity_I/O) - Input/Output system

**Entity Classes**:
- Entities have classnames (e.g., `prop_physics`, `trigger_multiple`)
  - **Verified:** Used in `hlvrcoop_interval.lua` for entity tracking
- Entities have names (targetname)
  - **Verified:** Used in `hlvrcoop_interval.lua` line 127
- Entities can have inputs/outputs
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 135-137, 144-145
- Entities can be spawned, modified, removed
  - **Note:** Verify spawn/remove functions in official docs

**Input/Output System**:
- **Inputs**: Actions entities can receive (e.g., `Enable`, `Disable`, `SetHealth`)
  - **Verified:** Used via `DoEntFireByInstanceHandle()` in `hlvrcoop_interval.lua`
- **Outputs**: Events entities fire (e.g., `OnTrigger`, `OnStartTouch`)
  - **Verified:** Outputs redirected in `hlvrcoop_interval.lua` lines 136, 145
- **I/O connections**: Map-defined connections between entities
  - **Note:** See official Entity I/O documentation for details

### 13.4 VR-Specific Features

**Hand Tracking**:
- Left hand: `GetVRHand(0)`
- Right hand: `GetVRHand(1)`
- Hands have position, rotation, and attached entities

**Head Tracking**:
- HMD (Head-Mounted Display) position and rotation
- Used for player view synchronization

**Avatar System**:
- `GetHMDAvatar()` returns VR avatar entity
- Avatar contains hands, head, and body representation

### 13.5 Game Events

The mod listens to game events:

```lua
ListenToGameEvent("item_pickup", function(data)
    -- Handle item pickup
end, nil)

ListenToGameEvent("player_drop_resin_in_backpack", function(data)
    -- Handle resin collection
end, nil)
```

### 13.6 Limitations

**Official Documentation:**
- [VScript Limitations](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting#Limitations) - Official limitations documentation

**Known Limitations:**
- **No direct C# access**: C# cannot directly call game functions
  - **Workaround:** All communication via VConsole TCP protocol
- **VConsole only**: Communication via TCP console protocol
  - **Note:** Protocol format may be reverse-engineered, not officially documented
- **Lua limitations**: VScript has restrictions (no file I/O from game, limited APIs)
  - **Official:** See [official limitations documentation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting#Limitations)
  - **Verified:** File I/O not available (mod uses VConsole for external communication)
- **Workshop dependency**: Requires Workshop addon to be enabled
  - **Verified:** Addon must be enabled via `addon_enable hlvr_coop`
- **Single-player engine**: Not designed for multiplayer (hacky workarounds)
  - **Note:** This is a fundamental limitation - HL:A is single-player only
  - **Workaround:** Mod synchronizes multiple single-player instances

---

## 14. Development Guide

**⚠️ Before developing, read:**
- [VERIFICATION_AND_RESOURCES.md](VERIFICATION_AND_RESOURCES.md) - How to verify code will work
- [Official VScript API Documentation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting) - Always check official docs first

### 14.1 Adding Features via Lua Scripts

**Best for**: Chat commands, server logic, player tracking, custom rules

**Official Documentation:**
- [Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)
- [VScript API Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)

**Step 1**: Create script file
```
HLVRCoOpMod/scripts/my_feature.lua
```

**Step 2**: Register handler
```lua
if not lua_env.persistence["my_handler"] then
    lua_env.persistence["my_handler"] = #lua_env.handlers + 1
end

lua_env.handlers[lua_env.persistence["my_handler"]] = function(handleType, arg1, arg2, arg3, arg4)
    -- Handle events
end
```

**Step 3**: Implement logic
```lua
if handleType == "Server_PreGamemode_PreResponse" then
    if arg1.type == "chat" and arg1.data == "/mycommand" then
        -- Handle command
        arg1.type = "lua_chat_handled"  -- Prevent default handling
    end
end
```

**See**: `FEATURE_DEVELOPMENT_GUIDE.md` for detailed examples

### 14.2 Creating C# Plugins

**Best for**: Complex logic, external libraries, performance-critical code

**Step 1**: Create class library project
- Add references: `HLVRCoOpModCore.dll`, `Newtonsoft.Json.dll`

**Step 2**: Implement plugin
```csharp
public class MyPlugin : BasePlugin
{
    public MyPlugin() : base()
    {
        Name = "My Plugin";
        Author = "Your Name";
        Description = "Description";
    }
    
    public MyPlugin(PluginHandleType type, params object[]? vs)
    {
        switch (type)
        {
            case PluginHandleType.Server_PreGamemode_PreResponse:
                // Handle event
                break;
        }
    }
}
```

**Step 3**: Build and deploy
- Build project
- Copy `.dll` to `HLVRCoOpMod/plugins/`
- Restart mod

### 14.3 Extending Gamemode

**Best for**: Game-specific sync logic, new packet types

**Modify**: `AlyxGamemode/AlyxGamemode.cs`

Add packet handling:
```csharp
case PacketType.YourPacket:
    // Handle packet
    Response cmd = new("command", "your_command " + args);
    // Broadcast to clients
    break;
```

### 14.4 Modifying Workshop Addon

**Best for**: Syncing new entity types, game behavior, new commands

**Location**: `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/`

**Official Documentation:**
- [Workshop Addon Structure](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Addon_Structure)
- [VScript API Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)
- **⚠️ Always verify VScript functions exist in HL:A before using them**

**Add entity tracking**:
```lua
hlvrcoop_trackers["your_entity_class"] = true
```

**Add VConsole command**:
```lua
Convars:RegisterCommand("hlvrcoop_yourcommand", function(command, arg1)
    -- Command logic
    print("YOUR " .. arg1 .. " HLVRCOOP")
end, "HLVR Co-Op", 0)
```

### 14.5 Adding New Packet Types

**Step 1**: Add to enum (`Packet.cs`)
```csharp
public enum PacketType
{
    YourPacket,
}
```

**Step 2**: Add parsing (`Packet.cs`)
```csharp
private static PacketType ParseType(string type)
{
    return type.ToUpper() switch
    {
        "YOUR" => PacketType.YourPacket,
        // ...
    };
}

public override string ToString()
{
    return type switch
    {
        PacketType.YourPacket => "YOUR",
        // ...
    };
}
```

**Step 3**: Send from game
```lua
print("YOUR ... HLVRCOOP")
```

**Step 4**: Handle in gamemode (`AlyxGamemode.cs`)
```csharp
case PacketType.YourPacket:
    // Process packet
    // Broadcast to clients
    break;
```

### 14.6 Event Types

**Client Events**:
- `Client_PreStart` - Before client connects
- `Client_PostStart` - After client connects
- `Client_PreClose` - Before client disconnects
- `Client_PostClose` - After client disconnects
- `Client_PreResponse` - Before processing server response
- `Client_PostResponse` - After processing server response

**Server Events**:
- `Server_PreGamemode_PreStart` - Before server starts
- `Server_PostGamemode_PreStart` - After server initialization
- `Server_PreGamemode_PostStart` - Before server fully starts
- `Server_PostGamemode_PostStart` - After server fully starts
- `Server_PreGamemode_PreResponse` - Before processing client message
- `Server_PostGamemode_PreResponse` - After initial processing
- `Server_PreGamemode_PostResponse` - Before final response
- `Server_PostGamemode_PostResponse` - After final response
- `Server_PreGamemode_ClientOpen` - When client connects
- `Server_PostGamemode_ClientOpen` - After client connects
- `Server_PreGamemode_ClientClose` - When client disconnects
- `Server_PostGamemode_ClientClose` - After client disconnects
- `Server_PreGamemode_Command` - Server operator command
- `Server_PostGamemode_Command` - After command processed
- `Server_PreGamemode_Think` - Server tick (every 66ms)
- `Server_PostGamemode_Think` - After server tick

### 14.7 Best Practices

1. **Start with Lua**: Easiest to test and iterate
2. **Use persistent storage**: For data that should survive restarts
3. **Handle errors**: Wrap code in try-catch
4. **Test incrementally**: Add small features and test
5. **Check existing code**: Look at examples in codebase
6. **Document code**: Comment your code
7. **Version control**: Use Git for your changes
8. **Performance**: Consider tick rate and update frequency
9. **Security**: Validate inputs, sanitize data
10. **Compatibility**: Test with different player counts

---

## 15. Troubleshooting

**Community Support:**
- [HLVRCoop Discord](https://discord.gg/X6Dp95WRR4) - Active community support
- [Valve Developer Community Forums](https://developer.valvesoftware.com/wiki/Main_Page) - Official support forums

### 15.1 Common Issues

#### VConsole Connection Fails
**Symptoms**: "Failed to connect to VConsole"
**Solutions**:
- Ensure Half-Life: Alyx is running
- Check VConsole port in settings (default: 29000)
- Verify game launched with `-console -vconsole` flags
- Check firewall/antivirus blocking TCP connection

#### Workshop Addon Not Loading
**Symptoms**: "INIT HLVRCOOP" not received, entities not syncing
**Solutions**:
- Check addon enabled in game: `addon_enable hlvr_coop`
- Verify addon files are installed in game directory (`game/hlvr_addons/hlvr_coop/` and `content/hlvr_addons/hlvr_coop/`)
- Check addon files in game directory

#### Players Not Visible
**Symptoms**: Other players don't appear
**Solutions**:
- Verify client connected successfully
- Check player index assignment (max 16)
- Verify Workshop addon loaded
- Check entity creation in game (hlvrcoop_head_0, etc.)
- Look for errors in mod UI

#### Entity Sync Not Working
**Symptoms**: Physics objects not syncing
**Solutions**:
- Verify entity class in `hlvrcoop_trackers` table
- Check entity has name (or gets auto-named)
- Verify entity cached: `HLVRCOOP_EntitySyncSpecific()` called
- Check packet being sent: Look for "PHYS" in VConsole output
- Verify ownership system (entity not locked by another player)

#### Chat Commands Not Working
**Symptoms**: `/command` doesn't respond
**Solutions**:
- Check command handled in Lua script
- Verify `arg1.type = "lua_chat_handled"` set
- Check script loaded: Look in scripts folder
- Verify handler registered: Check `lua_env.handlers`

#### Server Won't Start
**Symptoms**: Server fails to start
**Solutions**:
- Check port not in use
- Verify firewall allows port
- Check gamemode loaded correctly
- Look for errors in UI output
- Verify settings valid

#### Client Can't Connect
**Symptoms**: Connection fails
**Solutions**:
- Verify server IP and port correct
- Check server password (if set)
- Verify firewall allows connection
- Check server running
- Verify username valid (no special chars, max 32)

### 15.2 Debugging Tips

#### Enable VConsole Output
- Check "Print VConsole" setting in client tab
- View VConsole channel in UI output

#### Lua Script Debugging
- Use `print()` to output debug info
- Check Lua Errors channel in UI
- Use `printerr()` for errors
- Check script syntax: Lua syntax errors prevent loading

#### Packet Debugging
- Look for "HLVRCOOP" in VConsole output
- Check packet parsing in `AlyxGamemode.cs`
- Verify packet type registered in `Packet.cs`
- Check packet format (space-separated, ends with "HLVRCOOP")

#### Network Debugging
- Check WebSocket connection status
- Verify JSON serialization (Response format)
- Check message types match expected
- Verify client/server versions compatible

### 15.3 Performance Optimization

#### Reduce Update Frequency
- Player position updates already reduced (every 5 frames)
- Entity sync only on position change (threshold: 1 unit)
- Consider increasing thresholds for less frequent updates

#### Optimize Lua Scripts
- Cache lookups (avoid repeated table access)
- Use local variables
- Avoid heavy operations in tick events
- Consider C# plugin for performance-critical code

#### Network Optimization
- Reduce packet size (minimize data sent)
- Batch updates when possible
- Consider compression for large data

### 15.4 Known Limitations

- **Entity limit**: Large numbers of entities may impact performance
- **Network latency**: Updates depend on network speed
- **Ownership conflicts**: Multiple players can't manipulate same entity simultaneously
- **Workshop dependency**: Requires Workshop addon enabled
- **Single-player engine**: Not designed for multiplayer (workarounds used)
- **VR-specific**: Requires VR headset (not tested without)

---

## Appendix: Quick Reference

### File Locations

| Purpose | Location |
|---------|----------|
| Main Application | `HLVRCoOpMod/` |
| Core Library | `HLVRCoOpModCore/` |
| Gamemode | `AlyxGamemode/` |
| Workshop Addon | `WorkshopAddon/` |
| Lua Scripts | `HLVRCoOpMod/scripts/` |
| Base Scripts | `HLVRCoOpMod/base_scripts/` |
| Plugins | `HLVRCoOpMod/plugins/*.dll` |
| Gamemodes | `HLVRCoOpMod/gamemodes/*.dll` |

### Key Classes

| Class | Purpose | Location |
|-------|---------|----------|
| `AlyxGamemode` | Main gamemode logic | `AlyxGamemode/AlyxGamemode.cs` |
| `ServerProgram` | Server hosting | `HLVRCoOpMod/ServerProgram.cs` |
| `ClientProgram` | Client connection | `HLVRCoOpMod/ClientProgram.cs` |
| `VConsole` | Game communication | `HLVRCoOpMod/VConsole.cs` |
| `LuaEnvironment` | Lua execution | `HLVRCoOpMod/LuaEnvironment.cs` |
| `Packet` | Packet definitions | `AlyxGamemode/Packet.cs` |
| `Response` | Message format | `HLVRCoOpModCore/Response.cs` |

### Useful Links

- **Repository**: https://github.com/dayflaree/hlvr-coop
- **Discord**: https://discord.gg/X6Dp95WRR4
- **Half-Life: Alyx**: https://store.steampowered.com/app/546560/HalfLife_Alyx/

---

## Conclusion 

This guide covers the comprehensive architecture and operation of the Half-Life: Alyx Co-Op Mod. For usage instructions, see `README.md`. 

---

## Verification & Official Resources

**Before making changes to this mod, always verify against official documentation:**

### Essential Official Links

- **[Half-Life: Alyx Lua Scripting (VScript API)](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)** - Official VScript API documentation
- **[Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)** - Step-by-step tutorial
- **[Workshop Tools Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)** - Complete API reference
- **[Source 2 Engine](https://developer.valvesoftware.com/wiki/Source_2)** - General Source 2 information
- **[Valve Console (VConsole)](https://developer.valvesoftware.com/wiki/Valve_Console)** - VConsole documentation
- **[VERIFICATION_AND_RESOURCES.md](VERIFICATION_AND_RESOURCES.md)** - Complete verification guide

### Important Notes

- ⚠️ **This documentation describes how the mod works, not official Source 2 specifications**
- ⚠️ **Some implementation details may be reverse-engineered rather than officially documented**
- ⚠️ **Always verify VScript functions against official documentation before using them**
- ⚠️ **VScript in HL:A is limited compared to other Source 2 games - check limitations**
- ⚠️ **VConsole protocol format may be reverse-engineered - test thoroughly when modifying**

### Verification Checklist

Before implementing any change:
1. ✅ Check official documentation for API availability
2. ✅ Verify function exists in HL:A (not just Source 2)
3. ✅ Test incrementally (small changes, verify, repeat)
4. ✅ Ask community if unsure ([Discord](https://discord.gg/X6Dp95WRR4))
5. ✅ Document assumptions and verify them

**Happy modding! -Dayshower**


