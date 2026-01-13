using HLVRCoOpModCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Text;
using Newtonsoft.Json;

namespace AlyxGamemode
{
    public class Moveable : IDisposable
    {
        private List<IndexedClient> Connections;
        private Guid ClientID;
        private Entity MovedObject;
        private readonly System.Timers.Timer Tracker;
        private bool TimedOut;
        private bool disposed = false;
        public Moveable(Entity MovedObject, Guid ClientID, List<IndexedClient> Connections)
        {
            this.MovedObject = MovedObject;
            this.ClientID = ClientID;
            this.Connections = Connections;
            System.Timers.Timer timer = new(5000);
            timer.AutoReset = false;
            timer.Elapsed += Track;
            Tracker = timer;
            Tracker.Start();
        }
        public void Update(Entity MovedObject, Guid ClientID, List<IndexedClient> Connections)
        {
            this.MovedObject = MovedObject;
            this.ClientID = ClientID;
            this.Connections = Connections;
            TimedOut = false;
            Tracker.Stop();
            Tracker.Start();
        }
        private void Track(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Response response = new("command", "hlvrcoop_grace " + MovedObject.Name);
            // Use ToList() to avoid modification during iteration
            if (Connections != null)
            {
                foreach (IndexedClient client in Connections.ToList())
                {
                    try
                    {
                        if (client.Session.ConnectionInfo.Id != ClientID)
                        {
                            client.Session.Send(JsonConvert.SerializeObject(response));
                        }
                    }
                    catch (ObjectDisposedException) { } // Connection already closed
                    catch (InvalidOperationException) { } // Connection not available
                    catch (Exception) { } // Ignore other errors in timer callback
                }
            }
            TimedOut = true;
        }
        public Entity GetMovedObject()
        {
            return MovedObject;
        }
        public Guid GetClientID()
        {
            return ClientID;
        }
        public bool GetTimedOut()
        {
            return TimedOut;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (Tracker != null)
                    {
                        Tracker.Stop();
                        Tracker.Dispose();
                    }
                }
                disposed = true;
            }
        }
    }
}
