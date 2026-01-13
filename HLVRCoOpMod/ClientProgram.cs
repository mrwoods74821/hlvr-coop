using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.ComponentModel;
using HLVRCoOpModCore;
using Websocket.Client;
using System.Diagnostics;
using System.Drawing;

namespace HLVRCoOpMod
{
    public class ClientProgram
    {
        private WebsocketClient? ws;
        private readonly UserInterface ui;
        private readonly Channel channel = new("CL", "Client", Color.Green);
        private readonly Channel chatChannel = new("CHAT", "Chat", Color.Black);
        private readonly Channel statusChannel = new("STATUS", "Status", Color.DeepPink);
        private readonly Channel vConsoleChannel = new("VC", "VConsole", Color.Maroon);
        private readonly VConsole? vConsole = new();
        private List<Type> plugins = new();
        public int version = 1; // Update version if netcode changes.
        public string map = "";
        public ClientProgram(UserInterface ui)
        {
            this.ui = ui;
        }

        public void ConnectVConsole(WebsocketClient ws)
        {
            if (ws != null && vConsole != null)
            {
                ui.Invoke(() => ui.LogToOutput(channel, "Connecting to VConsole using port " + Settings.Default.VconsolePort));
                if (!vConsole.Connect(ws, ui, channel))
                {
                    ui.Invoke(() =>
                    {
                        ui.LogToOutput(channel, "Failed to connect to VConsole. Starting automatic reconnection...");
                        ui.LogToOutput(channel, "Make sure Half-Life: Alyx is running with launch options: -console -vconsole -language english");
                    });
                    // Start background reconnection loop for initial connection failure
                    _ = vConsole.StartReconnectionLoop();
                }
            }
        }
        public void Start(List<Type> pluginTypes)
        {
            bool error = false;
            if (ws == null)
            {
                map = "";
                PluginHandler.Handle(pluginTypes, PluginHandleType.Client_PreStart, ui);
                LuaEnvironment.instance.Handle(PluginHandleType.Client_PreStart, ui);
                plugins = pluginTypes;
                ws = new WebsocketClient(new Uri("ws://" + Settings.Default.ClientIpAddress + ":" + Settings.Default.ClientPort))
                {
                    ReconnectTimeout = TimeSpan.FromSeconds(60),
                    IsReconnectionEnabled = true
                };
                ws.Start();
                ws.DisconnectionHappened.Subscribe(info =>
                {
                    ui.Invoke(() => ui.LogToOutput(channel, "Disconnected: " + info.Type.ToString()));
                    // Send disconnect command to game to return to main menu
                    if (vConsole != null)
                    {
                        try
                        {
                            vConsole.WriteCommand("disconnect", true);
                        }
                        catch { } // Ignore errors if VConsole already disconnected
                    }
                });
                ws.MessageReceived.Subscribe(msg =>
                {
                    Response? response = JsonConvert.DeserializeObject<Response>(msg.Text);
                    if (response != null && response.type != null)
                    {
                        PluginHandler.Handle(plugins, PluginHandleType.Client_PreResponse, response);
                        LuaEnvironment.instance.Handle(PluginHandleType.Client_PreResponse, response);
                        switch (response.type)
                        {
                            case "authenticated":
                                if (response.version.HasValue)
                                {
                                    if (response.version > Response.internalVersion)
                                    {
                                        ui.Invoke(() => ui.LogToOutput(channel, "Client is running an older version! Please update your client."));
                                    }
                                    else if (response.version < Response.internalVersion)
                                    {
                                        ui.Invoke(() => ui.LogToOutput(channel, "Server is running an older version! Please ask the owner to update their server."));
                                    }
                                }
                                if (vConsole != null && response.map != null)
                                {
                                    if (map != response.map)
                                    {
                                        ui.Invoke(() => ui.LogToOutput(channel, "Changing map to " + response.map));
                                        vConsole.WriteCommand("addon_play " + response.map + ";addon_tools_map " + response.map);
                                        map = response.map;
                                    }
                                }
                                break;
                            case "chat":
                                if (response.data != null && response.remoteClientUsername != null)
                                {
                                    ui.Invoke(() => ui.LogToOutput(chatChannel, response.remoteClientUsername + ": " + response.data));
                                }
                                break;
                            case "command":
                                if (vConsole != null && response.data != null)
                                {
                                    vConsole.WriteCommand(response.data, response.urgent);
                                }
                                break;
                            case "status":
                                if (vConsole != null && response.data != null)
                                {
                                    if (ui != null)
                                        ui.Invoke(() => ui.LogToOutput(statusChannel, response.data));
                                }
                                break;
                            case "vconsole":
                                if (vConsole != null && response.data != null && Settings.Default.ClientPrintVconsole)
                                {
                                    if (ui != null)
                                        ui.Invoke(() => ui.LogToOutput(vConsoleChannel, response.data));
                                }
                                break;
                            default:
                                ui.Invoke(() => ui.LogToOutput(channel, "UNIMPLEMENTED TYPE: " + response.type + "!"));
                                break;
                        }
                        PluginHandler.Handle(plugins, PluginHandleType.Client_PostResponse, response);
                        LuaEnvironment.instance.Handle(PluginHandleType.Client_PostResponse, response);
                    }
                    else
                    {
                        ui.Invoke(() => ui.LogToOutput(channel, "SERVER SENT INVALID DATA!"));
                    }
                });
                ws.ReconnectionHappened.Subscribe(recinfo =>
                {
                    Response input = new("client")
                    {
                        clientUsername = Settings.Default.ClientUsername,
                        password = Settings.Default.ClientPassword,
                        timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds
                    };
                    ws.Send(JsonConvert.SerializeObject(input));
                    ui.Invoke(() => ui.LogToOutput(channel, "Reconnected to server at " + Settings.Default.ClientIpAddress + ":" + Settings.Default.ClientPort));
                });
                ConnectVConsole(ws);
                // Subscribe to VConsole reconnection event to re-authenticate with server
                if (vConsole != null)
                {
                    vConsole.Reconnected += (sender, e) =>
                    {
                        // Re-authenticate with server when VConsole reconnects
                        if (ws != null && ws.IsStarted)
                        {
                            // Reset map so it will always reload (game quit/resumed scenario)
                            map = "";
                            Response input = new("client")
                            {
                                clientUsername = Settings.Default.ClientUsername,
                                password = Settings.Default.ClientPassword,
                                timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds
                            };
                            ws.Send(JsonConvert.SerializeObject(input));
                            ui.Invoke(() => ui.LogToOutput(channel, "VConsole reconnected, re-authenticating with server..."));
                            
                            // Send saved player body preference when VConsole reconnects
                            int preference = Settings.Default.PlayerBodyPreference;
                            if (preference >= 0 && preference <= 2)
                            {
                                SendVConsoleCommand($"convar_set player_body_preference {preference}");
                            }
                        }
                    };
                }
                
                // Send saved player body preference to game when client starts
                int savedPreference = Settings.Default.PlayerBodyPreference;
                if (savedPreference >= 0 && savedPreference <= 2 && vConsole != null)
                {
                    // Delay slightly to ensure VConsole is connected
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000); // Wait 1 second for VConsole to connect
                        SendVConsoleCommand($"convar_set player_body_preference {savedPreference}");
                    });
                }
                
                PluginHandler.Handle(plugins, PluginHandleType.Client_PostStart, ui, ws, error);
                LuaEnvironment.instance.Handle(PluginHandleType.Client_PostStart, ui, ws, error);
            }
        }
        public void SendVConsoleCommand(string command)
        {
            if (vConsole != null)
            {
                vConsole.WriteCommand(command);
            }
        }

        public void Close()
        {
            if (ws != null)
            {
                PluginHandler.Handle(plugins, PluginHandleType.Client_PreClose, ui, ws);
                LuaEnvironment.instance.Handle(PluginHandleType.Client_PreClose, ui, ws);
                ws.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closed by HLVRCOOP.");
                ws.Dispose();
                ws = null;
                ui.Invoke(() => ui.LogToOutput(channel, "Client closed"));
            }
            if (vConsole != null)
            {
                vConsole.DisconnectAndStopReconnecting(); // Stop reconnection when client is closing
            }
        }
        public void Chat(string text)
        {
            if (ws != null && ws.IsStarted && text.Length > 0)
            {
                Response chatResponse = new("chat", text);
                chatResponse.timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
                ws.Send(chatResponse.ToString());
            }
        }
    }
}
