# Entity Synchronization

[← Packet System](Packet-System) | [Player Management →](Player-Management)

---

## Entity Ownership System

To prevent conflicts when multiple players interact with the same entity:

1. **First-come-first-served**: First client to manipulate entity "owns" it
2. **Ownership timeout**: Ownership expires after 5 seconds of inactivity
3. **Ownership transfer**: When timeout occurs, next client can take ownership
4. **Re-enable on timeout**: Entity re-enabled for other clients after timeout

**Implementation**:
```csharp
Moveable class tracks:
- Entity reference
- Owning client GUID
- Timer (5 seconds)
- On timeout: Sends "hlvrcoop_grace" command to re-enable entity
```

---

## Entity Cache System (Game-Side)

The Workshop addon maintains `HLVRCOOP_ENTCACHE`:

```lua
HLVRCOOP_ENTCACHE = {
    [uuid] = {
        name = "entity_name",
        origin = {x, y, z},
        angles = {pitch, yaw, roll},
        class = "prop_physics",
        entity = entity_handle,
        health = 100  -- For NPCs
    }
}
```

**Sync Process**:
1. Entity cached when first detected
2. Position/angles monitored every frame
3. Changes detected (position delta > 1 unit or angle change)
4. Update sent via `print("PHYS ... HLVRCOOP")`
5. Other clients receive update and move entity

---

## Entity Tracking Flow

```
1. Entity spawns/created
   ↓
2. Game: HLVRCOOP_EntitySyncSpecific() called
   ↓
3. Game: Entity added to HLVRCOOP_ENTCACHE
   ↓
4. Game: Entity outputs redirected to HLVRCOOP functions
   ↓
5. Game: Position monitored every frame
   ↓
6. Player interacts → Position changes
   ↓
7. Game: Detects change, sends PHYS packet
   ↓
8. Server: Checks ownership
   ↓
9. Server: If owned by sender, broadcasts update
   ↓
10. Other clients: Receive command, move entity
```

---

## Entity Output Synchronization

Entity outputs (OnTrigger, OnStartTouch, etc.) are synchronized:

1. **Output redirection**: All outputs redirected to HLVRCOOP functions
2. **Output capture**: When output fires, HLVRCOOP function called
3. **Packet sent**: `print("FIRE entity_uuid outputName HLVRCOOP")`
4. **Server broadcasts**: All clients receive output
5. **Clients execute**: `hlvrcoop_fireoutput` command sent to game
6. **Game fires output**: Entity output executed for all players

---

## Supported Entity Classes

See [Workshop Addon System - Synchronized Entity Types](Workshop-Addon-System#synchronized-entity-types) for the complete list.

---

## Related Documentation

- [Workshop Addon System](Workshop-Addon-System) - Game-side entity tracking
- [Packet System](Packet-System) - PHYS and FIRE packets
- [VConsole Communication](VConsole-Communication) - Entity commands
- [Development Guide](Development-Guide) - Adding entity types
