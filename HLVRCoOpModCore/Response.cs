using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HLVRCoOpModCore
{
    public class Response
    {
        public string? type;
        public string? data;
        public string? map;
        public string? remoteClientUsername;
        public string? clientUsername;
        public string? password;
        public readonly static int internalVersion = 1;
        public int? version = internalVersion;
        public bool urgent;
        public long? timestamp;
        /// <summary>
        /// Indicates whether this response has been handled by a plugin, gamemode, or Lua script.
        /// When set to true, default processing will be skipped by the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property provides a proper mechanism for plugins, gamemodes, and Lua scripts to signal
        /// that they have handled a response and default processing should not occur.
        /// </para>
        /// <para>
        /// <strong>Usage in Lua scripts:</strong> Set <c>arg1.handled = true</c> in event handlers to prevent
        /// default processing. For example, when handling chat commands in <c>Server_PreGamemode_PreResponse</c>:
        /// </para>
        /// <code>
        /// if arg1.type == "chat" and arg1.data == "/mycommand" then
        ///     -- Handle the command
        ///     arg1.handled = true  -- Prevent default chat processing
        /// end
        /// </code>
        /// <para>
        /// <strong>Backward compatibility:</strong> The legacy mechanism of setting <c>arg1.type = "lua_chat_handled"</c>
        /// is still supported but deprecated. New scripts should use the <c>handled</c> property instead.
        /// </para>
        /// <para>
        /// <strong>Default value:</strong> <c>false</c> (response is not handled)
        /// </para>
        /// </remarks>
        public bool handled;
        public Response()
        {
        }
        public Response(string type)
        {
            this.type = type;
        }
        public Response(string type, string data)
        {
            this.type = type;
            this.data = data;
        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
