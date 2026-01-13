using System;
using System.Collections.Generic;

namespace HLVRCoOpModCore
{
    /// <summary>
    /// Represents information about a game server
    /// </summary>
    public class ServerInfo
    {
        public string IpAddress { get; set; } = "";
        public int Port { get; set; }
        public string Map { get; set; } = "";
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; } = 16;
        public bool PasswordProtected { get; set; }
        public string Gamemode { get; set; } = "";
        public string ServerName { get; set; } = "";
        public long Ping { get; set; } = -1; // -1 means not pinged yet
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public bool IsLocalNetwork { get; set; } = false;
        
        /// <summary>
        /// Gets a display name for the server
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(ServerName))
                    return ServerName;
                return $"{IpAddress}:{Port}";
            }
        }
        
        /// <summary>
        /// Gets player count display string
        /// </summary>
        public string PlayerCountDisplay => $"{PlayerCount}/{MaxPlayers}";
    }
}
