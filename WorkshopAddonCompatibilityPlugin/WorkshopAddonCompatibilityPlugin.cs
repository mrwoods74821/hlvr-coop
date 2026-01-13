using HLVRCoOpModCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Fleck;

namespace WorkshopAddonCompatibilityPlugin
{
    /// <summary>
    /// Plugin that enables and synchronizes Steam Workshop addons for cooperative gameplay.
    /// Supports: Player Body, More Resin, Unpin Ragdolls, Removable Health Vials, Partial Clip Storage, Scalable Init Support, AlyxLib
    /// </summary>
    public class WorkshopAddonCompatibilityPlugin : BasePlugin
    {
        private static readonly Dictionary<string, WorkshopAddonInfo> SupportedAddons = new()
        {
            // Player Body - ALYX / GORDON
            { "3581426521", new WorkshopAddonInfo
            {
                WorkshopId = "3581426521",
                AddonName = "player_body", // May need to be determined from actual addon files
                DisplayName = "Player Body - ALYX / GORDON",
                RequiresSync = true, // Body visibility needs synchronization
                Dependencies = new List<string> { "2182586257", "3329679071" }, // Scalable Init Support or AlyxLib (either one)
                RequiresAnyDependency = true // Only needs ONE of the dependencies, not all
            }},
            // More Resin
            { "2396122619", new WorkshopAddonInfo
            {
                WorkshopId = "2396122619",
                AddonName = "2396122619", // Workshop ID (map extensions only, no scripts)
                DisplayName = "More Resin",
                RequiresSync = false // Resin locations are map-based extensions, no sync needed
            }},
            // Unpin Ragdolls
            { "2173405711", new WorkshopAddonInfo
            {
                WorkshopId = "2173405711",
                AddonName = "unpin_ragdolls",
                DisplayName = "Unpin Ragdolls",
                RequiresSync = true, // Ragdoll physics need synchronization
                Dependencies = new List<string> { "2182586257" } // Scalable Init Support
            }},
            // Removable Health Vials
            { "3527754624", new WorkshopAddonInfo
            {
                WorkshopId = "3527754624",
                AddonName = "removable_health_vials",
                DisplayName = "Removable Health Vials",
                RequiresSync = true, // Vial state needs synchronization
                Dependencies = new List<string> { "3329679071" } // AlyxLib
            }},
            // Partial Clip Storage
            { "3329684800", new WorkshopAddonInfo
            {
                WorkshopId = "3329684800",
                AddonName = "partial_clip_storage",
                DisplayName = "Partial Clip Storage",
                RequiresSync = true, // Clip state needs synchronization
                Dependencies = new List<string> { "3329679071" } // AlyxLib
            }},
            // Scalable Init Support
            { "2182586257", new WorkshopAddonInfo
            {
                WorkshopId = "2182586257",
                AddonName = "scalable_init_support",
                DisplayName = "Scalable Init Support",
                RequiresSync = false // Mod-loader, no sync needed
            }},
            // AlyxLib
            { "3329679071", new WorkshopAddonInfo
            {
                WorkshopId = "3329679071",
                AddonName = "alyxlib",
                DisplayName = "AlyxLib",
                RequiresSync = false // Library, no sync needed
            }}
        };

        private static List<string> enabledAddons = new();
        private static bool pluginInitialized = false;

        public WorkshopAddonCompatibilityPlugin() : base()
        {
            Author = "Dayshower";
            Name = "Workshop Addon Compatibility";
            Description = "Enables and synchronizes Steam Workshop addons for cooperative gameplay";
        }

        public WorkshopAddonCompatibilityPlugin(PluginHandleType type, params object[]? vs)
        {
            switch (type)
            {
                case PluginHandleType.UserInterface_Initialized:
                    // Load enabled addons from settings
                    LoadEnabledAddons();
                    pluginInitialized = true;
                    break;

                case PluginHandleType.Server_PostGamemode_ClientOpen:
                    // Enable required addons when client connects
                    if (vs != null && vs.Length >= 2)
                    {
                        List<IndexedClient> connections = (List<IndexedClient>)vs[0];
                        IWebSocketConnection socket = (IWebSocketConnection)vs[1];
                        EnableAddonsForClient(socket);
                    }
                    break;

                case PluginHandleType.Server_PostGamemode_PreResponse:
                    // Handle addon-specific synchronization if needed
                    if (vs != null && vs.Length >= 3)
                    {
                        Response response = (Response)vs[0];
                        List<IndexedClient> connections = (List<IndexedClient>)vs[1];
                        IWebSocketConnection socket = (IWebSocketConnection)vs[2];
                        
                        // Handle Player Body synchronization
                        HandlePlayerBodySync(response, connections, socket);
                    }
                    break;

                case PluginHandleType.UserInterface_Exit:
                    // Save enabled addons to settings
                    SaveEnabledAddons();
                    break;
            }
        }

        /// <summary>
        /// Loads enabled addons from settings or uses default list
        /// </summary>
        private void LoadEnabledAddons()
        {
            // Default: Enable all supported addons
            // In the future, this could load from user settings
            enabledAddons = SupportedAddons.Keys.ToList();
        }

        /// <summary>
        /// Saves enabled addons to settings
        /// </summary>
        private void SaveEnabledAddons()
        {
            // In the future, save to user settings
        }

        /// <summary>
        /// Enables required addons for a connecting client.
        /// 
        /// IMPORTANT: This does NOT download addons. Users must subscribe to addons in Steam Workshop first.
        /// This method only sends 'addon_enable' commands - the game will load addons if they're subscribed,
        /// or ignore the command if they're not subscribed.
        /// 
        /// Addons are enabled by Workshop ID, which is what the game expects.
        /// </summary>
        private void EnableAddonsForClient(IWebSocketConnection socket)
        {
            if (!pluginInitialized || socket == null) return;

            // Enable dependencies first (Scalable Init Support, AlyxLib)
            var dependencyAddons = enabledAddons
                .Where(id => SupportedAddons.ContainsKey(id) && SupportedAddons[id].Dependencies.Count == 0)
                .ToList();

            foreach (var addonId in dependencyAddons)
            {
                // Enable by Workshop ID (this is what the game expects)
                Response enableCommand = new("command", $"addon_enable {addonId}");
                socket.Send(JsonConvert.SerializeObject(enableCommand));
            }

            // Enable dependent addons (after dependencies are loaded)
            // Only enable if their dependency requirements are met
            var dependentAddons = enabledAddons
                .Where(id => SupportedAddons.ContainsKey(id) && SupportedAddons[id].Dependencies.Count > 0)
                .Where(id =>
                {
                    var addon = SupportedAddons[id];
                    if (addon.RequiresAnyDependency)
                    {
                        // For "or" dependencies: at least one dependency must be enabled
                        return addon.Dependencies.Any(depId => enabledAddons.Contains(depId));
                    }
                    else
                    {
                        // For "and" dependencies: all dependencies must be enabled
                        return addon.Dependencies.All(depId => enabledAddons.Contains(depId));
                    }
                })
                .OrderBy(id => SupportedAddons[id].Dependencies.Count) // Enable in dependency order
                .ToList();

            foreach (var addonId in dependentAddons)
            {
                Response enableCommand = new("command", $"addon_enable {addonId}");
                socket.Send(JsonConvert.SerializeObject(enableCommand));
            }
        }

        /// <summary>
        /// Handles Player Body synchronization across clients
        /// This ensures all players can see each other's bodies in multiplayer
        /// Player Body uses convar 'player_body_preference': 0=auto, 1=Gordon, 2=Alyx
        /// </summary>
        private void HandlePlayerBodySync(Response response, List<IndexedClient> connections, IWebSocketConnection socket)
        {
            // Check if Player Body addon is enabled
            if (!enabledAddons.Contains("3581426521") || socket == null)
                return;

            // Check if response contains player body preference changes
            // This would need to be detected from VConsole output or a custom packet
            // For now, we can send a command to query the current preference
            // and broadcast it to all clients
            
            // TODO: Implement Player Body synchronization
            // Implementation approach:
            // 1. Detect convar changes via VConsole: "player_body_preference" convar changes
            // 2. When a player changes their body preference, broadcast to all other clients:
            //    - Send command: "convar_set player_body_preference <value>"
            //    - Each client will then spawn the appropriate body model
            // 
            // This could be done via:
            // - Custom packet type (e.g., "BODY" packet with preference value)
            // - Or via VConsole command monitoring and broadcasting
            // 
            // Note: Player Body addon automatically spawns the correct model based on the convar,
            // so we just need to synchronize the convar value across clients
        }

        /// <summary>
        /// Gets the list of enabled addon Workshop IDs
        /// </summary>
        public static List<string> GetEnabledAddons()
        {
            return new List<string>(enabledAddons);
        }

        /// <summary>
        /// Sets the list of enabled addon Workshop IDs
        /// </summary>
        public static void SetEnabledAddons(List<string> addonIds)
        {
            enabledAddons = addonIds.Where(id => SupportedAddons.ContainsKey(id)).ToList();
        }

        /// <summary>
        /// Gets information about a supported addon
        /// </summary>
        public static WorkshopAddonInfo? GetAddonInfo(string workshopId)
        {
            return SupportedAddons.ContainsKey(workshopId) ? SupportedAddons[workshopId] : null;
        }

        /// <summary>
        /// Gets all supported addons
        /// </summary>
        public static Dictionary<string, WorkshopAddonInfo> GetSupportedAddons()
        {
            return new Dictionary<string, WorkshopAddonInfo>(SupportedAddons);
        }
    }

    /// <summary>
    /// Information about a supported Workshop addon
    /// </summary>
    public class WorkshopAddonInfo
    {
        public string WorkshopId { get; set; } = "";
        public string AddonName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool RequiresSync { get; set; } = false;
        public List<string> Dependencies { get; set; } = new();
        /// <summary>
        /// If true, addon only needs ONE of the dependencies (OR logic).
        /// If false, addon needs ALL dependencies (AND logic).
        /// </summary>
        public bool RequiresAnyDependency { get; set; } = false;
    }
}
