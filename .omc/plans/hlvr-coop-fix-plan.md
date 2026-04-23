# HLVR Co-Op Mod - Comprehensive Fix Plan (v2)

**Plan ID:** hlvr-coop-fix-plan
**Created:** 2026-04-23
**Revised:** 2026-04-23 (v3 — iteration 3: corrected FIRE diagnosis, added variable-rate strategy, username delimiter fix)
**Status:** DRAFT
**Source Analysis:** `.omc/research/hlvr-coop-analysis-report.md`

---

## Context

The HLVR Co-Op mod enables cooperative multiplayer in Half-Life: Alyx (a single-player-only VR game) through: VR Tracking -> Lua script -> print() -> WebSocket -> C# server -> VConsole TCP (20 cmd/sec hard cap) -> entity updates.

A gap analysis identified 16 issues. This plan addresses ALL issues in a phased approach with level/map transitions explicitly LAST per user requirement.

### VConsole Command Budget Reality (Critical Constraint)

**Current state: ALREADY 5x OVER CAP**

| Metric | Value | Source |
|--------|-------|--------|
| VConsole hard cap | 20 commands/sec | `VConsole.cs:37` MIN_COMMAND_INTERVAL_MS=50 |
| HEAD handler commands | 3 per remote player per tick | `AlyxGamemode.cs:360-362` |
| HAND handler commands | 2 per remote player per tick | `AlyxGamemode.cs:407-408` |
| Tick cycle rate | 20 cycles/sec | Lua: hlvrcoop_inbetween>=5, timer=0.01s |
| **Current per-player cost** | **100 commands/sec** | 5 cmds × 20 cycles |
| **Overage factor** | **5x** | 100 actual vs 20 cap |

Every new feature must be budgeted against this cap. The plan adds a **Command Budget Ledger** that tracks cost per phase.

### Architectural Constraints (Immutable)

- VConsole is the ONLY external communication channel (20 cmd/sec max)
- Source 2 has NO native multiplayer for HL:A
- Remote players render as prop_dynamic entities (headset + gloves)
- Lua scripts communicate via print() statement interception
- Maximum 16 players supported

---

## Phased Implementation Plan

### PHASE 0: Unused Packet Research (Analysis Only) — UNCHANGED

**Goal:** Determine which unused packet types in `Packet.cs` should be implemented, removed, or kept.

| Packet | Code | Assessment | Rationale |
|--------|------|------------|-----------|
| RightHandIndexes | RHND | **REMOVE** | Fixed naming `hlvrcoop_righthand_<i>` makes this unnecessary |
| LeftHandIndexes | LHND | **REMOVE** | Fixed naming `hlvrcoop_lefthand_<i>` makes this unnecessary |
| HeadsetIndexes | HSET | **REMOVE** | Fixed naming `hlvrcoop_head_<i>` makes this unnecessary |
| TextIndexes | TAGS | **REMOVE** | Fixed naming `hlvrcoop_text_<i>` makes this unnecessary |
| ColliderIndexes | NPCS | **IMPLEMENT** | NPC collider tracking for physics interaction sync |
| Prefix | PRFX | **REMOVE** | No clear use case, legacy artifact |
| Alive | ALIV | **REMOVE** | PLYR already tracks health, redundancy wastes rate budget |
| ColliderDamage | DMGE | **IMPLEMENT** | Explicit damage events separate from health updates |
| PhysicsObjectIndexStartPos | PROP | **KEEP** | For late-join initial position sync |
| ButtonIndexStartPos | BUTN | **KEEP** | For late-join button state restoration |
| ButtonPressIndex | BPRS | **REMOVE** | FIRE already handles button outputs |
| DoorIndexStartPos | DOOR | **KEEP** | For late-join door state restoration |
| TriggerIndexStartPos | TRIG | **KEEP** | For late-join trigger state restoration |
| TriggerActivateIndex | TRGD | **KEEP** | Useful for distinguishing originating vs relayed triggers (see trigger double-fire fix) |

**Summary:** Remove 7, Implement 2 (NPCS, DMGE), Keep 5 for late-join sync, plus TRGD for trigger disambiguation.

**Acceptance Criteria:**
- [ ] Each of 14 unused packet types has documented assessment
- [ ] Decision made: implement, remove, or keep-for-later for each

**Risk:** LOW — Analysis only, no code changes.

**Budget Impact:** NPCS adds ~1 command per tracked NPC (one-time, not per tick). DMGE adds ~1 command per damage event (infrequent). Both acceptable within budget AFTER consolidation.

---

### PHASE 1: Foundation — Structural Fixes & Command Budget

**Goal:** Fix the most impactful bugs with minimal changes, extract the God handler, establish command budget headroom, and build connection state infrastructure.

**Addresses Issues:** #7 (connection state), #2 (packet ordering — partial), trigger double-fire (root cause of #15)

#### Step 1.0: Critical Bug Fixes (Immediate Impact)

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` — Fix NPHP originator exclusion

**Changes:**

1. **Fix NPHP broadcast originator bug** (`AlyxGamemode.cs:650`):
   - Current: `connections.ForEach(c => c.Session.Send(...))` with no originator exclusion
   - Fix: exclude originator from health update relay (same pattern as HEAD at line 366 and FIRE at line 630)
   - Impact: Prevents health echo back to the client that sent it
   - Note: FIRE handler at line 630 ALREADY excludes originator — no change needed there

2. **Trigger double-fire diagnosis** (documentation only, fix in Phase 3 Step 3.4):
   - Root cause: `trigger_once` entities exist in BOTH game instances. When Client A walks through a trigger, it fires locally via Source 2's entity system AND the FIRE packet relays to Client B which fires Client B's local copy of the same trigger.
   - Server-side originator exclusion is correct (already in place at `AlyxGamemode.cs:630`).
   - The actual fix requires Lua-side tracking (see Phase 3 Step 3.4): maintain a `hlvrcoop_fired_triggers` set in Lua, skip `hlvrcoop_fireoutput` for trigger_once entities already in the set.
   - No code change in this step — diagnosis documented here to guide Phase 3.

**Acceptance Criteria:**
- [ ] NPHP relay excludes the client that sent the NPHP packet
- [ ] FIRE handler left unchanged (already correct)
- [ ] NPC health updates no longer echo back to the reporting client

#### Step 1.1: Extract God Handler (Structural Refactoring)

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` — Extract packet handlers into separate methods

**Changes:**
1. Extract each `case PacketType.X:` block from the 700-line constructor into `HandleXPacket(Player player, Packet packet, ...)` methods
2. Create a `PacketDispatcher` class that routes packets to handler methods
3. No behavior changes — pure extraction refactoring
4. Enables all subsequent phases to modify individual handlers without merge conflicts

**Acceptance Criteria:**
- [ ] AlyxGamemode constructor reduced to setup + dispatcher call
- [ ] Each packet type has its own handler method
- [ ] All existing behavior preserved (binary-compatible)
- [ ] No packet handling logic removed or changed

#### Step 1.2: Command Consolidation & Variable-Rate Strategy (Critical Budget Fix)

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Consolidate HEAD and HAND commands, add variable-rate logic
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Add multi-target position commands, implement variable tick rates

**Changes:**

1. **Consolidate HEAD handler** from 3 commands to 1:
   - Current: `hlvrcoop_setlocation_nonuuid` (head) + `hlvrcoop_setlocation_nonuuid` (text) + `ent_fire` (text message)
   - New: `hlvrcoop_setlocation_head <index> <hx> <hy> <hz> <hp> <hyaw> <hr> <tx> <ty> <tz> |<name>`
   - Username delimited by `|` before the name field (spaces in usernames handled correctly)
   - One Lua command updates head entity, text entity position, and text message simultaneously

2. **Consolidate HAND handler** from 2 commands to 1:
   - Current: `hlvrcoop_setlocation_nonuuid` (left) + `hlvrcoop_setlocation_nonuuid` (right)
   - New: `hlvrcoop_setlocation_hands <index> <lx> <ly> <lz> <lp> <lyaw> <lr> <rx> <ry> <rz> <rp> <ryaw> <rr>`
   - One Lua command updates both hand positions simultaneously

3. **Variable-rate send strategy** (reduces sustained rate to near-cap):
   - Extend `hlvrcoop_inbetween` counter to per-packet-type throttles in Lua:
   - HEAD: send every 10th tick = **10 Hz** (100Hz base / 10)
   - HAND: send every 10th tick = **10 Hz** (offset by 5 ticks from HEAD for interleaving)
   - PLYR: send every 20th tick = **5 Hz** (low-frequency body position)
   - Result: HEAD(10) + HAND(10) + PLYR(5) = **25 consolidated commands/sec** — 5 over cap but manageable with priority queue

**Result:**
- Before: 5 commands × 20 cycles = **100 commands/sec** per remote player (5x over cap)
- After consolidation + variable rate: **25 commands/sec** per remote player (1.25x over cap)
- Priority queue handles the 5/sec overflow; critical commands never dropped

**Acceptance Criteria:**
- [ ] HEAD updates sent as single consolidated command with `|` username delimiter
- [ ] HAND updates sent as single consolidated command
- [ ] Variable tick rates: HEAD 10Hz, HAND 10Hz, PLYR 5Hz
- [ ] Usernames with spaces handled correctly (parsed on `|` delimiter)
- [ ] Remote player visual appearance unchanged (headset + text + both hands still update)
- [ ] Measured VConsole command rate ≤25/sec per remote player

#### Step 1.3: Connection State Management

**Files to Modify:**
- `HLVRCoOpModCore/Client.cs` — Add ConnectionState enum
- `HLVRCoOpModCore/IndexedClient.cs` — Add state property
- `AlyxGamemode/AlyxGlobalData.cs` — Reconcile with existing SessionPhase
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Validate state before processing

**Changes:**

1. Add `ConnectionState` enum: `Connecting → Connected → Authenticated → Initializing → Ready → Disconnected`
2. **Reconcile with existing `SessionPhase`** (`AlyxGlobalData.cs:18-25`):
   - Map: `SessionPhase.Connected` → `ConnectionState.Connected`
   - Map: `SessionPhase.AddonEnabled` → `ConnectionState.Authenticated`
   - Map: `SessionPhase.ScriptBootstrapSent` + `InitPacketReceived` → `ConnectionState.Initializing`
   - Map: `SessionPhase.SessionReady` → `ConnectionState.Ready`
   - Replace `SessionPhase` with `ConnectionState` in all `UpsertSession` calls
3. Server rejects packets from non-Ready clients (except INIT, IENT, MAPN)
4. **Add hard API version rejection**: mismatched version → disconnect with error message (not just warn)

**Acceptance Criteria:**
- [ ] `SessionPhase` replaced by `ConnectionState` with explicit mapping
- [ ] Server drops packets from non-Ready clients with logging
- [ ] Hard API version rejection disconnects mismatched clients
- [ ] Existing connection flow still works (connecting through Ready)

#### Step 1.4: Packet Sequence Numbers (Backward Compatible)

**Files to Modify:**
- `AlyxGamemode/Packet.cs` — Parse trailing sequence/timestamp fields
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Process sequences
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Append sequence/timestamp to packets

**Changes:**

1. **Append** (not prepend) new fields to preserve backward compatibility:
   - Format: `HEAD <x> <y> <z> <p> <y> <r> <seq> <ts> HLVRCOOP`
   - Old servers/parsers ignore extra fields (they stop at fixed positions)
   - New parsers read the trailing seq/ts fields

2. Per-client sequence tracking in `AlyxGamemode.cs`: drop movement packets with seq <= last-seen
3. Stateless packets (INIT, IENT, MAPN) never dropped regardless of sequence
4. Timestamps enable latency measurement (used in Phase 4)
5. API version bump from 4 to 5 (deployed atomically with Step 1.3's version enforcement)

**Acceptance Criteria:**
- [ ] Sequence numbers and timestamps appended to outgoing packets (backward compatible)
- [ ] Server drops stale movement packets (HEAD, HAND, PLYR, PHYS)
- [ ] Old-format packets (without seq/ts) still parse correctly (graceful degradation)
- [ ] API version bumped to 5; mismatched clients disconnected (from Step 1.3)
- [ ] Latency measurable from timestamp delta

**Budget Ledger After Phase 1:**
| Item | cmds/sec | Notes |
|------|----------|-------|
| HEAD (consolidated) | 10 | 1 cmd × 10Hz (variable rate) |
| HAND (consolidated) | 10 | 1 cmd × 10Hz (variable rate, offset) |
| PLYR (consolidated) | 5 | 1 cmd × 5Hz (variable rate) |
| **Total per remote player** | **25** | Down from 100, 1.25x cap |
| VConsole cap | 20 | 5/sec overflow handled by priority queue |
| Headroom freed | 75 | Available for Phases 2-6 |

**Risk:** MEDIUM — Command consolidation is additive (new Lua commands). Packet format is backward compatible. Connection state replaces existing enum. Trigger fix is 2-line change.

---

### PHASE 2: Movement Quality — Interpolation & Priority Queue

**Goal:** Smooth remote player movement through client-side interpolation, protect critical commands with priority queue.

**Addresses Issues:** #3 (no interpolation), #6 (VConsole queue limitations)

#### Step 2.1: Client-Side Interpolation

**Files to Modify:**
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Add interpolation between position updates

**Changes:**
1. Maintain position buffer per remote player entity (last 2 positions + timestamps)
2. On every game tick (not just on packet receipt), linearly interpolate entity positions:
   - Calculate `t = (now - last_update_time) / (target_time - last_update_time)`
   - Clamp t to [0, 1.2] (allow slight extrapolation beyond 1.0)
   - `current_pos = last_pos + (target_pos - last_pos) * t`
3. If no update for >150ms, switch to velocity extrapolation with decay
4. If no update for >500ms, freeze entity at last position

**Acceptance Criteria:**
- [ ] Remote players move smoothly between updates (measurable: max position jump < 0.1 units between ticks)
- [ ] No visible "teleporting" during normal play
- [ ] Entities stop within 0.2 units of final position when remote player stops
- [ ] Interpolation runs every game tick, independent of network updates

#### Step 2.2: VConsole Priority Queue

**Files to Modify:**
- `HLVRCoOpMod/VConsole.cs` — Replace flat queue with priority queue

**Changes:**
1. Priority levels:
   - **Critical:** INIT, IENT, MAPN, SPWN, TELE, FIRE (state-changing)
   - **Normal:** HEAD, HAND, PLYR (movement)
   - **Low:** BRAK, EREM, NPHP, TENT (entity state)
2. When queue >80% capacity, drop Low priority commands
3. Increase `MAX_QUEUE_SIZE` from 100 to 150 (mitigates burst spikes)
4. Add queue depth warning log when >50% full (every 10 seconds)

**Acceptance Criteria:**
- [ ] Critical commands never dropped while lower-priority commands exist
- [ ] Queue overflow drops lowest-priority commands first
- [ ] No regression in movement command delivery

**Budget Ledger After Phase 2:**
| Item | cmds/sec | Notes |
|------|----------|-------|
| Existing (consolidated + variable-rate) | 25 | From Phase 1 |
| New in Phase 2 | 0 | Interpolation is Lua-side only, no new VConsole commands |
| **Total per remote player** | **25** | No budget change |

**Risk:** LOW — Interpolation is purely Lua-side (additive). Priority queue is internal to VConsole.cs.

---

### PHASE 3: Entity & Physics Sync

**Goal:** Fix physics ownership, add entity lifecycle, implement new packet types, enhance trigger sync.

**Addresses Issues:** #4 (physics ownership), #9 (entity states), #14 (collision sync), #15 (trigger sync)

#### Step 3.1: Physics Ownership Protocol

**Files to Modify:**
- `AlyxGamemode/Moveable.cs` — Add ownership request/confirm
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Add ownership handler
- `AlyxGamemode/AlyxGlobalData.cs` — Track ownership state
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Add ownership request/response

**Changes:**
1. Replace passive 5-second timeout with active ownership protocol:
   - First PHYS from client → server checks ownership → grant or deny
   - On grant: broadcast "OWNR grant <entity> <owner>" to all clients
   - On deny: send "OWNR deny <entity>" to requester
   - On release (client drops object): broadcast "OWNR release <entity>"
2. Safety timeout reduced from 5s to 2s (active protocol makes long timeout unnecessary)
3. Add velocity to PHYS packets: `PHYS <name> <x> <y> <z> <p> <y> <r> <vx> <vy> <vz> <seq> <ts> HLVRCOOP`
   - Requires new Lua command `hlvrcoop_setvelocity` (separate from `hlvrcoop_setlocation`)

**Acceptance Criteria:**
- [ ] Two clients cannot simultaneously own same physics object
- [ ] Ownership transfers smoothly on release
- [ ] Denied ownership leaves entity at last-known position
- [ ] Timeout works as safety net for disconnected clients
- [ ] Velocity data included in broadcasts

**Budget Impact:** OWNR packets sent only on grab/release (infrequent, ~2-4 per minute during gameplay). Acceptable.

#### Step 3.2: Entity Lifecycle State Machine

**Files to Modify:**
- `AlyxGamemode/Entity.cs` — Add entity state enum
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Track lifecycle
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Detect death/despawn explicitly

**Changes:**
1. Entity state enum: `Spawned → Active → Dying → Dead → Despawned`
2. Lua detects NPC death (health→0 + BecomeRagdoll) and sends explicit state change
3. Lua detects entity despawn (IsValidEntity false) with reason context
4. Server maintains entity state dictionary, broadcasts transitions
5. New clients receive current entity states (partial late-join support)

**Acceptance Criteria:**
- [ ] NPC death state synced (not just health)
- [ ] Entity removal has clear reason (broken, despawned, killed)
- [ ] State transitions are ordered (no Dead→Active without Spawn)
- [ ] Late-joining client receives snapshot of known entity states

#### Step 3.3: Implement NPCS and DMGE Packets

**Files to Modify:**
- `AlyxGamemode/Packet.cs` — Add NPCS and DMGE handlers
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Handle new packets
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Generate new packets

**Changes:**
1. `NPCS` packet: `NPCS <name> <x> <y> <z> <radius> <height> <seq> <ts> HLVRCOOP`
   - Sent once on entity sync, updated if NPC moves significantly
2. `DMGE` packet: `DMGE <target> <damage> <damagetype> <attacker> <seq> <ts> HLVRCOOP`
   - Sent when damage occurs, separate from health update
   - Coexists with NPHP (DMGE = event, NPHP = resulting health)

**Acceptance Criteria:**
- [ ] NPCS generated for tracked NPCs on entity sync
- [ ] DMGE generated on damage events
- [ ] Both broadcast to all clients (excluding originator per Step 1.0 fix)
- [ ] Rate impact assessed: NPCS is one-time, DMGE is event-driven

**Budget Impact:** NPCS ~1-5 commands per map load (one-time). DMGE ~2-10 commands per combat encounter (event-driven). Both within freed budget.

#### Step 3.4: Enhanced Trigger Output Sync

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Enhanced FIRE handling
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Include parameters in FIRE

**Changes:**
1. Extended FIRE format: `FIRE <name> <output> <param1> <param2> <seq> <ts> HLVRCOOP`
2. Multi-output batching: if entity fires multiple outputs in same frame, send as single command
3. **trigger_once Lua-side tracking**: maintain `hlvrcoop_fired_triggers` set in Lua. When a trigger_once fires locally, add its name to the set. When `hlvrcoop_fireoutput` receives a relayed trigger_once fire for a name already in the set, skip it (already fired locally). When the name is NOT in the set, fire it (legitimate relay from another client).
4. **trigger_once server tracking**: server remembers fired trigger_once entities, prevents duplicate relay (belt-and-suspenders with Lua-side tracking)
5. Consider TRGD packet for distinguishing originating vs relayed triggers (kept from Phase 0)

**Acceptance Criteria:**
- [ ] Trigger outputs with parameters sync correctly
- [ ] trigger_once fires exactly once per client (Lua-side tracking + server-side tracking)
- [ ] Multi-output triggers fire in correct sequence
- [ ] Delayed outputs fire at correct time

**Dependencies:** Phase 1 complete (command consolidation + originator fix + extracted handlers).

**Risk:** HIGH — Physics ownership changes fundamental behavior. Mitigation: 2-client testing with simultaneous grabs.

---

### PHASE 4: Bootstrap Reliability & Latency

**Goal:** Replace hardcoded delays, add latency measurement, broadcast PLYR data.

**Addresses Issues:** #5 (latency compensation), #10 (bootstrap timing), #12 (PLYR local-only)

#### Step 4.1: Bootstrap Readiness Verification

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Replace delays with polls
- `AlyxGamemode/AlyxGlobalData.cs` — Track bootstrap phases

**Changes:**
1. Replace `Thread.Sleep(2000)` at line 79 with polling loop:
   - Check every 200ms for addon-related VConsole output
   - Maximum 10 seconds, then proceed with warning
2. Replace `Thread.Sleep(2500)` at line 470 with verification:
   - Check every 200ms for IENT packet (confirms script loaded)
   - Maximum 8 seconds, then retry once
3. Extend watchdog from 12s to 20s total
4. Track bootstrap phase in `ConnectionState` (Connecting → Initializing transitions)

**Acceptance Criteria:**
- [ ] No hardcoded delays in bootstrap
- [ ] Slower systems bootstrap successfully
- [ ] Failures logged with specific phase

#### Step 4.2: Latency Measurement and Display

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Calculate latency from timestamps
- `AlyxGamemode/AlyxGlobalData.cs` — Store per-client latency

**Changes:**
1. Calculate one-way latency from packet timestamps (Phase 1 Step 1.4)
2. Store smoothed latency (exponential moving average) per client
3. Broadcast ping info every 5 seconds via status message
4. Log latency spikes (>200ms jump)

**Acceptance Criteria:**
- [ ] Per-client latency measured from packet timestamps
- [ ] Latency displayed in status messages
- [ ] No additional VConsole commands needed (reuses timestamps)

#### Step 4.3: PLYR Packet Broadcast

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Broadcast PLYR to others

**Changes:**
1. Broadcast player body position, angles, health to other clients
2. Rate: included in existing tick cycle, uses consolidated command format
3. Clients can show health bars, spawn at checkpoints, track alive/dead

**Acceptance Criteria:**
- [ ] PLYR data broadcast to all other clients (excluding originator)
- [ ] Health visible to other clients
- [ ] Body position usable for respawn/checkpoint logic

**Budget Impact:** PLYR broadcast already included in variable-rate budget (5Hz from Phase 1). No additional commands per tick — PLYR sends at its own 5Hz rate.

**Risk:** MEDIUM — Bootstrap changes affect all new connections. Keep existing retry logic.

---

### PHASE 5: Late-Join State Sync & Packet Cleanup

**Goal:** Enable mid-game joins with correct state, implement keep-for-later packets, remove redundant packets.

**Addresses Issues:** Phase 0 findings, #16 (unused packets cleanup)

#### Step 5.1: Late-Join State Snapshot

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — State snapshot generation
- `AlyxGamemode/AlyxGlobalData.cs` — Entity state tracking
- `WorkshopAddon/.../hlvrcoop_interval.lua` — Process snapshot

**Changes:**
1. On new client ready, server generates condensed state snapshot:
   - Batch multiple entities per command (max 5 entities per command)
   - Maximum 40 commands per snapshot (delivered over 2 seconds)
   - Client enters "loading" state during snapshot, hides entities
2. Implement PROP, BUTN, DOOR, TRIG, TRGD packets for initial position sync:
   - `PROP <name> <x> <y> <z> <p> <y> <r> HLVRCOOP`
   - `BUTN <name> <x> <y> <z> <state> HLVRCOOP`
   - `DOOR <name> <x> <y> <z> <angle> <open|closed> HLVRCOOP`
   - `TRIG <name> <x> <y> <z> <enabled|disabled> HLVRCOOP`
   - `TRGD <name> <fired|pending> HLVRCOOP`

**Acceptance Criteria:**
- [ ] New client receives state snapshot within 2 seconds
- [ ] Max 40 VConsole commands per snapshot
- [ ] Entities at correct positions for late-joining client
- [ ] Trigger states correct (fired/not-fired)
- [ ] Client sees loading indicator during snapshot delivery

#### Step 5.2: Unused Packet Cleanup

**Files to Modify:**
- `AlyxGamemode/Packet.cs` — Remove 7 redundant types
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Remove empty cases

**Changes:**
1. Remove: RHND, LHND, HSET, TAGS, PRFX, ALIV, BPRS
2. Add removal rationale comments
3. Keep: NPCS, DMGE (Phase 3), PROP, BUTN, DOOR, TRIG, TRGD (Phase 5)

**Acceptance Criteria:**
- [ ] 7 redundant types removed from enum
- [ ] No regression in packet parsing
- [ ] Removal rationale documented

**Risk:** LOW — Removing unused code is low risk. Snapshot is additive (only affects new clients).

---

### PHASE 6: Level/Map Transition Synchronization (LAST)

**Goal:** Seamless map transitions where all clients switch together and re-sync state.

**Addresses Issues:** #1 (map transition sync DISABLED — code commented out)

**Why last:** Most disruptive change. Affects all clients simultaneously. Requires coordinated re-initialization. Everything else must be stable first.

#### Step 6.1: Map Transition Protocol

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Uncomment and enhance lines 530-543
- `AlyxGamemode/AlyxGlobalData.cs` — Add transition state tracking
- `HLVRCoOpMod/ServerProgram.cs` — ChangeMap coordination
- `HLVRCoOpMod/ClientProgram.cs` — Handle map change commands

**Changes:**

1. Add transition states: `Idle → TransitionInitiated → ClientsNotified → WaitingForReady → AllReady → Executing`
2. Uncomment the map transition code at `AlyxGamemode.cs:530-543`
3. Protocol:
   - Client sends MAPN with new map → server enters `TransitionInitiated`
   - Server broadcasts `MAP_CHANGE <mapname> <transition_id>` to ALL clients
   - Server pauses normal packet processing (movement, physics) during transition
   - Each client loads map → script re-initializes → sends MAPN for new map → server marks client ready
   - Server waits for all clients (30-second timeout per client)
   - Failed clients: disconnected or reverted
   - All ready → server broadcasts `MAP_GO <transition_id>`
   - Clients re-initialize and resume normal play
4. Clear entity cache on map change (new map = new entities)
5. Preserve player indices across transitions

**Acceptance Criteria:**
- [ ] Map transition broadcasts to all clients
- [ ] Server waits for all clients to confirm loaded
- [ ] Timeout: 30 seconds per client, disconnected clients don't block others
- [ ] Entity cache cleared on map change
- [ ] Player indices preserved
- [ ] Normal gameplay resumes after transition

#### Step 6.2: Map Transition Recovery

**Files to Modify:**
- `AlyxGamemode/AlyxGamemode.cs` (extracted handlers) — Rollback logic
- `HLVRCoOpMod/ClientProgram.cs` — Reconnection after failure

**Changes:**
1. Client disconnect during transition → server proceeds with remaining clients
2. Failed map load → server sends `MAP_REVERT <old_map>` to all clients
3. Client tracks last successful map name for revert
4. Add transition progress to client UI: "Waiting for X/Y players..."

**Acceptance Criteria:**
- [ ] Disconnect during transition does not hang server
- [ ] Failed loads trigger revert
- [ ] Client UI shows transition progress
- [ ] Players can rejoin after missed transition

**Dependencies:** ALL previous phases complete.

**Risk:** CRITICAL — Most complex operation. Mitigations: 30-second timeout, revert mechanism, all other systems stable.

---

## VConsole Command Budget Ledger (Cumulative)

| Phase | Per-Player cmds/sec | Total 2-Player | Notes |
|-------|---------------------|----------------|-------|
| Current (before fixes) | 100 | 100 | 5x over cap |
| After Phase 1 | 25 | 25 | Consolidation + variable rate: HEAD 10Hz + HAND 10Hz + PLYR 5Hz |
| After Phase 2 | 25 | 25 | Interpolation is Lua-side only |
| After Phase 3 | 25 + ~2 event | ~27 | OWNR/NPCS/DMGE are event-driven |
| After Phase 4 | 25 | 25 | PLYR already in budget from Phase 1 variable rate |
| After Phase 5 | 25 + ~40 snapshot | 25 + one-time | Snapshot is burst (2 sec), not sustained |
| After Phase 6 | 25 | 25 | Map transitions are one-time events |

Note: 25 cmds/sec sustained is 1.25x the 20/sec cap. The priority queue (Phase 2) handles the 5/sec overflow by dropping low-priority entity state commands before movement commands. This is a 96% improvement from the current 100/sec (5x over cap).

---

## Success Criteria (Overall)

1. Two clients play through a level with smooth remote player movement (max jump < 0.1 units/tick)
2. Physics objects sync correctly when grabbed by either player
3. NPC death and entity state changes sync across all clients
4. trigger_once fires exactly once per client (no double-fire)
5. New clients can join mid-game with correct entity state
6. Map transitions coordinate all clients with timeout and rollback
7. VConsole command rate reduced from 100 to 25 per remote player per second (96% improvement)
8. Bootstrap succeeds without hardcoded delays
9. API version mismatch detected and client disconnected (not just warned)

---

## Guardrails

### MUST HAVE
- API version check with hard rejection (disconnect mismatched clients)
- Existing packet flow not broken (backward compatible format changes)
- 16-player maximum preserved
- VConsole 20 cmd/sec rate limit accounted for in every change
- WebSocket remains the transport layer

### MUST NOT
- No new network transport
- No Source 2 binary modifications
- No breaking changes without API version bump
- No new third-party dependencies

---

## RALPLAN-DR Summary

### Principles (5)

1. **Reduce before adding.** Consolidate existing commands before adding new packet types. The VConsole budget is scarce; free capacity before consuming it.
2. **Server-authoritative.** C# server is arbiter of truth for entity ownership, player state, and map transitions. Clients defer to server.
3. **Respect the VConsole rate limit.** Every change quantified against the 20 cmd/sec cap. Budget ledger tracked per phase.
4. **Backward-compatible format changes.** New fields appended, not prepended. Old parsers ignore extra data gracefully.
5. **Fail gracefully, never hang.** Timeouts, reverts, and fallbacks at every layer. Single client failure never blocks others.

### Decision Drivers (Top 3)

1. **User-mandated priority:** Map transitions MUST be last. Overrides technical dependency ordering.
2. **VConsole bandwidth constraint:** Budget analysis drives feature ordering. Consolidation before addition.
3. **Risk progression:** Low-risk fixes and structural refactoring first, high-risk behavioral changes later.

### Viable Options

**Option A: Phased approach with budget-first ordering (recommended)**
- Fixes existing budget overage before adding features
- Extracts God handler before compounding changes
- Fixes trigger double-fire immediately (Step 1.0)
- Pros: Each phase produces working mod, budget-aware, addresses root causes first
- Cons: Longer timeline, requires structural refactoring (Phase 1 Step 1.1)

**Option B: Parallel track approach**
- Split into concurrent tracks: (1) Budget + foundation, (2) Entity sync, (3) Map transitions
- Pros: Potentially faster with team
- Cons: Merge conflicts in shared files, no team to parallelize

**Option B invalidation:** Single-developer project, tightly coupled codebase (every phase touches AlyxGamemode.cs). Parallel development would cause constant merge conflicts in the same 700-line method. Only viable after God handler extraction (Phase 1 Step 1.1), at which point sequential phases are just as efficient.

---

## ADR: Architecture Decision Record

**Decision:** Phased sequential with budget-first ordering, command consolidation in Phase 1, backward-compatible packet format, God handler extraction before behavioral changes.

**Drivers:**
- VConsole 5x over cap requires consolidation before addition
- User requires map transitions last
- God handler prevents safe incremental development
- FIRE originator bug needs immediate fix (root cause of trigger double-fire)

**Alternatives Considered:**
1. **Add features first, consolidate later** — Violates rate limit principle, compounds queue overflow
2. **Parallel track development** — Requires team + integration testing infrastructure
3. **Big-bang rewrite** — Too risky, breaks existing functionality for extended period

**Why Chosen:** Budget-first ordering addresses the fundamental constraint before building on it. Structural refactoring (God handler extraction) prevents regression risk. Trigger fix is immediate.

**Consequences:**
- API version bump (4→5) in Phase 1
- Each phase touches extracted handler methods (not 700-line constructor)
- VConsole budget tracked as a first-class metric

**Follow-ups:**
- Performance testing after Phase 2 (interpolation frame budget)
- Multi-client stress testing after Phase 3 (ownership under load)
- Full playthrough after Phase 6 (end-to-end transitions)

---

## Open Questions

See `.omc/plans/open-questions.md`

---

## Revision History

| Version | Date | Changes |
|---------|------|---------|
| v1 | 2026-04-23 | Initial plan from Planner |
| v2 | 2026-04-23 | Architect + Critic iteration 1: Added command budget analysis, consolidated commands, extracted God handler, backward-compatible packet format, reconciled SessionPhase/ConnectionState |
| v3 | 2026-04-23 | Architect + Critic iteration 2: Corrected FIRE handler diagnosis (already excludes originator at line 630), re-diagnosed trigger double-fire as client-side duplicate-entity issue requiring Lua tracking, added variable-rate send strategy (HEAD 10Hz + HAND 10Hz + PLYR 5Hz = 25/sec), added `|` username delimiter for consolidated commands, updated budget ledger to 25/sec (1.25x cap, 96% improvement) |
