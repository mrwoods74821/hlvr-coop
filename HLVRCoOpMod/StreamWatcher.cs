using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

// https://stackoverflow.com/a/2784979

namespace HLVRCoOpMod
{
    public delegate void MessageAvailableEventHandler(object sender,
    MessageAvailableEventArgs e);

    public class MessageAvailableEventArgs : EventArgs
    {
        public MessageAvailableEventArgs(string messageType, byte[] data) : base()
        {
            MessageType = messageType;
            Data = data;
        }

        public string MessageType { get; private set; }
        public byte[] Data { get; private set;}
    }
    public class StreamWatcher
    {
        private readonly Stream stream;

        private bool working = false;

        public StreamWatcher(Stream stream)
        {
            this.stream = stream;
        }

        public void SetWorking(bool state)
        {
            bool oldWorking = working;
            working = state;
            if(oldWorking != state && state)
                Read();
        }

        protected void OnMessageAvailable(MessageAvailableEventArgs e)
        {
            MessageAvailable?.Invoke(this, e);
        }

        protected async void Read()
        {
            while (working)
            {
                if (stream != null)
                {
                    try
                    {
                        byte[] headerBuffer = new byte[10];
                        int bytesRead = 0;
                        while (bytesRead < 10)
                        {
                            int read = await stream.ReadAsync(headerBuffer, bytesRead, 10 - bytesRead);
                            if (read == 0)
                            {
                                // Stream ended - connection lost
                                OnConnectionLost();
                                break; // End of stream
                            }
                            bytesRead += read;
                        }
                        if (bytesRead < 10)
                        {
                            // Incomplete header, connection lost
                            OnConnectionLost();
                            break;
                        }
                        
                        byte[] type = new byte[4] {
                            headerBuffer[0], headerBuffer[1], headerBuffer[2], headerBuffer[3]
                        };
                        string commandType = Encoding.ASCII.GetString(type);
                        // headerBuffer[9] contains (dataLength + 13), so subtract 13 to get actual data length
                        // (13 = header overhead: 4 bytes command + 8 bytes header info + 1 byte terminator)
                        byte length = (byte)(headerBuffer[9] - 13);
                        byte[] data = new byte[length];
                        bytesRead = 0;
                        while (bytesRead < length)
                        {
                            int read = await stream.ReadAsync(data, bytesRead, length - bytesRead);
                            if (read == 0)
                            {
                                // Stream ended - connection lost
                                OnConnectionLost();
                                break; // End of stream
                            }
                            bytesRead += read;
                        }
                        if (bytesRead < length)
                        {
                            // Incomplete data, connection lost
                            OnConnectionLost();
                            break;
                        }
                        
                        OnMessageAvailable(new MessageAvailableEventArgs(commandType, data));
                    }
                    catch (Exception ex)
                    {
                        // Don't log errors if we're intentionally stopping (working == false)
                        // This happens when stream is closed during intentional disconnect
                        if (working)
                        {
                            // Check if this is an expected exception from game exit or connection reset
                            bool isExpectedException = IsExpectedConnectionException(ex);
                            
                            if (!isExpectedException)
                            {
                                // Only log unexpected errors
                                ErrorLogger.LogError("StreamWatcher", ex);
                            }
                            // Connection error - trigger connection lost (even for expected exceptions)
                            // This allows reconnection to work properly
                            OnConnectionLost();
                        }
                        // If working == false, we're intentionally stopping, so just exit
                        break;
                    }
                }
            }
        }

        protected void OnConnectionLost()
        {
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Determines if an exception is an expected connection closure exception.
        /// These occur when the game exits and closes the socket gracefully.
        /// </summary>
        /// <param name="ex">The exception to check</param>
        /// <returns>True if the exception is expected (game exit, connection reset), false otherwise</returns>
        private static bool IsExpectedConnectionException(Exception ex)
        {
            // ObjectDisposedException is expected when stream is closed
            if (ex is ObjectDisposedException)
                return true;

            // SocketException with ConnectionReset or Shutdown is expected when game exits
            if (ex is System.Net.Sockets.SocketException socketEx)
            {
                return socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset ||
                       socketEx.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown;
            }

            // IOException that wraps a SocketException with expected error codes
            if (ex is System.IO.IOException ioEx && ioEx.InnerException is System.Net.Sockets.SocketException innerSocketEx)
            {
                return innerSocketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset ||
                       innerSocketEx.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown;
            }

            // All other exceptions are unexpected and should be logged
            return false;
        }

        public event MessageAvailableEventHandler? MessageAvailable;
        public event EventHandler? ConnectionLost;
    }
}
