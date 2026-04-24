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
        private volatile bool killed = false;
        private volatile bool shouldStopReconnecting = false; // Flag to stop reconnection loop (set when client closes)
        private List<byte[]> commandQueue = new(); // Queue for byte[] commands when connection is killed

        // Priority queue system for buffer full commands
        private enum CommandPriority { Low = 0, Normal = 1, Critical = 2 }

        private class PrioritizedCommand
        {
            public string Command { get; set; } = "";
            public CommandPriority Priority { get; set; }
        }

        private readonly Queue<PrioritizedCommand> criticalQueue = new();
        private readonly Queue<PrioritizedCommand> normalQueue = new();
        private readonly Queue<PrioritizedCommand> lowQueue = new();
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
        private const int MAX_QUEUE_SIZE = 150;
        private DateTime lastQueueWarningTime = DateTime.MinValue;
        private const int BUFFER_FULL_THRESHOLD = 3;
        private const int BUFFER_FULL_RECONNECT_THRESHOLD = 10;
        private const int MIN_COMMAND_INTERVAL_MS = 50; // 20 commands/second max
        private DateTime lastCommandTime = DateTime.MinValue;
        private bool isProcessingQueue = false;
        private bool isHandlingPersistentBufferFull = false;
        private readonly object bufferLock = new object(); // Lock for thread-safe buffer state access
        private readonly object streamLock = new object(); // Lock for thread-safe stream operations

        public event EventHandler? Reconnected;

        /// <summary>
        /// Classifies a command by priority based on its content
        /// </summary>
        private static CommandPriority ClassifyCommand(string command)
        {
            if (command.StartsWith("addon_enable") ||
                command.StartsWith("ent_create") ||
                command.StartsWith("ent_remove") ||
                command.StartsWith("ent_fire") ||
                command.StartsWith("hlvrcoop_fireoutput") ||
                command.StartsWith("hlvrcoop_cache_entity") ||
                command.StartsWith("hlvrcoop_teleport") ||
                command.StartsWith("hlvrcoop_sethealth") ||
                command.StartsWith("hlvrcoop_break") ||
                command.StartsWith("echo ") ||
                command.StartsWith("sv_cheats") ||
                command.StartsWith("buddha") ||
                command.StartsWith("unpause") ||
                command.StartsWith("play "))
                return CommandPriority.Critical;

            if (command.StartsWith("hlvrcoop_setlocation_head") ||
                command.StartsWith("hlvrcoop_setlocation_hands") ||
                command.StartsWith("hlvrcoop_setlocation "))
                return CommandPriority.Normal;

            return CommandPriority.Low;
        }

        /// <summary>
        /// Gets the total size of all priority queues combined
        /// </summary>
        private int GetTotalQueueSize()
        {
            return criticalQueue.Count + normalQueue.Count + lowQueue.Count;
        }

        /// <summary>
        /// Dequeues the next command based on priority (Critical > Normal > Low)
        /// Returns null if all queues are empty
        /// </summary>
        private PrioritizedCommand? DequeueNext()
        {
            if (criticalQueue.Count > 0)
                return criticalQueue.Dequeue();
            if (normalQueue.Count > 0)
                return normalQueue.Dequeue();
            if (lowQueue.Count > 0)
                return lowQueue.Dequeue();
            return null;
        }

        /// <summary>
        /// Enqueues a command to the appropriate priority queue with overflow handling
        /// </summary>
        private void EnqueuePrioritizedCommand(string command)
        {
            CommandPriority priority = ClassifyCommand(command);
            int totalSize = GetTotalQueueSize();

            // Check for queue depth warning
            if (totalSize > 75 && (DateTime.Now - lastQueueWarningTime).TotalSeconds > 10)
            {
                lastQueueWarningTime = DateTime.Now;
                ErrorLogger.LogSync($"VConsole queue depth: {totalSize}/150");
            }

            // Handle overflow - drop from low priority first, then normal, never critical
            if (totalSize >= MAX_QUEUE_SIZE)
            {
                if (priority == CommandPriority.Critical)
                {
                    // Always accept critical commands
                    criticalQueue.Enqueue(new PrioritizedCommand { Command = command, Priority = priority });
                }
                else if (priority == CommandPriority.Normal)
                {
                    // Drop from low queue first to make room
                    if (lowQueue.Count > 0)
                    {
                        lowQueue.Dequeue();
                        normalQueue.Enqueue(new PrioritizedCommand { Command = command, Priority = priority });
                    }
                    // If low queue is empty, drop the command
                }
                else // Low priority
                {
                    // Drop low priority commands when queue is full
                    if (lowQueue.Count < MAX_QUEUE_SIZE / 3) // Allow some low priority commands
                    {
                        lowQueue.Enqueue(new PrioritizedCommand { Command = command, Priority = priority });
                    }
                }
            }
            else
            {
                // Add to appropriate queue
                switch (priority)
                {
                    case CommandPriority.Critical:
                        criticalQueue.Enqueue(new PrioritizedCommand { Command = command, Priority = priority });
                        break;
                    case CommandPriority.Normal:
                        normalQueue.Enqueue(new PrioritizedCommand { Command = command, Priority = priority });
                        break;
                    case CommandPriority.Low:
                        lowQueue.Enqueue(new PrioritizedCommand { Command = command, Priority = priority });
                        break;
                }
            }
        }
        
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
            
            NetworkStream? newStream = null;
            lock (streamLock)
            {
                client = new TcpClient("127.0.0.1", Settings.Default.VconsolePort);
                newStream = client.GetStream();
                stream = newStream;
                killed = false; // Set killed = false inside lock before stream operations
            }
            
            watcher = new StreamWatcher(newStream);
            watcher.MessageAvailable += MessageAvailable;
            watcher.ConnectionLost += OnConnectionLost;
            if (watcher != null)
                watcher.SetWorking(true);
            WriteWindowFocus(true);
            if (stream != null && !killed)
            {
                foreach (byte[] command in commandQueue)
                {
                    try
                    {
                        stream.Write(command);
                    }
                    catch (ObjectDisposedException)
                    {
                        killed = true;
                        break;
                    }
                    catch (SocketException)
                    {
                        killed = true;
                        break;
                    }
                }
                if (!killed)
                {
                    commandQueue.Clear();
                }
            }
            reconnectAttempts = 0; // Reset reconnection attempts on successful connect
            lock (bufferLock)
            {
                bufferFull = false; // Reset buffer state on successful connect
                bufferFullCount = 0;
                criticalQueue.Clear();
                normalQueue.Clear();
                lowQueue.Clear();
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
            if (isReconnecting)
                return;

            // Mark as killed immediately and synchronously to prevent WriteCommand from attempting to write to disposed stream
            // Use streamLock to ensure atomic operation with any ongoing WriteCommand calls
            lock (streamLock)
            {
                killed = true;
            }

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
            if (isReconnecting)
                return;

            isReconnecting = true;
            reconnectAttempts = 0;
            
            // Reset flags to allow reconnection loop to run
            // (killed was set in OnConnectionLost to prevent writes, but we need to allow reconnection)
            shouldStopReconnecting = false; // Reset stop flag when starting new reconnection loop
            lock (streamLock)
            {
                killed = false;
            }

            while (true)
            {
                // Calculate delay with exponential backoff, capped at MAX_RETRY_DELAY
                int delay = INITIAL_RETRY_DELAY * (int)Math.Pow(2, reconnectAttempts);
                if (delay > MAX_RETRY_DELAY)
                    delay = MAX_RETRY_DELAY;
                await Task.Delay(delay);

                // Check if we should stop reconnecting (only set when client intentionally closes)
                // Don't check 'killed' here because it's set during cleanup and shouldn't stop reconnection
                if (shouldStopReconnecting)
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
                // Note: We don't reset shouldStopReconnecting here - that should only be reset when starting a new loop
                lock (streamLock)
                {
                    killed = false;
                }

                reconnectAttempts++;
                System.Diagnostics.Debug.WriteLine($"VConsole: Reconnection attempt {reconnectAttempts} failed, will retry...");
                
                // Notify UI of reconnection attempt (every 5 attempts to avoid spam)
                if (ui != null && channel != null && reconnectAttempts % 5 == 0)
                {
                    ui.Invoke(() => ui.LogToOutput(channel, $"VConsole reconnection attempt {reconnectAttempts}... (waiting for game to start)"));
                }
            }

            // Only reached if shouldStopReconnecting is set to true (client intentionally closed)
            reconnectAttempts = 0;
            isReconnecting = false;
            shouldStopReconnecting = false; // Reset flag for next time
            
            // Notify UI that reconnection was stopped
            if (ui != null && channel != null)
            {
                ui.Invoke(() => ui.LogToOutput(channel, "VConsole reconnection stopped."));
            }
        }

        public void Disconnect()
        {
            lock (streamLock)
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
        }
        
        /// <summary>
        /// Disconnects and stops any active reconnection loops.
        /// Use this when the client is intentionally closing, not during automatic reconnection.
        /// </summary>
        public void DisconnectAndStopReconnecting()
        {
            shouldStopReconnecting = true; // Signal reconnection loop to stop
            Disconnect(); // Clean up connection
        }
        
        /// <summary>
        /// Checks if VConsole is currently connected and ready to send commands
        /// </summary>
        public bool IsConnected()
        {
            lock (streamLock)
            {
                if (killed || stream == null || client == null)
                    return false;
                
                try
                {
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void WriteRaw(byte[] data)
        {
            if (stream != null && !killed)
            {
                try
                {
                    stream.Write(data);
                }
                catch (ObjectDisposedException)
                {
                    killed = true;
                }
                catch (SocketException)
                {
                    killed = true;
                }
            }
        }

        public void WriteWindowFocus(bool focused = true)
        {
            if (stream != null && !killed)
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
                try
                {
                    stream.Write(completeData);
                }
                catch (ObjectDisposedException)
                {
                    killed = true;
                    commandQueue.Add(completeData);
                }
                catch (SocketException)
                {
                    killed = true;
                    commandQueue.Add(completeData);
                }
            }
            else if (killed)
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
                commandQueue.Add(completeData);
            }
        }
        public void WriteCommand(string command, bool urgent = false)
        {
            // Check killed flag FIRST to prevent writes to disposed stream
            // This must be checked before any stream operations to avoid ObjectDisposedException
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
                        // Sending too fast - queue the command with priority
                        EnqueuePrioritizedCommand(command);
                        return;
                    }
                }

                // If buffer is full, queue the command (unless urgent)
                if (bufferFull && !urgent)
                {
                    EnqueuePrioritizedCommand(command);
                    return;
                }
            }
            
            // Lock stream operations to prevent race conditions with disposal
            lock (streamLock)
            {
                // Re-check killed flag after acquiring lock (stream might have been disposed)
                if (killed)
                {
                    if (urgent)
                    {
                        byte[] completeData = BuildCommandBytes(command);
                        commandQueue.Add(completeData);
                    }
                    return;
                }
                
                // Capture stream reference inside lock
                NetworkStream? currentStream = stream;
                if (currentStream == null)
                    return;
                
                // Check if underlying socket is still connected before attempting write
                // This prevents ObjectDisposedException from being thrown
                try
                {
                    if (client != null && !client.Connected)
                    {
                        killed = true;
                        return;
                    }
                }
                catch
                {
                    // If we can't check connection state, assume it's dead
                    killed = true;
                    return;
                }
                
                byte[] commandBytes = BuildCommandBytes(command);
                try
                {
                    currentStream.Write(commandBytes);
                    lock (bufferLock)
                    {
                        lastCommandTime = DateTime.Now;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Stream was disposed (game quit), mark as killed silently
                    // Don't log this error - it's expected when the game quits
                    killed = true;
                    return; // Exit immediately to prevent any further processing
                }
                catch (SocketException)
                {
                    // Connection was aborted (game quit/network error), mark as killed silently
                    // Don't log this error - it's expected when the game quits
                    killed = true;
                    return; // Exit immediately to prevent any further processing
                }
                catch (Exception ex)
                {
                    // Check if exception is related to stream disposal/connection loss
                    // If so, mark as killed and don't log
                    if (ex.Message.Contains("disposed") || 
                        ex.Message.Contains("aborted") || 
                        ex.Message.Contains("connection") ||
                        ex is System.IO.IOException)
                    {
                        killed = true;
                        return;
                    }
                    
                    // Only log if not already killed and it's not a connection-related error
                    if (!killed)
                    {
                        ErrorLogger.LogError("VConsole.WriteCommand", ex);
                    }
                }
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
                        if (GetTotalQueueSize() == 0 || bufferFull || killed)
                            break;

                        currentStream = stream;
                        if (currentStream == null)
                            break;

                        PrioritizedCommand? prioritizedCommand = DequeueNext();
                        if (prioritizedCommand == null)
                            break;

                        command = prioritizedCommand.Command;
                        shouldContinue = true;
                    }
                    
                    if (!shouldContinue || command == null || currentStream == null)
                        break;
                    
                    byte[] commandBytes = BuildCommandBytes(command);
                    
                    try
                    {
                        // Re-check stream is still valid before writing (use streamLock for consistency)
                        lock (streamLock)
                        {
                            if (killed || stream != currentStream || stream == null)
                                break;
                            
                            currentStream.Write(commandBytes);
                        }
                        
                        lock (bufferLock)
                        {
                            lastCommandTime = DateTime.Now;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Stream was disposed (game quit), mark as killed silently
                        lock (streamLock)
                        {
                            killed = true;
                        }
                        break; // Stop processing on error
                    }
                    catch (SocketException)
                    {
                        // Connection was aborted (game quit/network error), mark as killed silently
                        lock (streamLock)
                        {
                            killed = true;
                        }
                        break; // Stop processing on error
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("VConsole.ProcessCommandQueue", ex);
                        // If write fails, re-queue the command (unless queue is full)
                        lock (bufferLock)
                        {
                            if (GetTotalQueueSize() < MAX_QUEUE_SIZE)
                            {
                                EnqueuePrioritizedCommand(command);
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
