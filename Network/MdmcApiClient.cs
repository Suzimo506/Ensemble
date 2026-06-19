using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using MelonLoader;

namespace MDEN.Network
{
    public static class MdmcApiClient
    {
        private static readonly HttpClient _client = new HttpClient
        {
            BaseAddress = new Uri("https://api.mdmc.moe/v3/")
        };

        public static async Task<(string Name, string AvatarUrl)?> GetUserProfileAsync(int mdmcUid)
        {
            try
            {
                var response = await _client.GetAsync($"users/{mdmcUid}");
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                    
                    string name = null;
                    if (data.TryGetProperty("username", out var nameElement))
                    {
                        name = nameElement.GetString();
                    }

                    string avatarUrl = null;
                    if (data.TryGetProperty("profile", out var profileElement))
                    {
                        if (profileElement.TryGetProperty("avatar", out var avatarElement))
                        {
                            string discordId = data.GetProperty("discordId").GetString();
                            string avatarString = avatarElement.GetString();
                            // 按照 MDMC 规范拼接头像 URL
                            avatarUrl = $"https://cdn.mdmc.moe/avatars/{discordId}.{avatarString}.webp";
                        }
                    }

                    return (name, avatarUrl);
                }
                else
                {
                    MDEN.Managers.ClientLogManager.Warning($"[MDMC API] Failed to fetch user info. Uid: {mdmcUid}, status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MDMC API] Request exception: {ex}");
            }
            
            return null;
        }
    }
}
