# System Components

[← Architecture Overview](Architecture-Overview) | [How Everything Works Together →](How-Everything-Works-Together)

---

## Core Projects

### HLVRCoOpMod (Main Application)
- **Type**: Windows Forms desktop application
- **Purpose**: Main user interface and orchestration
- **Key Classes**:
  - `Program.cs` - Entry point
  - `UserInterface.cs` - Windows Forms UI
  - `ClientProgram.cs` - Client connection logic
  - `ServerProgram.cs` - Server hosting logic
  - `VConsole.cs` - Game communication
  - `LuaEnvironment.cs` - Lua script execution

### HLVRCoOpModCore (Core Library)
- **Type**: Class library (shared code)
- **Purpose**: Shared interfaces and base classes
- **Key Classes**:
  - `IGamemode` - Gamemode interface
  - `IPlugin` - Plugin interface
  - `Response` - Message format
  - `IndexedClient` - Client representation
  - `Vector`, `Angle`, `Location` - Math types
  - `PluginHandler`, `GamemodeHandler` - Event dispatchers

### AlyxGamemode (Game-Specific Logic)
- **Type**: Class library (gamemode implementation)
- **Purpose**: Half-Life: Alyx specific synchronization
- **Key Classes**:
  - `AlyxGamemode.cs` - Main gamemode logic
  - `AlyxGlobalData.cs` - Global state management
  - `Player.cs` - Player representation
  - `Packet.cs` - Packet type definitions
  - `Entity.cs`, `Moveable.cs` - Entity tracking

### DiscordRpcPlugin (Discord Integration)
- **Type**: Class library (plugin)
- **Purpose**: Discord Rich Presence
- **Key Classes**:
  - `DiscordRpcPlugin.cs` - Discord integration
  - `DiscordRpcGlobalData.cs` - State management

---

## Workshop Addon

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

See [Workshop Addon System](Workshop-Addon-System) for detailed information.

---

## Base Scripts

**Location**: `HLVRCoOpMod/base_scripts/`

Server-side Lua scripts loaded automatically:
- `base/__init.lua` - Core functionality
- `base/basic.lua` - Basic commands and features
- `gamemodes/campaign/` - Campaign-specific logic

See [Lua Scripting System](Lua-Scripting-System) for more details.

---

## Related Documentation

- [Architecture Overview](Architecture-Overview) - High-level design
- [Workshop Addon System](Workshop-Addon-System) - Game-side addon details
- [Lua Scripting System](Lua-Scripting-System) - Server-side scripts
- [Plugin & Gamemode System](Plugin-Gamemode-System) - Extensibility
