using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HLVRCoOpModCore;
using Fleck;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.WebSockets;

namespace HLVRCoOpMod
{
    public class ServerProgram
    {
        public static readonly ServerProgram instance = new();
        public WebSocketServer? wss;
        public List<IndexedClient> connections = new() { };
        private static readonly object connectionsLock = new();
        /// <summary>
        /// Returns a thread-safe snapshot of the current connections list.
        /// </summary>
        public List<IndexedClient> GetConnectionsSnapshot()
        {
            lock (connectionsLock)
            {
                return connections.ToList();
            }
        }
        /// <summary>
        /// Returns the current number of connected clients in a thread-safe manner.
        /// </summary>
        public int GetConnectionsCount()
        {
            lock (connectionsLock)
            {
                return connections.Count;
            }
        }
        public Type? gamemodeType;
        public List<Type> plugins = new();
        public Channel channel = new("SV", "Server", Color.Olive);
        public int tickrate = 66;
        public bool executeThink = false;
        public void Tick()
        {
            var snapshot = GetConnectionsSnapshot();
            PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_Think, tickrate, snapshot, Map.map);
            LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_Think, tickrate, snapshot, Map.map);
            if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.Think, tickrate, snapshot, Map.map) == HandleState.Continue)
            {
                PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_Think, tickrate, snapshot, Map.map);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_Think, tickrate, snapshot, Map.map);
            }
        }
        public void Start(Type type, List<Type> plugins)
        {
            if (wss == null)
            {
                PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PreStart);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PreStart);
                if (GamemodeHandler.Handle(type, GamemodeHandleType.PreStart) == HandleState.Continue)
                {
                    Map.map = Settings.Default.ServerMap;
                    this.plugins = plugins;

                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PreStart, type, plugins, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PreStart, type, plugins, Map.map);

                    gamemodeType = type;
                    wss = new WebSocketServer("ws://[::]:" + Settings.Default.ServerPort);
                    wss.Start(socket =>
                    {
                        // OnOpen is called manually after authentication (see line 283) rather than automatically on connection
                        //socket.OnOpen = () => OnOpen(socket);
                        socket.OnClose = () => OnClose(socket);
                        socket.OnMessage = message => OnMessage(message, socket);
                    });
                    if (gamemodeType != null)
                    {
                        Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, "Starting " + gamemodeType.Name + " gamemode on map " + Map.map + " with port " + Settings.Default.ServerPort));
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PostStart, gamemodeType, plugins, Map.map);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PostStart, gamemodeType, plugins, Map.map);
                        GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PostStart, gamemodeType, plugins);
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PostStart, gamemodeType, plugins, Map.map);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PostStart, gamemodeType, plugins, Map.map);
                    }
                    executeThink = true;
                    // Tick/thinking with Stopwatch to prevent drift
                    Task.Run(() =>
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        while (executeThink)
                        {
                            sw.Restart();
                            Tick();
                            int elapsed = (int)sw.ElapsedMilliseconds;
                            int remaining = tickrate - elapsed;
                            if (remaining > 0)
                                Thread.Sleep(remaining);
                        }
                    });
                }
            }
        }
        public void Close()
        {
            executeThink = false;
            if (wss != null)
            {
                if (gamemodeType != null)
                {
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PreClose, gamemodeType, plugins, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PreClose, gamemodeType, plugins, Map.map);
                    if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PreClose, wss) == HandleState.Continue)
                    {
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PostClose, gamemodeType, plugins, Map.map);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PostClose, gamemodeType, plugins, Map.map);
                        GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PostClose, gamemodeType, plugins, Map.map);
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PostClose, gamemodeType, plugins, Map.map);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PostClose, gamemodeType, plugins, Map.map);
                    }
                }
                // Send disconnect command to all clients before closing
                Response disconnectCommand = new("command", "disconnect");
                foreach (IndexedClient con in GetConnectionsSnapshot())
                {
                    try
                    {
                        con.Session.Send(JsonConvert.SerializeObject(disconnectCommand));
                    }
                    catch { } // Ignore errors if connection already closed
                }
                Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, "Closing server on port " + Settings.Default.ServerPort));
                wss.Dispose();
                wss = null;
            }
        }
        /// <summary>
        /// Changes the map for all connected clients by broadcasting map change commands.
        /// </summary>
        /// <param name="map">The map name to switch to</param>
        public void ChangeMap(string map)
        {
            Map.map = map;
            Response output2 = new("command", "addon_play "+map+"; addon_tools_map "+map);
            foreach (IndexedClient con in GetConnectionsSnapshot())
            {
                con.Session.Send(JsonConvert.SerializeObject(output2));
            }
        }
        /// <summary>
        /// Sends a VConsole command to all connected clients.
        /// </summary>
        /// <param name="command">The VConsole command to execute on all clients</param>
        public void GlobalVConsole(string command)
        {
            Response output2 = new("command", command);
            foreach (IndexedClient con in GetConnectionsSnapshot())
            {
                con.Session.Send(JsonConvert.SerializeObject(output2));
            }
        }
        public void OnOpen(IWebSocketConnection socket)
        {
            var snapshot = GetConnectionsSnapshot();
            foreach (IndexedClient client in snapshot)
            {
                if (client.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id)
                {
                    Response outputDisconnect = new("status")
                    {
                        data = client.Username + " connected"
                    };
                    if (gamemodeType != null)
                    {
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_ClientOpen, snapshot, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_ClientOpen, snapshot, socket, client.Username);
                        GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.ClientOpen, snapshot, socket, client.Username);
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_ClientOpen, snapshot, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_ClientOpen, snapshot, socket, client.Username);
                    }
                    snapshot.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(outputDisconnect)));
                    Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, client.Username + " connected"));
                    break;
                }
            }
        }
        public void OnClose(IWebSocketConnection socket)
        {
            List<IndexedClient> connectionsCopy;
            lock(connectionsLock)
            {
                connectionsCopy = connections.ToList();
            }

            foreach(IndexedClient client in connectionsCopy)
            {
                if(client.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id)
                {
                    Response outputDisconnect = new("status")
                    {
                        data = client.Username + " disconnected"
                    };
                    if (gamemodeType != null)
                    {
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_ClientClose, connectionsCopy, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_ClientClose, connectionsCopy, socket, client.Username);
                        GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.ClientClose, connectionsCopy, socket, client.Username);
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_ClientClose, connectionsCopy, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_ClientClose, connectionsCopy, socket, client.Username);
                    }
                    lock(connectionsLock)
                    {
                        connections.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(outputDisconnect)));
                    }
                    Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, client.Username + " disconnected"));
                    lock(connectionsLock)
                    {
                        connections.Remove(client);
                    }
                    break;
                }
            }
        }

        public void Command(List<string> command)
        {
            if (gamemodeType != null)
            {
                var snapshot = GetConnectionsSnapshot();
                PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_Command, command, snapshot, gamemodeType);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_Command, command, snapshot, gamemodeType);
                if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.Command, command, snapshot, gamemodeType) != HandleState.Handled)
                {
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_Command, command, snapshot, gamemodeType);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_Command, command, snapshot, gamemodeType);
                }
            }
        }
        public void OnMessage(string message, IWebSocketConnection socket)
        {
            Response ? response = JsonConvert.DeserializeObject<Response>(message);
            if (response != null && response.type != null)
            {
                // Handle query requests even if gamemode is not initialized
                if (response.type.ToLower() == "query")
                {
                    try
                    {
                        // Get server's local IP address
                        string serverIp = GetLocalIPAddress();
                        
                        int playerCount;
                        lock(connectionsLock)
                        {
                            playerCount = connections.Count;
                        }

                        ServerInfo serverInfo = new ServerInfo
                        {
                            IpAddress = serverIp, // Will be overridden by client with the IP they used to connect
                            Port = Settings.Default.ServerPort,
                            Map = Map.map,
                            PlayerCount = playerCount,
                            MaxPlayers = 16,
                            PasswordProtected = !string.IsNullOrEmpty(Settings.Default.ServerPassword),
                            Gamemode = gamemodeType?.Name ?? "Unknown",
                            ServerName = !string.IsNullOrEmpty(Settings.Default.ServerMap) ?
                                Settings.Default.ServerMap + " Server" : "HLVR Co-Op Server"
                        };
                        
                        Response serverInfoResponse = new("serverinfo")
                        {
                            data = Newtonsoft.Json.JsonConvert.SerializeObject(serverInfo)
                        };
                        socket.Send(JsonConvert.SerializeObject(serverInfoResponse));
                        socket.Close(); // Close query connection immediately
                        return;
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("ServerProgram.Query", ex);
                        socket.Close();
                        return;
                    }
                }
            }
            
            if (gamemodeType != null && response != null && response.type != null)
            {
                var snapshot = GetConnectionsSnapshot();
                PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PreResponse, response, snapshot, socket, Map.map);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PreResponse, response, snapshot, socket, Map.map);
                if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PreResponse, response, snapshot, socket, Map.map) == HandleState.Continue)
                {
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PreResponse, response, snapshot, socket, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PreResponse, response, snapshot, socket, Map.map);
                    
                    // Check if response was handled by plugins/Lua
                    // New proper mechanism: check response.handled property
                    // Backward compatibility: also check for legacy "lua_chat_handled" type
                    bool responseHandled = response.handled;
                    if (!responseHandled && response.type != null && response.type.ToLower() == "lua_chat_handled")
                    {
                        // Legacy workaround: response.type was changed to "lua_chat_handled"
                        responseHandled = true;
                        // Log the chat message that was handled (for chat commands)
                        IndexedClient? handledClient = snapshot.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
                        if (handledClient != null && response.data != null && response.data.Length > 0)
                        {
                            Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, handledClient.Username + ": " + response.data));
                        }
                    }
                    
                    // Only process default handling if not already handled
                    if (!responseHandled && response.type != null)
                    {
                        switch (response.type.ToLower())
                    {
                        case "client":
                            if (response.clientUsername != null && response.password != null)
                            {
                                if (Settings.Default.ServerPassword != "" && response.password != Settings.Default.ServerPassword)
                                {
                                    Response output2 = new("status", "Invalid server password! Closing connection...");
                                    socket.Send(JsonConvert.SerializeObject(output2));
                                    socket.Close();
                                    return;
                                }
                                else
                                {
                                    // Check if this connection is already authenticated (same connection ID)
                                    IndexedClient? existingClient = snapshot.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
                                    if (existingClient != null)
                                    {
                                        // Same connection re-authenticating (e.g., VConsole reconnected)
                                        // Send the current map and trigger re-bootstrap so the client
                                        // receives addon_enable + script creation commands again.
                                        Response output2 = new("authenticated")
                                        {
                                            clientUsername = response.clientUsername,
                                            map = Map.map,
                                        };
                                        socket.Send(JsonConvert.SerializeObject(output2));
                                        if (gamemodeType != null)
                                        {
                                            GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.ReBootstrap, socket, response.clientUsername, snapshot);
                                        }
                                        return;
                                    }
                                    
                                    // Check if username is already taken by a different connection
                                    IndexedClient? clientToRemove = null;
                                    foreach (IndexedClient indexed1 in snapshot)
                                    {
                                        if (indexed1.Username == response.clientUsername)
                                        {
                                            clientToRemove = indexed1;
                                            break;
                                        }
                                    }
                                    if (clientToRemove != null)
                                    {
                                        Response output2 = new("status", "Client reconnected elsewhere, closing connection...");
                                        socket.Send(JsonConvert.SerializeObject(output2));
                                        socket.Close();
                                        lock(connectionsLock)
                                        {
                                            connections.Remove(clientToRemove);
                                        }
                                    }
                                    if (response.clientUsername != null)
                                    {
                                        if (response.clientUsername.Length > 32 || Regex.Match(response.clientUsername, @"[\p{Cc}""]").Success)
                                        {
                                            Response outputRegex = new("status", "Invalid username, closing connection...");
                                            socket.Send(JsonConvert.SerializeObject(outputRegex));
                                            socket.Close();
                                        }
                                        else
                                        {
                                            Response output2 = new("authenticated")
                                            {
                                                clientUsername = response.clientUsername,
                                                map = Map.map,
                                            };
                                            IndexedClient client = new(socket, response.clientUsername, Map.map);
                                            lock(connectionsLock)
                                            {
                                                connections.Add(client);
                                            }
                                            socket.Send(JsonConvert.SerializeObject(output2));
                                            OnOpen(socket);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Response output2 = new("status", "Invalid username! Closing connection...");
                                socket.Send(JsonConvert.SerializeObject(output2));
                                socket.Close();
                            }
                            break;
                        case "chat":
                            IndexedClient? indexedClient = snapshot.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
                            if (indexedClient != null)
                            {
                                if (response.data != null)
                                {
                                    if (response.data.Length == 0) return;
                                    Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, indexedClient.Username + ": " + response.data));
                                    if (response.data.StartsWith("/"))
                                    {
                                        string command = response.data.Split(" ").ToArray().First().Replace("/", "").ToLower();
                                        string[] args = response.data.Split(" ").Skip(1).ToArray();
                                        Response output3 = new("status", response.data)
                                        {
                                            clientUsername = indexedClient.Username,
                                            data = "Unknown command '" + command + "'"
                                        };
                                        switch (command)
                                        {
                                            case "vc":
                                                if (!Settings.Default.ServerDisableUserVconsoleInput)
                                                {
                                                    output3 = new Response("command", response.data)
                                                    {
                                                        data = string.Join(" ", args),
                                                        urgent = false
                                                    };
                                                }
                                                else
                                                {
                                                    output3 = new Response("status", response.data)
                                                    {
                                                        data = "VConsole input is disabled on this server."
                                                    };
                                                }
                                                break;
                                        }
                                        socket.Send(JsonConvert.SerializeObject(output3));
                                    }
                                    else
                                    {
                                        Response output4 = new("chat", response.data)
                                        {
                                            remoteClientUsername = indexedClient.Username
                                        };
                                        snapshot.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(output4)));
                                    }
                                }
                            }
                            break;
                        case "print":
                            IndexedClient? indexed2 = snapshot.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
                            if (indexed2 != null)
                            {
                                if (response.data != null)
                                {
                                    Response output5 = new("vconsole", response.data)
                                    {
                                        clientUsername = indexed2.Username
                                    };
                                    socket.Send(JsonConvert.SerializeObject(output5));
                                }
                            }
                            break;
                        default:
                            // Unknown response type - no action needed
                            break;
                        }
                    }
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PostResponse, response, snapshot, socket, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PostResponse, response, snapshot, socket, Map.map);
                    GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PostResponse, response, snapshot, socket, Map.map);
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PostResponse, response, snapshot, socket, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PostResponse, response, snapshot, socket, Map.map);
                }
            }
        }
        
        /// <summary>
        /// Gets the local IP address of the server
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                using (var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
