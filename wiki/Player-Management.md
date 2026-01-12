# Player Management

[← Entity Synchronization](Entity-Synchronization) | [Lua Scripting System →](Lua-Scripting-System)

---

## Player Representation

### Server-Side (C#)
```csharp
public class Player : Location
{
    public int Index;              // 0-15 (player slot)
    public IndexedClient Client;   // WebSocket client reference
}
```

### Client-Side (Game)
- `hlvrcoop_head_<index>` - Headset model (prop_dynamic)
- `hlvrcoop_lefthand_<index>` - Left hand model (prop_dynamic)
- `hlvrcoop_righthand_<index>` - Right hand model (prop_dynamic)
- `hlvrcoop_text_<index>` - Name tag (point_worldtext)

---

## Player Limit

- **Maximum**: 16 players
- **Index assignment**: Sequential (0, 1, 2, ...)
- **Full server**: Returns `null` on `AddPlayer()` if 16 players

---

## Player Join Flow

1. Client connects to server
2. Server authenticates client
3. Server creates `IndexedClient`
4. Server calls `AlyxGlobalData.instance.AddPlayer(client)`
5. Server assigns index (players.Count)
6. Server broadcasts join message
7. Server enables Workshop addon for client
8. Client initializes game-side entities
9. Client sends `INIT` packet
10. Server responds with initialization commands
11. Client sends `IENT` packet
12. Server enables game (buddha mode, unpause)

---

## Player Leave Flow

1. Client disconnects (or server kicks)
2. Server calls `OnClose(socket)`
3. Server removes player from `AlyxGlobalData`
4. Server broadcasts leave message
5. Other clients: Player visual entities remain (not removed)
6. Server frees player index (slot available for new player)

---

## Player Data Synchronization

### Position Updates
- **Frequency**: Every 5 frames (~12 times per second at 60 FPS)
- **Data sent**: Head position, hand positions, body position
- **Packet types**: `HEAD`, `HAND`, `PLYR`

### Health Tracking
- Tracked in Lua scripts (`lua_env.persistence["players"][username]["health"]`)
- Updated when `PLYR` packet received

---

## Player State Management

**AlyxGlobalData** manages:
- List of active players
- Player lookup by GUID
- Player lookup by index
- Entity ownership tracking

---

## Related Documentation

- [Networking Architecture](Networking-Architecture) - Connection flow
- [Packet System](Packet-System) - Player position packets
- [Workshop Addon System](Workshop-Addon-System) - Player visual representation
- [Lua Scripting System](Lua-Scripting-System) - Player data tracking
