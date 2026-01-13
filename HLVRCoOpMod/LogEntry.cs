using System;
using System.Drawing;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Represents a single log entry with metadata for filtering and searching.
    /// </summary>
    public class LogEntry
    {
        public string Text { get; set; } = "";
        public Color Color { get; set; } = Color.Black;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Channel { get; set; } = "";
        public string FriendlyChannelName { get; set; } = "";

        public LogEntry() { }

        public LogEntry(string text, Color color, string channel = "", string friendlyChannelName = "")
        {
            Text = text;
            Color = color;
            Timestamp = DateTime.Now;
            Channel = channel;
            FriendlyChannelName = friendlyChannelName;
        }
    }
}
