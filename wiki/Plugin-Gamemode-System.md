# Plugin & Gamemode System

[← Lua Scripting System](Lua-Scripting-System) | [Half-Life: Alyx Integration →](Half-Life-Alyx-Integration)

---

## Plugin System

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

---

## Gamemode System

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

---

## Event Flow

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

---

## Available Events

See [Development Guide - Event Types](Development-Guide#event-types) for the complete list.

---

## Related Documentation

- [Development Guide](Development-Guide) - Creating plugins and extending gamemode
- [Lua Scripting System](Lua-Scripting-System) - Lua event handlers
- [System Components](System-Components) - Core project structure
