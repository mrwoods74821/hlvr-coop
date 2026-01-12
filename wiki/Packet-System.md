# Packet System

[← Networking Architecture](Networking-Architecture) | [Entity Synchronization →](Entity-Synchronization)

---

## Packet Types

Packets are 4-letter codes sent from game via `print()`:

| Code | Type | Description |
|------|------|-------------|
| `PLYR` | PlayerPosAng | Player body position/angles |
| `HEAD` | HeadPosAng | Headset position/angles |
| `HAND` | HandPosAng | Left/right hand positions |
| `INIT` | Initialization | Client initialization request |
| `IENT` | InitializedEntities | Entities initialized |
| `PHYS` | PhysicsObjectPosAng | Physics object position/angles |
| `PROP` | PhysicsObjectIndexStartPos | Physics object initial position |
| `MAPN` | MapName | Map name detection |
| `FIRE` | EntityFired | Entity output fired |
| `NPHP` | NPCHealth | NPC health update |
| `BRAK` | BrokenProp | Prop broken |
| `EREM` | EntityRemoved | Entity removed |
| `TELE` | Teleport | Teleport command |
| `SPWN` | Spawn | Entity spawn |
| `TENT` | TemplateEntity | Template entity cache |
| `CMND` | HLVRCOOPCommand | Custom command |

---

## Packet Format

Packets are space-separated strings:

```
<CODE> <arg1> <arg2> ... <argN> HLVRCOOP
```

Example:
```
HEAD 1234.5 5678.9 9012.3 0.0 90.0 0.0 HLVRCOOP
FIRE entity_uuid OnStartTouch HLVRCOOP
PHYS prop_physics_001 100.0 200.0 300.0 0.0 0.0 0.0 HLVRCOOP
```

---

## Packet Processing

1. **Game sends**: `print("HEAD x y z pitch yaw roll HLVRCOOP")`
2. **VConsole captures**: Raw TCP message
3. **C# receives**: `Response("print", "HEAD x y z pitch yaw roll HLVRCOOP")`
4. **AlyxGamemode parses**:
   ```csharp
   if (response.data.Contains("HLVRCOOP"))
   {
       List<string> parts = response.data.Split(" ").ToList();
       string code = parts[0];  // "HEAD"
       parts.RemoveAt(0);
       parts.RemoveAt(parts.Count - 1);  // Remove "HLVRCOOP"
       Packet packet = new(code, parts.ToArray());
   }
   ```
5. **Gamemode handles**: Switch statement processes packet type
6. **Server broadcasts**: Sends commands to other clients

---

## Adding New Packet Types

See [Development Guide - Adding New Packet Types](Development-Guide#adding-new-packet-types) for detailed instructions.

---

## Related Documentation

- [VConsole Communication](VConsole-Communication) - How packets are transmitted
- [Entity Synchronization](Entity-Synchronization) - Entity packet handling
- [Player Management](Player-Management) - Player position packets
- [Development Guide](Development-Guide) - How to add new packet types
