# Open Questions

## HLVR Co-Op Fix Plan - 2026-04-23

- [ ] **Interpolation lerp factor** -- The plan proposes 0.3-0.5 exponential smoothing. Too low causes visible lag; too high causes jitter. Needs playtesting to tune. Decided during Phase 2.

- [ ] **Physics ownership timeout value** -- Plan proposes reducing from 5s to 2s. May be too aggressive for high-latency connections (>200ms). Needs testing with varied ping. Decided during Phase 3.

- [ ] **Map transition timeout** -- Plan proposes 30 seconds per client. Some HL:A maps are large and may load slowly on HDD. Should this be configurable via settings? Decided during Phase 6.

- [ ] **Late-join state snapshot size** -- Large maps with many entities could generate a snapshot that exceeds VConsole rate limits during delivery. May need chunked delivery or priority filtering. Assessed during Phase 5.

- [ ] **NPCS packet rate impact** -- Sending NPC collider data for every tracked NPC on entity sync adds commands to the VConsole queue. Need to assess whether this causes queue overflow on entity-heavy maps. Assessed during Phase 3.

- [ ] **API version bump scope** -- Plan proposes bumping from 4 to 5 in Phase 1 (packet format changes). Should the version instead be bumped per-phase? This affects how updates are rolled out to users. Decided before Phase 1 implementation.

- [ ] **DMGE packet vs NPHP packet relationship** -- DMGE adds explicit damage events while NPHP tracks health changes. Need to decide: does DMGE replace NPHP, or do they coexist? Having both may double the sync commands for damage events. Decided during Phase 3.

## Critical Bug Fixes Wave Plan - 2026-04-24

- [ ] **input.lua refactoring scope** -- The half-refactored input.lua has complex interactions. Should we fully complete the migration to `input_data` structure, or is there a simpler incremental fix that avoids touching callback systems? Assessed during Wave 1.

- [ ] **ConcurrentDictionary vs lock pattern** -- For ServerProgram.cs connections list, should we migrate to ConcurrentDictionary or use lock-based synchronization? Lock+List preserves existing APIs but ConcurrentDictionary is more idiomatic for C#. Decided during Wave 2.

- [ ] **VConsole WriteRaw lock contention** -- Adding a lock to WriteRaw() may introduce contention during high-throughput scenarios. Should we benchmark performance impact before applying the fix? Assessed during Wave 2.

- [ ] **CreateDamageInfo API availability** -- redirect_damage_to_player.lua uses CreateDamageInfo which may not exist in Source 2 VScript. Need to verify API availability in current HL:A version before implementing fallback. Decided during Wave 3.

- [ ] **DiscordRpcGlobalData server state tracking** -- The fix adds a dedicated boolean for server state. Should this be reset when server closes, or does it persist across server restarts? Affects Discord rich presence display. Decided during Wave 2.
