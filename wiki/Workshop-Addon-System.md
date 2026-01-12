# Workshop Addon System

[← VConsole Communication](VConsole-Communication) | [Networking Architecture →](Networking-Architecture)

---

## What is a Workshop Addon?

Half-Life: Alyx supports Steam Workshop addons that can add scripts, assets, and maps. The mod uses the Workshop addon `hlvr_coop`.

**Official Documentation:**
- [Half-Life: Alyx Workshop Tools](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools) - Complete Workshop addon documentation
- [Workshop Addon Structure](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Addon_Structure) - How to structure addons
- [Workshop Tools Installation](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Installation) - Installing Workshop Tools DLC

---

## Addon Structure

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

---

## Main Script: hlvrcoop_interval.lua

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

---

## Entity Tracking System

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

---

## Synchronized Entity Types

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

---

## Output Events

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

---

## Development Workflow

1. **Edit scripts** in `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/`
2. **Use symlinks** (see README.md) to link to game directory, OR
3. **Copy files** to game's `hlvr_addons/hlvr_coop/` folder
4. **Enable addon** in game: `addon_enable hlvr_coop`
5. **Test changes** - scripts reload on map change

---

## Related Documentation

- [VConsole Communication](VConsole-Communication) - How commands are sent
- [Entity Synchronization](Entity-Synchronization) - Entity sync details
- [Half-Life: Alyx Integration](Half-Life-Alyx-Integration) - VScript API reference
- [Development Guide](Development-Guide) - Modifying the addon
