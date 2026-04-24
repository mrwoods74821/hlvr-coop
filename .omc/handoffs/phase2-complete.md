## Handoff: Phase 2 Complete -> Phase 3 Next

- **Decided**: Client-side interpolation via per-entity prev/target position tracking at 100ms windows (matching 10Hz update rate). Priority queue with 3 tiers (Critical > Normal > Low) using separate Queue objects.
- **Rejected**: Heap-based priority queue (overkill for 3 levels), interpolation with velocity extrapolation (adds complexity, freeze-at-target is sufficient), single sorted list (O(n) dequeue).
- **Risks**: Interpolation assumes ~10Hz updates — if network conditions cause larger gaps, the 500ms freeze timeout needs tuning. Priority classification is string-prefix-based and may need updates if new command types are added.
- **Files**: `hlvrcoop_interval.lua` (interpolation), `VConsole.cs` (priority queue), `ErrorLogger.cs` (LogSync from earlier SyncLog fix)
- **Remaining**: Phase 3 (physics ownership, entity lifecycle, NPCS/DMGE packets, trigger double-fire fix), Phase 4 (bootstrap reliability, latency), Phase 5 (late-join sync), Phase 6 (map transitions — last)
