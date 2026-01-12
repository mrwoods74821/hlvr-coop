using System.Drawing;

namespace HLVRCoOpModCore
{
    public class CoreGamemode : IGamemode
    {
        public CoreGamemode() : base()
        {
            Author = "KiwifruitDev";
            Name = "Core Gamemode";
            Description = "The absolute minimum.";
        }
        public CoreGamemode(GamemodeHandleType type, params object[]? vs)
        {
            State = HandleState.Continue;
        }
        public HandleState? State { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
    }
}