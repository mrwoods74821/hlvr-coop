using HLVRCoOpModCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Fleck;

namespace WorkshopAddonCompatibilityPlugin
{
    /// <summary>
    /// Plugin that enables and synchronizes Steam Workshop addons for cooperative gameplay.
    /// Supports: Player Body, More Resin, Unpin Ragdolls, Removable Health Vials, Partial Clip Storage, Scalable Init Support, AlyxLib,
    /// Charger Melee Charge, Fast Headcrab Restored, Brutal Blood, Bullet Penetration Mod
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
            }},
            // Charger Melee Charge
            { "3616305735", new WorkshopAddonInfo
            {
                WorkshopId = "3616305735",
                AddonName = "charger_melee_charge",
                DisplayName = "Charger Melee Charge",
                RequiresSync = false, // NPC behavior is handled by existing NPC sync system
                Dependencies = new List<string> { "2182586257" } // Scalable Init Support
            }},
            // Fast Headcrab Restored
            { "3308356996", new WorkshopAddonInfo
            {
                WorkshopId = "3308356996",
                AddonName = "3308356996", // Workshop ID (map extensions only, no scripts)
                DisplayName = "Fast Headcrab Restored",
                RequiresSync = false // Map extensions only, no sync needed
            }},
            // Brutal Blood
            { "2857095424", new WorkshopAddonInfo
            {
                WorkshopId = "2857095424",
                AddonName = "brutal_blood",
                DisplayName = "Brutal Blood",
                RequiresSync = false, // Blood effects are visual only, no sync needed
                Dependencies = new List<string> { "2182586257" } // Scalable Init Support
            }},
            // Bullet Penetration Mod
            { "3008209128", new WorkshopAddonInfo
            {
                WorkshopId = "3008209128",
                AddonName = "bullet_penetration_mod",
                DisplayName = "Bullet Penetration Mod",
                RequiresSync = false, // Bullet penetration is client-side visual effect, no sync needed
                Dependencies = new List<string> { "2182586257" } // Scalable Init Support
            }}
        };

        private static readonly object enabledAddonsLock = new object();
        private static List<string> enabledAddons = new();
        private static volatile bool pluginInitialized = false;

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

                // Note: Player Body synchronization is handled by PlayerBodySyncPlugin
                // This plugin only handles enabling addons, not synchronizing their state

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
            lock (enabledAddonsLock)
            {
                enabledAddons = SupportedAddons.Keys.ToList();
            }
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

            List<string> localEnabledAddons;
            lock (enabledAddonsLock)
            {
                localEnabledAddons = new List<string>(enabledAddons);
            }

            // Enable dependencies first (Scalable Init Support, AlyxLib)
            var dependencyAddons = localEnabledAddons
                .Where(id => SupportedAddons.ContainsKey(id) && SupportedAddons[id].Dependencies.Count == 0)
                .ToList();

            foreach (var addonId in dependencyAddons)
            {
                // Enable by Workshop ID (this is what the game expects)
                Response enableCommand = new("command", $"addon_enable {addonId}");
                socket.Send(JsonConvert.SerializeObject(enableCommand));
                // Small delay to avoid command buffer overflow
                Thread.Sleep(100);
            }

            // Enable dependent addons (after dependencies are loaded)
            // Only enable if their dependency requirements are met
            var dependentAddons = localEnabledAddons
                .Where(id => SupportedAddons.ContainsKey(id) && SupportedAddons[id].Dependencies.Count > 0)
                .Where(id =>
                {
                    var addon = SupportedAddons[id];
                    if (addon.RequiresAnyDependency)
                    {
                        // For "or" dependencies: at least one dependency must be enabled
                        return addon.Dependencies.Any(depId => localEnabledAddons.Contains(depId));
                    }
                    else
                    {
                        // For "and" dependencies: all dependencies must be enabled
                        return addon.Dependencies.All(depId => localEnabledAddons.Contains(depId));
                    }
                })
                .OrderBy(id => SupportedAddons[id].Dependencies.Count) // Enable in dependency order
                .ToList();

            foreach (var addonId in dependentAddons)
            {
                Response enableCommand = new("command", $"addon_enable {addonId}");
                socket.Send(JsonConvert.SerializeObject(enableCommand));
                // Small delay to avoid command buffer overflow
                Thread.Sleep(100);
            }
        }


        /// <summary>
        /// Gets the list of enabled addon Workshop IDs
        /// </summary>
        public static List<string> GetEnabledAddons()
        {
            lock (enabledAddonsLock)
            {
                return new List<string>(enabledAddons);
            }
        }

        /// <summary>
        /// Sets the list of enabled addon Workshop IDs
        /// </summary>
        public static void SetEnabledAddons(List<string> addonIds)
        {
            lock (enabledAddonsLock)
            {
                enabledAddons = addonIds.Where(id => SupportedAddons.ContainsKey(id)).ToList();
            }
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

        /// <summary>
        /// Enables addons for the local client (listen server host).
        /// This is called when VConsole connects/reconnects to ensure addons are enabled
        /// even if the client hasn't connected to the server yet.
        /// </summary>
        /// <param name="sendCommand">Function to send VConsole commands (e.g., ClientProgram.SendVConsoleCommand)</param>
        /// <param name="delayMs">Delay in milliseconds before enabling addons (to allow game to be ready)</param>
        /// <param name="logMessage">Optional function to log messages (for UI output)</param>
        public static void EnableAddonsForLocalClient(Action<string> sendCommand, int delayMs = 2000, Action<string>? logMessage = null)
        {
            if (!pluginInitialized || sendCommand == null) return;

            // Get a snapshot of enabled addons to avoid holding lock during async operation
            List<string> localEnabledAddons;
            int addonCount;
            lock (enabledAddonsLock)
            {
                localEnabledAddons = new List<string>(enabledAddons);
                addonCount = enabledAddons.Count;
            }

            // Log that we're starting to enable addons
            logMessage?.Invoke($"Enabling {addonCount} Workshop addon(s)...");

            // Enable addons asynchronously with delay
            Task.Run(async () =>
            {
                await Task.Delay(delayMs); // Wait for game to be ready

                // Enable dependencies first (Scalable Init Support, AlyxLib)
                var dependencyAddons = localEnabledAddons
                    .Where(id => SupportedAddons.ContainsKey(id) && SupportedAddons[id].Dependencies.Count == 0)
                    .ToList();

                // Log dependencies being enabled
                if (dependencyAddons.Count > 0)
                {
                    logMessage?.Invoke($"Enabling {dependencyAddons.Count} dependency addon(s)...");
                }

                foreach (var addonId in dependencyAddons)
                {
                    var addonInfo = SupportedAddons[addonId];
                    sendCommand($"addon_enable {addonId}");
                    
                    // Log each addon being enabled
                    logMessage?.Invoke($"  → {addonInfo.DisplayName} (ID: {addonId})");
                    
                    await Task.Delay(100); // Small delay between commands to avoid buffer overflow
                }

                // Enable dependent addons (after dependencies are loaded)
                var dependentAddons = localEnabledAddons
                    .Where(id => SupportedAddons.ContainsKey(id) && SupportedAddons[id].Dependencies.Count > 0)
                    .Where(id =>
                    {
                        var addon = SupportedAddons[id];
                        if (addon.RequiresAnyDependency)
                        {
                            return addon.Dependencies.Any(depId => localEnabledAddons.Contains(depId));
                        }
                        else
                        {
                            return addon.Dependencies.All(depId => localEnabledAddons.Contains(depId));
                        }
                    })
                    .OrderBy(id => SupportedAddons[id].Dependencies.Count)
                    .ToList();

                // Log dependent addons being enabled
                if (dependentAddons.Count > 0)
                {
                    logMessage?.Invoke($"Enabling {dependentAddons.Count} dependent addon(s)...");
                }

                foreach (var addonId in dependentAddons)
                {
                    var addonInfo = SupportedAddons[addonId];
                    sendCommand($"addon_enable {addonId}");
                    
                    // Log each addon being enabled
                    logMessage?.Invoke($"  → {addonInfo.DisplayName} (ID: {addonId})");
                    
                    await Task.Delay(100); // Small delay between commands
                }

                // Log completion
                int totalEnabled = dependencyAddons.Count + dependentAddons.Count;
                logMessage?.Invoke($"✓ Enabled {totalEnabled} Workshop addon(s) successfully");
            });
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
