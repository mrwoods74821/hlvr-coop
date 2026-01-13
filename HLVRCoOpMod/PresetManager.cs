using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Manages named presets for application settings
    /// </summary>
    public static class PresetManager
    {
        private static string GetPresetsFilePath()
        {
            string? appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (appDir == null)
                throw new InvalidOperationException("Unable to determine application directory");
            
            return Path.Combine(appDir, "presets.json");
        }

        /// <summary>
        /// Represents a saved preset containing all application settings
        /// </summary>
        public class Preset
        {
            public string Name { get; set; } = "";
            public DateTime CreatedDate { get; set; }
            public DateTime ModifiedDate { get; set; }
            
            // Client settings
            public bool ClientEnabled { get; set; }
            public string ClientIpAddress { get; set; } = "";
            public int ClientPort { get; set; }
            public string ClientUsername { get; set; } = "";
            public string ClientPassword { get; set; } = "";
            public bool ClientPrintVconsole { get; set; }
            
            // Server settings
            public bool ServerEnabled { get; set; }
            public int ServerPort { get; set; }
            public string ServerPassword { get; set; } = "";
            public string ServerMap { get; set; } = "";
            public bool ServerDisableUserVconsoleInput { get; set; }
            
            // Options
            public bool SaveOptionsOnExit { get; set; }
            public bool AlwaysOnTop { get; set; }
            public int PlayerBodyPreference { get; set; }
            public string CurrentAddons { get; set; } = "[]";
        }

        /// <summary>
        /// Loads all presets from disk
        /// </summary>
        public static Dictionary<string, Preset> LoadPresets()
        {
            string filePath = GetPresetsFilePath();
            
            if (!File.Exists(filePath))
                return new Dictionary<string, Preset>();
            
            try
            {
                string json = File.ReadAllText(filePath);
                var presets = JsonConvert.DeserializeObject<Dictionary<string, Preset>>(json);
                return presets ?? new Dictionary<string, Preset>();
            }
            catch (Exception)
            {
                // If file is corrupted, return empty dictionary
                return new Dictionary<string, Preset>();
            }
        }

        /// <summary>
        /// Saves all presets to disk
        /// </summary>
        public static void SavePresets(Dictionary<string, Preset> presets)
        {
            string filePath = GetPresetsFilePath();
            
            try
            {
                string json = JsonConvert.SerializeObject(presets, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save presets: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a preset from current settings
        /// </summary>
        public static Preset CreatePresetFromCurrentSettings(string name)
        {
            return new Preset
            {
                Name = name,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                
                // Client settings
                ClientEnabled = Settings.Default.ClientEnabled,
                ClientIpAddress = Settings.Default.ClientIpAddress,
                ClientPort = Settings.Default.ClientPort,
                ClientUsername = Settings.Default.ClientUsername,
                ClientPassword = Settings.Default.ClientPassword,
                ClientPrintVconsole = Settings.Default.ClientPrintVconsole,
                
                // Server settings
                ServerEnabled = Settings.Default.ServerEnabled,
                ServerPort = Settings.Default.ServerPort,
                ServerPassword = Settings.Default.ServerPassword,
                ServerMap = Settings.Default.ServerMap,
                ServerDisableUserVconsoleInput = Settings.Default.ServerDisableUserVconsoleInput,
                
                // Options
                SaveOptionsOnExit = Settings.Default.SaveOptionsOnExit,
                AlwaysOnTop = Settings.Default.AlwaysOnTop,
                PlayerBodyPreference = Settings.Default.PlayerBodyPreference,
                CurrentAddons = Settings.Default.CurrentAddons
            };
        }

        /// <summary>
        /// Applies a preset to current settings (without saving to Settings.Default)
        /// </summary>
        public static void ApplyPresetToSettings(Preset preset)
        {
            // Client settings
            Settings.Default.ClientEnabled = preset.ClientEnabled;
            Settings.Default.ClientIpAddress = preset.ClientIpAddress;
            Settings.Default.ClientPort = preset.ClientPort;
            Settings.Default.ClientUsername = preset.ClientUsername;
            Settings.Default.ClientPassword = preset.ClientPassword;
            Settings.Default.ClientPrintVconsole = preset.ClientPrintVconsole;
            
            // Server settings
            Settings.Default.ServerEnabled = preset.ServerEnabled;
            Settings.Default.ServerPort = preset.ServerPort;
            Settings.Default.ServerPassword = preset.ServerPassword;
            Settings.Default.ServerMap = preset.ServerMap;
            Settings.Default.ServerDisableUserVconsoleInput = preset.ServerDisableUserVconsoleInput;
            
            // Options
            Settings.Default.SaveOptionsOnExit = preset.SaveOptionsOnExit;
            Settings.Default.AlwaysOnTop = preset.AlwaysOnTop;
            Settings.Default.PlayerBodyPreference = preset.PlayerBodyPreference;
            Settings.Default.CurrentAddons = preset.CurrentAddons;
        }

        /// <summary>
        /// Saves a preset with the given name
        /// </summary>
        public static void SavePreset(string name, Preset preset)
        {
            var presets = LoadPresets();
            preset.Name = name;
            preset.ModifiedDate = DateTime.Now;
            // Preserve original CreatedDate if overwriting an existing preset
            if (presets.ContainsKey(name))
            {
                preset.CreatedDate = presets[name].CreatedDate;
            }
            else
            {
                preset.CreatedDate = DateTime.Now;
            }
            presets[name] = preset;
            SavePresets(presets);
        }

        /// <summary>
        /// Deletes a preset by name
        /// </summary>
        public static bool DeletePreset(string name)
        {
            var presets = LoadPresets();
            if (presets.Remove(name))
            {
                SavePresets(presets);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Renames a preset
        /// </summary>
        public static bool RenamePreset(string oldName, string newName)
        {
            var presets = LoadPresets();
            if (!presets.ContainsKey(oldName) || presets.ContainsKey(newName))
                return false;
            
            var preset = presets[oldName];
            presets.Remove(oldName);
            preset.Name = newName;
            preset.ModifiedDate = DateTime.Now;
            presets[newName] = preset;
            SavePresets(presets);
            return true;
        }

        /// <summary>
        /// Gets a list of all preset names
        /// </summary>
        public static List<string> GetPresetNames()
        {
            var presets = LoadPresets();
            return presets.Keys.OrderBy(k => k).ToList();
        }

        /// <summary>
        /// Gets a preset by name
        /// </summary>
        public static Preset? GetPreset(string name)
        {
            var presets = LoadPresets();
            return presets.ContainsKey(name) ? presets[name] : null;
        }
    }
}
