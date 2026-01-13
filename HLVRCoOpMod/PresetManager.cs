using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace HLVRCoOpMod
{
    /// <summary>
    /// Manages configuration presets - loading, saving, and organizing presets.
    /// </summary>
    public static class PresetManager
    {
        private static string PresetsDirectory => Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
            "presets");

        private static string PresetsFilePath => Path.Combine(PresetsDirectory, "presets.json");

        /// <summary>
        /// Gets all saved presets.
        /// </summary>
        public static List<ConfigurationPreset> GetPresets()
        {
            try
            {
                if (!Directory.Exists(PresetsDirectory))
                    Directory.CreateDirectory(PresetsDirectory);

                if (!File.Exists(PresetsFilePath))
                    return new List<ConfigurationPreset>();

                string json = File.ReadAllText(PresetsFilePath);
                var presets = JsonConvert.DeserializeObject<List<ConfigurationPreset>>(json);
                return presets ?? new List<ConfigurationPreset>();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.GetPresets", ex);
                return new List<ConfigurationPreset>();
            }
        }

        /// <summary>
        /// Saves a preset.
        /// </summary>
        public static bool SavePreset(ConfigurationPreset preset)
        {
            try
            {
                if (!Directory.Exists(PresetsDirectory))
                    Directory.CreateDirectory(PresetsDirectory);

                var presets = GetPresets();
                
                // Remove existing preset with same name
                presets.RemoveAll(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
                
                preset.LastModified = DateTime.Now;
                presets.Add(preset);

                string json = JsonConvert.SerializeObject(presets, Formatting.Indented);
                File.WriteAllText(PresetsFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.SavePreset", ex);
                return false;
            }
        }

        /// <summary>
        /// Deletes a preset by name.
        /// </summary>
        public static bool DeletePreset(string presetName)
        {
            try
            {
                var presets = GetPresets();
                int removed = presets.RemoveAll(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
                
                if (removed > 0)
                {
                    string json = JsonConvert.SerializeObject(presets, Formatting.Indented);
                    File.WriteAllText(PresetsFilePath, json);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.DeletePreset", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets a preset by name.
        /// </summary>
        public static ConfigurationPreset? GetPreset(string presetName)
        {
            var presets = GetPresets();
            return presets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Exports a preset to a JSON file.
        /// </summary>
        public static bool ExportPreset(ConfigurationPreset preset, string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(preset, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.ExportPreset", ex);
                return false;
            }
        }

        /// <summary>
        /// Imports a preset from a JSON file.
        /// </summary>
        public static ConfigurationPreset? ImportPreset(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var preset = JsonConvert.DeserializeObject<ConfigurationPreset>(json);
                return preset;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.ImportPreset", ex);
                return null;
            }
        }

        /// <summary>
        /// Exports all presets to a JSON file.
        /// </summary>
        public static bool ExportAllPresets(string filePath)
        {
            try
            {
                var presets = GetPresets();
                string json = JsonConvert.SerializeObject(presets, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.ExportAllPresets", ex);
                return false;
            }
        }

        /// <summary>
        /// Imports presets from a JSON file (adds to existing presets).
        /// </summary>
        public static bool ImportPresets(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var importedPresets = JsonConvert.DeserializeObject<List<ConfigurationPreset>>(json);
                
                if (importedPresets == null || importedPresets.Count == 0)
                    return false;

                var existingPresets = GetPresets();
                
                foreach (var preset in importedPresets)
                {
                    // Remove existing preset with same name, then add imported one
                    existingPresets.RemoveAll(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
                    existingPresets.Add(preset);
                }

                string saveJson = JsonConvert.SerializeObject(existingPresets, Formatting.Indented);
                File.WriteAllText(PresetsFilePath, saveJson);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PresetManager.ImportPresets", ex);
                return false;
            }
        }
    }
}
