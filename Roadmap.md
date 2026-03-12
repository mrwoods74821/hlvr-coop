# HLVRCoop Workspace Roadmap

This roadmap reflects the **current repository state** and focuses on practical next steps.

Last updated: 2026-03-11

---

## 1) Current Workspace Snapshot

### Solution / Projects
- `HLVRCOOP.sln`
- `HLVRCoOpMod/`  
  Main Windows app (UI, server/client program flow, VConsole bridge, Lua runtime hosting, settings, dialogs)
- `HLVRCoOpModCore/`  
  Shared protocol/domain types (`Response`, `Client`, `ServerInfo`, vectors/angles, plugin abstractions)
- `AlyxGamemode/`  
  Core gameplay synchronization handler (packet processing, entity/player sync, session bootstrap flow)
- `DiscordRpcPlugin/`  
  Discord Rich Presence integration plugin
- `PlayerBodySyncPlugin/`  
  Additional player-body synchronization support
- `WorkshopAddonCompatibilityPlugin/`  
  Compatibility helpers for selected workshop addons
- `WorkshopAddon/`  
  HL:A addon assets/scripts (content + game scripts/vscripts)
- `wiki/`  
  Architecture/dev docs and Lua API references

---

## 2) Verified Current Status

## Done / Implemented
- Core multiplayer packet pipeline is present and building.
- VConsole connectivity and reconnection systems are implemented.
- WebSocket client/server flows are implemented.
- Error logging infrastructure exists (`ErrorLogger`) and is used broadly.
- Lua scripting integration exists and is documented in wiki.
- Workshop addon compatibility plugin exists in workspace.
- Recent session-hardening work is present in `AlyxGamemode`:
  - session phase tracking (`Connected`, `AddonEnabled`, `ScriptBootstrapSent`, `InitPacketReceived`, `SessionReady`)
  - dropped-packet diagnostics
  - bootstrap retry/fail diagnostics
  - watchdog gating so retry timing starts after first HLVRCOOP packet (avoids false pre-game timeout noise)

## Important runtime note
If server/mod is started before HLA + VConsole are active, delayed initialization logs are expected until game-side packets begin. This is normal.

---

## 3) Current Known Priority Areas

1. **Map transition synchronization reliability**
   - Validate/finish automatic map switch behavior for all connected players.
   - Ensure transition does not desync player sessions.

2. **Audio reliability during level transitions**
   - Reproduce and stabilize intermittent post-transition audio issues.
   - Confirm deterministic reset/restore behavior.

3. **Session bootstrap verification matrix**
   - Validate host/client startup order permutations:
     - game first vs server first
     - reconnect flows
     - map changes during join windows
   - Confirm session phases always converge to `SessionReady`.

4. **Build and release workflow hardening**
   - Ensure consistent publish artifacts (`HLVRCoOpMod.exe`) and plugin copy behavior when app is running.
   - Reduce lock-related warnings during build when binaries are in use.

---

## 4) Recommended Next Milestones

### Milestone A — Transition Stability
- Re-test and finalize map transition sync.
- Add explicit transition diagnostics and success/fail logging.
- Verify with 2+ clients.

### Milestone B — Audio Transition Fix
- Isolate trigger conditions for intermittent audio issue.
- Implement/validate robust transition audio reset strategy.
- Regression test across multiple chapter boundaries.

### Milestone C — Bootstrap Robustness Validation
- Run repeated join/rejoin tests and confirm:
  - expected phase progression
  - no persistent `ScriptBootstrapSent` stalls
  - bounded retries and useful fail diagnostics only when truly broken.

### Milestone D — Packaging / Operator UX
- Document clean run/build cycle (close app before rebuild to avoid locked plugin DLL warnings).
- Publish repeatable release output path and checklist.

---

## 5) Practical Test Checklist (Current)

- [ ] Fresh start: host + one client reaches `SessionReady`.
- [ ] Start server before game: no false failure before first game packet.
- [ ] Reconnect client after temporary disconnect recovers cleanly.
- [ ] Map transition keeps all players synchronized.
- [ ] Audio remains correct after transition.
- [ ] Release publish output launches correctly:
  - `HLVRCoOpMod\bin\Release\HLVRCoOpMod.exe`

---

## 6) Out of Scope for Immediate Stability

These are optional/longer-term:
- Internet master server registry for global discovery.
- New feature packets beyond current sync stability needs.
- Large architecture refactors not required for transition/bootstrap reliability.

---

## 7) Documentation Alignment Notes

Primary docs already present in `wiki/` and should remain source-of-truth for architecture/API details.  
This roadmap is intentionally short and execution-focused, meant to track **current status + next operational priorities** rather than full historical development history.