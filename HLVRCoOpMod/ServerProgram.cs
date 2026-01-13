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

namespace HLVRCoOpMod
{
    public class ServerProgram
    {
        public static readonly ServerProgram instance = new();
        public WebSocketServer? wss;
        public List<IndexedClient> connections = new() { };
        public Type? gamemodeType;
        public List<Type> plugins = new();
        public Channel channel = new("SV", "Server", Color.Olive);
        public int tickrate = 66;
        public bool executeThink = false;
        public void Tick()
        {
            PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_Think, tickrate, connections, Map.map);
            LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_Think, tickrate, connections, Map.map);
            if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.Think, tickrate, connections, Map.map) == HandleState.Continue)
            {
                PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_Think, tickrate, connections, Map.map);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_Think, tickrate, connections, Map.map);
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
                    // Tick/thinking
                    Task.Run(() =>
                    {
                        while (executeThink)
                        {
                            Tick();
                            Thread.Sleep(tickrate);
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
                foreach (IndexedClient con in connections.ToList())
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
            foreach (IndexedClient con in connections)
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
            foreach (IndexedClient con in connections)
            {
                con.Session.Send(JsonConvert.SerializeObject(output2));
            }
        }
        public void OnOpen(IWebSocketConnection socket)
        {
            foreach (IndexedClient client in connections)
            {
                if (client.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id)
                {
                    Response outputDisconnect = new("status")
                    {
                        data = client.Username + " connected"
                    };
                    if (gamemodeType != null)
                    {
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_ClientOpen, connections, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_ClientOpen, connections, socket, client.Username);
                        GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.ClientOpen, connections, socket, client.Username);
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_ClientOpen, connections, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_ClientOpen, connections, socket, client.Username);
                    }
                    connections.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(outputDisconnect)));
                    Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, client.Username + " connected"));
                    break;
                }
            }
        }
        public void OnClose(IWebSocketConnection socket)
        {
            foreach(IndexedClient client in connections.ToList())
            {
                if(client.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id)
                {
                    Response outputDisconnect = new("status")
                    {
                        data = client.Username + " disconnected"
                    };
                    if (gamemodeType != null)
                    {
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_ClientClose, connections, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_ClientClose, connections, socket, client.Username);
                        GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.ClientClose, connections, socket, client.Username);
                        PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_ClientClose, connections, socket, client.Username);
                        LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_ClientClose, connections, socket, client.Username);
                    }
                    connections.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(outputDisconnect)));
                    Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, client.Username + " disconnected"));
                    connections.Remove(client);
                    break;
                }
            }
        }

        public void Command(List<string> command)
        {
            if (gamemodeType != null)
            {
                PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_Command, command, connections, gamemodeType);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_Command, command, connections, gamemodeType);
                if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.Command, command, connections, gamemodeType) != HandleState.Handled)
                {
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_Command, command, connections, gamemodeType);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_Command, command, connections, gamemodeType);
                }
            }
        }
        public void OnMessage(string message, IWebSocketConnection socket)
        {
            Response ? response = JsonConvert.DeserializeObject<Response>(message);
            if (gamemodeType != null && response != null && response.type != null)
            {
                PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PreResponse, response, connections, socket, Map.map);
                LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PreResponse, response, connections, socket, Map.map);
                if (GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PreResponse, response, connections, socket, Map.map) == HandleState.Continue)
                {
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PreResponse, response, connections, socket, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PreResponse, response, connections, socket, Map.map);
                    
                    // Check if chat command was handled by plugins/Lua (workaround: response.type changed to "lua_chat_handled")
                    // TODO: Replace with proper HandleState mechanism in future
                    bool chatHandled = false;
                    if (response.type != null && response.type.ToLower() == "lua_chat_handled")
                    {
                        chatHandled = true;
                        // Log the chat message that was handled
                        IndexedClient? handledClient = connections.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
                        if (handledClient != null && response.data != null && response.data.Length > 0)
                        {
                            Program.userInterface.Invoke(() => Program.userInterface.LogToOutput(channel, handledClient.Username + ": " + response.data));
                        }
                        // Skip default processing - command was handled by plugin/Lua
                    }
                    
                    // Only process default handling if not already handled
                    if (!chatHandled)
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
                                    IndexedClient? existingClient = connections.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
                                    if (existingClient != null)
                                    {
                                        // Same connection re-authenticating (e.g., VConsole reconnected)
                                        // Just send the current map
                                        Response output2 = new("authenticated")
                                        {
                                            clientUsername = response.clientUsername,
                                            map = Map.map,
                                        };
                                        socket.Send(JsonConvert.SerializeObject(output2));
                                        return;
                                    }
                                    
                                    // Check if username is already taken by a different connection
                                    foreach (IndexedClient indexed1 in connections)
                                    {
                                        if (indexed1.Username == response.clientUsername)
                                        {
                                            Response output2 = new("status", "Client reconnected elsewhere, closing connection...");
                                            socket.Send(JsonConvert.SerializeObject(output2));
                                            socket.Close();
                                            connections.Remove(indexed1);
                                            break;
                                        }
                                    }
                                    if (response.clientUsername != null)
                                    {
                                        if (response.clientUsername.Length > 32 || Regex.Match(response.clientUsername, @"[\;""]").Success)
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
                                            connections.Add(client);
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
                            IndexedClient? indexedClient = connections.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
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
                                        connections.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(output4)));
                                    }
                                }
                            }
                            break;
                        case "print":
                            IndexedClient? indexed2 = connections.Find(c => c.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id);
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
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PreGamemode_PostResponse, response, connections, socket, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PreGamemode_PostResponse, response, connections, socket, Map.map);
                    GamemodeHandler.Handle(gamemodeType, GamemodeHandleType.PostResponse, response, connections, socket, Map.map);
                    PluginHandler.Handle(plugins, PluginHandleType.Server_PostGamemode_PostResponse, response, connections, socket, Map.map);
                    LuaEnvironment.instance.Handle(PluginHandleType.Server_PostGamemode_PostResponse, response, connections, socket, Map.map);
                }
            }
        }
    }
}
