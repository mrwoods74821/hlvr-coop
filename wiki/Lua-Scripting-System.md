# Lua Scripting System

[← Player Management](Player-Management) | [Plugin & Gamemode System →](Plugin-Gamemode-System)

---

## NLua Integration

The mod uses **NLua** library to execute Lua scripts from C#:

```csharp
Lua lua = new Lua();
lua.LoadCLRPackage();  // Enable C# integration
lua.DoFile("script.lua");
```

---

## Script Loading

1. **Auto-load**: All `.lua` files in `scripts/` folder loaded on startup
2. **Alphabetical order**: Files sorted before loading
3. **Subdirectories**: Recursive search (`SearchOption.AllDirectories`)
4. **Reloadable**: Scripts can be reloaded via `script_refresh` command

---

## C# Integration

Lua scripts can:
- **Import C# types**: `import('HLVRCoOpModCore')`
- **Call C# methods**: `LuaEnvironment.instance:RunLua(code)`
- **Access C# objects**: `Program.userInterface:LogToOutput(...)`
- **Use .NET classes**: `DateTime.UtcNow`, `System.Drawing.Color`, etc.

---

## Event Handling

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

---

## Persistent Storage

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

---

## Available Functions

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

---

## Base Scripts

### base/__init.lua
- Sets up global functions
- Imports C# libraries
- Initializes Lua environment

### base/basic.lua
- Basic chat commands (`/help`, `/ping`, `/echo`, `/list`)
- Server commands (`kick`, `ban`, `tp`, `lua`, etc.)
- Player data tracking
- Configuration management
- Ban system

---

## Writing Lua Scripts

See [Development Guide - Lua Scripts](Development-Guide#adding-features-via-lua-scripts) for detailed instructions.

---

## Related Documentation

- [Development Guide](Development-Guide) - How to write Lua scripts
- [Plugin & Gamemode System](Plugin-Gamemode-System) - Event system
- [Player Management](Player-Management) - Player data tracking
