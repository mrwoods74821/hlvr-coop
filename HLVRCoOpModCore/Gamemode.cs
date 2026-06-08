using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace HLVRCoOpModCore
{
    public enum HandleState
    {
        Continue = 0,
        Handled
    }
    public enum GamemodeHandleType
    {
        None = 0,
        PreStart,
        PostStart,
        PreClose,
        PostClose,
        PreResponse,
        PostResponse,
        ClientOpen,
        ClientClose,
        Command,
        Think,
        ReBootstrap,
    }
    public static class GamemodeHandler
    {
        public static HandleState Handle(Type gamemodeType, GamemodeHandleType handleType, params object[]? vs)
        {
            IGamemode? newGamemode = (IGamemode?)Activator.CreateInstance(gamemodeType, handleType, vs);
            if (newGamemode != null && newGamemode.State != null)
            {
                return (HandleState)newGamemode.State;
            }
            return HandleState.Continue;
        }
        public static IGamemode? Instance(Type gamemodeType)
        {
            return (IGamemode?)Activator.CreateInstance(gamemodeType);
        }
    }
    public interface IGamemode
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public HandleState? State { get; set; }
    }
}
