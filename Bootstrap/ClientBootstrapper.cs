using System.IO;
using MDEN.Managers;
using MDEN.Network;
using MDEN.Protocol.Enums;
using MDEN.Threading;
using MDEN.UI.Core;
using MelonLoader;

namespace MDEN.Bootstrap
{
    internal static class ClientBootstrapper
    {
        public static void Initialize()
        {
            ClientLogManager.Msg("Mod is initializing...");
            ClientThreadDispatcher.Configure(action => MainThreadDispatcher.Enqueue(action));
            ModConfigManager.LoadConfig();
            I18nManager.Initialize();
            PushDispatcher.Instance.Init();
            ReconnectionManager.Instance.Init();
            LobbyManager.Init();
            ChatManager.Init();
            SocialManager.Init();
            ChartManager.Initialize();
            BattleManager.Init();
            SettlementManager.Init();
            MainThreadWatchdog.Initialize();
            UiNotificationController.Initialize();
            MuseDashToolStatusListener.Initialize();
            BattleHudController.Initialize();
            SettlementHudController.Initialize();
            RoomHudController.Initialize();
            NodePlayersOverlay.Initialize();
            ClientLogManager.Msg("Initialization complete.");
        }

        public static void HandleSceneLoaded(string sceneName)
        {
            MainThreadWatchdog.SetScene(sceneName);
            if (sceneName == "UISystem_PC")
            {
                HandleUiSceneLoaded();
                return;
            }

            if (sceneName == "GameMain")
            {
                HandleGameSceneLoaded();
                return;
            }

            PerfTrace.SetGameMain(false);
        }

        public static void Update()
        {
            MainThreadWatchdog.Heartbeat("OnUpdate.Begin");
            PerfTrace.BeginFrame();
            MainThreadWatchdog.SetStage("MainThreadDispatcher.ProcessQueue");
            MainThreadDispatcher.ProcessQueue();

            if (!BattleManager.IsActiveMultiplayerBattle)
            {
                MainThreadWatchdog.SetStage("PlayerManager.SyncCurrentSelectionIfChanged");
                PlayerManager.SyncCurrentSelectionIfChanged();
            }

            MainThreadWatchdog.SetStage("BattleFlowPatch.UpdateBattleUiState");
            Patches.BattleFlowPatch.UpdateBattleUiState();
            MainThreadWatchdog.SetStage("SettlementOverlayController.Update");
            SettlementOverlayController.Update();
            MainThreadWatchdog.SetStage("RoomHudController.Update");
            RoomHudController.Update();

            MainThreadWatchdog.SetStage("PerfTrace.UpdateReport");
            PerfTrace.UpdateReport();
            MainThreadWatchdog.SetStage("OnUpdate.End");
            MainThreadWatchdog.Heartbeat("OnUpdate.End");
        }

        public static void Shutdown()
        {
            NativeInputBlocker.ClearAll();
            SettlementOverlayController.ClearAll();
            PerfTrace.SetGameMain(false);
            RoomHudController.SetBattleSceneActive(false);
            NodePlayersOverlay.Destroy();
            BattleHudController.Deinitialize();
            SettlementHudController.Deinitialize();
            MuseDashToolStatusListener.Shutdown();
            UiNotificationController.Deinitialize();
            RoomHudController.Deinitialize();
            NodePlayersOverlay.Deinitialize();
            MainThreadWatchdog.Shutdown();
            ConnectionManager.Disconnect();
        }

        private static void HandleUiSceneLoaded()
        {
            WindowStackController.ForceUnlock();
            NodePlayersOverlay.Destroy();
            NativeInputBlocker.ClearAll();
            SettlementOverlayController.Reset();
            BattleHealthBarController.Reset();
            Patches.BattleFlowPatch.ResetBattleSceneState();
            RoomHudController.SetBattleSceneActive(false);
            PerfTrace.SetGameMain(false);
            NavigationButton.ResetSceneObjects();
            RoomHudController.ResetSceneObjects();

            var bundlePath = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MDEN", "ui.bundle");
            ResourceManager.Initialize(bundlePath);

            // Keep UI injection timing aligned with the original scene lifecycle.
            CloudSyncIndicator.Initialize();
            NavigationButton.Create();
            RoomHudController.Refresh();
            RoomHudController.RequestRefresh();
            VersionCheckManager.CheckOnce();
            PlayerManager.SyncPresenceIfChanged(PlayerStatus.Online);
        }

        private static void HandleGameSceneLoaded()
        {
            WindowStackController.ForceUnlock();
            CustomAlbumsWindowGuard.CloseIfOpen("GameMain scene load");
            NodePlayersOverlay.Destroy();
            NativeInputBlocker.ClearAll();
            SettlementOverlayController.Reset();
            BattleHealthBarController.Reset();
            RoomHudController.SetBattleSceneActive(true);
            PerfTrace.SetGameMain(true);
            Patches.BattleFlowPatch.SceneLoaded();
            if (!LobbyManager.IsInLobby)
            {
                PlayerManager.SyncPresenceIfChanged(PlayerStatus.SinglePlaying);
            }
        }
    }
}
