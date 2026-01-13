using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Represents a saved configuration preset.
    /// </summary>
    public class ConfigurationPreset
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;

        // Server settings
        public bool ServerEnabled { get; set; }
        public int ServerPort { get; set; }
        public string ServerPassword { get; set; } = "";
        public string ServerMap { get; set; } = "";
        public bool ServerDisableUserVconsoleInput { get; set; }

        // Client settings
        public bool ClientEnabled { get; set; }
        public string ClientIpAddress { get; set; } = "";
        public int ClientPort { get; set; }
        public string ClientUsername { get; set; } = "";
        public string ClientPassword { get; set; } = "";
        public bool ClientPrintVconsole { get; set; }

        // VConsole settings
        public int VconsolePort { get; set; }
        public int VconsoleProtocol { get; set; }

        // Addons
        public List<string> CurrentAddons { get; set; } = new List<string>();

        public ConfigurationPreset() { }

        /// <summary>
        /// Creates a preset from current settings.
        /// </summary>
        public static ConfigurationPreset FromCurrentSettings(string name, string category = "")
        {
            var preset = new ConfigurationPreset
            {
                Name = name,
                Category = category,
                CreatedDate = DateTime.Now,
                LastModified = DateTime.Now,
                ServerEnabled = Settings.Default.ServerEnabled,
                ServerPort = Settings.Default.ServerPort,
                ServerPassword = Settings.Default.ServerPassword,
                ServerMap = Settings.Default.ServerMap,
                ServerDisableUserVconsoleInput = Settings.Default.ServerDisableUserVconsoleInput,
                ClientEnabled = Settings.Default.ClientEnabled,
                ClientIpAddress = Settings.Default.ClientIpAddress,
                ClientPort = Settings.Default.ClientPort,
                ClientUsername = Settings.Default.ClientUsername,
                ClientPassword = Settings.Default.ClientPassword,
                ClientPrintVconsole = Settings.Default.ClientPrintVconsole,
                VconsolePort = Settings.Default.VconsolePort,
                VconsoleProtocol = Settings.Default.VconsoleProtocol
            };

            // Parse current addons
            try
            {
                preset.CurrentAddons = JsonConvert.DeserializeObject<List<string>>(Settings.Default.CurrentAddons) ?? new List<string>();
            }
            catch
            {
                preset.CurrentAddons = new List<string>();
            }

            return preset;
        }

        /// <summary>
        /// Applies this preset to current settings.
        /// </summary>
        public void ApplyToSettings()
        {
            Settings.Default.ServerEnabled = ServerEnabled;
            Settings.Default.ServerPort = ServerPort;
            Settings.Default.ServerPassword = ServerPassword;
            Settings.Default.ServerMap = ServerMap;
            Settings.Default.ServerDisableUserVconsoleInput = ServerDisableUserVconsoleInput;
            Settings.Default.ClientEnabled = ClientEnabled;
            Settings.Default.ClientIpAddress = ClientIpAddress;
            Settings.Default.ClientPort = ClientPort;
            Settings.Default.ClientUsername = ClientUsername;
            Settings.Default.ClientPassword = ClientPassword;
            Settings.Default.ClientPrintVconsole = ClientPrintVconsole;
            Settings.Default.VconsolePort = VconsolePort;
            Settings.Default.VconsoleProtocol = VconsoleProtocol;
            Settings.Default.CurrentAddons = JsonConvert.SerializeObject(CurrentAddons);
            Settings.Default.Save();
        }
    }
}
