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
            string technicalMessage = $"[{source}] {ex.Message}\n{ex.StackTrace}";
            string userFriendlyMessage = ErrorMessageHelper.GetUserFriendlyMessage(ex, source);

            // Log technical details to UI
            if (Program.userInterface != null)
            {
                Program.userInterface.Invoke(() =>
                {
                    // Show user-friendly message first
                    Program.userInterface.LogToOutput(errorChannel, userFriendlyMessage);
                    // Show technical details for debugging
                    Program.userInterface.LogToOutput(errorChannel, "\nTechnical details: " + technicalMessage);
                });
            }

            // Log to Debug (for development)
            System.Diagnostics.Debug.WriteLine(technicalMessage);
        }

        public static void LogError(string source, string message)
        {
            string technicalMessage = $"[{source}] {message}";
            string userFriendlyMessage = ErrorMessageHelper.GetUserFriendlyMessage(message, source);

            if (Program.userInterface != null)
            {
                Program.userInterface.Invoke(() =>
                {
                    // Show user-friendly message
                    Program.userInterface.LogToOutput(errorChannel, userFriendlyMessage);
                    // If messages differ, show technical details
                    if (userFriendlyMessage != message)
                    {
                        Program.userInterface.LogToOutput(errorChannel, "\nTechnical details: " + technicalMessage);
                    }
                });
            }

            System.Diagnostics.Debug.WriteLine(technicalMessage);
        }
    }
}
