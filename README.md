# Half-Life: Alyx Co-Op Mod

**By Dayshower**

> **Note:** This is a fork of [Kiwifruit's HLVRCOOP (Half-Life: Alyx Co-Op Mod)](https://github.com/KiwifruitDev/KCOM).

<a href="https://github.com/dayflaree/hlvr-coop"><img align="left" width="256" src="https://i.imgur.com/8ExaaRw.png"></a>

[![Releases](https://img.shields.io/github/v/tag/teampopplio/HLVRCoop?label=release)](https://github.com/dayflaree/hlvr-coop/releases)

[![Downloads](https://img.shields.io/github/downloads/teampopplio/HLVRCoop/total)](https://github.com/dayflaree/hlvr-coop/releases)

[![Discord](https://img.shields.io/discord/738131767944282183?label=discord)](https://discord.gg/X6Dp95WRR4)


--------

### Half-Life: Alyx Co-Op Mod (HLVRCoop) is a cooperative experience for Half-Life: Alyx that brings multiplayer to the game.

## Features

- Simultaneous gameplay with up to 16 players
- VR hands are synced across clients
- Physics objects are synced across clients
- Trigger outputs are synced across clients
- Custom addon (gamemode/plugin) support
- Lua scripting support
- Support for Workshop maps
- Discord rich presence
- Public addon contents

## Installation

- Download the [latest release](https://github.com/dayflaree/hlvr-coop/releases) from GitHub
- Extract the contents of the zip file to a safe place (e.g. a new folder on your desktop)
- **Install the Workshop addon first** (required):
	- Copy the `WorkshopAddon/game/hlvr_addons/hlvr_coop` folder to your Half-Life: Alyx installation directory under `game/hlvr_addons/`
	- Copy the `WorkshopAddon/content/hlvr_addons/hlvr_coop` folder to your Half-Life: Alyx installation directory under `content/hlvr_addons/`
	- The Workshop addon is required for the mod to function properly
- Set the following launch options for Half-Life: Alyx in Steam: `-console -vconsole -language english`
- Launch the game
- Open HLVRCoop using `HLVRCoOpMod.exe`
- Follow instructions within the HLVRCoop application
- HLVRCoop is ready!

## Connecting to a Server
- Follow the [instructions](#installation) to install HLVRCoop
- Click on the "Client" tab in the HLVRCoop application
- Check the "Enabled" box to allow your client to connect
- Set the IP address to the *public* IP address of the remote server
- Set the port to the port of the remote server
- *(Optional)* If provided, set the password to the password of the remote server
- Set a username for your client
- Click "Connect"
- HLVRCoop will connect to the remote server and start playing!

## Hosting a Server
- Follow the [instructions](#installation) to install HLVRCoop
- *(Optional)* Follow the [client instructions](#connecting-to-a-server) to create a listen server
	- Note: Non-listen servers are not fully developed yet, please follow the above instructions for now
- Click on the "Server" tab in the HLVRCoop application
- Check the "Enabled" box to host a server
- *(Optional)* Set a password for the server, make sure to provide it to peers
- Type in a map name to load initially
- **You need to manually port forward the server port** (typically in your router's settings)
	- Access your router's admin panel
	- Navigate to Port Forwarding/Virtual Server settings
	- Forward the port you're using for the server (default is shown in the application) to your computer's local IP address
	- Ensure the port is open for both TCP and UDP protocols
- Click on the "Start" button to start the server
- HLVRCoop is now hosting a server! You can now provide your *public* IP address to peers

## Client Commands
- Type "/help" into the chat box as a client to view a list of commands
- Default commands:
	- `/echo <message>` - Echo a message
	- `/ping` - Check your ping
	- `/help` - This help menu
	- `/list` - List all players on the server.

## Server Commands
- As a server host, click on the "Chat" button in HLVRCoop until it says "Server" to enter server operator mode
- Type "help" to view a list of server operator commands
- Default commands:
	- `echo <message>` - Echo a message
	- `persistent_set <key> <value>` - Set a persistent Lua value
	- `persistent_get <key>` - Get a persistent Lua value
	- `persistent_remove <key>` - Remove a persistent Lua value
	- `persistent_get_all` - List all persistent Lua values
	- `persistent_clear` - Clear all persistent Lua values
	- `script_refresh <script>` - Refresh a Lua script
	- `script_refresh_all` - Refresh all Lua scripts
	- `kick <username>` - Kick a player from the server
	- `ban <username>` - Ban a player from the server
	- `ipban <username>` - Ban a player from the server by IP
	- `unban <username>` - Remove a player's ban from the server
	- `lua <code>` - Run Lua code
	- `tp <username> (<username>/<x> <y> <z>)` - Teleport a player to a location
	- `tpall (<username>/<x> <y> <z>)` - Teleport all players to a location.

## Lua Scripting

### Requirements
- A code editor (such as [Visual Studio Code](https://code.visualstudio.com/))

### Instructions
- Follow the [instructions](#installation) to install HLVRCoop
- Follow the [server instructions](#hosting-a-server) to host a server
- Open the `scripts` folder in the HLVRCoop installation directory using a code editor

### Configuration
- Open `base/_config.lua` in the `scripts` folder using a code editor
- Follow instructions within the file to configure base Lua settings

### Plugins
- Copy `base/basic.lua` to a new folder (or the root `scripts` folder) under a different name to create a new "plugin" script
- Edit the script to your liking, pay attention to what is being "handled" as handling events will cancel out internal HLVRCoop events and other scripts in alphabetical order

### Lua Documentation
- **[Lua API Reference](wiki/Lua-API-Reference)** - Complete API documentation with examples and tutorials
- [Lua Scripting System](wiki/Lua-Scripting-System) - System overview and integration details
- [Development Guide](wiki/Development-Guide) - How to write Lua scripts

Check out the [Discord](https://discord.gg/X6Dp95WRR4) for Lua help if needed.

### Script Redistribution
You are free to modify and redistribute HLVRCoop's default ("base") Lua scripts without permission, however when it comes to others' scripts, please provide credit to the original author(s) and link to the original source.

Feel free to provide scripts to the HLVRCoop community via the [Discord](https://discord.gg/X6Dp95WRR4) as there is a dedicated channel for Lua scripting.

## Debugging/Modding

### Requirements
- [Visual Studio 2022 or later](https://visualstudio.microsoft.com/downloads/)
	- Select ".NET desktop development" during installation
- [7-Zip](https://www.7-zip.org/download.html)
- [Git](https://git-scm.com/downloads)
- Half-Life: Alyx - Workshop Tools
	- You can find this under the DLC section of Half-Life: Alyx within your Steam library

### Instructions
- Clone the repository within Visual Studio
- Build all projects for HLVRCoop (x64) by pressing `Ctrl+Shift+B` or by clicking "Build" > "Build Solution"
	- Note: You may need to build the `HLVRCoOpModCore` project first, this can be done by right-clicking the project and selecting "Build"
- In Visual Studio, set `HLVRCoOpMod` as the startup project by right-clicking the project and selecting "Set as Startup Project"
- Click on the green "Start" button to start debugging
- Visual Studio will automatically launch HLVRCoop with debugging enabled

### Symlinks
For addon development, you may want to "symlink" folders from this repository to your game:
- Open a command prompt as an administrator
- Type `cd` into the command prompt and press space
- Copy Half-Life: Alyx's `game/hlvr_addons` directory path and paste it into the command prompt, then press enter
	- Note: You can create these folders relative to the `Half-Life Alyx` folder if they don't exist
	- Note: You may need to "wrap" the path in quotes if it contains spaces
- Type `mkdir hlvr_coop` into the command prompt and press enter
- Type `cd hlvr_coop` into the command prompt and press enter
- Type `mklink /J scripts` and press space
- Find the repository's directory, this is likely `C:\Users\{username}\source\repos\HLVRCoop`
- Copy the HLVRCoop repository directory path and paste it into the command prompt
- Type `\WorkshopAddon\game\hlvr_addons\hlvr_coop\scripts` at the end of the command prompt to complete the path and press enter
- Type `cd` into the command prompt and press space
- Copy Half-Life: Alyx's `content/hlvr_addons` directory path and paste it into the command prompt, then press enter
	- Note: You can create these folders relative to the `Half-Life Alyx` folder if they don't exist
	- Note: You may need to "wrap" the path in quotes if it contains spaces
- Type `mklink /J hlvr_coop` and press space
- Copy the HLVRCoop repository directory path and paste it into the command prompt
- Type `\WorkshopAddon\content\hlvr_addons\hlvr_coop` at the end of the command prompt to complete the path and press enter
- Launch Half-Life: Alyx with Workshop tools enabled 
	- Type `-console -vconsole -language english` into the launch options for Half-Life: Alyx in Steam before launching
- Open VConsole by pressing the tilde (`~`) key
- Type `addon_enable hlvr_coop` into VConsole and press enter
- Changes within the Workshop tools will now be reflected to the HLVRCoop repository and vice versa


## Help & Support

Join the [Discord](https://discord.gg/X6Dp95WRR4) for support between users and developers.
