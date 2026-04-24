using HLVRCoOpModCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AlyxGamemode
{
    public sealed class AlyxGlobalData
    {
        public static readonly AlyxGlobalData instance = new();
        private readonly List<Player> players = new();
        private readonly List<Moveable> manipulatedEntities = new();

        public enum ConnectionState
        {
            Connecting,
            Connected,
            Authenticated,
            Initializing,
            Ready,
            Disconnected
        }

        public sealed class SessionState
        {
            public Guid ClientId { get; set; }
            public string Username { get; set; } = "";
            public ConnectionState Phase { get; set; } = ConnectionState.Connecting;
            public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
            public int DroppedPacketCount { get; set; } = 0;
            public string LastDropReason { get; set; } = "";
            public DateTime? FirstPacketUtc { get; set; }
            public bool WatchdogStarted { get; set; } = false;
            public int BootstrapRetryCount { get; set; } = 0;
        }

        private readonly Dictionary<Guid, SessionState> sessionStates = new();
        private readonly object sync = new();
        public bool AddManipulatedEntity(Entity MovedObject, Guid ClientID, List<IndexedClient> Connections)
        {
            lock (sync)
            {
                Moveable? oldMoveable = manipulatedEntities.Find(m => m.GetMovedObject().Name == MovedObject.Name);
                if (oldMoveable == null)
                {
                    manipulatedEntities.Add(new Moveable(MovedObject, ClientID, Connections));
                    return true;
                }
                else if(oldMoveable.GetClientID() == ClientID || oldMoveable.GetTimedOut())
                {
                    oldMoveable.Update(MovedObject, ClientID, Connections);
                    return true;
                }
                return false;
            }
        }
        public void RemoveManipulatedEntity(string name)
        {
            lock (sync)
            {
                Moveable? moveable = manipulatedEntities.Find(m => m.GetMovedObject().Name == name);
                if (moveable != null)
                {
                    moveable.Dispose();
                    manipulatedEntities.Remove(moveable);
                }
            }
        }

        public List<Player> GetPlayers()
        {
            return players;
        }

        public SessionState UpsertSession(Guid id, string username, ConnectionState phase)
        {
            lock (sync)
            {
                if (!sessionStates.TryGetValue(id, out SessionState? state))
                {
                    state = new SessionState
                    {
                        ClientId = id,
                        Username = username,
                        Phase = phase,
                        LastUpdatedUtc = DateTime.UtcNow
                    };
                    sessionStates[id] = state;
                }
                else
                {
                    state.Username = string.IsNullOrWhiteSpace(username) ? state.Username : username;
                    state.Phase = phase;
                    state.LastUpdatedUtc = DateTime.UtcNow;
                }

                return state;
            }
        }

        public SessionState? GetSession(Guid id)
        {
            lock (sync)
            {
                sessionStates.TryGetValue(id, out SessionState? state);
                return state;
            }
        }

        public bool IsSessionReady(Guid id)
        {
            lock (sync)
            {
                if (!sessionStates.TryGetValue(id, out SessionState? state))
                    return false;
                return state.Phase == ConnectionState.Ready;
            }
        }

        public SessionState MarkPacketObserved(Guid id)
        {
            lock (sync)
            {
                if (!sessionStates.TryGetValue(id, out SessionState? state))
                {
                    state = new SessionState
                    {
                        ClientId = id,
                        Phase = ConnectionState.Connecting
                    };
                    sessionStates[id] = state;
                }

                state.FirstPacketUtc ??= DateTime.UtcNow;
                state.LastUpdatedUtc = DateTime.UtcNow;
                return state;
            }
        }

        public bool TryStartWatchdog(Guid id)
        {
            lock (sync)
            {
                if (!sessionStates.TryGetValue(id, out SessionState? state))
                {
                    state = new SessionState
                    {
                        ClientId = id,
                        Phase = ConnectionState.Connecting
                    };
                    sessionStates[id] = state;
                }

                if (state.WatchdogStarted)
                    return false;

                state.WatchdogStarted = true;
                state.LastUpdatedUtc = DateTime.UtcNow;
                return true;
            }
        }

        public SessionState IncrementBootstrapRetry(Guid id)
        {
            lock (sync)
            {
                if (!sessionStates.TryGetValue(id, out SessionState? state))
                {
                    state = new SessionState
                    {
                        ClientId = id,
                        Phase = ConnectionState.Connecting
                    };
                    sessionStates[id] = state;
                }

                state.BootstrapRetryCount++;
                state.LastUpdatedUtc = DateTime.UtcNow;
                return state;
            }
        }

        public SessionState MarkPacketDropped(Guid id, string reason)
        {
            lock (sync)
            {
                if (!sessionStates.TryGetValue(id, out SessionState? state))
                {
                    state = new SessionState
                    {
                        ClientId = id,
                        Phase = ConnectionState.Connecting
                    };
                    sessionStates[id] = state;
                }

                state.DroppedPacketCount++;
                state.LastDropReason = reason;
                state.LastUpdatedUtc = DateTime.UtcNow;
                return state;
            }
        }
        public Player? GetPlayer(Guid id)
        {
            lock (sync)
            {
                Player? oldPlayer = players.Find(p => {
                    if (p != null)
                        return p.Client.Session.ConnectionInfo.Id == id;
                    else
                        return false;
                });
                if (oldPlayer != null)
                    return oldPlayer;
                return null;
            }
        }
        public Player? AddPlayer(IndexedClient client)
        {
            lock (sync)
            {
                if(players.Count < 16)
                {
                    int newIndex = Enumerable.Range(0, 16).Except(players.Select(p => p.Index)).First();
                    Player player = new(newIndex, client);
                    players.Add(player);
                    return player;
                }
                return null;
            }
        }
        public bool RemovePlayer(Guid id)
        {
            lock (sync)
            {
                sessionStates.Remove(id);
                Player? oldPlayer = players.Find(p => p != null && p.Client.Session.ConnectionInfo.Id == id);
                if (oldPlayer != null)
                    return players.Remove(oldPlayer);
                return false;
            }
        }
        public bool RemovePlayer(int index)
        {
            lock (sync)
            {
                Player? oldPlayer = players.Find(p => p.Index == index);
                if (oldPlayer != null)
                {
                    players.Remove(oldPlayer);
                    return true;
                }
                return false;
            }
        }
    }
}
