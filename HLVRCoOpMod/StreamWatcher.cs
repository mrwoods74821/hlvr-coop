using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                        byte length = (byte)(headerBuffer[9] - headerBuffer.Length);
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
                            ErrorLogger.LogError("StreamWatcher", ex);
                            // Connection error - trigger connection lost
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

        public event MessageAvailableEventHandler? MessageAvailable;
        public event EventHandler? ConnectionLost;
    }
}
