using System;
using System.Drawing;
using HLVRCoOpModCore;

namespace HLVRCoOpMod
{
    public static class ErrorLogger
    {
        private static readonly Channel errorChannel = new("ERR", "Errors", Color.Red);

        public static void LogError(string source, Exception ex)
        {
            string message = $"[{source}] {ex.Message}\n{ex.StackTrace}";

            // Log to UI
            if (Program.userInterface != null)
            {
                Program.userInterface.Invoke(() =>
                {
                    Program.userInterface.LogToOutput(errorChannel, message);
                });
            }

            // Log to Debug (for development)
            System.Diagnostics.Debug.WriteLine(message);
        }

        public static void LogError(string source, string message)
        {
            string fullMessage = $"[{source}] {message}";

            if (Program.userInterface != null)
            {
                Program.userInterface.Invoke(() =>
                {
                    Program.userInterface.LogToOutput(errorChannel, fullMessage);
                });
            }

            System.Diagnostics.Debug.WriteLine(fullMessage);
        }
    }
}
