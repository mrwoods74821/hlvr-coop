# Development Guide

[← Half-Life: Alyx Integration](Half-Life-Alyx-Integration) | [Troubleshooting →](Troubleshooting)

---

**⚠️ Before developing, read:**
- [Official VScript API Documentation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting) - Always check official docs first

---

## Adding Features via Lua Scripts

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

---

## Creating C# Plugins

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

---

## Extending Gamemode

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

---

## Modifying Workshop Addon

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

---

## Adding New Packet Types

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

---

## Event Types

### Client Events
- `Client_PreStart` - Before client connects
- `Client_PostStart` - After client connects
- `Client_PreClose` - Before client disconnects
- `Client_PostClose` - After client disconnects
- `Client_PreResponse` - Before processing server response
- `Client_PostResponse` - After processing server response

### Server Events
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

---

## Best Practices

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

## Related Documentation

- **[Lua API Reference](Lua-API-Reference)** - Complete Lua API documentation with examples
- [Lua Scripting System](Lua-Scripting-System) - Lua integration details
- [Plugin & Gamemode System](Plugin-Gamemode-System) - Plugin architecture
- [Packet System](Packet-System) - Packet types
- [Workshop Addon System](Workshop-Addon-System) - Game-side development
- [Half-Life: Alyx Integration](Half-Life-Alyx-Integration) - VScript API reference
