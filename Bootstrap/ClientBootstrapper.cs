using System.IO;
using MDEN.Managers;
using MDEN.Network;
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
            BattleHudController.Initialize();
            SettlementHudController.Initialize();
            RoomHudController.Initialize();
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
            MainThreadWatchdog.Heartbeat("MainThreadDispatcher.ProcessQueue");
            MainThreadDispatcher.ProcessQueue();

            if (!BattleManager.IsActiveMultiplayerBattle)
            {
                MainThreadWatchdog.Heartbeat("PlayerManager.SyncCurrentSelectionIfChanged");
                PlayerManager.SyncCurrentSelectionIfChanged();
            }

            MainThreadWatchdog.Heartbeat("BattleFlowPatch.UpdateBattleUiState");
            Patches.BattleFlowPatch.UpdateBattleUiState();
            MainThreadWatchdog.Heartbeat("SettlementOverlayController.Update");
            SettlementOverlayController.Update();
            MainThreadWatchdog.Heartbeat("RoomHudController.Update");
            RoomHudController.Update();

            MainThreadWatchdog.Heartbeat("PerfTrace.UpdateReport");
            PerfTrace.UpdateReport();
            MainThreadWatchdog.Heartbeat("OnUpdate.End");
        }

        public static void Shutdown()
        {
            NativeInputBlocker.ClearAll();
            SettlementOverlayController.ClearAll();
            PerfTrace.SetGameMain(false);
            RoomHudController.SetBattleSceneActive(false);
            BattleHudController.Deinitialize();
            SettlementHudController.Deinitialize();
            UiNotificationController.Deinitialize();
            RoomHudController.Deinitialize();
            MainThreadWatchdog.Shutdown();
            ConnectionManager.Disconnect();
        }

        private static void HandleUiSceneLoaded()
        {
            WindowStackController.ForceUnlock();
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
        }

        private static void HandleGameSceneLoaded()
        {
            WindowStackController.ForceUnlock();
            CustomAlbumsWindowGuard.CloseIfOpen("GameMain scene load");
            NativeInputBlocker.ClearAll();
            SettlementOverlayController.Reset();
            BattleHealthBarController.Reset();
            RoomHudController.SetBattleSceneActive(true);
            PerfTrace.SetGameMain(true);
            Patches.BattleFlowPatch.SceneLoaded();
        }
    }
}
