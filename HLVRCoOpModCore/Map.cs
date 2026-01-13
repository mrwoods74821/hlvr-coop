using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLVRCoOpModCore
{
    /// <summary>
    /// Static class for tracking the current map name across the server.
    /// </summary>
    public static class Map
    {
        /// <summary>
        /// The current map name. Updated when clients report map changes via MAPN packet.
        /// </summary>
        public static string map = "";
    }
}
