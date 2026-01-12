# Half-Life: Alyx Integration

[← Plugin & Gamemode System](Plugin-Gamemode-System) | [Development Guide →](Development-Guide)

---

## Source 2 Engine

Half-Life: Alyx runs on **Source 2** engine, which supports:
- **VScript**: Lua-based scripting
- **VConsole**: Developer console via TCP
- **Workshop**: Steam Workshop addon support
- **Entity I/O**: Input/output system for entities

**Official Documentation:**
- [Source 2 Engine](https://developer.valvesoftware.com/wiki/Source_2) - General Source 2 information
- [Half-Life: Alyx Workshop Tools](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools) - HL:A specific tools and APIs

---

## VScript API

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

### Entity Functions (used in `hlvrcoop_interval.lua`)
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

### VR Functions (used in `hlvrcoop_interval.lua`)
- `Player:GetHMDAvatar()` - Get VR avatar
  - **Verified:** Used in `hlvrcoop_interval.lua` line 26
- `avatar:GetVRHand(index)` - Get hand controller
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 29-30
- `hand:GetAbsOrigin()` - Get hand position
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 36-37
- `hand:GetAnglesAsVector()` - Get hand angles
  - **Verified:** Used in `hlvrcoop_interval.lua` lines 38-39

### Game Functions (used in mod)
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

---

## Entity System

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

---

## VR-Specific Features

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

---

## Game Events

The mod listens to game events:

```lua
ListenToGameEvent("item_pickup", function(data)
    -- Handle item pickup
end, nil)

ListenToGameEvent("player_drop_resin_in_backpack", function(data)
    -- Handle resin collection
end, nil)
```

---

## Limitations

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

## Related Documentation

- [Workshop Addon System](Workshop-Addon-System) - Game-side scripts
- [VConsole Communication](VConsole-Communication) - Game communication
- [Development Guide](Development-Guide) - Using VScript functions
