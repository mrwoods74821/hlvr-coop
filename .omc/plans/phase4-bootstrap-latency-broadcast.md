# Phase 4: Bootstrap Latency Reduction + PLYR Broadcast

## Context

Phase 3 implemented connection stability improvements. Phase 4 addresses remaining bootstrap latency issues and adds player state broadcasting.

**Bootstrap Problem:** Client joins → addon loads → entities spawn → Ready. Current sequence uses fixed delays (2s + 2.5s + 12s watchdog) because server has no visibility into when the addon actually loads. The IENT (InitializedEntities) packet provides a partial signal we can poll for.

**PLYR Broadcast Problem:** When a late-joining player enters, existing clients don't receive the `PLYR` packet to create the PlayerBody. Without this, the new player's entity is invisible to others.

---

## Work Objectives

1. Reduce bootstrap latency by polling for the IENT signal (where available) instead of blind delays
2. Add PLYR packet broadcasting so late-joining players are visible to existing clients
3. Extend watchdog timeout to accommodate the new bootstrap sequence
4. Maintain all existing safety guards and error handling

---

## Guardrails

### Must Have
- IENT polling with 8s timeout and 200ms poll interval
- Session null guard before player creation
- Socket.IsAvailable checks before all delayed sends
- ErrorLogger.LogError on all async exceptions
- HasReceivedIENT flag tracking in SessionState
- BroadcastToOthers for PLYR packets in HandlePlayerPosAngPacket
- Watchdog extended to 20s

### Must NOT Have
- Busy-wait loops where no signal exists (addon enable has no server-side confirmation)
- Circular Ready-state polling (HandleInitializedEntitiesPacket sets Ready, polling for it is circular)
- Removal of existing error handling or guards
- Changes to the bootstrap command sequence itself

---

## RALPLAN-DR Summary

### Principles
1. **Correctness over cleverness** - Don't poll where there's no signal to poll for
2. **Follow existing patterns** - Use existing guards, logging, and async patterns
3. **Fail gracefully** - All async paths must have try/catch with ErrorLogger
4. **Defer incomplete work** - PLYR on first connect is Phase 5

### Decision Drivers (Top 3)
1. Connection reliability (primary)
2. Code correctness (no circular polls, no busy-waits)
3. Scope control (targeted fixes only)

### Options Considered

**Option A: Full polling everywhere**
- Replace all delays with polling loops
- *Rejected:* Addon enable has no server-side signal. Nothing to poll for = busy-wait.

**Option B: Targeted improvements (SELECTED)**
- Poll only where a signal exists (IENT flag)
- Keep delays where no signal exists (addon enable, Ready-state)
- Add guards and checks to make delays safer
- *Why chosen:* Matches what the server can actually observe. No busy-waits, no circular polls.

### Decision Record (ADR)

**Decision:** Use targeted polling for IENT signal only; keep delays for addon enable and Ready-state with improved guards.

**Drivers:** Connection reliability, code correctness, scope control.

**Alternatives considered:**
- Full polling everywhere (rejected - no signal for addon enable, circular for Ready-state)

**Why chosen:** Addon enable has no server-side confirmation → keep delay with guard. IENT has a signal (packet arrival) → poll for it. Ready is set synchronously in the same method → keep delay with socket check.

**Consequences:** Bootstrap latency reduced from ~14.5s to ~8s (best case) with fallback to retry. Watchdog extended to 20s to accommodate retries.

**Follow-ups:** Phase 5 will address PLYR on first connect (requires different approach).

---

## Task Flow

```
Gate Task (4.1.1)
    |
    |--- Workstream A: Bootstrap Improvements (4.1.2 - 4.1.6)
    |    |
    |    |--- 4.1.2: Addon enable guard (parallel)
    |    |--- 4.1.3: IENT polling (parallel, after gate)
    |    |--- 4.1.4: Set IENT flag (parallel, after gate)
    |    |--- 4.1.5: Handle IENT packet (parallel, after gate)
    |    |--- 4.1.6: Extend watchdog (parallel)
    |
    |--- Workstream B: PLYR Broadcast (4.3.1 - 4.3.2)
         |
         |--- 4.3.1: Broadcast PLYR (parallel, after gate)
         |--- 4.3.2: Lua command (parallel, after gate)
```

---

## Detailed TODOs

### Gate Task

#### 4.1.1: Add HasReceivedIENT to SessionState

**File:** `AlyxGamemode/AlyxGlobalData.cs`

**Location:** Inside the `SessionState` class (around line 200-250, after `ConnectionState` enum)

**Add to SessionState class:**
```csharp
/// <summary>
/// Tracks whether the client has sent an InitializedEntities (IENT) packet.
/// Used by bootstrap polling to detect when entities are ready.
/// </summary>
public bool HasReceivedIENT { get; set; } = false;
```

**Acceptance Criteria:**
- [ ] HasReceivedIENT bool added to SessionState
- [ ] Initialized to false by default
- [ ] XML documentation comment included

---

### Workstream A - Bootstrap Improvements

#### 4.1.2: Add Session Guard to Addon Enable Delay

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Location:** Lines 122-177 (the addon enable delay sequence in `OnClientConnected`)

**Rationale:** Architect Round 2 feedback: "Remove the loop entirely, keep fixed delay with guard." There is no server-side signal for addon load completion, so polling would be a busy-wait. The 2s delay is necessary; we just need to guard against session invalidation.

**Current code pattern (keep this, just add guard):**
```csharp
_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    // ... rest of code
});
```

**Replacement:**
```csharp
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Wait for addon to enable (no server-side signal available)

    // Guard: check connection still valid
    var existingPlayer = AlyxGlobalData.instance.GetPlayer(connectionId);
    if (existingPlayer != null)
    {
        ErrorLogger.LogWarning($"AddonEnable: Player already exists for {connectionId}, skipping creation");
        return;
    }

    // ... rest of player creation code (UNCHANGED)
    // Response vconsoleInput = new("command", "addon_enable hlvr_coop");
    // Response output = new("status", "Loading co-op addon...");
    // socket.Send(...)
    // await Task.Delay(5000);
    // ... etc
```

**Acceptance Criteria:**
- [ ] 2s delay retained (no polling loop)
- [ ] Player existence check added before proceeding
- [ ] Warning logged if player already exists
- [ ] Rest of player creation code unchanged
- [ ] Socket sends still guarded by existing checks

---

#### 4.1.3: Replace Script Entities Delay with IENT Polling

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Location:** Line 148 (the 2.5s delay before "script entities exist" commands)

**Rationale:** Architect Round 2 feedback: "IENT has a signal → poll for it." The HasReceivedIENT flag is set when the IENT packet arrives. This is a real signal we can poll for.

**Current code:**
```csharp
await Task.Delay(2500); // Wait for script entities to exist
```

**Replacement:**
```csharp
// Poll for IENT packet (max 8s, 200ms interval)
bool ientReceived = false;
for (int i = 0; i < 40; i++) // 40 * 200ms = 8s
{
    var session = AlyxGlobalData.instance.GetSession(socket.ConnectionInfo.Id);
    if (session != null && session.HasReceivedIENT)
    {
        ientReceived = true;
        SyncLog($"{client.Username}: IENT received after {i * 200}ms");
        break;
    }
    await Task.Delay(200);
}

if (!ientReceived)
{
    ErrorLogger.LogWarning($"{client.Username}: IENT timeout after 8s, retrying bootstrap");
    // Retry: trigger bootstrap again, which will re-send IENT
    Response vconsoleInputRetry = new("command", "buddha 0;ent_fire hlvrcoop_manager_* kill;buddha 1;addon_enable hlvr_coop");
    socket.Send(JsonConvert.SerializeObject(vconsoleInputRetry));
    // Give it another chance (2.5s fallback)
    await Task.Delay(2500);
}
```

**Acceptance Criteria:**
- [ ] 2.5s fixed delay replaced with IENT polling loop
- [ ] Poll interval: 200ms
- [ ] Poll timeout: 8s (40 iterations)
- [ ] Retry on timeout: re-send bootstrap commands
- [ ] Fallback delay of 2.5s after retry
- [ ] SyncLog on successful IENT receive with timing
- [ ] Warning logged on timeout

---

#### 4.1.4: Set HasReceivedIENT in IENT Packet Handler

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Location:** Line 570 in `HandleInitializedEntitiesPacket` method

**Rationale:** This flag is polled by 4.1.3. It must be set when the IENT packet arrives from the client.

**Current code:**
```csharp
private static void HandleInitializedEntitiesPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
{
    // ... existing code ...
}
```

**Add at the START of the method:**
```csharp
private static void HandleInitializedEntitiesPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
{
    // Mark IENT received for polling (used by 4.1.3)
    var session = AlyxGlobalData.instance.GetSession(socket.ConnectionInfo.Id);
    if (session != null)
    {
        session.HasReceivedIENT = true;
        SyncLog($"{player.Client.Username}: IENT flag set");
    }

    // ... rest of existing method unchanged ...
}
```

**Acceptance Criteria:**
- [ ] HasReceivedIENT set at start of HandleInitializedEntitiesPacket
- [ ] Session null check before setting flag
- [ ] SyncLog when flag is set
- [ ] Rest of method unchanged

---

#### 4.1.5: Improve HandleInitializedEntitiesPacket

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Location:** Lines 570-595 in `HandleInitializedEntitiesPacket` method (the existing 2.5s delay at end)

**Rationale:** Architect Round 2 feedback: "Keep the 2.5s delay but add socket.IsAvailable check and proper exception handling." The Ready state is set synchronously at line 587, so polling for it would be circular. The 2.5s delay waits for entities to initialize after IENT.

**Current code pattern:**
```csharp
_ = Task.Run(async () =>
{
    await Task.Delay(2500);
    // ... sends
});
AlyxGlobalData.instance.UpsertSession(socket.ConnectionInfo.Id, player.Client.Username, AlyxGlobalData.ConnectionState.Ready);
```

**Replacement:**
```csharp
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(2500); // Wait for entities to initialize after IENT
        if (socket.IsAvailable)
        {
            Response vconsoleInput5 = new("command", "buddha 1;unpause;ent_fire hlvrcoop_timer addoutput OnTimer>hlvrcoop_script>CallScriptFunction>HLVRCoOpMod>0>-1");
            Response output4 = new("status", "Co-op initialized!");
            socket.Send(JsonConvert.SerializeObject(output4));
            socket.Send(JsonConvert.SerializeObject(vconsoleInput5));
        }
        else
        {
            ErrorLogger.LogWarning($"{player.Client.Username}: Socket unavailable during IENT finalization");
        }
    }
    catch (Exception ex)
    {
        ErrorLogger.LogError("HandleInitializedEntitiesPacket", ex);
    }
});

// Set Ready state synchronously (polling for this would be circular)
AlyxGlobalData.instance.UpsertSession(socket.ConnectionInfo.Id, player.Client.Username, AlyxGlobalData.ConnectionState.Ready);
SyncLog($"{player.Client.Username}: session ready");
```

**Acceptance Criteria:**
- [ ] 2.5s delay retained (no polling loop)
- [ ] socket.IsAvailable check before sends
- [ ] try/catch with ErrorLogger.LogError
- [ ] Warning logged if socket unavailable
- [ ] Ready state set synchronously (not polled)
- [ ] SyncLog for ready state

---

#### 4.1.6: Extend Watchdog Timeout

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Location:** Line 260 in `ClientOpen` method (the watchdog Task.Run)

**Rationale:** With IENT polling and retry, the bootstrap sequence can take longer. Extend from 12s to 20s.

**Current code:**
```csharp
if (elapsed > 12000) // 12 second timeout
```

**Replacement:**
```csharp
if (elapsed > 20000) // 20 second timeout (extended for IENT polling + retry)
```

**Acceptance Criteria:**
- [ ] Timeout changed from 12000 to 20000
- [ ] Comment updated to reflect new timeout and reason

---

### Workstream B - PLYR Broadcast

#### 4.3.1: Add BroadcastToOthers to HandlePlayerPosAngPacket

**File:** `AlyxGamemode/AlyxGamemode.cs`

**Location:** In `HandlePlayerPosAngPacket` method (around line 420-450)

**Rationale:** When a late-joining player sends position updates, existing clients need the PLYR packet to create their PlayerBody. Broadcasting ensures all clients have the player data.

**Current code pattern:**
```csharp
private static void HandlePlayerPosAngPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
{
    // ... deserialize pos/ang ...

    // Store position
    player.Position = position;
    player.ViewAngle = angle;

    // ... rest of method ...
}
```

**Add after position storage:**
```csharp
private static void HandlePlayerPosAngPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
{
    // ... existing deserialization code ...

    player.Position = position;
    player.ViewAngle = angle;

    // Broadcast PLYR to other clients so they can create this player's body
    Packet plyrPacket = new("PLYR", $"{player.Client.Username}|{position.X}|{position.Y}|{position.Z}|{angle.P}|{angle.Y}|{angle.R}");
    BroadcastToOthers(socket.ConnectionInfo.Id, connections, plyrPacket);

    // ... rest of method unchanged ...
}
```

**Acceptance Criteria:**
- [ ] PLYR packet created with username, position, and view angles
- [ ] BroadcastToOthers called with socket ID (excludes sender)
- [ ] Packet format matches existing PLYR pattern
- [ ] Rest of method unchanged

---

#### 4.3.2: Add hlvrcoop_setbody Lua Command

**File:** `WorkshopAddon/game/hlvr_addons/hlvr_coop/scripts/vscripts/hlvrcoop_interval.lua`

**Location:** Add new function at end of file

**Rationale:** The PLYR packet needs to trigger body creation on receiving clients. This Lua command handles that.

**Add to file:**
```lua
-- hlvrcoop_setbody <username> <x> <y> <z> <pitch> <yaw> <roll>
-- Creates or updates a player body from PLYR packet data
Convars:RegisterCommand("hlvrcoop_setbody", function(cmd, username, x, y, z, p, yaw, r)
    local ply = Entities:FindByClassnameNearest("player", Vector(0, 0, 0), 99999)
    if not ply then return end

    local targetName = "hlvrcoop_body_" .. username
    local body = Entities:FindByName(nil, targetName)

    local pos = Vector(tonumber(x), tonumber(y), tonumber(z))
    local ang = QAngle(tonumber(p), tonumber(yaw), tonumber(r))

    if not body then
        -- Create new body
        body = SpawnEntitySandbox({
            classname = "prop_dynamic",
            targetname = targetName,
            model = "models/player/alyx.vmdl",
            origin = pos,
            angles = ang
        })
        print("[HLVRCoOp] Created body for " .. username)
    else
        -- Update existing body
        body:SetAbsOrigin(pos)
        body:SetAbsAngles(ang)
        print("[HLVRCoOp] Updated body for " .. username)
    end
end, "Create/update player body from PLYR packet", FCVAR_CHEAT)
```

**Acceptance Criteria:**
- [ ] Command registered with 7 parameters (username, x, y, z, p, yaw, r)
- [ ] Finds local player for reference
- [ ] Searches for existing body with hlvrcoop_body_ prefix
- [ ] Creates new body if not found
- [ ] Updates position/angles if body exists
- [ ] Print statements for debugging

---

## IENT Handler Sequencing Design Note

**Per Architect Round 2 feedback:**

The race condition concern between IENT flag set (4.1.4) and poll timeout (4.1.3) is resolved by understanding the design:

1. The poll (4.1.3) runs in ClientOpen's Task.Run, started when client first connects
2. The flag is set when the IENT packet arrives from the client (PreResponse handler sets the flag, then HandleInitializedEntitiesPacket is called)
3. The poll waits up to 8s for the flag
4. If timeout occurs, the bootstrap is retried (which re-triggers IENT)
5. This sequencing is intentional and correct

**No code change needed.** This is the designed flow.

---

## Deferred to Phase 5

**PLYR on First Connect:** Broadcasting PLYR from HandlePlayerPosAngPacket only works for late-joining players AFTER they start sending position updates. For the very first position update, we need a different approach (likely a dedicated "player joined" broadcast). This is intentionally deferred to Phase 5.

---

## Success Criteria

- [ ] All tasks completed with acceptance criteria met
- [ ] Bootstrap latency reduced from ~14.5s to ~8s (best case)
- [ ] No busy-wait loops (polling only where signals exist)
- [ ] No circular polling (Ready state not polled)
- [ ] Session invalidation properly guarded
- [ ] Socket sends protected with IsAvailable checks
- [ ] All async exceptions logged with ErrorLogger
- [ ] PLYR broadcast working for late-joining players
- [ ] Watchdog timeout extended to 20s
- [ ] No regression in connection success rate

---

## Testing Checklist

### Manual Testing
- [ ] Fresh client connect (bootstrap completes in <10s)
- [ ] IENT timeout triggers retry correctly
- [ ] Session invalidation during addon enable is handled
- [ ] Late-joining player appears to existing clients
- [ ] Multiple late-joining players all visible
- [ ] Watchdog does not fire on successful connections
- [ ] Watchdog still fires on genuinely stuck connections

### Log Verification
- [ ] "IENT flag set" appears in logs
- [ ] "IENT received after Xms" shows polling worked
- [ ] "IENT timeout after 8s" with retry shows fallback worked
- [ ] "Created body for username" appears for new players
- [ ] "Updated body for username" appears for position updates

---

**Plan Version:** Final (Architect Round 2 Feedback Incorporated)
**Created:** 2025-04-23
**Status:** Ready for implementation
