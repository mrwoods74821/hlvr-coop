# Workshop Addon Compatibility Plugin

This plugin enables and synchronizes Steam Workshop addons for cooperative gameplay in Half-Life: Alyx Co-Op Mod.

## Supported Addons

The plugin currently supports the following Workshop addons:

1. **Player Body - ALYX / GORDON** (Workshop ID: 3581426521)
   - Synchronizes player body visibility across clients
   - Requires: Scalable Init Support or AlyxLib

2. **More Resin** (Workshop ID: 2396122619)
   - Adds additional resin pod locations
   - No synchronization required (map-based)

3. **Unpin Ragdolls** (Workshop ID: 2173405711)
   - Makes ragdolls moveable
   - Requires synchronization for ragdoll physics
   - Requires: Scalable Init Support

4. **Removable Health Vials** (Workshop ID: 3527754624)
   - Allows removing health vials from stations
   - Requires synchronization for vial state
   - Requires: AlyxLib

5. **Partial Clip Storage** (Workshop ID: 3329684800)
   - Allows storing partially empty clips
   - Requires synchronization for clip state
   - Requires: AlyxLib

6. **Scalable Init Support** (Workshop ID: 2182586257)
   - VScript mod-loader dependency
   - No synchronization required

7. **AlyxLib** (Workshop ID: 3329679071)
   - Script library dependency
   - No synchronization required

8. **Charger Melee Charge** (Workshop ID: 3616305735)
   - Adds melee charge behavior to Combine Charger NPCs
   - No synchronization required (NPC behavior handled by existing sync system)
   - Requires: Scalable Init Support

9. **Fast Headcrab Restored** (Workshop ID: 3308356996)
   - Restores fast headcrab NPCs to campaign maps
   - No synchronization required (map extensions only)

10. **Brutal Blood** (Workshop ID: 2857095424)
    - Enhanced blood effects with animated drips and pools
    - No synchronization required (visual effects only)
    - Requires: Scalable Init Support

11. **Bullet Penetration Mod** (Workshop ID: 3008209128)
    - Adds bullet penetration mechanics to weapons
    - No synchronization required (client-side visual effect)
    - Requires: Scalable Init Support

## How It Works

### Important: The Plugin Does NOT Download Addons

**The plugin does NOT automatically download or install Workshop addons.** Users must manually subscribe to addons in Steam Workshop first. The plugin only **enables** addons that are already subscribed and downloaded.

### Setup Process

1. **Subscribe to Addons** (One-time setup):
   - Open Steam Workshop for Half-Life: Alyx
   - Subscribe to all desired addons (Player Body, More Resin, etc.)
   - Steam will automatically download them to your game directory
   - Location: `Steam\steamapps\workshop\content\686810\<workshop_id>\`

2. **Plugin Enables Addons** (Automatic):
   - When a client connects to the server, the plugin sends `addon_enable <workshop_id>` commands
   - The game then loads the addon files that Steam already downloaded
   - Dependencies are enabled first (Scalable Init Support, AlyxLib)
   - Then dependent addons are enabled (Player Body, Unpin Ragdolls, etc.)

3. **Synchronization**: For addons that require synchronization (Player Body, etc.), the plugin handles state synchronization across clients.

### What Happens When a Client Connects

1. Client connects to server
2. Plugin checks which addons should be enabled
3. Plugin sends `addon_enable <workshop_id>` commands via VConsole
4. Game loads the addon files (if subscribed) or ignores the command (if not subscribed)
5. If addon is subscribed, it becomes active in-game

## Configuration

Currently, all supported addons are enabled by default. Future versions will allow per-addon configuration through the UI.

## Implementation Status

### ✅ Completed
- Plugin project structure
- Addon registry with dependencies
- Automatic addon enabling on client connect
- Dependency resolution and ordering
- Addon folder structure analysis from provided sources

### 🚧 In Progress / TODO
- **Player Body Synchronization**: 
  - Player Body uses convar `player_body_preference` (0=auto, 1=Gordon, 2=Alyx)
  - Need to detect convar changes and broadcast to all clients
  - Each client will automatically spawn the correct body model based on the convar
  - **Implementation**: Monitor VConsole for convar changes or create custom packet type

- **Player Body Synchronization**: 
  - Detect body model selection (Alyx vs Gordon)
  - Broadcast body model to all clients
  - Ensure body visibility synchronization
  - **Action Required**: Need to understand how Player Body addon works internally

- **Ragdoll Synchronization** (Unpin Ragdolls):
  - Synchronize ragdoll physics state
  - Handle ragdoll position/rotation
  - **Action Required**: Need to understand ragdoll physics synchronization requirements

- **Health Vial Synchronization** (Removable Health Vials):
  - Track vial state (removed/inserted)
  - Synchronize vial positions
  - **Action Required**: Need to understand vial state management

- **Clip Storage Synchronization** (Partial Clip Storage):
  - Track partial clip states
  - Synchronize clip contents
  - **Action Required**: Need to understand clip storage system

- **Configuration UI**: 
  - Add UI controls to enable/disable specific addons
  - Save addon preferences to settings

## Addon Analysis (From Provided Sources)

### Map-Only Addons (No Special Handling Needed)
- **More Resin** (2396122619): Only contains map extensions (.vpk files)
  - No scripts, no synchronization needed
  - Map extensions are automatically loaded when the addon is enabled
  - **Answer**: No special handling needed - just enable the addon

### Script-Based Addons (May Need Synchronization)
- **Player Body** (3581426521): Uses convar `player_body_preference` for body model selection
  - Convar values: 0=auto, 1=Gordon, 2=Alyx
  - Body model is spawned automatically based on convar value
  - **Synchronization**: Broadcast convar changes to all clients

- **Unpin Ragdolls** (2173405711): Sets convar `vr_hold_ragdoll_large_biped_counter_spring_freq`
  - Convar is set once on init, no per-player state
  - Ragdoll physics are already handled by existing physics sync system
  - **Synchronization**: Not needed (convar is global, physics already synced)

- **Partial Clip Storage** (3329684800): Uses AlyxLib, has init scripts
  - **Synchronization**: May need clip state sync (requires further analysis of main scripts)

- **Removable Health Vials** (3527754624): Uses AlyxLib, has init scripts
  - **Synchronization**: May need vial state sync (requires further analysis of main scripts)

## Usage

### Prerequisites

**Before using the plugin, all players must:**
1. Subscribe to the desired Workshop addons in Steam
2. Wait for Steam to download the addons
3. Ensure addons are downloaded before joining a server

### Plugin Behavior

The plugin is automatically loaded when placed in the `plugins` directory. It will:
- **Enable** required addons when clients connect (sends `addon_enable` commands)
- Handle synchronization for supported features
- Log addon-related activities to the output

### What If an Addon Isn't Subscribed?

- If a client hasn't subscribed to an addon, the `addon_enable` command will be ignored
- The addon simply won't load for that client
- Other clients with the addon subscribed will still have it enabled
- **Result**: Mixed compatibility - some players may see features others don't

## Notes

- **The plugin does NOT download addons** - Steam Workshop handles downloads
- **Addons must be subscribed to in Steam Workshop** before the plugin can enable them
- **All clients should have the same addons subscribed** for best compatibility
- The plugin enables addons by Workshop ID (e.g., `addon_enable 3581426521`)
- If an addon isn't subscribed, the enable command is silently ignored by the game
