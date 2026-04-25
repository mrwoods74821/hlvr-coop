using HLVRCoOpModCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Fleck;

namespace PlayerBodySyncPlugin
{
    /// <summary>
    /// Plugin that synchronizes player body preferences across clients for multiplayer visibility.
    /// Works with the Player Body Workshop addon (ID: 3581426521).
    /// 
    /// The Player Body addon uses convar 'player_body_preference':
    /// - 0 = Auto (detects based on gloves)
    /// - 1 = Gordon Freeman
    /// - 2 = Alyx Vance
    /// 
    /// When a player changes their body preference, this plugin broadcasts it to all other clients
    /// so everyone can see each other's bodies correctly.
    /// </summary>
    public class PlayerBodySyncPlugin : BasePlugin
    {
        /// <summary>
        /// Stores each player's body preference (0=auto, 1=Gordon, 2=Alyx)
        /// Key: Client connection GUID
        /// Value: Body preference value
        /// </summary>
        private static readonly Dictionary<Guid, int> playerBodyPreferences = new();

        /// <summary>
        /// Tracks which clients have been queried for their initial preference
        /// </summary>
        private static readonly HashSet<Guid> queriedClients = new();

        private static readonly object syncLock = new();
        private static bool pluginInitialized = false;

        public PlayerBodySyncPlugin() : base()
        {
            Author = "Dayshower";
            Name = "Player Body Sync";
            Description = "Synchronizes player body preferences across clients for multiplayer visibility";
        }

        public PlayerBodySyncPlugin(PluginHandleType type, params object[]? vs)
        {
            switch (type)
            {
                case PluginHandleType.UserInterface_Initialized:
                    pluginInitialized = true;
                    break;

                case PluginHandleType.Server_PostGamemode_ClientOpen:
                    // When a client connects, query their body preference and sync with others
                    if (vs != null && vs.Length >= 2)
                    {
                        List<IndexedClient> connections = (List<IndexedClient>)vs[0];
                        IWebSocketConnection socket = (IWebSocketConnection)vs[1];
                        HandleClientConnect(connections, socket);
                    }
                    break;

                case PluginHandleType.Server_PostGamemode_ClientClose:
                    // Clean up when a client disconnects
                    if (vs != null && vs.Length >= 2)
                    {
                        IWebSocketConnection socket = (IWebSocketConnection)vs[1];
                        HandleClientDisconnect(socket);
                    }
                    break;

                case PluginHandleType.Server_PostGamemode_PreResponse:
                    // Monitor responses for body preference changes
                    if (vs != null && vs.Length >= 3)
                    {
                        Response response = (Response)vs[0];
                        List<IndexedClient> connections = (List<IndexedClient>)vs[1];
                        IWebSocketConnection socket = (IWebSocketConnection)vs[2];
                        HandleResponse(response, connections, socket);
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles when a client connects - queries their body preference and syncs with others
        /// </summary>
        private void HandleClientConnect(List<IndexedClient> connections, IWebSocketConnection socket)
        {
            if (!pluginInitialized || socket == null) return;

            Guid clientId = socket.ConnectionInfo.Id;

            // Query the client's current body preference
            // The game will respond via VConsole print with the convar value
            Response queryCommand = new("command", "convar_get player_body_preference");
            socket.Send(JsonConvert.SerializeObject(queryCommand));

            // Mark this client as queried (we'll process the response when it comes back)
            lock(syncLock)
            {
                queriedClients.Add(clientId);
            }

            // Note: We don't send existing players' preferences to the new client here.
            // The player_body_preference convar is per-client and determines what body model
            // that client sees for themselves. When the new client's preference is queried
            // and stored, it will be broadcast to all other clients so they can see the
            // new player's body. The new client will see other players' bodies based on
            // their own preference and the Player Body addon's multiplayer handling.
        }

        /// <summary>
        /// Handles when a client disconnects - cleans up their stored preference
        /// </summary>
        private void HandleClientDisconnect(IWebSocketConnection socket)
        {
            if (socket == null) return;

            Guid clientId = socket.ConnectionInfo.Id;
            lock(syncLock)
            {
                playerBodyPreferences.Remove(clientId);
                queriedClients.Remove(clientId);
            }
        }

        /// <summary>
        /// Monitors responses for body preference changes and custom BODY packets
        /// </summary>
        private void HandleResponse(Response response, List<IndexedClient> connections, IWebSocketConnection socket)
        {
            if (!pluginInitialized || socket == null || response == null) return;

            Guid clientId = socket.ConnectionInfo.Id;

            // Check for custom BODY packet from game-side Lua scripts
            // Format: BODY <preference_value> HLVRCOOP
            if (response.type == "print" && response.data != null)
            {
                // Check for BODY packet
                if (response.data.Contains("BODY") && response.data.Contains("HLVRCOOP"))
                {
                    HandleBodyPacket(response.data, connections, socket);
                    return;
                }

                // Check for convar_get response: "player_body_preference" = "X"
                // This happens when we query a client's preference on connect
                bool handledByConvarResponse = false;
                if (response.data.Contains("player_body_preference"))
                {
                    // Try to handle as a query response first
                    var queryMatch = Regex.Match(response.data, @"""player_body_preference""\s*=\s*""(\d+)""");
                    bool isQueriedClient;
                    lock(syncLock)
                    {
                        isQueriedClient = queriedClients.Contains(clientId);
                    }
                    if (queryMatch.Success && isQueriedClient)
                    {
                        HandleConvarResponse(response.data, connections, socket);
                        handledByConvarResponse = true;
                    }
                }

                // If not handled as a query response, check for convar_set confirmation or value change
                // Pattern: "player_body_preference" = "X" (when set via convar_set)
                if (!handledByConvarResponse)
                {
                    var convarMatch = Regex.Match(response.data, @"player_body_preference[""\s]*=[""\s]*[""\s]*(\d+)");
                    if (convarMatch.Success)
                    {
                        if (int.TryParse(convarMatch.Groups[1].Value, out int preference))
                        {
                            UpdatePlayerPreference(clientId, preference, connections, socket);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles a BODY packet from game-side Lua scripts
        /// Format: BODY <preference_value> HLVRCOOP
        /// </summary>
        private void HandleBodyPacket(string data, List<IndexedClient> connections, IWebSocketConnection socket)
        {
            if (socket == null) return;

            try
            {
                // Parse: "BODY 1 HLVRCOOP" -> preference = 1
                var parts = data.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] == "BODY")
                {
                    if (int.TryParse(parts[1], out int preference) && preference >= 0 && preference <= 2)
                    {
                        Guid clientId = socket.ConnectionInfo.Id;
                        UpdatePlayerPreference(clientId, preference, connections, socket);
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore parsing errors
            }
        }

        /// <summary>
        /// Handles convar_get response when querying a client's preference
        /// Format: "player_body_preference" = "X"
        /// </summary>
        private void HandleConvarResponse(string data, List<IndexedClient> connections, IWebSocketConnection socket)
        {
            if (socket == null) return;

            try
            {
                // Pattern: "player_body_preference" = "1"
                var match = Regex.Match(data, @"""player_body_preference""\s*=\s*""(\d+)""");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int preference) && preference >= 0 && preference <= 2)
                    {
                        Guid clientId = socket.ConnectionInfo.Id;

                        // Only update if this was a query response (not a change notification)
                        bool shouldProcess;
                        lock(syncLock)
                        {
                            shouldProcess = queriedClients.Contains(clientId);
                            if (shouldProcess)
                            {
                                queriedClients.Remove(clientId);
                            }
                        }
                        if (shouldProcess)
                        {
                            UpdatePlayerPreference(clientId, preference, connections, socket, isInitialQuery: true);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore parsing errors
            }
        }

        /// <summary>
        /// Updates a player's body preference and broadcasts it to all other clients
        /// </summary>
        private void UpdatePlayerPreference(Guid clientId, int preference, List<IndexedClient> connections, IWebSocketConnection socket, bool isInitialQuery = false)
        {
            if (socket == null || preference < 0 || preference > 2) return;

            bool shouldBroadcast;
            lock(syncLock)
            {
                // Check if preference actually changed (before storing)
                bool hadPreference = playerBodyPreferences.ContainsKey(clientId);
                bool preferenceChanged = !hadPreference || playerBodyPreferences[clientId] != preference;

                // Store the preference
                playerBodyPreferences[clientId] = preference;

                // If this was just an initial query and we already had the same preference,
                // skip broadcasting (client reconnected with same preference)
                if (isInitialQuery && !preferenceChanged && hadPreference)
                {
                    // Preference unchanged and we already had it - no need to broadcast
                    shouldBroadcast = false;
                }
                else
                {
                    shouldBroadcast = true;
                }
            }

            if (!shouldBroadcast) return;

            // Broadcast the preference change to all OTHER clients
            // Each client will set the convar, and the Player Body addon will automatically
            // spawn the correct body model based on the convar value
            Response syncCommand = new("command", $"convar_set player_body_preference {preference}");
            string syncCommandJson = JsonConvert.SerializeObject(syncCommand);

            foreach (var client in connections)
            {
                if (client.Session.ConnectionInfo.Id != clientId)
                {
                    try
                    {
                        client.Session.Send(syncCommandJson);
                    }
                    catch (Exception)
                    {
                        // Silently ignore send errors (client may have disconnected)
                    }
                }
            }
        }

        /// <summary>
        /// Gets the body preference for a specific client
        /// </summary>
        public static int? GetPlayerPreference(Guid clientId)
        {
            lock(syncLock)
            {
                return playerBodyPreferences.ContainsKey(clientId) ? playerBodyPreferences[clientId] : null;
            }
        }

        /// <summary>
        /// Gets all player body preferences
        /// </summary>
        public static Dictionary<Guid, int> GetAllPreferences()
        {
            lock(syncLock)
            {
                return new Dictionary<Guid, int>(playerBodyPreferences);
            }
        }
    }
}
