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

    public class ModConfigData
    {
        public List<CustomServerInfo> CustomServers { get; set; } = new List<CustomServerInfo>();
        public string ClientUid { get; set; }
        public string PlayerName { get; set; }
        public string PlayerBio { get; set; }
        public string PlayerChatColor { get; set; }
        public string PlayerEntranceMessage { get; set; }
        public string PlayerTitle { get; set; }
        public string PlayerAvatarName { get; set; }
        public bool EnableFavGirlDisplayForOthers { get; set; }
        public bool HideBattleHealthBar { get; set; }
    }

    public static class ModConfigManager
    {
#pragma warning disable CS0618
        private static readonly string ConfigPath = Path.Combine(MelonUtils.UserDataDirectory, "Ensemble.json");
#pragma warning restore CS0618
        public static List<CustomServerInfo> CustomServers { get; private set; } = new List<CustomServerInfo>();
        public static string ClientUid { get; private set; }
        public static string PlayerName { get; private set; }
        public static string PlayerBio { get; private set; }
        public static string PlayerChatColor { get; private set; }
        public static string PlayerEntranceMessage { get; private set; }
        public static string PlayerTitle { get; private set; }
        public static string PlayerAvatarName { get; private set; }
        public static bool EnableFavGirlDisplayForOthers { get; private set; }
        public static bool HideBattleHealthBar { get; private set; }

        public static void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    LoadConfigFromJson(json);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Failed to load Ensemble.json: {ex.Message}");
                    CustomServers = new List<CustomServerInfo>();
                }
            }

            EnsureIdentity();
        }

        public static void SaveConfig()
        {
            try
            {
                var data = new ModConfigData
                {
                    CustomServers = CustomServers,
                    ClientUid = ClientUid,
                    PlayerName = PlayerName,
                    PlayerBio = PlayerBio,
                    PlayerChatColor = PlayerChatColor,
                    PlayerEntranceMessage = PlayerEntranceMessage,
                    PlayerTitle = PlayerTitle,
                    PlayerAvatarName = PlayerAvatarName,
                    EnableFavGirlDisplayForOthers = EnableFavGirlDisplayForOthers,
                    HideBattleHealthBar = HideBattleHealthBar
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
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

        public static void DeleteCustomServer(int index)
        {
            if (index >= 0 && index < CustomServers.Count)
            {
                CustomServers.RemoveAt(index);
                SaveConfig();
            }
        }

        public static void SetPlayerName(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                PlayerName = playerName;
                SaveConfig();
            }
        }

        public static void SetPlayerBio(string bio)
        {
            PlayerBio = bio ?? string.Empty;
            SaveConfig();
        }

        public static void SetPlayerChatColor(string chatColor)
        {
            PlayerChatColor = string.IsNullOrWhiteSpace(chatColor) ? "ffffff" : chatColor.Trim().TrimStart('#');
            SaveConfig();
        }

        public static void SetPlayerEntranceMessage(string entranceMessage)
        {
            PlayerEntranceMessage = entranceMessage ?? string.Empty;
            SaveConfig();
        }

        public static void SetPlayerTitle(string title)
        {
            PlayerTitle = title ?? string.Empty;
            SaveConfig();
        }

        public static void SetEnableFavGirlDisplayForOthers(bool enabled)
        {
            EnableFavGirlDisplayForOthers = enabled;
            SaveConfig();
        }

        public static void SetHideBattleHealthBar(bool enabled)
        {
            HideBattleHealthBar = enabled;
            SaveConfig();
        }

        private static void LoadConfigFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                CustomServers = new List<CustomServerInfo>();
                return;
            }

            if (json.TrimStart().StartsWith("["))
            {
                CustomServers = JsonSerializer.Deserialize<List<CustomServerInfo>>(json) ?? new List<CustomServerInfo>();
                return;
            }

            var data = JsonSerializer.Deserialize<ModConfigData>(json);
            CustomServers = data?.CustomServers ?? new List<CustomServerInfo>();
            ClientUid = data?.ClientUid;
            PlayerName = data?.PlayerName;
            PlayerBio = data?.PlayerBio;
            PlayerChatColor = data?.PlayerChatColor;
            PlayerEntranceMessage = data?.PlayerEntranceMessage;
            PlayerTitle = data?.PlayerTitle;
            PlayerAvatarName = data?.PlayerAvatarName;
            EnableFavGirlDisplayForOthers = data?.EnableFavGirlDisplayForOthers ?? false;
            HideBattleHealthBar = data?.HideBattleHealthBar ?? false;
        }

        private static void EnsureIdentity()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(PlayerName))
            {
                PlayerName = "Player";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(PlayerChatColor))
            {
                PlayerChatColor = "ffffff";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(PlayerAvatarName))
            {
                PlayerAvatarName = "head_0";
                changed = true;
            }

            if (changed)
            {
                SaveConfig();
            }
        }
    }
}
