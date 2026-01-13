using HLVRCoOpModCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlyxGamemode
{
    public sealed class AlyxGlobalData
    {
        public static readonly AlyxGlobalData instance = new();
        private readonly List<Player> players = new();
        private readonly List<Moveable> manipulatedEntities = new();
        private readonly object playersLock = new object(); // Lock for thread-safe access to players list
        private readonly object entitiesLock = new object(); // Lock for thread-safe access to manipulatedEntities list
        public bool AddManipulatedEntity(Entity MovedObject, Guid ClientID, List<IndexedClient> Connections)
        {
            lock (entitiesLock)
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
        public List<Player> GetPlayers()
        {
            lock (playersLock)
            {
                return new List<Player>(players); // Return a copy to prevent external modification
            }
        }
        public Player? GetPlayer(Guid id)
        {
            lock (playersLock)
            {
                Player? oldPlayer = players.Find(p => {
                    if (p != null)
                        return p.Client.Session.ConnectionInfo.Id == id;
                    else
                        return false;
                });
                return oldPlayer;
            }
        }
        public Player? AddPlayer(IndexedClient client)
        {
            lock (playersLock)
            {
                if(players.Count < 16)
                {
                    Player player = new(players.Count, client);
                    players.Add(player);
                    return player;
                }
                return null;
            }
        }
        public bool RemovePlayer(Guid id)
        {
            lock (playersLock)
            {
                Player? oldPlayer = players.Find(p => p != null && p.Client.Session.ConnectionInfo.Id == id);
                if (oldPlayer != null)
                    return players.Remove(oldPlayer);
                return false;
            }
        }
        public bool RemovePlayer(int index)
        {
            lock (playersLock)
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
