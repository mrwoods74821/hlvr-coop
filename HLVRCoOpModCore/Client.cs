using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLVRCoOpModCore
{
    public class Client
    {
        public string Username { get; set; } = "";
        public Client(string username)
        {
            Username = username;
        }
    }
    
}
