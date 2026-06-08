using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HLVRCoOpModCore;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Handles server discovery via UDP broadcast and server querying
    /// </summary>
    public class ServerDiscovery
    {
        private static ServerDiscovery? instance;
        private static readonly object instanceLock = new object();
        public static ServerDiscovery Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (instanceLock)
                    {
                        if (instance == null)
                            instance = new ServerDiscovery();
                    }
                }
                return instance;
            }
        }
        
        private const int DISCOVERY_PORT = 27026; // Different port for discovery
        private const int BROADCAST_INTERVAL_MS = 5000; // Broadcast every 5 seconds
        private const int SERVER_TIMEOUT_MS = 15000; // Remove servers not seen for 15 seconds
        
        private UdpClient? udpClient;
        private bool isDiscovering = false;
        private bool isBroadcasting = false;
        private CancellationTokenSource? cancellationTokenSource;
        private CancellationTokenSource? broadcastCancellationTokenSource;
        private Dictionary<string, ServerInfo> discoveredServers = new();
        private readonly object serversLock = new object();
        
        private ServerDiscovery()
        {
            // Private constructor for singleton
        }
        
        public event EventHandler<ServerInfo>? ServerDiscovered;
        public event EventHandler<ServerInfo>? ServerUpdated;
        public event EventHandler<string>? ServerRemoved;
        
        /// <summary>
        /// Starts server discovery (listening for servers)
        /// </summary>
        public void StartDiscovery()
        {
            if (isDiscovering)
                return;
                
            isDiscovering = true;
            cancellationTokenSource = new CancellationTokenSource();
            
            Task.Run(() => ListenForServers(cancellationTokenSource.Token));
            Task.Run(() => CleanupStaleServers(cancellationTokenSource.Token));
        }
        
        /// <summary>
        /// Starts broadcasting this server's presence
        /// </summary>
        public void StartBroadcasting()
        {
            if (isBroadcasting)
                return;
                
            isBroadcasting = true;
            broadcastCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => BroadcastServer(broadcastCancellationTokenSource.Token));
        }
        
        /// <summary>
        /// Stops broadcasting this server's presence
        /// </summary>
        public void StopBroadcasting()
        {
            isBroadcasting = false;
            broadcastCancellationTokenSource?.Cancel();
        }
        
        /// <summary>
        /// Stops server discovery
        /// </summary>
        public void StopDiscovery()
        {
            isDiscovering = false;
            cancellationTokenSource?.Cancel();
            
            try
            {
                udpClient?.Close();
                udpClient?.Dispose();
            }
            catch { }
            
            udpClient = null;
            lock (serversLock)
            {
                discoveredServers.Clear();
            }
        }
        
        /// <summary>
        /// Gets all currently discovered servers
        /// </summary>
        public List<ServerInfo> GetDiscoveredServers()
        {
            lock (serversLock)
            {
                return discoveredServers.Values.ToList();
            }
        }
        
        /// <summary>
        /// Queries a specific server for information
        /// </summary>
        public async Task<ServerInfo?> QueryServer(string ipAddress, int port)
        {
            var pingStart = DateTime.Now;
            
            try
            {
                using (var client = new ClientWebSocket())
                {
                    var uri = new Uri($"ws://{ipAddress}:{port}");
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    
                    try
                    {
                        await client.ConnectAsync(uri, cts.Token);
                        
                        // Send query request
                        var queryRequest = new Response("query");
                        var requestJson = JsonConvert.SerializeObject(queryRequest);
                        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                        await client.SendAsync(new ArraySegment<byte>(requestBytes), 
                            WebSocketMessageType.Text, true, cts.Token);
                        
                        // Wait for response (with timeout)
                        var buffer = new byte[4096];
                        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            var response = JsonConvert.DeserializeObject<Response>(responseJson);
                            
                            if (response != null && response.type == "serverinfo")
                            {
                                var serverInfo = ParseServerInfo(response, ipAddress, port);
                                
                                // Measure ping (time from connect to response)
                                serverInfo.Ping = (long)(DateTime.Now - pingStart).TotalMilliseconds;
                                
                                try
                                {
                                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                                        "Query complete", cts.Token);
                                }
                                catch { }
                                
                                return serverInfo;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Server doesn't support query or is unreachable
                        return null;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            
            return null;
        }
        
        /// <summary>
        /// Broadcasts this server's presence on the local network
        /// </summary>
        private async void BroadcastServer(CancellationToken cancellationToken)
        {
            try
            {
                using (var broadcaster = new UdpClient())
                {
                    broadcaster.EnableBroadcast = true;
                    var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                    
                    while (!cancellationToken.IsCancellationRequested && isBroadcasting)
                    {
                        // Only broadcast if server is enabled and running
                        if (Settings.Default.ServerEnabled && 
                            ServerProgram.instance.wss != null)
                        {
                            try
                            {
                                var serverInfo = CreateServerInfo();
                                var infoJson = JsonConvert.SerializeObject(serverInfo);
                                var data = Encoding.UTF8.GetBytes(infoJson);
                                
                                await broadcaster.SendAsync(data, data.Length, broadcastEndpoint);
                            }
                            catch
                            {
                                // Ignore send errors, continue broadcasting
                            }
                        }
                        
                        await Task.Delay(BROADCAST_INTERVAL_MS, cancellationToken);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore broadcast errors
            }
        }
        
        /// <summary>
        /// Listens for server broadcasts on the local network
        /// </summary>
        private async void ListenForServers(CancellationToken cancellationToken)
        {
            try
            {
                udpClient = new UdpClient(DISCOVERY_PORT);
                udpClient.EnableBroadcast = true;
                
                while (!cancellationToken.IsCancellationRequested && isDiscovering)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync();
                        var json = Encoding.UTF8.GetString(result.Buffer);
                        var serverInfo = JsonConvert.DeserializeObject<ServerInfo>(json);
                        
                        if (serverInfo != null)
                        {
                            serverInfo.IpAddress = result.RemoteEndPoint.Address.ToString();
                            serverInfo.IsLocalNetwork = true;
                            serverInfo.LastSeen = DateTime.Now;
                            
                            string serverKey = $"{serverInfo.IpAddress}:{serverInfo.Port}";
                            
                            lock (serversLock)
                            {
                                bool isNew = !discoveredServers.ContainsKey(serverKey);
                                discoveredServers[serverKey] = serverInfo;
                                
                                if (isNew)
                                {
                                    ServerDiscovered?.Invoke(this, serverInfo);
                                }
                                else
                                {
                                    ServerUpdated?.Invoke(this, serverInfo);
                                }
                            }
                        }
                    }
                    catch (SocketException)
                    {
                        // Ignore socket errors, continue listening
                    }
                    catch (Exception)
                    {
                        // Ignore other errors, continue listening
                    }
                }
            }
            catch (Exception)
            {
                // Discovery failed to start
            }
            finally
            {
                try
                {
                    udpClient?.Close();
                    udpClient?.Dispose();
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Removes servers that haven't been seen recently
        /// </summary>
        private async void CleanupStaleServers(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && isDiscovering)
            {
                try
                {
                    await Task.Delay(5000, cancellationToken); // Check every 5 seconds
                    
                    lock (serversLock)
                    {
                        var staleServers = discoveredServers
                            .Where(kvp => (DateTime.Now - kvp.Value.LastSeen).TotalMilliseconds > SERVER_TIMEOUT_MS)
                            .ToList();
                        
                        foreach (var kvp in staleServers)
                        {
                            discoveredServers.Remove(kvp.Key);
                            ServerRemoved?.Invoke(this, kvp.Key);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        
        /// <summary>
        /// Creates server info for this server
        /// </summary>
        private ServerInfo CreateServerInfo()
        {
            return new ServerInfo
            {
                IpAddress = GetLocalIPAddress(),
                Port = Settings.Default.ServerPort,
                Map = Map.map,
                PlayerCount = ServerProgram.instance.GetConnectionsCount(),
                MaxPlayers = 16,
                PasswordProtected = !string.IsNullOrEmpty(Settings.Default.ServerPassword),
                Gamemode = ServerProgram.instance.gamemodeType?.Name ?? "Unknown",
                ServerName = $"{Settings.Default.ServerMap} Server"
            };
        }
        
        /// <summary>
        /// Parses server info from a query response
        /// </summary>
        private ServerInfo ParseServerInfo(Response response, string ipAddress, int port)
        {
            // Parse server info from response.data (JSON string)
            try
            {
                if (response.data != null)
                {
                    var serverInfo = JsonConvert.DeserializeObject<ServerInfo>(response.data);
                    if (serverInfo != null)
                    {
                        serverInfo.IpAddress = ipAddress;
                        serverInfo.Port = port;
                        return serverInfo;
                    }
                }
            }
            catch { }
            
            // Fallback: create basic server info
            return new ServerInfo
            {
                IpAddress = ipAddress,
                Port = port,
                Map = response.map ?? "Unknown"
            };
        }
        
        /// <summary>
        /// Gets the local IP address
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
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
