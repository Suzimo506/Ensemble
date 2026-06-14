using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CustomAlbums.Managers;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Player;

namespace MDEN.Managers
{
    public static class PlayerManager
    {
        public static string CurrentUid { get; private set; }
        public static GetPlayerResponse CurrentProfile { get; private set; }

        public static void SetCurrentUid(string uid)
        {
            CurrentUid = uid;
        }

        public static void SetCurrentIdentity(string uid, string name)
        {
            CurrentUid = uid;
            CurrentProfile = new GetPlayerResponse
            {
                Uid = uid,
                Name = name,
                Status = (byte)PlayerStatus.Online
            };
        }

        public static void ClearSession()
        {
            CurrentUid = null;
            CurrentProfile = null;
        }

        public static async Task<GetPlayerResponse> GetMyProfileAsync()
        {
            EnsureReady();

            CurrentProfile = await NetworkClient.Instance.SendRequestAsync<GetPlayerRequest, GetPlayerResponse>(
                OpCodes.GetPlayerReq,
                new GetPlayerRequest { TargetUid = CurrentUid });

            return CurrentProfile;
        }

        public static Task SyncCurrentSelectionAsync()
        {
            var selection = GameAccountManager.GetCurrentSelection();
            return UpdateMyProfileAsync(new UpdatePlayerRequest
            {
                GirlIndex = selection.GirlIndex,
                ElfinIndex = selection.ElfinIndex,
                FavGirlIndex = selection.GirlIndex,
                FavElfinIndex = selection.ElfinIndex
            });
        }

        public static Task SyncCustomChartsAsync()
        {
            var customs = new List<string>();
            foreach (var pair in AlbumManager.LoadedAlbums)
            {
                var md5 = ChartManager.GetCustomChartMd5(pair.Value?.Uid);
                if (!string.IsNullOrEmpty(md5))
                {
                    customs.Add(md5);
                }
            }

            return UpdateMyProfileAsync(new UpdatePlayerRequest { Customs = customs.Distinct().ToArray() });
        }

        public static async Task UpdateMyProfileAsync(UpdatePlayerRequest request)
        {
            EnsureReady();

            await NetworkClient.Instance.SendRequestAsync<UpdatePlayerRequest, UpdatePlayerResponse>(
                OpCodes.UpdatePlayerReq,
                request);

            if (CurrentProfile != null)
            {
                ApplyLocalUpdate(CurrentProfile, request);
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                ModConfigManager.SetPlayerName(request.Name);
            }
        }

        public static Task UpdateNameAsync(string name)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { Name = name });
        }

        public static Task UpdateChatColorAsync(string chatColor)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { ChatColor = chatColor });
        }

        public static Task UpdateBioAsync(string bio)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { Bio = bio });
        }

        public static Task UpdateEntranceMessageAsync(string entranceMessage)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { EntranceMessage = entranceMessage });
        }

        public static Task UpdateTitleAsync(string title)
        {
            return UpdateMyProfileAsync(new UpdatePlayerRequest { Title = title });
        }

        private static void EnsureReady()
        {
            if (!NetworkClient.Instance.IsConnected)
            {
                throw new System.InvalidOperationException("Not connected to server.");
            }

            if (string.IsNullOrEmpty(CurrentUid))
            {
                throw new System.InvalidOperationException("Not logged in.");
            }
        }

        private static void ApplyLocalUpdate(GetPlayerResponse profile, UpdatePlayerRequest request)
        {
            if (request.Name != null) profile.Name = request.Name;
            if (request.Bio != null) profile.Bio = request.Bio;
            if (request.ChatColor != null) profile.ChatColor = request.ChatColor;
            if (request.EntranceMessage != null) profile.EntranceMessage = request.EntranceMessage;
            if (request.Title != null) profile.Title = request.Title;
            if (request.GirlIndex.HasValue) profile.GirlIndex = request.GirlIndex.Value;
            if (request.ElfinIndex.HasValue) profile.ElfinIndex = request.ElfinIndex.Value;
            if (request.FavGirlIndex.HasValue) profile.FavGirlIndex = request.FavGirlIndex.Value;
            if (request.FavElfinIndex.HasValue) profile.FavElfinIndex = request.FavElfinIndex.Value;
        }
    }
}
