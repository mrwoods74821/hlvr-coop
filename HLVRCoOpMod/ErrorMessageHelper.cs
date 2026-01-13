using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Provides user-friendly error messages for common issues.
    /// Converts technical error messages into actionable guidance for users.
    /// </summary>
    public static class ErrorMessageHelper
    {
        /// <summary>
        /// Gets a user-friendly error message for common connection issues.
        /// </summary>
        /// <param name="errorMessage">The technical error message</param>
        /// <param name="errorSource">The source of the error (e.g., "VConsole", "WebSocket", "Client")</param>
        /// <returns>A user-friendly error message with actionable guidance</returns>
        public static string GetUserFriendlyMessage(string errorMessage, string errorSource = "")
        {
            if (string.IsNullOrEmpty(errorMessage))
                return "An unknown error occurred.";

            string lowerMessage = errorMessage.ToLower();

            // VConsole connection errors
            if (lowerMessage.Contains("vconsole") || errorSource.ToLower().Contains("vconsole"))
            {
                if (lowerMessage.Contains("connection") && (lowerMessage.Contains("refused") || lowerMessage.Contains("failed")))
                {
                    return "Cannot connect to Half-Life: Alyx. Make sure:\n" +
                           "• Half-Life: Alyx is running\n" +
                           "• Launch options include: -console -vconsole -language english\n" +
                           "• VConsole port matches (default: 29000)\n" +
                           "• No firewall is blocking the connection";
                }
                if (lowerMessage.Contains("port") && lowerMessage.Contains("listening"))
                {
                    return "VConsole port is not listening. Make sure Half-Life: Alyx is running with -vconsole launch option.";
                }
            }

            // WebSocket connection errors
            if (lowerMessage.Contains("websocket") || lowerMessage.Contains("connection") || errorSource.ToLower().Contains("websocket"))
            {
                if (lowerMessage.Contains("refused") || lowerMessage.Contains("could not connect"))
                {
                    return "Cannot connect to server. Check:\n" +
                           "• Server IP address and port are correct\n" +
                           "• Server is running and accepting connections\n" +
                           "• Firewall allows the connection\n" +
                           "• You're using the correct password (if required)";
                }
                if (lowerMessage.Contains("timeout"))
                {
                    return "Connection timeout. The server may be:\n" +
                           "• Unreachable (check IP address)\n" +
                           "• Not running\n" +
                           "• Behind a firewall\n" +
                           "• Experiencing network issues";
                }
                if (lowerMessage.Contains("invalid password") || lowerMessage.Contains("password"))
                {
                    return "Invalid server password. Please check your password and try again.";
                }
                if (lowerMessage.Contains("username") && (lowerMessage.Contains("taken") || lowerMessage.Contains("invalid")))
                {
                    return "Username issue:\n" +
                           "• Username may already be in use\n" +
                           "• Username must be 32 characters or less\n" +
                           "• Username cannot contain ; or \" characters";
                }
            }

            // Network errors
            if (lowerMessage.Contains("network") || lowerMessage.Contains("socket"))
            {
                if (lowerMessage.Contains("unreachable"))
                {
                    return "Network unreachable. Check your internet connection and try again.";
                }
                if (lowerMessage.Contains("address") && lowerMessage.Contains("invalid"))
                {
                    return "Invalid IP address. Please check the server IP address format (e.g., 192.168.1.1 or example.com).";
                }
            }

            // Version mismatch errors
            if (lowerMessage.Contains("version") || lowerMessage.Contains("incompatible"))
            {
                if (lowerMessage.Contains("older"))
                {
                    return "Version mismatch detected. Please update to the latest version to connect.";
                }
            }

            // Authentication errors
            if (lowerMessage.Contains("authenticated") || lowerMessage.Contains("authentication"))
            {
                return "Authentication failed. Please check your username and password.";
            }

            // Generic connection errors
            if (lowerMessage.Contains("disconnected") || lowerMessage.Contains("connection lost"))
            {
                return "Connection lost. The mod will attempt to reconnect automatically.";
            }

            // Return original message if no specific pattern matches
            return errorMessage;
        }

        /// <summary>
        /// Gets a user-friendly error message from an exception.
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <param name="errorSource">The source of the error</param>
        /// <returns>A user-friendly error message</returns>
        public static string GetUserFriendlyMessage(Exception ex, string errorSource = "")
        {
            if (ex == null)
                return "An unknown error occurred.";

            string message = GetUserFriendlyMessage(ex.Message, errorSource);
            
            // Add exception type context if helpful
            if (ex is System.Net.Sockets.SocketException)
            {
                message += "\n\nThis is a network connection error. Check your network settings and firewall.";
            }
            else if (ex is System.Net.WebSockets.WebSocketException)
            {
                message += "\n\nThis is a WebSocket connection error. Check server availability and network connectivity.";
            }
            else if (ex is TimeoutException)
            {
                message += "\n\nThe operation timed out. The server may be slow or unreachable.";
            }

            return message;
        }
    }
}
