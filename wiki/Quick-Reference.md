# Quick Reference

[← Troubleshooting](Troubleshooting) | [Home →](Home)

---

## File Locations

| Purpose | Location |
|---------|----------|
| Main Application | `HLVRCoOpMod/` |
| Core Library | `HLVRCoOpModCore/` |
| Gamemode | `AlyxGamemode/` |
| Workshop Addon | `WorkshopAddon/` |
| Lua Scripts | `HLVRCoOpMod/scripts/` |
| Base Scripts | `HLVRCoOpMod/base_scripts/` |
| Plugins | `HLVRCoOpMod/plugins/*.dll` |
| Gamemodes | `HLVRCoOpMod/gamemodes/*.dll` |

---

## Key Classes

| Class | Purpose | Location |
|-------|---------|----------|
| `AlyxGamemode` | Main gamemode logic | `AlyxGamemode/AlyxGamemode.cs` |
| `ServerProgram` | Server hosting | `HLVRCoOpMod/ServerProgram.cs` |
| `ClientProgram` | Client connection | `HLVRCoOpMod/ClientProgram.cs` |
| `VConsole` | Game communication | `HLVRCoOpMod/VConsole.cs` |
| `LuaEnvironment` | Lua execution | `HLVRCoOpMod/LuaEnvironment.cs` |
| `Packet` | Packet definitions | `AlyxGamemode/Packet.cs` |
| `Response` | Message format | `HLVRCoOpModCore/Response.cs` |

---

## Useful Links

- **Repository**: https://github.com/dayflaree/hlvr-coop
- **Discord**: https://discord.gg/X6Dp95WRR4
- **Half-Life: Alyx**: https://store.steampowered.com/app/546560/HalfLife_Alyx/

---

## Official Documentation Links

- **[Half-Life: Alyx Lua Scripting (VScript API)](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting)** - Official VScript API documentation
- **[Getting Started with Lua Scripting](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Lua_Scripting/Getting_Started)** - Step-by-step tutorial
- **[Workshop Tools Reference](https://developer.valvesoftware.com/wiki/Half-Life%3A_Alyx_Workshop_Tools/Reference)** - Complete API reference
- **[Source 2 Engine](https://developer.valvesoftware.com/wiki/Source_2)** - General Source 2 information
- **[Valve Console (VConsole)](https://developer.valvesoftware.com/wiki/Valve_Console)** - VConsole documentation

---

## Verification Checklist

Before implementing any change:
1. ✅ Check official documentation for API availability
2. ✅ Verify function exists in HL:A (not just Source 2)
3. ✅ Test incrementally (small changes, verify, repeat)
4. ✅ Ask community if unsure ([Discord](https://discord.gg/X6Dp95WRR4))
5. ✅ Document assumptions and verify them

---

## Important Notes

- ⚠️ **This documentation describes how the mod works, not official Source 2 specifications**
- ⚠️ **Some implementation details may be reverse-engineered rather than officially documented**
- ⚠️ **Always verify VScript functions against official documentation before using them**
- ⚠️ **VScript in HL:A is limited compared to other Source 2 games - check limitations**
- ⚠️ **VConsole protocol format may be reverse-engineered - test thoroughly when modifying**

---

## Documentation Index

### Core Architecture
- [Architecture Overview](Architecture-Overview)
- [System Components](System-Components)
- [How Everything Works Together](How-Everything-Works-Together)

### Communication & Networking
- [VConsole Communication](VConsole-Communication)
- [Networking Architecture](Networking-Architecture)
- [Packet System](Packet-System)

### Game Integration
- [Workshop Addon System](Workshop-Addon-System)
- [Half-Life: Alyx Integration](Half-Life-Alyx-Integration)
- [Entity Synchronization](Entity-Synchronization)
- [Player Management](Player-Management)

### Development
- [Lua Scripting System](Lua-Scripting-System)
- [Plugin & Gamemode System](Plugin-Gamemode-System)
- [Development Guide](Development-Guide)

### Reference & Support
- [Troubleshooting](Troubleshooting)
- [Quick Reference](Quick-Reference) (this page)

---

**Happy modding! -Dayshower**
