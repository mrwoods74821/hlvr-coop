# Troubleshooting

[← Development Guide](Development-Guide) | [Quick Reference →](Quick-Reference)

---

**Community Support:**
- [HLVRCoop Discord](https://discord.gg/X6Dp95WRR4) - Active community support
- [Valve Developer Community Forums](https://developer.valvesoftware.com/wiki/Main_Page) - Official support forums

---

## Common Issues

### VConsole Connection Fails
**Symptoms**: "Failed to connect to VConsole"
**Solutions**:
- Ensure Half-Life: Alyx is running
- Check VConsole port in settings (default: 29000)
- Verify game launched with `-console -vconsole` flags
- Check firewall/antivirus blocking TCP connection

### Workshop Addon Not Loading
**Symptoms**: "INIT HLVRCOOP" not received, entities not syncing
**Solutions**:
- Check addon enabled in game: `addon_enable hlvr_coop`
- Verify addon files are installed in game directory (`game/hlvr_addons/hlvr_coop/` and `content/hlvr_addons/hlvr_coop/`)
- Check addon files in game directory

### Players Not Visible
**Symptoms**: Other players don't appear
**Solutions**:
- Verify client connected successfully
- Check player index assignment (max 16)
- Verify Workshop addon loaded
- Check entity creation in game (hlvrcoop_head_0, etc.)
- Look for errors in mod UI

### Entity Sync Not Working
**Symptoms**: Physics objects not syncing
**Solutions**:
- Verify entity class in `hlvrcoop_trackers` table
- Check entity has name (or gets auto-named)
- Verify entity cached: `HLVRCOOP_EntitySyncSpecific()` called
- Check packet being sent: Look for "PHYS" in VConsole output
- Verify ownership system (entity not locked by another player)

### Chat Commands Not Working
**Symptoms**: `/command` doesn't respond
**Solutions**:
- Check command handled in Lua script
- Verify `arg1.type = "lua_chat_handled"` set
- Check script loaded: Look in scripts folder
- Verify handler registered: Check `lua_env.handlers`

### Server Won't Start
**Symptoms**: Server fails to start
**Solutions**:
- Check port not in use
- Verify firewall allows port
- Check gamemode loaded correctly
- Look for errors in UI output
- Verify settings valid

### Client Can't Connect
**Symptoms**: Connection fails
**Solutions**:
- Verify server IP and port correct
- Check server password (if set)
- Verify firewall allows connection
- Check server running
- Verify username valid (no special chars, max 32)

---

## Debugging Tips

### Enable VConsole Output
- Check "Print VConsole" setting in client tab
- View VConsole channel in UI output

### Lua Script Debugging
- Use `print()` to output debug info
- Check Lua Errors channel in UI
- Use `printerr()` for errors
- Check script syntax: Lua syntax errors prevent loading

### Packet Debugging
- Look for "HLVRCOOP" in VConsole output
- Check packet parsing in `AlyxGamemode.cs`
- Verify packet type registered in `Packet.cs`
- Check packet format (space-separated, ends with "HLVRCOOP")

### Network Debugging
- Check WebSocket connection status
- Verify JSON serialization (Response format)
- Check message types match expected
- Verify client/server versions compatible

---

## Performance Optimization

### Reduce Update Frequency
- Player position updates already reduced (every 5 frames)
- Entity sync only on position change (threshold: 1 unit)
- Consider increasing thresholds for less frequent updates

### Optimize Lua Scripts
- Cache lookups (avoid repeated table access)
- Use local variables
- Avoid heavy operations in tick events
- Consider C# plugin for performance-critical code

### Network Optimization
- Reduce packet size (minimize data sent)
- Batch updates when possible
- Consider compression for large data

---

## Known Limitations

- **Entity limit**: Large numbers of entities may impact performance
- **Network latency**: Updates depend on network speed
- **Ownership conflicts**: Multiple players can't manipulate same entity simultaneously
- **Workshop dependency**: Requires Workshop addon enabled
- **Single-player engine**: Not designed for multiplayer (workarounds used)
- **VR-specific**: Requires VR headset (not tested without)

---

## Related Documentation

- [VConsole Communication](VConsole-Communication) - Connection troubleshooting
- [Workshop Addon System](Workshop-Addon-System) - Addon issues
- [Networking Architecture](Networking-Architecture) - Network problems
- [Development Guide](Development-Guide) - Debugging development code
