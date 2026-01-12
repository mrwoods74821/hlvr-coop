using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace HLVRCoOpModCore
{
    public class BasePlugin : IPlugin
    {
        public BasePlugin() : base()
        {
            Author = "KiwifruitDev";
            Name = "Base Plugin";
            Description = "The absolute minimum.";
        }
        public BasePlugin(PluginHandleType type, params object[]? vs)
        { }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
    }
}
