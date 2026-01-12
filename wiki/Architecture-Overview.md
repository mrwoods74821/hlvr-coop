# Architecture Overview

[← Home](Home) | [System Components →](System-Components)

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Half-Life: Alyx                         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │        Workshop Addon (hlvr_coop)                    │   │
│  │  ┌────────────────────────────────────────────────┐  │   │
│  │  │  Lua Scripts (hlvrcoop_interval.lua)               │  │   │
│  │  │  - Tracks player/entity positions              │  │   │
│  │  │  - Monitors entity outputs                     │  │   │
│  │  │  - Sends data via print() with "HLVRCOOP" tag      │  │   │
│  │  └────────────────────────────────────────────────┘  │   │
│  │              ↕ VConsole (TCP)                        │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│              HLVRCoOpMod (C# Application)                   │
│  ┌──────────────────────┐  ┌──────────────────────────┐     │
│  │   VConsole Client    │  │   WebSocket Server       │     │
│  │  - Connects to game  │  │  - Manages connections   │     │
│  │  - Sends commands    │  │  - Processes messages    │     │
│  │  - Receives output   │  │  - Broadcasts updates    │     │
│  └──────────────────────┘  └──────────────────────────┘     │
│  ┌──────────────────────┐  ┌──────────────────────────┐     │
│  │   AlyxGamemode       │  │   Lua Environment        │     │
│  │  - Packet processing │  │  - Script execution      │     │
│  │  - Entity management │  │  - Event handling        │     │
│  └──────────────────────┘  └──────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│              Client Applications (Multiple)                 │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  WebSocket Client                                    │   │
│  │  - Connects to server                                │   │
│  │  - Sends player data                                 │   │
│  │  - Receives updates                                  │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Data Flow

1. **Game → C#**: Lua scripts in game send data via `print()` → VConsole captures → Parsed as packets
2. **C# → Game**: C# sends VConsole commands → Executed in game → Affects game state
3. **Client ↔ Server**: WebSocket messages → JSON responses → Synchronized state

---

## Related Documentation

- [System Components](System-Components) - Detailed component breakdown
- [VConsole Communication](VConsole-Communication) - Game communication protocol
- [Networking Architecture](Networking-Architecture) - Client-server system
- [How Everything Works Together](How-Everything-Works-Together) - Runtime flows
