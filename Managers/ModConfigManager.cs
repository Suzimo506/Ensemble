using System.Collections.Generic;
using System.IO;
using MelonLoader;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MDEN.Managers
{
    public class CustomServerInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public static class ModConfigManager
    {
#pragma warning disable CS0618
        private static readonly string ConfigPath = Path.Combine(MelonUtils.UserDataDirectory, "Ensemble.json");
#pragma warning restore CS0618
        public static List<CustomServerInfo> CustomServers { get; private set; } = new List<CustomServerInfo>();

        public static void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    CustomServers = JsonSerializer.Deserialize<List<CustomServerInfo>>(json) ?? new List<CustomServerInfo>();
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to load Ensemble.json: {ex.Message}");
                    CustomServers = new List<CustomServerInfo>();
                }
            }
        }

        public static void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(CustomServers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to save Ensemble.json: {ex.Message}");
            }
        }

        public static void AddCustomServer(string address, string name = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = $"节点{CustomServers.Count + 1}";
            }
            CustomServers.Add(new CustomServerInfo { Address = address, Name = name });
            SaveConfig();
        }

        public static void RenameCustomServer(int index, string newName)
        {
            if (index >= 0 && index < CustomServers.Count)
            {
                CustomServers[index].Name = newName;
                SaveConfig();
            }
        }
    }
}
