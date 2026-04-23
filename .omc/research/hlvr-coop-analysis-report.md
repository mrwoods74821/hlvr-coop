# HLVR Co-op Mod - Synchronization Gap Analysis Report

**Date:** 2026-04-23
**Project:** dayflaree/hlvr-coop
**Analysis Scope:** Multiplayer synchronization architecture, networking, and game state management
**Repository:** https://github.com/dayflaree/hlvr-coop

---

## Executive Summary

The HLVR Co-op Mod implements a custom multiplayer layer for Half-Life: Alyx, a game that was designed as single-player only with no native networking capabilities. The mod achieves this through a creative but fragile architecture combining WebSocket client-server communication, TCP-based VConsole protocol injection, and Lua script packet parsing via print() statement interception.

**Overall Assessment:** The mod successfully enables basic cooperative gameplay but suffers from numerous synchronization gaps, missing critical reliability mechanisms, and fundamental architectural limitations. The most critical issue is that **map transition synchronization is completely disabled** (commented out code at AlyxGamemode.cs:531-543), meaning players who load different maps will not be synchronized. Additionally, there are no interpolation mechanisms for player movement (causing visible jitter), no packet ordering or reliability guarantees, no latency compensation, and significant race conditions in physics ownership and connection state management.

The mod's architecture relies on a chain of fragile communication channels: VR tracking data → Lua script → print() statements → WebSocket → C# server → broadcast → VConsole TCP injection → entity updates. Each layer introduces latency and potential failure points. While this approach was necessary given Source 2's lack of native multiplayer, it results in a multiplayer experience that is functional but fundamentally limited in smoothness and reliability.

**Critical Finding:** This mod is a continuation of the original (cancelled) KCOM project by KiwifruitDev. The active fork maintains the basic co-op functionality but has not addressed several fundamental architectural limitations. Users should expect:
- Visible jitter in remote player movement
- Map transitions requiring manual coordination
- Physics interactions that may not sync properly with multiple players
- No rollback or reconciliation for lag compensation
- Entity spawn/death states that may desync

---

## Architecture Overview

### Communication Flow

The mod uses a multi-layer communication architecture:

1. **VR Tracking → Lua Script (Game)**
   - VR tracking data (headset, controllers) captured by game
   - Lua script (`hlvrcoop_interval`) processes positions every tick
   - Packets formatted as print() statements with "HLVRCOOP" tag

2. **Lua → WebSocket (Client → Server)**
   - `StreamWatcher` (HLVRCoOpMod/StreamWatcher.cs) monitors VConsole output
   - Messages containing "HLVRCOOP" parsed into packets
   - Sent via WebSocket to C# server

3. **Server Processing (AlyxGamemode.cs)**
   - Receives packets, updates player/entity state
   - Broadcasts relevant updates to other clients
   - Manages connection lifecycle, sessions, and watchdog timers

4. **Server → WebSocket (Server → Clients)**
   - JSON `Response` objects with type: "command"
   - Commands executed via VConsole injection on clients

5. **VConsole TCP Injection (Client Game)**
   - Custom TCP protocol (VConsole.cs) with 4-byte commands (CMND, PRNT, VFCS)
   - Rate-limited to 20 commands/second (50ms minimum interval)
   - Command queue limited to 100 commands (silently dropped when full)

### Packet Types

**Implemented Packets (13 types):**
- `PLYR` - Player body position, angles, health (local-only, NOT broadcast)
- `HEAD` - Headset position and angles
- `HAND` - Left/right controller positions and angles
- `INIT` - Client initialization request
- `IENT` - Entities initialized confirmation
- `PHYS` - Physics object position/angles update
- `MAPN` - Map name and API version
- `BRAK` - Broken prop notification
- `EREM` - Entity removed notification
- `FIRE` - Entity output fired (triggers, buttons)
- `NPHP` - NPC health update
- `CMND` - Custom HLVRCoop command
- `TELE` - Teleport command
- `TENT` - Template entity cache request
- `SPWN` - Entity spawn notification
- `RESP` - Player respawn notification

**Legacy/Unused Packets (20 types):**
- RHND, LHND, HSET, TAGS, NPCS, PRFX, ALIV, DMGE, PROP, BUTN, BPRS, DOOR, TRIG, TRGD
- Defined in Packet.cs enum but never sent by Lua scripts
- Likely planned features that were never implemented

### Entity Naming Convention

Remote players rendered as floating entities:
- `hlvrcoop_head_<index>` - Headset prop
- `hlvrcoop_lefthand_<index>` - Left hand glove model
- `hlvrcoop_righthand_<index>` - Right hand glove model
- `hlvrcoop_text_<index>` - Name tag above headset

---

## Critical Issues

### P0 - CRITICAL (Breaking Co-op Functionality)

#### 1. Map Transition Synchronization Disabled
**File:** `AlyxGamemode/AlyxGamemode.cs:531-543`
**Severity:** CRITICAL - Multiplayer completely breaks when players load different maps

```csharp
/*
if (packet.args[0] != player.Client.Map)
{
    Response mapOutput = new("command", "addon_play " + packet.args[0] + "; addon_tools_map " + packet.args[0]);
    foreach (IndexedClient broadcast in connections)
    {
        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
        {
            broadcast.Session.Send(JsonConvert.SerializeObject(mapOutput));
        }
    }
    suffix = " - Telling all clients to switch...";
}
*/
```

**Impact:** When a player loads a new map and sends MAPN packet, the server detects the map change but does NOT broadcast to other clients. Players will be in different maps with no synchronization.
**Root Cause:** Code is commented out - possibly disabled due to bugs or incomplete testing.
**Fix Required:** Uncomment and test map transition broadcast logic. Add map load verification.

#### 2. No Packet Ordering or Reliability
**Files:**
- `HLVRCoOpModCore/Client.cs` - Data class only, no connection handling
- `HLVRCoOpModCore/Channel.cs` - Data class only, no networking
- `AlyxGamemode/Packet.cs` - Enum parsing only, no serialization

**Severity:** CRITICAL - Packets can arrive out of order or be lost without detection

**Impact:**
- Entity spawn/death events may be processed out of sequence
- Physics updates can overwrite newer state with older state
- No sequence numbers or timestamps for ordering
- No acknowledgment system for critical packets (INIT, MAPN, SPWN)
- Race conditions when packets from multiple clients interact

**Missing Mechanisms:**
- Packet sequence numbers
- ACK/NACK protocol
- Packet loss detection
- Out-of-order detection and reordering
- Critical packet retransmission

**Current Implementation:** Packets are simple enums with string arrays. No metadata for reliability.

#### 3. No Player Movement Interpolation
**File:** `AlyxGamemode/AlyxGamemode.cs:333-423`
**Severity:** HIGH - Visible jitter in remote player movement

**Current Update Rate:** Every 5 game ticks (~20 FPS)
**VR Multiplayer Standard:** 60-90 FPS with interpolation

```csharp
case PacketType.HeadPosAng:
    // Direct position update with no interpolation
    Response movement = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_head_" + player.Index + " " + HeadOrigin + " " + HeadAngles);
```

**Impact:**
- Remote players appear to "teleport" between positions
- VR motion sickness potential due to jerky movement
- Cannot aim or interact smoothly with moving players

**Missing:**
- Client-side interpolation between position updates
- Velocity extrapolation
- Update rate throttling based on movement amount

#### 4. Physics Ownership Race Conditions
**File:** `AlyxGamemode/AlyxGamemode.cs:495-521`
**Severity:** HIGH - Physics sync fails when multiple clients interact

**Ownership Timeout:** 5 seconds (hardcoded in Lua, not visible in C#)
**Position Sync Threshold:** 1 unit movement (Lua script line 229-238)

**Impact:**
- Two clients grabbing same object causes sync conflicts
- No collision state synchronization
- No velocity synchronization (only position)
- Physics object can "snap" between ownership transfers
- No deterministic physics simulation

**Example Scenario:**
1. Client A grabs physics object
2. Client B grabs same object within 5 seconds
3. Both clients think they own it
4. Position updates conflict
5. Object snaps unpredictably

#### 5. Session Ready Validation Only in AlyxGamemode
**File:** `AlyxGamemode/AlyxGamemode.cs:212-241`
**Severity:** MEDIUM - Inconsistent initialization validation

**Watchdog Timer:** 12 seconds with 1 retry
**Issue:** SessionReady validation only happens in AlyxGamemode packet handler, not in core networking

**Impact:**
- Different gamemodes may not have same initialization safety
- No centralized session state validation
- Packets dropped if player object not initialized (line 701-706)

**Drop Counter Implementation Exists:**
```csharp
AlyxGlobalData.SessionState dropped = AlyxGlobalData.instance.MarkPacketDropped(socket.ConnectionInfo.Id, "Player object not initialized when HLVRCOOP packet arrived");
```

---

## Risk Areas

### P1 - HIGH (Major Limitations)

#### 6. No Latency Compensation
**Impact:** Players with high latency experience:
- Hit detection issues (no client-side prediction)
- Delayed interaction feedback
- "Rubber banding" when lag spikes occur

**Missing:**
- Client-side prediction for local player
- Server-side reconciliation
- Lag compensation for entity interactions
- Ping measurement and display

#### 7. VConsole Command Queue Limitations
**File:** `HLVRCoOpMod/VConsole.cs:34, 474-495`
**Severity:** MEDIUM - Commands silently dropped under load

**Constraints:**
- Maximum queue size: 100 commands
- Minimum command interval: 50ms (20 commands/sec max)
- Buffer full handling: Commands dropped when queue full

```csharp
private const int MAX_QUEUE_SIZE = 100;
private const int MIN_COMMAND_INTERVAL_MS = 50;

// Silently drops commands when buffer full
if (bufferFullCommandQueue.Count < MAX_QUEUE_SIZE)
{
    bufferFullCommandQueue.Enqueue(command);
}
```

**Impact:**
- During heavy sync (multiple players, many entities), commands dropped
- No indication to user that commands were lost
- Entity spawn/death events may be missed

#### 8. No Connection State Validation
**Files:**
- `HLVRCoOpMod/ServerProgram.cs:142-190`
- `HLVRCoOpModCore/Client.cs:9-18`

**Severity:** MEDIUM - Race conditions possible

**Issues:**
- `Client.cs` is just a data wrapper (Username string)
- No connection state enum (Connecting, Connected, Ready, Disconnected)
- No validation that client can receive packets before sending
- Connection can close mid-packet processing

**Current State:**
```csharp
public class Client
{
    public string Username { get; set; } = "";
    public Client(string username)
    {
        Username = username;
    }
}
```

#### 9. Map Load Coordination Missing
**File:** `AlyxGamemode/AlyxGamemode.cs:522-562`
**Severity:** MEDIUM - Map transitions fragile

**Missing:**
- No verification that all clients loaded map successfully
- No rollback if map load fails
- No coordination of map load timing
- Map transition disabled (see P0 #1)

**Current Behavior:**
1. Client sends MAPN packet when map loads
2. Server receives and logs it
3. Server does NOT tell other clients to switch (code commented out)
4. Players potentially in different maps

#### 10. Entity Despawn/Death States Not Synchronized
**Files:**
- `AlyxGamemode/AlyxGamemode.cs:570-617`

**Severity:** MEDIUM - Entity state inconsistencies

**Implemented:**
- `BRAK` - Broken prop notification
- `EREM` - Entity removed notification

**Missing:**
- NPC death states (only health synced via NPHP)
- Entity despawn conditions
- Entity state machine synchronization
- Entity respawn handling

#### 11. Bootstrap Timing Issues
**File:** `AlyxGamemode/AlyxGamemode.cs:31-39, 73-141`
**Severity:** MEDIUM - Race conditions in initialization

**Hardcoded Delays:**
- 2000ms for addon enable (line 79)
- 2500ms for script entity creation (line 111)
- No verification that game is ready

**Race Condition:**
```csharp
Task.Run(async () =>
{
    await Task.Delay(2000); // Hope addon loaded by now
    // No verification - assumes success
    SendBootstrap(openSocket, clientToAdd.Username);
});
```

**Impact:**
- On slower systems, bootstrap may fail
- No retry for most bootstrap failures
- Watchdog timeout (12s) may not be enough

### P2 - MEDIUM (Design Limitations)

#### 12. No Full Body Models
**File:** `AlyxGamemode/AlyxGamemode.cs:425-458`
**Severity:** LOW - Visual limitation

**Current Representation:**
- Headset prop
- Left hand glove
- Right hand glove
- Name tag text entity

**Missing:**
- Torso, legs, feet
- Arm position between hands and headset
- Full body IK (Inverse Kinematics)

**Impact:** Players appear as floating hands and headset, not full avatars.

#### 13. PLYR Packet Local-Only
**File:** `AlyxGamemode/AlyxGamemode.cs:425-458`
**Severity:** LOW - Inconsistent data usage

```csharp
case PacketType.PlayerPosAng:
    // Update player state
    player.Origin = playerOrigin;
    player.Angles = playerAngles;
    player.Health = playerHealth;
    
    // Note: Body position is tracked for internal use (respawn, checkpoints, etc.)
    // Body position is not broadcasted to other clients as head/hand positions
```

**Impact:** Server knows body position but doesn't sync it. Inconsistent with HEAD/HAND packets.

#### 14. VConsole Protocol Limitations
**File:** `HLVRCoOpMod/VConsole.cs:14-744`
**Severity:** MEDIUM - Protocol design issues

**Limitations:**
- No command acknowledgment system
- No ping/pong for connection health
- Synchronous stream writes (no async/await)
- 20+ packet types defined but never used
- TCP connection loss handled but no proactive health checks

**Command Types:**
- `CMND` - Execute console command
- `PRNT` - Print output
- `VFCS` - Window focus state

#### 15. No Collision State Synchronization
**Impact:**
- Players cannot push each other
- Cannot detect if players are colliding
- Physics interactions with other players' bodies not synced

#### 16. Trigger Output Sync Limited
**File:** `AlyxGamemode/AlyxGamemode.cs:618-641`
**Severity:** MEDIUM

**Current Implementation:**
```csharp
case PacketType.EntityFired:
    // Basic output firing only
    Response fire = new("command", "hlvrcoop_fireoutput " + packet.args[0] + " " + packet.args[1]);
```

**Missing:**
- Multi-stage trigger sequences
- Trigger state (on/off)
- Trigger parameters
- Trigger delay handling

---

## Source 2 / HL:A Constraints

### Fundamental Engine Limitations

1. **No Native Multiplayer Networking**
   - Half-Life: Alyx was built as single-player only
   - No authoritative server model in Source 2 for HL:A
   - No client-side prediction
   - No entity replication system
   - No networked entity component system

2. **VConsole is Only External Communication Channel**
   - TCP-based console injection (custom protocol)
   - Rate-limited to 20 commands/second
   - No built-in acknowledgment
   - Synchronous writes only

3. **Entity Component System (ECS) Architecture**
   - Source 2 uses ECS for game entities
   - Mod must work around ECS to sync entities
   - No direct access to entity state from external tools
   - Entity creation via `ent_create` console command

4. **VR Multiplayer Requirements Not Met**
   - Standard: 60-90 FPS update rate
   - Mod achieves: ~20 FPS (every 5 ticks)
   - Interpolation essential for VR comfort
   - Mod has no interpolation

### Alternative Approaches

**Reference:** alyx-multiplayer.com (different mod)
- Uses friend as Combine scanner approach
- Different synchronization strategy
- Not directly comparable to this mod

### Original Project Status

**KCOM by KiwifruitDev:** CANCELLED
- This fork (dayflaree/hlvr-coop) is active continuation
- Maintains basic co-op functionality
- Has not addressed all architectural limitations

---

## Recommendations

### P0 - CRITICAL (Must Fix for Basic Co-op)

1. **Enable Map Transition Synchronization**
   - File: `AlyxGamemode/AlyxGamemode.cs:531-543`
   - Action: Uncomment and test map change broadcast
   - Add map load verification from all clients
   - Implement rollback if load fails
   - Add map load state to session management

2. **Implement Packet Ordering**
   - Add sequence numbers to Packet.cs
   - Implement reordering buffer in packet handler
   - Drop or reorder out-of-order packets
   - Add timestamps for latency measurement

3. **Add Basic Interpolation**
   - File: `AlyxGamemode/AlyxGamemode.cs:333-423`
   - Client-side interpolation between HEAD/HAND updates
   - Target: 60 FPS interpolated updates
   - Exponential smoothing for position updates
   - Consider velocity extrapolation for lower latency

### P1 - HIGH (Major Improvements)

4. **Physics Ownership Conflict Resolution**
   - File: `AlyxGamemode/AlyxGamemode.cs:495-521`
   - Implement ownership request/confirm protocol
   - Add ownership transfer acknowledgment
   - Reduce ownership timeout or make configurable
   - Add velocity synchronization (not just position)

5. **Latency Compensation**
   - Client-side prediction for local player
   - Server-side reconciliation
   - Ping measurement and display
   - Lag compensation for entity interactions

6. **Connection State Management**
   - Add ConnectionState enum to Client.cs
   - Validate connection state before packet processing
   - Add connection health monitoring (ping/pong)
   - Implement graceful degradation

7. **VConsole Queue Improvements**
   - File: `HLVRCoOpMod/VConsole.cs:474-495`
   - Increase queue size or make configurable
   - Add queue full notification to user
   - Prioritize critical packets (INIT, MAPN, SPWN)
   - Implement command coalescing

8. **Entity State Synchronization**
   - Add entity state machine (spawn, active, dead, despawned)
   - Synchronize NPC death states
   - Add entity respawn handling
   - Implement entity lifecycle tracking

### P2 - MEDIUM (Quality of Life)

9. **Map Load Coordination**
   - Add "ready to receive" acknowledgment for map changes
   - Coordinate map load timing across clients
   - Add map load progress indication
   - Implement map pre-loading

10. **Bootstrap Reliability**
    - File: `AlyxGamemode/AlyxGamemode.cs:73-141`
    - Replace hardcoded delays with readiness checks
    - Add retry logic for bootstrap failures
    - Extend watchdog timeout or make configurable
    - Add bootstrap progress indication

11. **Trigger Output Enhancement**
    - File: `AlyxGamemode/AlyxGamemode.cs:618-641`
    - Add multi-stage trigger sequence support
    - Synchronize trigger state (on/off)
    - Handle trigger delays properly
    - Add trigger parameter passing

12. **Clean Up Legacy Code**
    - File: `AlyxGamemode/Packet.cs:51-84`
    - Remove unused packet types from enum
    - Document why they were never implemented
    - Consider if any should be implemented

13. **Add Connection Validation**
    - File: `HLVRCoOpMod/ServerProgram.cs:142-190`
    - Add validation that client is ready before sending packets
    - Add connection state checks in packet handlers
    - Implement graceful handling of mid-connection state changes

14. **Improve VConsole Protocol**
    - File: `HLVRCoOpMod/VConsole.cs:14-744`
    - Add acknowledgment system for critical commands
    - Implement ping/pong for connection health
    - Consider async/await for stream writes
    - Add command timeout mechanism

15. **Documentation**
    - Document packet flow in architecture diagram
    - Add inline comments for complex synchronization logic
    - Document entity lifecycle
    - Add troubleshooting guide for common sync issues

---

## Validation Checklist

### Tests to Verify Sync Correctness

#### Basic Connection Test
- [ ] Client connects to server successfully
- [ ] Authentication works with correct password
- [ ] Invalid password rejected
- [ ] Username validation works (length, special characters)
- [ ] Reconnection handling works (VConsole reconnect)

#### Player Sync Test
- [ ] Remote player headset appears at correct position
- [ ] Remote player hands appear at correct position
- [ ] Name tag displays correctly above headset
- [ ] Player health updates correctly
- [ ] Position updates at expected rate (~20 FPS)

#### Entity Sync Test
- [ ] Physics object grab syncs to other clients
- [ ] Physics object position updates when moved
- [ ] Physics object break syncs to other clients (BRAK)
- [ ] Entity remove syncs to other clients (EREM)
- [ ] Entity spawn syncs to other clients (SPWN)

#### Trigger/Output Test
- [ ] Button press syncs to other clients (FIRE)
- [ ] Trigger activation syncs to other clients
- [ ] Multi-stage triggers work correctly
- [ ] Entity outputs with parameters sync correctly

#### NPC Sync Test
- [ ] NPC health updates correctly (NPHP)
- [ ] NPC death syncs correctly
- [ ] NPC position updates correctly

#### Map Transition Test
- [ ] **CRITICAL:** Map change broadcasts to other clients
- [ ] All clients load same map after transition
- [ ] Entity state preserved across map transition
- [ ] Player positions reset or preserved correctly
- [ ] MAPN packet sent with correct map name and API version

#### Physics Interaction Test
- [ ] Two clients grabbing same object handled correctly
- [ ] Ownership transfer works smoothly
- [ ] Physics object doesn't snap between positions
- [ ] Collision with other players handled correctly

#### Error Handling Test
- [ ] Connection loss handled gracefully
- [ ] VConsole disconnect/reconnect works
- [ ] Packet loss handled gracefully
- [ ] Invalid packets rejected without crash
- [ ] Buffer full condition handled correctly

#### Performance Test
- [ ] 4+ players can play without excessive jitter
- [ ] Command queue doesn't overflow during normal play
- [ ] CPU usage reasonable on server
- [ ] Memory usage stable over extended session
- [ ] No memory leaks in VConsole connection handling

#### Edge Cases
- [ ] Client disconnect during map load handled
- [ ] Server shutdown graceful (disconnect command sent)
- [ ] Rapid connect/disconnect doesn't cause issues
- [ ] Very high latency (200ms+) playable
- [ ] Packet loss (10%+) handled gracefully

---

## Conclusion

The HLVR Co-op Mod represents an impressive engineering achievement given the constraints of working with a single-player VR game that has no native networking. The architecture successfully enables basic cooperative play through creative use of WebSocket communication, VConsole injection, and Lua script packet parsing.

However, the mod suffers from several critical limitations:
1. **Map transition synchronization is completely disabled** - players can end up in different maps
2. **No interpolation** - remote players appear jerky and can cause motion sickness
3. **No packet ordering or reliability** - entity state can become inconsistent
4. **Physics ownership conflicts** - multiple players interacting with same object causes sync issues
5. **No latency compensation** - high-latency players have poor experience

The most urgent fix is enabling the map transition synchronization code that is currently commented out. Beyond that, adding interpolation and packet reliability would significantly improve the multiplayer experience.

These limitations are largely due to the fundamental constraint that Half-Life: Alyx was not designed for multiplayer, and the VConsole protocol is the only external communication channel available. The mod does the best possible within these constraints, but there is an upper bound on how smooth the experience can be without deeper engine integration.

For users, the mod is functional for casual co-op play but should not be expected to provide the same level of smoothness and reliability as natively multiplayer games. Players should expect some jitter, occasional sync issues, and the need to manually coordinate map transitions until these issues are addressed.

---

## File Reference Index

| Component | File Path | Key Lines |
|-----------|-----------|-----------|
| Main Gamemode | `AlyxGamemode/AlyxGamemode.cs` | 1-748 |
| Packet Definitions | `AlyxGamemode/Packet.cs` | 85-203 |
| Player State | `AlyxGamemode/Player.cs` | 1-25 |
| Entity State | `AlyxGamemode/Entity.cs` | 1-25 |
| VConsole Client | `HLVRCoOpMod/VConsole.cs` | 14-744 |
| WebSocket Server | `HLVRCoOpMod/ServerProgram.cs` | 19-445 |
| Client Data | `HLVRCoOpModCore/Client.cs` | 9-18 |
| Channel Data | `HLVRCoOpModCore/Channel.cs` | 68-161 |

---

**Report Generated:** 2026-04-23
**Analysis Method:** Code review + external research on Source 2/HL:A modding
**Next Review:** After implementing P0 fixes
