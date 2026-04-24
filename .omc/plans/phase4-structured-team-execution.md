# Phase 4: Bootstrap Reliability & Latency - Structured Team Execution Plan

## RALPLAN-DR Summary

### Principles (3-5)
1. **Readiness over Rhythm** - Polling for actual game state beats arbitrary timing
2. **Observability First** - Latency measurement and logging enable debugging
3. **Budget Discipline** - VConsole 20 cmd/sec cap is non-negotiable
4. **Graceful Degradation** - Timeouts log warnings and proceed where possible
5. **No Regression** - All changes preserve existing behavior paths

### Decision Drivers (Top 3)
1. **Connection Reliability** - Bootstrap failures are the top user complaint
2. **Parallel Execution** - Team structure needs minimal blocking dependencies
3. **Incremental Value** - Each workstream should be independently testable

### Options Considered

#### Option A: Sequential Waterfall (REJECTED)
- Single executor handles 4.1 → 4.2 → 4.3
- Duration: 6-8 hours
- Rejected: No parallelism benefit, bottleneck on 4.1

#### Option B: Parallel Workstreams with 4.1.1-4.1.2 Gate (SELECTED)
- 2 executors, field additions first, then parallel
- Duration: 4-5 hours
- Selected: Best balance of coordination and speed

#### Option C: Full Parallel (INVALIDATED)
- All workstreams start simultaneously
- Invalidated: SessionState merge conflicts would waste more time than saved

### Decision: Option B

**Why chosen:**
- 4.1.1-4.1.2 gate is minimal (~15 min)
- After gate, 4.2 and 4.3 are fully independent
- 4.1.3-4.1.7 continues without blocking 4.2/4.3

**Consequences:**
- Team must coordinate on AlyxGlobalData.cs merge
- Executor A handles bootstrap + PLYR, Executor B handles latency
- Both touch AlyxGamemode.cs but in different regions

**Follow-ups:**
- Consider extracting SessionState to partial class for future phases
- Document field addition protocol in AGENTS.md

---

## Team Structure

### Executor A: Bootstrap & PLYR
**Workstream 4.1:** Bootstrap Readiness (tasks 4.1.1-4.1.7)
**Workstream 4.3:** PLYR Broadcast (tasks 4.3.1-4.3.2)

### Executor B: Latency Measurement
**Workstream 4.2:** Latency Measurement (tasks 4.2.1-4.2.4)

---

## Dependency Graph

```
GATE: 4.1.1-4.1.2 (SessionState fields) - BOTH executors
|
├── Executor A Branch
|   ├── 4.1.3 Addon enable poll (independent of 4.1.4-4.1.7)
|   ├── 4.1.4 IENT poll (depends on 4.1.2)
|   ├── 4.1.5 IENT handler flag (depends on 4.1.2)
|   ├── 4.1.6 IENT handler poll (depends on 4.1.5)
|   ├── 4.1.7 Extend watchdog (depends on 4.1.4)
|   └── 4.3.1-4.3.2 PLYR broadcast (INDEPENDENT of 4.1.3-4.1.7)
|
└── Executor B Branch (blocked ONLY until gate)
    ├── 4.2.1 Latency fields (gate provides SessionState)
    ├── 4.2.2 ExtractTimestamp helper (independent)
    ├── 4.2.3 Latency calculation (depends on 4.2.1-4.2.2)
    └── 4.2.4 Latency broadcaster (depends on 4.2.1)
```

---

## Gate Phase: 4.1.1-4.1.2 (Both Executors Coordinate)

### Task 4.1.1: Add BootstrapPhase to SessionState
**File:** `AlyxGamemode/AlyxGlobalData.cs`
**Location:** SessionState class, after line 38
**Duration:** 5 minutes

```csharp
public ConnectionState BootstrapPhase { get; set; } = ConnectionState.Connecting;
```

### Task 4.1.2: Add HasReceivedIENT to SessionState
**File:** `AlyxGamemode/AlyxGlobalData.cs`
**Location:** SessionState class, after line 38
**Duration:** 5 minutes

```csharp
public bool HasReceivedIENT { get; set; } = false;
```

**Gate Acceptance:**
- [ ] Both fields added to SessionState
- [ ] Code compiles
- [ ] Both executors have updated AlyxGlobalData.cs

---

## Executor A: Bootstrap Readiness (4.1.3-4.1.7)

### Task 4.1.3: Replace Addon Enable Delay with Poll
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Lines 122-177 (ClientOpen, first Task.Run)
**Duration:** 20 minutes
**Acceptance:**
- Polls every 200ms, max 10s timeout
- Logs timeout warning
- Proceeds if Authenticated state detected

### Task 4.1.4: Poll for IENT After Bootstrap
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Lines 146-153 (second Task.Run)
**Duration:** 30 minutes
**Acceptance:**
- Polls every 200ms for up to 8s
- Retries bootstrap once on timeout
- Fails with explicit message after double timeout
- Marks Ready only after IENT confirmed

### Task 4.1.5: Mark IENT Received
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Packet parsing section (line ~250) + HandleInitializedEntitiesPacket (line ~570)
**Duration:** 15 minutes
**Acceptance:**
- IENT packet type handler sets flag
- HandleInitializedEntitiesPacket sets flag at entry

### Task 4.1.6: Replace IENT Handler Delay with Poll
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Lines 571-586 in HandleInitializedEntitiesPacket
**Duration:** 20 minutes
**Acceptance:**
- Polls for Ready state every 200ms up to 8s
- Forces initialization on timeout with log
- Exception handling improved

### Task 4.1.7: Extend Watchdog Timeout
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Line 260 (watchdog Task.Run)
**Duration:** 5 minutes
**Acceptance:**
- Watchdog waits 20s instead of 12s

---

## Executor A: PLYR Broadcast (4.3.1-4.3.2) - Can run in parallel with 4.1.3-4.1.7

### Task 4.3.1: Broadcast PLYR to Other Clients
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** HandlePlayerPosAngPacket method (lines 531-558)
**Duration:** 15 minutes
**Acceptance:**
- PLYR packet broadcast to other clients
- Includes position, angles, health
- Uses BroadcastToOthers helper

### Task 4.3.2: Add Body Tracking Command to Lua
**File:** `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/hlvrcoop_interval.lua`
**Location:** After line 1309 (after hlvrcoop_setresin)
**Duration:** 15 minutes
**Acceptance:**
- Command registered: hlvrcoop_setbody
- Body state stored per player index
- No errors on invalid index

---

## Executor B: Latency Measurement (4.2.1-4.2.4) - Blocked until Gate completes

### Task 4.2.1: Add Latency Tracking to SessionState
**File:** `AlyxGamemode/AlyxGlobalData.cs`
**Location:** SessionState class, after line 38
**Duration:** 5 minutes
**Gate Dependency:** 4.1.1-4.1.2 must be merged first

```csharp
public double SmoothedLatencyMs { get; set; } = 0;
public DateTime? LastLatencySpikeUtc { get; set; }
```

**Acceptance:** SessionState tracks smoothed latency and spike timestamp

### Task 4.2.2: Extract Timestamp Helper
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Before AlyxGamemode class (after line 13)
**Duration:** 10 minutes

```csharp
private static double? ExtractTimestamp(Packet packet)
{
    if (packet.args.Length < 2) return null;
    string timestampStr = packet.args[packet.args.Length - 2];
    if (double.TryParse(timestampStr, CultureInfo.InvariantCulture.NumberFormat, out double ts))
        return ts;
    return null;
}
```

**Acceptance:** Helper extracts GameRules:GetGameTime() timestamp

### Task 4.2.3: Calculate Latency in Packet Handler
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** After line 253 (packet validation)
**Duration:** 30 minutes
**Dependencies:** 4.2.1 (SessionState field), 4.2.2 (helper)

**Acceptance:**
- Latency calculated on every timestamped packet
- EMA smoothing applied (alpha=0.2)
- Spikes >200ms logged

### Task 4.2.4: Broadcast Latency Status
**File:** `AlyxGamemode/AlyxGamemode.cs`
**Location:** Add field at class level, new method, call in ClientOpen
**Duration:** 20 minutes
**Dependencies:** 4.2.1 (SessionState field)

**Acceptance:**
- Each player sees "Ping: XXms" every 5s
- Only shows after first measurement
- Single global broadcaster instance

---

## File Collision Matrix

| File | Executor A | Executor B | Collision Risk |
|------|------------|------------|----------------|
| AlyxGlobalData.cs | 4.1.1, 4.1.2 | 4.2.1 | HIGH - Gate coordinates this |
| AlyxGamemode.cs | 4.1.3-4.1.7, 4.3.1 | 4.2.2-4.2.4 | LOW - Different regions |
| hlvrcoop_interval.lua | 4.3.2 | - | NONE |

**Merge strategy:**
1. Gate: Both executors present field additions together
2. After gate: Parallel work on different file regions
3. Final merge: AlyxGamemode.cs should auto-merge cleanly

---

## Success Criteria

### Bootstrap Readiness (4.1)
- [ ] No hardcoded 2000ms or 2500ms delays remain
- [ ] Connection succeeds if addon loads up to 10s
- [ ] IENT packet confirmed before Ready state
- [ ] Retry happens once on IENT timeout
- [ ] Explicit failure message after double timeout
- [ ] Watchdog extended to 20s

### Latency Measurement (4.2)
- [ ] Latency calculated from packet timestamps
- [ ] Smoothed latency stored per client
- [ ] Each client sees "Ping: XXms" every 5 seconds
- [ ] Latency spikes >200ms logged
- [ ] No errors on missing timestamps

### PLYR Broadcast (4.3)
- [ ] PLYR packet broadcast to other clients
- [ ] Body position, angles, health included
- [ ] Lua command stores body state
- [ ] No increase in command budget
- [ ] No null reference exceptions

---

## Testing Checklist

Per executor, run after completing assigned workstream:

### Bootstrap (Executor A)
- [ ] Fresh connection succeeds (addon loads immediately)
- [ ] Slow connection succeeds (addon takes 8s to load)
- [ ] Failed connection gets explicit error message
- [ ] Watchdog doesn't fire prematurely

### Latency (Executor B)
- [ ] Latency displayed accurately
- [ ] Latency spikes logged to SyncLog
- [ ] No crashes on missing timestamps

### PLYR (Executor A)
- [ ] PLYR data received by other clients
- [ ] Command budget not exceeded (verify with counter)
- [ ] No null reference exceptions

### Integration (Both)
- [ ] Full connection flow with all features enabled
- [ ] Latency displays during bootstrap
- [ ] PLYR broadcasts continue during latency measurement

---

## VConsole Budget Verification

**Current (Phase 1-3):** 25 cmd/sec per player
- HEAD: 10 Hz
- HAND: 10 Hz
- PLYR: 5 Hz

**Phase 4 additions:** ~0 cmd/sec
- PLYR broadcast: reuses existing 5Hz slot
- Latency broadcast: 0.2 cmd/sec (every 5s)
- Bootstrap polling: client-side only

**Verification step:** Add debug counter to confirm no budget overrun
