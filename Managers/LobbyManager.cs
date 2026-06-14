using System.Threading.Tasks;
using MDEN.Network;
using MDEN.Protocol;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MelonLoader;

namespace MDEN.Managers
{
    public static class LobbyManager
    {
        public static LobbyListEntry[] CurrentLobbies { get; private set; } = new LobbyListEntry[0];
        public static LobbySyncPush CurrentLobby { get; private set; }
        public static bool IsInLobby => CurrentLobby != null;

        public static async Task<LobbyListEntry[]> RefreshLobbiesAsync()
        {
            EnsureReady();

            var response = await NetworkClient.Instance.SendRequestAsync<GetLobbiesRequest, GetLobbiesResponse>(
                OpCodes.GetLobbiesReq,
                new GetLobbiesRequest());

            CurrentLobbies = response?.Lobbies ?? new LobbyListEntry[0];
            return CurrentLobbies;
        }

        public static async Task JoinLobbyAsync(int lobbyId)
        {
            EnsureReady();

            await NetworkClient.Instance.SendRequestAsync<JoinLobbyRequest, JoinLobbyResponse>(
                OpCodes.JoinLobbyReq,
                new JoinLobbyRequest { LobbyId = lobbyId });
        }

        public static async Task<int> CreateLobbyAsync(CreateLobbyRequest request)
        {
            EnsureReady();

            var response = await NetworkClient.Instance.SendRequestAsync<CreateLobbyRequest, CreateLobbyResponse>(
                OpCodes.CreateLobbyReq,
                request);

            return response?.LobbyId ?? 0;
        }

        public static void Init()
        {
            PushDispatcher.Instance.Register<LobbySyncPush>(OpCodes.LobbySyncPush, OnLobbySync);
        }

        public static void ClearSession()
        {
            CurrentLobbies = new LobbyListEntry[0];
            CurrentLobby = null;
        }

        public static void MarkLobbyEntered(int lobbyId, CreateLobbyRequest request)
        {
            CurrentLobby = new LobbySyncPush
            {
                Id = lobbyId,
                Name = request.Name,
                HostUid = PlayerManager.CurrentUid,
                HostName = PlayerManager.CurrentProfile?.Name,
                MaxPlayers = request.MaxPlayers,
                PlaylistSize = request.PlaylistSize,
                PlayType = request.PlayType,
                ChartSelection = request.ChartSelection,
                Goal = request.Goal,
                Locked = false,
                IsPlaying = false,
                Players = string.IsNullOrEmpty(PlayerManager.CurrentUid) ? new string[0] : new[] { PlayerManager.CurrentUid },
                ReadyPlayers = new string[0],
                Playlist = new string[0],
                PlayerDetails = new PlayerSyncEntry[0]
            };
        }

        private static void OnLobbySync(LobbySyncPush push)
        {
            CurrentLobby = push;
            MelonLogger.Msg($"Lobby sync received: {push.Id}, players: {push.Players?.Length ?? 0}/{push.MaxPlayers}");
        }

        private static void EnsureReady()
        {
            if (!NetworkClient.Instance.IsConnected)
            {
                throw new System.InvalidOperationException("Not connected to server.");
            }

            if (!ConnectionManager.IsLoggedIn)
            {
                throw new System.InvalidOperationException("Not logged in to server.");
            }
        }
    }
}
