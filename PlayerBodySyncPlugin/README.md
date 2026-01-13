# Player Body Sync Plugin

This plugin synchronizes player body preferences across clients in multiplayer sessions, enabling players to see each other's bodies when using the Player Body Workshop addon.

## Overview

The Player Body Workshop addon (ID: 3581426521) allows players to see their own body in VR. This plugin extends that functionality to multiplayer by synchronizing body preferences across all connected clients.

## How It Works

### Body Preference Values

The Player Body addon uses the `player_body_preference` convar with the following values:
- **0** = Auto (detects body type based on gloves)
- **1** = Gordon Freeman
- **2** = Alyx Vance

### Synchronization Process

1. **On Client Connect:**
   - Plugin queries the new client's current body preference
   - All existing players' body preferences are sent to the new client
   - The new client's preference is broadcast to all existing players

2. **On Preference Change:**
   - When a player changes their body preference (via the addon's menu or convar), the plugin detects it
   - The change is broadcast to all other connected clients
   - Each client sets the `player_body_preference` convar, and the Player Body addon automatically spawns the correct body model

3. **On Client Disconnect:**
   - The disconnected player's preference is removed from tracking

## Detection Methods

The plugin uses multiple methods to detect body preference changes:

1. **Custom BODY Packet** (Recommended for future enhancement):
   - Game-side Lua scripts can send: `print("BODY <preference> HLVRCOOP")`
   - This provides immediate, reliable detection of preference changes
   - **Note:** This requires adding Lua code to the Player Body addon (future enhancement)

2. **VConsole Convar Responses:**
   - Monitors VConsole print responses for convar value changes
   - Detects patterns like `"player_body_preference" = "1"`

3. **Convar Query on Connect:**
   - Queries each client's preference when they connect using `convar_get player_body_preference`

## Requirements

- **Player Body Workshop Addon** (ID: 3581426521) must be subscribed and enabled
- **WorkshopAddonCompatibilityPlugin** should be enabled to automatically enable the Player Body addon

## Usage

The plugin works automatically once loaded. No configuration is required.

### Automatic Behavior

- Automatically queries preferences when clients connect
- Automatically broadcasts preference changes to all clients
- Automatically cleans up when clients disconnect

### Manual Testing

To test the synchronization:

1. Start a server with the plugin loaded
2. Connect multiple clients
3. Have one client change their body preference (via the Player Body addon menu)
4. All other clients should see the body model change automatically

## Technical Details

### Plugin Events

The plugin listens to the following events:
- `Server_PostGamemode_ClientOpen` - When a client connects
- `Server_PostGamemode_ClientClose` - When a client disconnects
- `Server_PostGamemode_PreResponse` - To monitor VConsole responses

### Data Storage

- Preferences are stored in memory per client connection (using client GUID)
- Preferences are cleared when clients disconnect
- No persistent storage (preferences reset on server restart)

## Future Enhancements

1. **Custom BODY Packet Support:**
   - Add Lua code to Player Body addon to send BODY packets when preference changes
   - This would provide more reliable detection than VConsole monitoring

2. **Preference Persistence:**
   - Save preferences to settings file
   - Restore preferences on server restart

3. **UI Integration:**
   - Add UI controls to view/manage player body preferences
   - Display which body each player is using

## Troubleshooting

### Bodies Not Syncing

1. **Check Plugin Load:**
   - Verify `PlayerBodySyncPlugin.dll` is in the `plugins` folder
   - Check server logs for plugin initialization

2. **Check Player Body Addon:**
   - Ensure Player Body addon is subscribed in Steam Workshop
   - Verify WorkshopAddonCompatibilityPlugin is enabled

3. **Check VConsole:**
   - Verify VConsole communication is working
   - Check if `convar_get player_body_preference` returns a value

### Preference Not Detected

- The plugin relies on VConsole responses to detect changes
- If changes aren't detected, consider adding BODY packet support to the Player Body addon's Lua scripts

## Integration with WorkshopAddonCompatibilityPlugin

This plugin works in conjunction with `WorkshopAddonCompatibilityPlugin`:
- WorkshopAddonCompatibilityPlugin enables the Player Body addon
- PlayerBodySyncPlugin synchronizes body preferences across clients

Both plugins should be enabled for full functionality.
