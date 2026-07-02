namespace MDEN.Protocol
{
    /// <summary>
    /// 所有网络操作码定义
    /// 按功能模块划分区间，新增功能在对应区间内追加
    /// Request/Response 使用相邻的奇偶码
    /// </summary>
    public static class OpCodes
    {
        // === 系统 (0x0001 ~ 0x00FF) ===
        public const ushort Ping = 0x0001;
        public const ushort Pong = 0x0002;
        public const ushort Disconnect = 0x0003;
        public const ushort ServerInfoReq = 0x0004;
        public const ushort ServerInfoResp = 0x0005;
        public const ushort PingReportNotify = 0x0006;

        // === 认证 (0x0100 ~ 0x01FF) ===
        public const ushort LoginReq = 0x0100;
        public const ushort LoginResp = 0x0101;

        // === 玩家 (0x0200 ~ 0x02FF) ===
        public const ushort UpdatePlayerReq = 0x0200;
        public const ushort UpdatePlayerResp = 0x0201;
        public const ushort GetPlayerReq = 0x0202;
        public const ushort GetPlayerResp = 0x0203;

        // === 房间 (0x0300 ~ 0x03FF) ===
        public const ushort CreateLobbyReq = 0x0300;
        public const ushort CreateLobbyResp = 0x0301;
        public const ushort JoinLobbyReq = 0x0302;
        public const ushort JoinLobbyResp = 0x0303;
        public const ushort LeaveLobbyReq = 0x0304;
        public const ushort LeaveLobbyResp = 0x0305;
        public const ushort GetLobbiesReq = 0x0306;
        public const ushort GetLobbiesResp = 0x0307;
        public const ushort LobbyReadyReq = 0x0308;
        public const ushort LobbyReadyResp = 0x0309;
        public const ushort LobbyKickReq = 0x030A;
        public const ushort LobbyKickResp = 0x030B;
        public const ushort LobbyLockReq = 0x030C;
        public const ushort LobbyLockResp = 0x030D;
        public const ushort LobbyStopReq = 0x030E;
        public const ushort LobbyStopResp = 0x030F;
        public const ushort LobbyTransferHostReq = 0x0310;
        public const ushort LobbyTransferHostResp = 0x0311;
        public const ushort LobbyMuteReq = 0x0312;
        public const ushort LobbyMuteResp = 0x0313;
        public const ushort LobbyBanChartSelectReq = 0x0314;
        public const ushort LobbyBanChartSelectResp = 0x0315;
        public const ushort LobbySettingsReq = 0x0316;
        public const ushort LobbySettingsResp = 0x0317;
        public const ushort GetCurrentLobbyReq = 0x0318;
        public const ushort GetCurrentLobbyResp = 0x0319;

        // === 播放列表 (0x0400 ~ 0x04FF) ===
        public const ushort PlaylistAddReq = 0x0400;
        public const ushort PlaylistAddResp = 0x0401;
        public const ushort PlaylistRemoveReq = 0x0402;
        public const ushort PlaylistRemoveResp = 0x0403;
        public const ushort PlaylistContinueReq = 0x0404;
        public const ushort PlaylistContinueResp = 0x0405;

        // === 对战 (0x0500 ~ 0x05FF) ===
        public const ushort BattleDataNotify = 0x0500;
        public const ushort BattleDataPush = 0x0501;
        public const ushort BattleReturnedReq = 0x0502;
        public const ushort BattleReturnedResp = 0x0503;
        public const ushort SettlementResultPush = 0x0504;
        public const ushort BattleStartFailedNotify = 0x0505;
        public const ushort BattleDataDeltaPush = 0x0506;

        // === 聊天 (0x0600 ~ 0x06FF) ===
        public const ushort ChatNotify = 0x0600;
        public const ushort ChatPush = 0x0601;

        // === 社交 (0x0700 ~ 0x07FF) ===
        public const ushort FriendRequestReq = 0x0700;
        public const ushort FriendRequestResp = 0x0701;
        public const ushort GetFriendsReq = 0x0702;
        public const ushort GetFriendsResp = 0x0703;
        public const ushort FriendNotifyPush = 0x0704;
        public const ushort GetNodePlayersReq = 0x0710;
        public const ushort GetNodePlayersResp = 0x0711;
        public const ushort SendLobbyInviteReq = 0x0712;
        public const ushort SendLobbyInviteResp = 0x0713;
        public const ushort RespondLobbyInviteReq = 0x0714;
        public const ushort RespondLobbyInviteResp = 0x0715;
        public const ushort PlayerPresenceNotify = 0x0716;
        public const ushort LobbyInvitePush = 0x0717;

        // === 房间状态推送 (0x0800 ~ 0x08FF) ===
        public const ushort LobbySyncPush = 0x0800;
        public const ushort LobbyAllReturnedPush = 0x0801;
        public const ushort LobbyKickedPush = 0x0802;

        // === 喵斯兔联机大厅 (0x0900 ~ 0x09FF) ===
        public const ushort MdtObserveReq = 0x0900;
        public const ushort MdtObserveResp = 0x0901;
        public const ushort MdtGetLobbySnapshotReq = 0x0902;
        public const ushort MdtGetLobbySnapshotResp = 0x0903;
        public const ushort MdtChatReq = 0x0904;
        public const ushort MdtChatResp = 0x0905;
        public const ushort MdtWatchLobbyReq = 0x0906;
        public const ushort MdtWatchLobbyResp = 0x0907;
        public const ushort MdtViewerChatReq = 0x0908;
        public const ushort MdtViewerChatResp = 0x0909;
        public const ushort MdtLobbySnapshotPush = 0x0910;
        public const ushort MdtHostReplyNotify = 0x0911;
        public const ushort MdtChatPush = 0x0912;
        public const ushort MdtViewerChatPush = 0x0913;
    }
}
