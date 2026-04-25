# Source 2 Engine and Half-Life: Alyx VScript System Research

**Research Date:** 2026-04-24  
**Project:** HLVR Co-op Mod  
**Researcher:** source2-researcher

## Executive Summary

This research document covers the Source 2 engine's VScript (Lua) system as implemented in Half-Life: Alyx, focusing on the APIs and patterns used in the HLVR cooperative multiplayer mod. The mod uses VScript as a bridge between the game engine and external WebSocket networking to enable multiplayer functionality in a single-player game.

## 1. Source 2 VScript API Overview

### 1.1 Core VScript Functions

**Global Functions:**
- `print(...)` - Output to console (extensively used for networking)
- `require(modname)` - Load Lua modules
- `DoIncludeScript(scriptFileName, scope)` - Include script files
- `GetMapName()` - Get current map name
- `GetScriptFile(sep)` - Get current script filename

**Math Functions:**
- `Vector(x, y, z)` - Create vector
- `AngleDiff(angle1, angle2)` - Calculate angle difference
- `AnglesToVector(angle)` - Convert angles to vector
- `AxisAngleToQuaternion(axis, angle)` - Create quaternion
- `CrossVectors(vector1, vector2)` - Vector cross product
- `LerpVectors(vector1, vector2, t)` - Linear interpolation
- `RotatePosition(origin, angle, vector)` - Rotate vector
- `VectorToAngles(input)` - Convert vector to angles

### 1.2 Entity System

**Entity Finding Functions:**
```lua
Entities:GetLocalPlayer()                    -- Get player entity
Entities:FindAllByName(name)                 -- Find all entities by name
Entities:FindByNameNearest(name, pos, radius) -- Find nearest by name
Entities:FindAllByClassname(classname)       -- Find all by classname
Entities:FindByClassname(classname)          -- Find first by classname
Entities:FindByClassnameNearest(class, pos, radius) -- Find nearest by class
```

**Entity Properties:**
```lua
entity:GetName()              -- Get targetname
entity:GetClassname()         -- Get entity class
entity:GetEntityIndex()       -- Get entity index
entity:GetModelName()         -- Get model name
entity:GetAbsOrigin()         -- Get world position
entity:GetOrigin()            -- Get position (alias)
entity:GetAnglesAsVector()    -- Get rotation as vector
entity:GetAngles()            -- Get rotation
entity:GetHealth()            -- Get health value
entity:GetChildren()          -- Get child entities
entity:GetMoveParent()        -- Get parent entity
```

**Entity Manipulation:**
```lua
entity:SetAbsOrigin(origin)           -- Set position
entity:SetOrigin(origin)              -- Set position (alias)
entity:SetAbsAngles(pitch, yaw, roll)  -- Set rotation
entity:SetAngles(pitch, yaw, roll)     -- Set rotation (alias)
entity:SetHealth(value)                -- Set health
entity:SetEntityName(name)             -- Set targetname
entity:SetParent(parent, attachment)   -- Parent to entity
```

**Entity I/O System:**
```lua
-- Fire entity inputs
DoEntFireByInstanceHandle(target, action, value, delay, activator, caller)

-- Redirect entity outputs to Lua functions
entity:RedirectOutput(output, functionName, entity)

-- Fire outputs programmatically
entity:FireOutput(outputName, activator, caller, parameter, delay)
```

### 1.3 VR-Specific APIs

**Player VR Functions:**
```lua
player:GetHMDAvatar()     -- Get VR avatar entity
player:GetHMDAnchor()     -- Get HMD anchor entity
```

**VR Avatar Functions:**
```lua
avatar:GetVRHand(handId)  -- Get hand controller (0 = left, 1 = right)
```

**VR Hand Functions:**
```lua
hand:GetAbsOrigin()       -- Get hand position
hand:GetAnglesAsVector()  -- Get hand rotation
hand:GetChildren()        -- Get attached entities (gloves, etc.)
```

**Hand Tracking Utilities:**
```lua
-- Convert VR tip attachment to hand ID accounting for left-handedness
util.GetHandIdFromTip(vr_tip_attachment) -- Returns 0 or 1

-- Get hand from controller (with glove detection)
GetHandFromController(controller) -- Returns hand entity
```

### 1.4 Game Event System

**Event Listening:**
```lua
ListenToGameEvent(eventName, callback, context)
```

**Key Game Events Used:**
- `item_pickup` - Player grabs object
- `item_released` - Player drops object  
- `player_death` - Player dies
- `player_drop_resin_in_backpack` - Resin deposited
- `player_teleport_start/finish` - Teleportation events
- `weapon_switch` - Weapon changed
- `grabbity_glove_pull/catch` - Glove interactions

### 1.5 Think System (Frame-based Execution)

**Think Functions:**
```lua
entity:SetThink(thinkFunction, thinkName, initialDelay)
entity:SetContextThink(thinkName, thinkFunction, initialDelay)
entity:StopThink(thinkName)  -- Stop named think function
```

**Usage Pattern:**
```lua
-- Start a think function
Player:SetThink(MyThinkFunc, "ThinkName", 0.0)

-- Stop a think function  
Player:StopThink("ThinkName")
```

### 1.6 Console Command System

**Command Registration:**
```lua
Convars:RegisterCommand(name, callback, helpText, flags)
```

**Console Output:**
```lua
SendToConsole(commandString)  -- Execute console command
```

**Callback Signature:**
```lua
function callback(command, arg1, arg2, ...)
    -- command: full command string
    -- arg1, arg2, ...: space-separated arguments
end
```

### 1.7 Sound System

**Sound Functions:**
```lua
StartSoundEvent(soundName, entity)                    -- Play on entity
StartSoundEventFromPosition(soundName, position)      -- Play at position
StartSoundEventFromPositionReliable(soundName, pos)   -- Reliable position sound
StopSoundEvent(soundName, entity)                     -- Stop sound
```

### 1.8 Spawning System

**Entity Spawning:**
```lua
SpawnEntityFromTableSynchronous(classname, spawnKeys)
```

**Example:**
```lua
local ent = SpawnEntityFromTableSynchronous("prop_dynamic", {
    origin = "0 0 0",
    targetname = "my_prop",
    model = "models/path/to.vmdl",
    solid = "0"
})
```

### 1.9 Precaching System

**Precache Functions:**
```lua
PrecacheModel(modelName, context)                    -- Precache model
PrecacheResource(resourceType, resourcePath, context) -- Precache resource
PrecacheEntityFromTable(classname, spawnKeys, context) -- Precache entity
```

**Precache Context:**
```lua
function Precache(context)
    PrecacheModel("models/path.vmdl", context)
    -- Called during map load
end
```

### 1.10 Game Rules System

**GameRules Functions:**
```lua
GameRules:GetGameTime()  -- Get current game time in seconds
```

## 2. Half-Life: Alyx Specific Implementation

### 2.1 Differences from Source 1 VScript

**Key Improvements in Source 2:**
- Full Lua 5.1 support (vs. limited Squirrel in Source 1)
- More comprehensive entity API
- Better VR integration
- Improved think system with named functions
- Enhanced event system with more data

**HL:A Specific Limitations:**
- No direct file I/O from VScript
- Limited networking capabilities
- Single-player architecture (engine limitation)
- No multiplayer-specific functions

### 2.2 VR Integration Details

**Hand Tracking:**
- Left hand: `GetVRHand(0)` or `GetVRHand(1)` based on handedness
- Right hand: `GetVRHand(1)` or `GetVRHand(0)` based on handedness
- Handedness controlled by `hlvr_left_hand_primary` convar

**Avatar System:**
- HMDAvatar represents player's VR presence
- Contains hands, head tracking
- Used for position/rotation synchronization

**Body Tracking:**
- HMDAnchor provides stable reference point
- Used for teleportation and movement
- Can be parented to moving platforms

### 2.3 Entity I/O System

**Output Redirection Pattern:**
```lua
entity:RedirectOutput("OnTrigger", "MyFunction", thisEntity)

function MyFunction(params)
    local activator = params.activator
    local caller = params.caller  
    -- Handle output
end
```

**Input Firing Pattern:**
```lua
DoEntFireByInstanceHandle(entity, "Enable", "", 0, nil, nil)
DoEntFireByInstanceHandle(entity, "SetHealth", "100", 0, nil, nil)
```

### 2.4 VConsole Integration

**Communication Protocol:**
- Uses `print()` statements with specific prefixes
- External C# application monitors console output
- Commands sent via TCP console connection
- Custom commands registered via `Convars:RegisterCommand()`

**Protocol Pattern:**
```lua
-- Output format: PREFIX args... sequence timestamp TAG
print("HEAD x y z pitch yaw roll seq time HLVRCOOP")
print("HAND lx ly lz lp lyaw lr rx ry rz rp ryaw rr seq time HLVRCOOP")
print("PLYR x y z pitch yaw roll health seq time HLVRCOOP")
```

## 3. Common VScript Patterns in HLVR Co-op

### 3.1 Entity Caching Pattern

```lua
-- Cache entities with UUID system
HLVRCOOP_ENTCACHE = {}

function HLVRCOOP_EntitySyncSpecific(entity)
    local object = {}
    object.name = entity:GetName()
    object.origin = entity:GetAbsOrigin()
    object.angles = entity:GetAnglesAsVector()
    object.class = entity:GetClassname()
    object.model = entity:GetModelName()
    object.entity = entity
    
    -- Generate UUID
    local uu = uuid(object.name, object.origin[1], object.origin[2], object.origin[3], object.class)
    HLVRCOOP_ENTCACHE[uu] = object
end
```

### 3.2 Interpolation Pattern

```lua
-- Interpolation state management
hlvrcoop_interp = {}

function InterpolateEntity(interp_state, entity, current_time)
    if interp_state.target_pos == nil then return end
    
    -- First update - snap immediately
    if interp_state.prev_pos == nil then
        entity:SetAbsOrigin(interp_state.target_pos)
        entity:SetAbsAngles(interp_state.target_ang[1], 
                           interp_state.target_ang[2], 
                           interp_state.target_ang[3])
        return
    end
    
    -- Calculate interpolation factor
    local t = (current_time - interp_state.last_update) / 0.1
    
    -- Lerp position and angles
    local lerp_pos = LerpVectors(interp_state.prev_pos, 
                                interp_state.target_pos, t)
    entity:SetAbsOrigin(lerp_pos)
end
```

### 3.3 Event Handling Pattern

```lua
-- Listen for game events
local function HLVRCOOP_PickupSync(data)
    local handId = util.GetHandIdFromTip(data.vr_tip_attachment)
    local hand = Player.Hands[handId + 1]
    local ent_held = util.EstimateNearestEntity(data.item_name, 
                                                data.item, 
                                                hand:GetOrigin())
    if IsValidEntity(ent_held) then
        HLVRCOOP_EntitySyncSpecific(ent_held)
        -- Sync to other players
    end
end

ListenToGameEvent("item_pickup", HLVRCOOP_PickupSync, nil)
```

### 3.4 Respawn System Pattern

```lua
-- Health monitoring and respawn
function HLVRCOOP_CheckDeath()
    local currentHealth = Player:GetHealth()
    
    -- Detect death
    if previousHealth > 0 and currentHealth <= 0 then
        if not respawnScheduled then
            respawnScheduled = true
            
            -- Restore health immediately
            Player:SetHealth(100)
            
            -- Schedule teleport after 1 second
            Player:SetThink(HLVRCOOP_RespawnTeleport, 
                          respawnThinkName, 1.0)
        end
    end
    
    previousHealth = currentHealth
end
```

## 4. Common Pitfalls and Limitations

### 4.1 Entity Lifetime Issues

**Problem:** Entities can be deleted or invalidated
```lua
-- Always check validity
if IsValidEntity(entity) then
    -- Safe to use
end

-- Cache may contain stale references
if not IsValidEntity(object.entity) then
    -- Handle deletion
    object = nil
end
```

### 4.2 Thread Safety

**Issue:** VScript runs on main game thread
- No concurrent execution
- All operations are synchronous
- Be careful with long-running operations

### 4.3 VR Hand Identification

**Challenge:** Hand indices change with handedness setting
```lua
-- Use utility function
local handId = util.GetHandIdFromTip(vr_tip_attachment)

-- Check left-handedness convar
if not Convars:GetBool("hlvr_left_hand_primary") then
    handId = 1 - handId
end
```

### 4.4 Network Synchronization

**Limitation:** No built-in multiplayer support
- Must use custom console-based protocol
- Requires external WebSocket server
- Limited bandwidth (console output rate)
- No server-side authority

### 4.5 Performance Considerations

**Optimization Techniques:**
```lua
-- Variable-rate updates (reduce bandwidth)
if hlvrcoop_tick % 10 == 0 then
    -- Send HEAD data at 10Hz
end

if hlvrcoop_tick % 20 == 0 then
    -- Send PLYR data at 5Hz  
end

-- Interpolate on receiving end to smooth movement
```

## 5. VScript Function Verification

### 5.1 Verified Functions (Used in Codebase)

**Entity Functions:**
- ✅ `Entities:GetLocalPlayer()`
- ✅ `Entities:FindAllByName()`
- ✅ `Entities:FindByNameNearest()`
- ✅ `Entities:FindAllByClassname()`
- ✅ `entity:GetName()`
- ✅ `entity:GetClassname()`
- ✅ `entity:GetAbsOrigin()`
- ✅ `entity:GetAnglesAsVector()`
- ✅ `entity:GetHealth()`
- ✅ `entity:SetHealth()`
- ✅ `entity:SetAbsOrigin()`
- ✅ `entity:SetAbsAngles()`
- ✅ `entity:GetEntityIndex()`
- ✅ `entity:GetModelName()`
- ✅ `entity:GetChildren()`
- ✅ `entity:GetMoveParent()`
- ✅ `entity:SetEntityName()`

**VR Functions:**
- ✅ `player:GetHMDAvatar()`
- ✅ `player:GetHMDAnchor()`
- ✅ `avatar:GetVRHand()`

**I/O Functions:**
- ✅ `DoEntFireByInstanceHandle()`
- ✅ `entity:RedirectOutput()`
- ✅ `entity:FireOutput()`

**System Functions:**
- ✅ `ListenToGameEvent()`
- ✅ `Convars:RegisterCommand()`
- ✅ `Convars:GetBool()`
- ✅ `SendToConsole()`
- ✅ `SpawnEntityFromTableSynchronous()`
- ✅ `GameRules:GetGameTime()`
- ✅ `GetMapName()`
- ✅ `print()`
- ✅ `require()`
- ✅ `DoIncludeScript()`

**Think Functions:**
- ✅ `entity:SetThink()`
- ✅ `entity:StopThink()`

**Sound Functions:**
- ✅ `StartSoundEvent()`
- ✅ `StopSoundEvent()`

**Precache Functions:**
- ✅ `PrecacheModel()`

### 5.2 Functions Requiring Verification

**Math Functions:**
- ⚠️ `LerpVectors()` - Used in interpolation, verify availability
- ⚠️ `Vector()` constructor - Verify constructor syntax
- ⚠️ `AngleDiff()` - Used in utilities, verify in HL:A

**Advanced Entity Functions:**
- ⚠️ `entity:SetParent()` - Used in compatibility addons
- ⚠️ `entity:SetModel()` - Used in some utilities
- ⚠️ `entity:EmitSound()` - Alternative sound system

**Game Event Data:**
- ⚠️ Event data structures vary by event type
- ⚠️ Some events may not fire in all contexts

## 6. Best Practices for HLVR VScript Development

### 6.1 Error Handling

```lua
-- Always validate entities
if not IsValidEntity(entity) then
    return
end

-- Protect against nil values
local health = Player:GetHealth() or 100
if type(health) ~= "number" then
    return
end

-- Use pcall for risky operations
local success, result = pcall(require, modname)
if not success then
    -- Handle error
end
```

### 6.2 Performance Optimization

```lua
-- Cache frequently accessed entities
local Player = Entities:GetLocalPlayer()

-- Use variable-rate updates
if tick % rate == 0 then
    -- Process
end

-- Avoid expensive operations in loops
-- Pre-calculate values outside loops
```

### 6.3 Code Organization

```lua
-- Use modules for organization
require "util.util"
require "util.entities"
require "util.player"

-- Use meaningful function names
function HLVRCOOP_SyncPlayerPosition()
    -- Clear purpose
end

-- Add comments for complex logic
```

### 6.4 Networking Protocol Design

```lua
-- Use structured packet format
-- PREFIX arg1 arg2 arg3 ... sequence timestamp TAG

-- Include sequence numbers for ordering
HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1
print("PACKET data " .. HLVRCOOP_SEQ .. " " .. GameRules:GetGameTime() .. " TAG")

-- Use consistent prefixes
-- HEAD - Head position
-- HAND - Hand positions  
-- PLYR - Player body
-- PHYS - Physics object
-- FIRE - Entity output fired
```

## 7. Research Conclusions

### 7.1 Key Findings

1. **VScript is Powerful**: Lua scripting in HL:A provides comprehensive entity manipulation and game event access
2. **VR Integration is Deep**: Extensive VR-specific APIs for hand tracking, avatar management
3. **Networking is Limitation**: No built-in multiplayer, requires custom console-based protocol
4. **Entity System is Robust**: Full entity finding, manipulation, and I/O capabilities
5. **Think System is Flexible**: Named think functions enable frame-based logic

### 7.2 Technical Debt Opportunities

1. **Error Handling**: Many functions lack nil checks and validation
2. **Performance**: Some operations could be optimized (entity caching, update rates)
3. **Documentation**: Several utility functions need better documentation
4. **API Verification**: Some edge-case functions need testing in HL:A context

### 7.3 Recommended Improvements

1. **Validation Layer**: Add comprehensive entity and parameter validation
2. **Performance Profiling**: Measure VScript execution impact on game performance
3. **API Abstraction**: Create wrapper functions for commonly used patterns
4. **Error Recovery**: Implement graceful failure modes for network issues
5. **Documentation**: Create comprehensive API documentation for project-specific functions

## 8. References

### 8.1 Official Documentation

- [Half-Life: Alyx Workshop Tools](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools)
- [VScript API Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)
- [Entity System](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Entity_System)
- [Entity I/O](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Entity_I/O)

### 8.2 Project Files

- `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/hlvrcoop_interval.lua` - Main networking logic
- `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/util/entities.lua` - Entity utilities
- `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/util/util.lua` - General utilities
- `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vlua_globals.lua` - VScript API definitions

### 8.3 Community Resources

- Source 2 Modding Discord
- HL:A Workshop Tools Community
- VScript Documentation Wiki

---

**Research Status:** Complete  
**Next Steps:** Cross-reference with external documentation and test edge cases