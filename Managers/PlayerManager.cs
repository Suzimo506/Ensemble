using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
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
        }
    }
}
