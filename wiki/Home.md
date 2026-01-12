# Half-Life: Alyx Co-Op Mod Documentation

**Complete documentation for understanding, using, and developing the HLVR Co-Op Mod**

---

## ⚠️ Important: Official Documentation & Verification

**Before making changes to this mod, verify your code against official Half-Life: Alyx and Source 2 documentation:**

### Essential Official Resources

- **[Half-Life: Alyx Lua Scripting (VScript API)](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)** - Official VScript API documentation
- **[Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)** - Step-by-step guide
- **[Half-Life: Alyx Workshop Tools Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)** - Complete API reference
- **[Source 2 Engine Documentation](https://developer.valvesoftware.com/wiki/Source_2)** - General Source 2 information
- **[Valve Console (VConsole)](https://developer.valvesoftware.com/wiki/Valve_Console)** - VConsole information

**⚠️ Note:** This documentation describes how the mod currently works. Some implementation details may be reverse-engineered rather than officially documented. Always verify against official sources when making changes.

---

## What is This Mod?

The Half-Life: Alyx Co-Op Mod (HLVRCoop) is a multiplayer modification that enables cooperative gameplay in Half-Life: Alyx, allowing up to 16 players to play together simultaneously. The mod synchronizes:

- **Player positions and rotations** (head, hands, body)
- **Physics objects** (props, items, doors)
- **Entity outputs** (triggers, buttons, logic entities)
- **NPC states** (health, death)
- **Game events** (entity removal, prop breaking)

### Key Features

- **Up to 16 simultaneous players**
- **Real-time VR hand synchronization**
- **Physics object networking**
- **Trigger output synchronization**
- **Custom addon (gamemode/plugin) support**
- **Lua scripting for server-side logic**
- **Steam Workshop map support**
- **Discord Rich Presence integration**
- **Player name tags and visual representation**

### Technical Overview

The mod uses a **hybrid architecture**:

1. **C# Application** - Windows Forms desktop application handling networking and game communication
2. **Workshop Addon** - Lua/VScript addon running inside Half-Life: Alyx
3. **VConsole Protocol** - TCP-based communication between C# and the game
4. **WebSocket Networking** - Client-server architecture for multiplayer
5. **Lua Scripting** - Server-side scriptable logic

---

## Documentation Index

### Core Architecture
- [Architecture Overview](Architecture-Overview) - High-level system design
- [System Components](System-Components) - Core projects and structure
- [How Everything Works Together](How-Everything-Works-Together) - Startup and runtime flows

### Communication & Networking
- [VConsole Communication](VConsole-Communication) - Game-to-mod communication protocol
- [Networking Architecture](Networking-Architecture) - WebSocket client-server system
- [Packet System](Packet-System) - Data packet types and formats

### Game Integration
- [Workshop Addon System](Workshop-Addon-System) - Game-side Lua scripts
- [Half-Life: Alyx Integration](Half-Life-Alyx-Integration) - Source 2 engine integration
- [Entity Synchronization](Entity-Synchronization) - Entity tracking and sync
- [Player Management](Player-Management) - Player state and synchronization

### Development
- [Lua Scripting System](Lua-Scripting-System) - Server-side Lua integration
- [Plugin & Gamemode System](Plugin-Gamemode-System) - Extensibility framework
- [Development Guide](Development-Guide) - How to add features and extend the mod

### Reference & Support
- [Troubleshooting](Troubleshooting) - Common issues and solutions
- [Quick Reference](Quick-Reference) - File locations, classes, and links

---

## Quick Links

- **Repository**: https://github.com/dayflaree/hlvr-coop
- **Discord**: https://discord.gg/X6Dp95WRR4
- **Half-Life: Alyx**: https://store.steampowered.com/app/546560/HalfLife_Alyx/

---

## Important Notes

- ⚠️ **This documentation describes how the mod works, not official Source 2 specifications**
- ⚠️ **Some implementation details may be reverse-engineered rather than officially documented**
- ⚠️ **Always verify VScript functions against official documentation before using them**
- ⚠️ **VScript in HL:A is limited compared to other Source 2 games - check limitations**
- ⚠️ **VConsole protocol format may be reverse-engineered - test thoroughly when modifying**

---

**Happy modding! -Dayshower**
