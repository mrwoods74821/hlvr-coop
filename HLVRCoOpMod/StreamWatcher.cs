/*
    Kiwi's Co-Op Mod for Half-Life: Alyx
    Copyright (c) 2022 KiwifruitDev
    All rights reserved.
    This software is licensed under the MIT License.
    -----------------------------------------------------------------------------
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
    -----------------------------------------------------------------------------
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// https://stackoverflow.com/a/2784979

namespace KiwisCoOpMod
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
                                break; // End of stream
                            bytesRead += read;
                        }
                        if (bytesRead < 10)
                            break; // Incomplete header, exit loop
                        
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
                                break; // End of stream
                            bytesRead += read;
                        }
                        if (bytesRead < length)
                            break; // Incomplete data, exit loop
                        
                        OnMessageAvailable(new MessageAvailableEventArgs(commandType, data));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"StreamWatcher error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }

        public event MessageAvailableEventHandler? MessageAvailable;
    }
}
