# VConsole Communication

[← How Everything Works Together](How-Everything-Works-Together) | [Workshop Addon System →](Workshop-Addon-System)

---

## What is VConsole?

VConsole is Half-Life: Alyx's developer console accessible via TCP. The mod uses it for bidirectional communication with the game.

**Official Documentation:**
- [Valve Console (VConsole)](https://developer.valvesoftware.com/wiki/Valve_Console) - General VConsole information
- **Note:** The VConsole protocol format used by this mod may be reverse-engineered. There may not be official documentation for the TCP protocol format.

**⚠️ Verification:** The protocol implementation in `VConsole.cs` was discovered through reverse engineering. When modifying VConsole communication, test thoroughly as the protocol may not be officially documented.

---

## VConsole Protocol

The mod implements a custom protocol over TCP:

### Command Format (C# → Game)

```
[4 bytes: "CMND" reversed] [Protocol byte] [Length byte] [Command string] [0x00]
```

Example:
```
Bytes: [0x44 0x4E 0x4D 0x43] [0x01] [0x15] [0x61 0x64 0x64 0x6F 0x6E...] [0x00]
       ("CMND" reversed)      (protocol)  (length)  ("addon_enable...")   (null)
```

### Print Format (Game → C#)

Game sends via `print()` function:
```
PRNT [Message data]
```

VConsole captures "PRNT" messages and extracts the data.

---

## VConsole Commands

The mod sends various commands to the game:

**⚠️ Verification:** These commands are used by the mod. Verify command availability and syntax in the game before relying on them. Some commands may be undocumented or game-specific.

### Game Control
- `addon_enable <addon_name_or_workshop_id>` - Enable Workshop addon (use folder name for local addons, or Workshop ID for subscribed addons)
  - **Verified:** Used successfully in `AlyxGamemode.cs` line 51
- `addon_play <map_name>` - Load/addon map
  - **Verified:** Used successfully in `ServerProgram.ChangeMap()` line 122
- `addon_tools_map <map_name>` - Load map in editor mode
  - **Verified:** Used successfully in `ServerProgram.ChangeMap()` line 122
- `buddha 1` - Enable god mode (prevents death)
  - **Verified:** Used in `AlyxGamemode.cs` line 250
- `unpause` - Unpause game
  - **Verified:** Used in `AlyxGamemode.cs` line 250
- `sv_cheats 1` - Enable cheats
  - **Verified:** Used in `AlyxGamemode.cs` line 81

### Entity Management
- `ent_create <classname> {targetname "name" origin "x y z"}` - Create entity
- `ent_remove <targetname>` - Remove entity
- `ent_fire <targetname> <input> <value>` - Fire entity input

### Custom HLVRCOOP Commands (Registered in Workshop Addon)
- `hlvrcoop_teleport <x> <y> <z>` - Teleport player
- `hlvrcoop_teleportangles <x> <y> <z> <pitch> <yaw> <roll>` - Teleport with angles
- `hlvrcoop_setlocation <entityName> <x> <y> <z> <pitch> <yaw> <roll>` - Move entity (UUID)
- `hlvrcoop_setlocation_nonuuid <entityName> <x> <y> <z> <pitch> <yaw> <roll>` - Move entity (by name)
- `hlvrcoop_fireoutput <entityName> <outputName>` - Fire entity output
- `hlvrcoop_grace <entityName>` - Re-enable entity (after ownership timeout)
- `hlvrcoop_npc_sethealth <entityName> <health>` - Set NPC health
- `hlvrcoop_break <entityName>` - Break prop
- `hlvrcoop_sethealth <health>` - Set player health
- `hlvrcoop_cache_entity <entityName>` - Cache entity for sync
- `hlvrcoop_cache_entity_nonuuid <entityName>` - Cache entity (by name)

---

## Connection Process

1. **Check if port is listening**
   ```csharp
   IPEndPoint[] endPoints = IPGlobalProperties.GetActiveTcpListeners();
   // Check if VConsole port (29000) is in list
   ```

2. **Connect via TCP**
   ```csharp
   TcpClient client = new TcpClient("127.0.0.1", Settings.Default.VconsolePort);
   NetworkStream stream = client.GetStream();
   ```

3. **Set up stream watcher**
   - `StreamWatcher` monitors stream for incoming messages
   - Parses "PRNT" messages and fires events

4. **Send window focus**
   - Sends "VFCS" command to indicate focus state

5. **Queue commands**
   - Commands can be queued if connection not ready
   - Sent once connection established

---

## Message Parsing

VConsole messages are parsed in `AlyxGamemode.cs`:

```csharp
// Game output captured as "print" response type
if (response.data.Contains("HLVRCOOP"))
{
    // Split into packet parts
    List<string> packetList = response.data.Split(" ").ToList();
    string packetType = packetList[0];  // e.g., "HEAD", "HAND", "PHYS"
    packetList.Remove(packetType);
    
    // Create packet
    Packet packet = new(packetType, packetList.ToArray());
    
    // Process based on packet type
    switch (packet.type)
    {
        case PacketType.HeadPosAng:
            // Handle head position
            break;
        // ...
    }
}
```

---

## Related Documentation

- [Packet System](Packet-System) - Packet types and formats
- [Workshop Addon System](Workshop-Addon-System) - Game-side command registration
- [Networking Architecture](Networking-Architecture) - How VConsole fits into networking
