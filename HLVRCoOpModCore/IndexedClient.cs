using HLVRCoOpModCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;

namespace HLVRCoOpModCore
{
    public class IndexedClient : Client
    {
        public string Map = "";
        public IWebSocketConnection Session;
        public IndexedClient(IWebSocketConnection session, string username, string map) : base(username)
        {
            Map = map;
            Session = session;
        }
    }
}
