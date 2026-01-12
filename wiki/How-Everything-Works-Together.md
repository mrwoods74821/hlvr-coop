# How Everything Works Together

[← System Components](System-Components) | [VConsole Communication →](VConsole-Communication)

---

## Startup Sequence

1. **User launches HLVRCoOpMod.exe**
   - Windows Forms UI initializes
   - Plugins and gamemodes loaded from `plugins/` and `gamemodes/` folders
   - Lua scripts loaded from `scripts/` folder
   - Settings loaded from `.settings` files

2. **User starts Half-Life: Alyx**
   - Game launches with VConsole enabled (`-console -vconsole`)
   - VConsole listens on TCP port (default: 29000)
   - Workshop addon `hlvr_coop` must be enabled

3. **User connects client or starts server**
   - **Client**: Connects to WebSocket server, connects VConsole
   - **Server**: Starts WebSocket server, listens for clients

4. **Client connects to game**
   - VConsole connects via TCP to `localhost:29000`
   - Sends `addon_enable hlvr_coop` to enable Workshop addon
   - Game-side Lua scripts initialize

5. **Game-side initialization**
   - `hlvrcoop_interval.lua` creates player representation entities
   - Registers VConsole commands
   - Begins tracking entities and sending updates

---

## Runtime Data Flow

### Player Movement Synchronization

```
1. Player moves in VR
   ↓
2. Game: Lua script (every 5 frames) reads player position
   ↓
3. Game: print("HEAD x y z pitch yaw roll HLVRCOOP")
   ↓
4. VConsole: Captures print output via TCP
   ↓
5. C#: VConsole.MessageAvailable receives "PRNT" message
   ↓
6. C#: Response("print", message) sent via WebSocket to server
   ↓
7. Server: AlyxGamemode parses packet
   ↓
8. Server: Creates/updates Player object
   ↓
9. Server: Broadcasts to other clients
   ↓
10. Clients: Receive Response("command", "hlvrcoop_setlocation_nonuuid...")
   ↓
11. Clients: VConsole sends command to game
   ↓
12. Game: Moves other players' visual representations
```

### Entity Synchronization

```
1. Player picks up/moves physics object
   ↓
2. Game: Lua monitors entity cache for position changes
   ↓
3. Game: print("PHYS entityName x y z pitch yaw roll HLVRCOOP")
   ↓
4. C#: Receives packet, checks entity ownership
   ↓
5. Server: If client owns entity, broadcasts to others
   ↓
6. Other clients: Receive command to move entity
   ↓
7. Game: Entity position synchronized
```

### Trigger Output Synchronization

```
1. Player activates trigger (e.g., walks through trigger_multiple)
   ↓
2. Game: Entity output fires (OnStartTouch)
   ↓
3. Game: Output redirected to HLVRCOOP_OnStartTouch function
   ↓
4. Game: print("FIRE entityName OnStartTouch HLVRCOOP")
   ↓
5. Server: Parses packet, broadcasts to all clients
   ↓
6. All clients: Execute entity output
   ↓
7. Game: Trigger effects occur for all players
```

---

## Related Documentation

- [VConsole Communication](VConsole-Communication) - Game communication details
- [Networking Architecture](Networking-Architecture) - Client-server flow
- [Packet System](Packet-System) - Data packet processing
- [Entity Synchronization](Entity-Synchronization) - Entity sync details
- [Player Management](Player-Management) - Player state management
