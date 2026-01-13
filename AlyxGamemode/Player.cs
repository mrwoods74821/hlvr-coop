using HLVRCoOpModCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlyxGamemode
{
    public class Player : Location
    {
        public int Index = 0;
        public IndexedClient Client;
        /// <summary>
        /// Player's current health value. Updated from PLYR packets.
        /// Used for respawn logic, checkpoints, and other game mechanics.
        /// </summary>
        public int Health = 100;
        public Player(int index, IndexedClient client)
        {
            this.Index = index;
            this.Client = client;
        }
    }
}
