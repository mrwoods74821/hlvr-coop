# Networking Architecture

[← Workshop Addon System](Workshop-Addon-System) | [Packet System →](Packet-System)

---

## WebSocket-Based Communication

The mod uses **WebSocket** protocol for client-server communication:

- **Server**: Fleck library (C#)
- **Client**: WebsocketClient library (C#)
- **Protocol**: JSON over WebSocket
- **Port**: Configurable (default in settings)

---

## Server Architecture

```csharp
WebSocketServer wss = new WebSocketServer("ws://[::]:" + port);
wss.Start(socket =>
{
    socket.OnClose = () => OnClose(socket);
    socket.OnMessage = message => OnMessage(message, socket);
});
```

**Server manages**:
- List of connected clients (`List<IndexedClient>`)
- Player state (`AlyxGlobalData.instance`)
- Entity ownership (`Moveable` tracking)
- Message routing and broadcasting

---

## Client Architecture

```csharp
WebsocketClient ws = new WebsocketClient(new Uri("ws://" + ip + ":" + port));
ws.Start();
ws.MessageReceived.Subscribe(msg =>
{
    Response response = JsonConvert.DeserializeObject<Response>(msg.Text);
    // Process response
});
```

**Client handles**:
- Connection to server
- Sending player data
- Receiving updates from server
- VConsole communication

---

## Message Format: Response Class

All messages use the `Response` class (serialized as JSON):

```csharp
public class Response
{
    public string? type;              // "command", "chat", "status", "print", etc.
    public string? data;              // Message content
    public string? map;               // Current map
    public string? remoteClientUsername;  // Sender username
    public string? clientUsername;    // Client username
    public string? password;          // Authentication
    public int? version;              // Protocol version
    public bool urgent;               // Urgent flag
    public long? timestamp;           // Unix timestamp
}
```

**Response Types**:
- `"authenticated"` - Connection successful
- `"chat"` - Chat message
- `"command"` - VConsole command to execute
- `"status"` - Status message (displayed in UI)
- `"print"` - VConsole output (from game)
- `"vconsole"` - VConsole output (processed)
- `"client"` - Client authentication request
- `"lua_chat_handled"` - Chat command handled by Lua

---

## Connection Flow

### Client Connection

1. Client connects to WebSocket server
2. Server receives connection, socket created
3. Client sends `Response("client")` with username/password
4. Server validates:
   - Password matches (if set)
   - Username not already in use
   - Username valid (no special chars, max 32 chars)
5. Server creates `IndexedClient` and adds to connections
6. Server sends `Response("authenticated")` with map name
7. Server triggers `OnOpen` events
8. Client receives authentication, connects VConsole
9. Client sends `Response("print", "INIT HLVRCOOP")` to initialize

### Server Initialization

1. Server starts WebSocket server
2. Server begins tick loop (every 66ms)
3. Server loads gamemode and plugins
4. Server waits for client connections

---

## Message Broadcasting

When a client sends data, server broadcasts to other clients:

```csharp
// Example: Broadcast head position
Response movement = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_head_" + playerIndex + " " + origin + " " + angles);
foreach (IndexedClient client in connections)
{
    Player? otherPlayer = AlyxGlobalData.instance.GetPlayer(client.Session.ConnectionInfo.Id);
    if (otherPlayer != null && client.Session.ConnectionInfo.Id != senderSocket.ConnectionInfo.Id)
    {
        client.Session.Send(JsonConvert.SerializeObject(movement));
    }
}
```

---

## Authentication & Security

- **Password protection**: Server can require password
- **Username validation**: Max 32 chars, no `;` or `"`
- **Duplicate prevention**: Username must be unique
- **IP-based banning**: Support for IP bans
- **Version checking**: Protocol version validation

---

## Related Documentation

- [Packet System](Packet-System) - Data packet types
- [Player Management](Player-Management) - Player connection flow
- [VConsole Communication](VConsole-Communication) - Game communication
