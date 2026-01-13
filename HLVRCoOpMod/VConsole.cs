using HLVRCoOpModCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Websocket.Client;

namespace HLVRCoOpMod
{
    public class VConsole
    {
        private WebsocketClient? ws;
        private TcpClient? client;
        private NetworkStream? stream;
        private bool killed = false;
        private List<byte[]> commandQueue = new(); // Queue for byte[] commands when connection is killed
        private Queue<string> bufferFullCommandQueue = new(); // Queue for string commands when buffer is full
        private StreamWatcher? watcher;
        private int reconnectAttempts = 0;
        private const int INITIAL_RETRY_DELAY = 1000; // 1 second
        private const int MAX_RETRY_DELAY = 30000; // 30 seconds maximum delay
        private bool isReconnecting = false;
        private UserInterface? ui;
        private Channel? channel;
        
        // Command buffer full handling
        private bool bufferFull = false;
        private int bufferFullCount = 0;
        private const int MAX_QUEUE_SIZE = 100;
        private const int BUFFER_FULL_THRESHOLD = 3;
        private const int BUFFER_FULL_RECONNECT_THRESHOLD = 10;
        private const int MIN_COMMAND_INTERVAL_MS = 50; // 20 commands/second max
        private DateTime lastCommandTime = DateTime.MinValue;
        private bool isProcessingQueue = false;
        private bool isHandlingPersistentBufferFull = false;
        private readonly object bufferLock = new object(); // Lock for thread-safe buffer state access
        
        public event EventHandler? Reconnected;
        
        public VConsole()
        {
        }
        public bool Connect(WebsocketClient ws, UserInterface? ui = null, Channel? channel = null)
        {
            this.ws = ws;
            this.ui = ui;
            this.channel = channel;
            return Connect();
        }
        public bool Connect()
        {
            // Clean up old connection before creating new one
            if (watcher != null)
            {
                watcher.MessageAvailable -= MessageAvailable;
                watcher.ConnectionLost -= OnConnectionLost;
                watcher.SetWorking(false);
            }
            if (stream != null)
            {
                try { stream.Close(); } catch { }
            }
            if (client != null)
            {
                try { client.Close(); } catch { }
            }

            // Check if the port is listening before attempting to connect
            System.Net.IPEndPoint[] endPoints = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            bool listening = false;
            foreach (System.Net.IPEndPoint endPoint in endPoints)
            {
                if (endPoint.Port == Settings.Default.VconsolePort)
                {
                    listening = true;
                    break;
                }
            }
            if (!listening)
                return false;
            client = new TcpClient("127.0.0.1", Settings.Default.VconsolePort);
            stream = client.GetStream();
            watcher = new StreamWatcher(stream);
            watcher.MessageAvailable += MessageAvailable;
            watcher.ConnectionLost += OnConnectionLost;
            if (watcher != null)
                watcher.SetWorking(true);
            killed = false;
            WriteWindowFocus(true);
            if (stream != null)
            {
                foreach (byte[] command in commandQueue)
                {
                    stream.Write(command);
                }
                commandQueue.Clear();
            }
            reconnectAttempts = 0; // Reset reconnection attempts on successful connect
            lock (bufferLock)
            {
                bufferFull = false; // Reset buffer state on successful connect
                bufferFullCount = 0;
                bufferFullCommandQueue.Clear(); // Clear any queued commands from previous session
            }
            return true;
        }

        private void MessageAvailable(object sender, MessageAvailableEventArgs e)
        {
            if(ws != null)
            {
                string command = e.MessageType.ToUpper();
                switch (command)
                {
                    case "PRNT":
                        byte[] message = e.Data.Skip(30).SkipLast(1).ToArray();
                        string newMessage = Encoding.ASCII.GetString(message).Replace("\n", "");
                        if (newMessage != "")
                        {
                            // Check for "Command buffer full" message
                            if (newMessage.Contains("Command buffer full"))
                            {
                                lock (bufferLock)
                                {
                                    bufferFull = true;
                                    bufferFullCount++;
                                    
                                    // If buffer full persists, may need reconnection as last resort
                                    if (bufferFullCount >= BUFFER_FULL_RECONNECT_THRESHOLD)
                                    {
                                        bufferFullCount = 0; // Reset counter before attempting reconnection
                                        // Buffer persistently full - attempt reconnection (outside lock)
                                        _ = HandlePersistentBufferFull();
                                    }
                                }
                            }
                            else
                            {
                                bool wasBufferFull = false;
                                lock (bufferLock)
                                {
                                    // Buffer cleared - process any queued commands
                                    if (bufferFull)
                                    {
                                        wasBufferFull = true;
                                        bufferFull = false;
                                        bufferFullCount = 0;
                                    }
                                }
                                
                                if (wasBufferFull)
                                {
                                    _ = ProcessCommandQueue(); // Process queued commands asynchronously
                                }
                                
                                // Forward the message to WebSocket (excluding "Command buffer full" messages)
                                Response input = new("print", newMessage);
                                input.timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
                                ws.Send(JsonConvert.SerializeObject(input));
                            }
                        }
                        break;
                }
            }
        }

        public bool Reconnect()
        {
            try
            {
                Disconnect(); // Clean up old connection
                System.Threading.Thread.Sleep(1000); // Wait a moment before reconnecting
                return Connect(); // Attempt to reconnect
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("VConsole.Reconnect", ex);
                return false;
            }
        }

        private async void OnConnectionLost(object? sender, EventArgs e)
        {
            // Prevent multiple simultaneous reconnection attempts
            if (isReconnecting || killed)
                return;

            System.Diagnostics.Debug.WriteLine("VConsole: Connection lost, attempting to reconnect...");
            
            // Notify UI that connection was lost
            if (ui != null && channel != null)
            {
                ui.Invoke(() => ui.LogToOutput(channel, "VConsole connection lost. Attempting to reconnect..."));
            }

            await StartReconnectionLoop();
        }

        /// <summary>
        /// Starts a background reconnection loop that will keep trying to connect until successful or killed.
        /// Can be called from initial connection failure or from connection lost event.
        /// </summary>
        public async Task StartReconnectionLoop()
        {
            // Prevent multiple simultaneous reconnection attempts
            if (isReconnecting || killed)
                return;

            isReconnecting = true;
            reconnectAttempts = 0;

            while (!killed)
            {
                // Calculate delay with exponential backoff, capped at MAX_RETRY_DELAY
                int delay = INITIAL_RETRY_DELAY * (int)Math.Pow(2, reconnectAttempts);
                if (delay > MAX_RETRY_DELAY)
                    delay = MAX_RETRY_DELAY;
                await Task.Delay(delay);

                if (killed)
                    break;

                if (Reconnect())
                {
                    System.Diagnostics.Debug.WriteLine($"VConsole: Successfully reconnected after {reconnectAttempts + 1} attempt(s)");
                    
                    // Notify UI that reconnection succeeded
                    if (ui != null && channel != null)
                    {
                        ui.Invoke(() => ui.LogToOutput(channel, $"VConsole reconnected successfully after {reconnectAttempts + 1} attempt(s)"));
                    }
                    
                    reconnectAttempts = 0;
                    isReconnecting = false;
                    // Notify that reconnection succeeded
                    Reconnected?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // Reset killed flag after failed reconnection attempt so loop can continue
                // (Reconnect() calls Disconnect() which sets killed=true, but we want to keep retrying)
                killed = false;

                reconnectAttempts++;
                System.Diagnostics.Debug.WriteLine($"VConsole: Reconnection attempt {reconnectAttempts} failed, will retry...");
                
                // Notify UI of reconnection attempt (every 5 attempts to avoid spam)
                if (ui != null && channel != null && reconnectAttempts % 5 == 0)
                {
                    ui.Invoke(() => ui.LogToOutput(channel, $"VConsole reconnection attempt {reconnectAttempts}... (waiting for game to start)"));
                }
            }

            // Only reached if killed is set to true
            reconnectAttempts = 0;
            isReconnecting = false;
            
            // Notify UI that reconnection was stopped
            if (ui != null && channel != null)
            {
                ui.Invoke(() => ui.LogToOutput(channel, "VConsole reconnection stopped."));
            }
        }

        public void Disconnect()
        {
            if(!killed)
            {
                killed = true;
                lock (bufferLock)
                {
                    bufferFull = false; // Reset buffer state on disconnect
                    bufferFullCount = 0;
                }
                if (watcher != null)
                {
                    watcher.MessageAvailable -= MessageAvailable;
                    watcher.ConnectionLost -= OnConnectionLost;
                    watcher.SetWorking(false);
                }
                if (stream != null)
                    stream.Close();
                if (client != null)
                    client.Close();
            }
        }

        public void WriteRaw(byte[] data)
        {
            if (stream != null)
                stream.Write(data);
        }

        public void WriteWindowFocus(bool focused = true)
        {
            if (stream != null)
            {
                byte[] vcmd = Encoding.ASCII.GetBytes("VFCS").Reverse().ToArray();
                byte protocol = Convert.ToByte(Settings.Default.VconsoleProtocol);
                byte dataLength = Convert.ToByte(13);
                List<byte> dataList = new()
                {
                    0,
                    protocol,
                    0,
                    0,
                    0,
                    dataLength,
                    0,
                    0,
                    (byte)(focused == true ? 1 : 0)
                };
                foreach (byte cmdByte in vcmd)
                {
                    dataList = dataList.Prepend(cmdByte).ToList();
                }
                byte[] completeData = dataList.ToArray();
                if (!killed)
                    stream.Write(completeData);
                else
                    commandQueue.Add(completeData);
            }
        }
        public void WriteCommand(string command, bool urgent = false)
        {
            // Capture stream reference to avoid null reference if it changes
            NetworkStream? currentStream = stream;
            if (currentStream == null)
                return;
            
            // If connection is killed, queue urgent commands only (existing behavior)
            if (killed)
            {
                if (urgent)
                {
                    byte[] completeData = BuildCommandBytes(command);
                    commandQueue.Add(completeData);
                }
                return;
            }
            
            lock (bufferLock)
            {
                // Rate limiting to prevent buffer overflow (unless urgent)
                if (!urgent)
                {
                    TimeSpan timeSinceLastCommand = DateTime.Now - lastCommandTime;
                    if (timeSinceLastCommand.TotalMilliseconds < MIN_COMMAND_INTERVAL_MS)
                    {
                        // Sending too fast - queue the command
                        if (bufferFullCommandQueue.Count < MAX_QUEUE_SIZE)
                        {
                            bufferFullCommandQueue.Enqueue(command);
                        }
                        return;
                    }
                }
                
                // If buffer is full, queue the command (unless urgent)
                if (bufferFull && !urgent)
                {
                    if (bufferFullCommandQueue.Count < MAX_QUEUE_SIZE)
                    {
                        bufferFullCommandQueue.Enqueue(command);
                    }
                    return;
                }
            }
            
            // Send command immediately (re-check stream is still valid)
            if (currentStream != stream || stream == null)
                return;
            
            // Use currentStream to avoid race condition if stream changes
            NetworkStream? streamToUse = currentStream;
            byte[] commandBytes = BuildCommandBytes(command);
            try
            {
                streamToUse.Write(commandBytes);
                lock (bufferLock)
                {
                    lastCommandTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("VConsole.WriteCommand", ex);
            }
        }
        
        private byte[] BuildCommandBytes(string command)
        {
            byte[] vcmd = Encoding.ASCII.GetBytes("CMND").Reverse().ToArray();
            byte[] data = Encoding.ASCII.GetBytes(command);
            byte dataLength = Convert.ToByte(data.Length + 13);
            byte protocol = Convert.ToByte(Settings.Default.VconsoleProtocol);
            List<byte> dataList = new()
            {
                0, protocol, 0, 0, 0, dataLength, 0, 0
            };
            foreach (byte cmdByte in vcmd)
            {
                dataList = dataList.Prepend(cmdByte).ToList();
            }
            foreach (byte cmdByte in data)
            {
                dataList = dataList.Append(cmdByte).ToList();
            }
            dataList.Add(0x00);
            return dataList.ToArray();
        }
        
        private async Task ProcessCommandQueue()
        {
            // Prevent concurrent queue processing
            lock (bufferLock)
            {
                if (isProcessingQueue || stream == null || killed)
                    return;
                isProcessingQueue = true;
            }
            
            try
            {
                while (true)
                {
                    string? command = null;
                    NetworkStream? currentStream = null;
                    bool shouldContinue = false;
                    
                    lock (bufferLock)
                    {
                        if (bufferFullCommandQueue.Count == 0 || bufferFull || killed)
                            break;
                        
                        currentStream = stream;
                        if (currentStream == null)
                            break;
                        
                        command = bufferFullCommandQueue.Dequeue();
                        shouldContinue = true;
                    }
                    
                    if (!shouldContinue || command == null || currentStream == null)
                        break;
                    
                    byte[] commandBytes = BuildCommandBytes(command);
                    
                    try
                    {
                        // Re-check stream is still valid before writing
                        lock (bufferLock)
                        {
                            if (stream != currentStream || stream == null)
                                break;
                        }
                        
                        currentStream.Write(commandBytes);
                        
                        lock (bufferLock)
                        {
                            lastCommandTime = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("VConsole.ProcessCommandQueue", ex);
                        // If write fails, re-queue the command (unless queue is full)
                        lock (bufferLock)
                        {
                            if (bufferFullCommandQueue.Count < MAX_QUEUE_SIZE)
                            {
                                bufferFullCommandQueue.Enqueue(command);
                            }
                        }
                        break; // Stop processing on error
                    }
                    
                    // Rate limit to prevent buffer fill (20 commands/second max)
                    await Task.Delay(MIN_COMMAND_INTERVAL_MS);
                }
            }
            finally
            {
                lock (bufferLock)
                {
                    isProcessingQueue = false;
                }
            }
        }
        
        private async Task HandlePersistentBufferFull()
        {
            // Prevent concurrent calls to HandlePersistentBufferFull
            lock (bufferLock)
            {
                if (isHandlingPersistentBufferFull)
                    return; // Already handling, skip duplicate call
                isHandlingPersistentBufferFull = true;
            }
            
            try
            {
                // Buffer persistently full - attempt reconnection as last resort
                if (ui != null && channel != null)
                {
                    ui.Invoke(() => ui.LogToOutput(channel, "VConsole buffer persistently full. Attempting reconnection..."));
                }
                
                // Attempt reconnection
                bool reconnected = await Task.Run(() => Reconnect());
                
                if (reconnected)
                {
                    if (ui != null && channel != null)
                    {
                        ui.Invoke(() => ui.LogToOutput(channel, "VConsole reconnected successfully. Buffer cleared."));
                    }
                    // Process any remaining queued commands after reconnection
                    _ = ProcessCommandQueue();
                }
                else
                {
                    if (ui != null && channel != null)
                    {
                        ui.Invoke(() => ui.LogToOutput(channel, "VConsole reconnection failed. Commands may be lost."));
                    }
                }
            }
            finally
            {
                lock (bufferLock)
                {
                    isHandlingPersistentBufferFull = false;
                }
            }
        }
    }
}
