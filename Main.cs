using MelonLoader;
using MDEN.Managers;
using MDEN.Network;
using MDEN.UI.Core;
using System;

[assembly: MelonInfo(typeof(MDEN.Main), "Ensemble", "0.3.4", "MDENTeam")]
[assembly: MelonGame("PeroPeroGames", "MuseDash")]
[assembly: MelonOptionalDependencies("FavGirl")]

namespace MDEN
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MDEN.Managers.ClientLogManager.Msg("Mod is initializing...");
            ModConfigManager.LoadConfig();
            PushDispatcher.Instance.Init();
            ReconnectionManager.Instance.Init();
            LobbyManager.Init();
            ChatManager.Init();
            SocialManager.Init();
            ChartManager.Initialize();
            BattleManager.Init();
            SettlementManager.Init();
            UiNotificationController.Initialize();
            BattleHudController.Initialize();
            SettlementHudController.Initialize();
            RoomHudController.Initialize();
            MDEN.Managers.ClientLogManager.Msg("Initialization complete.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "UISystem_PC")
            {
                WindowStackController.ForceUnlock();
                NativeInputBlocker.ClearAll();
                BattleResultBannerDisplay.ClearAll();
                BattleHealthBarController.Reset();
                Patches.BattleFlowPatch.ResetBattleSceneState();
                RoomHudController.SetBattleSceneActive(false);
                PerfTrace.SetGameMain(false);
                NavigationButton.ResetSceneObjects();
                RoomHudController.ResetSceneObjects();

                string bundlePath = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MDEN", "ui.bundle");
                ResourceManager.Initialize(bundlePath);
                
                // 增加场景加载时的注入时机，确保和原版模组一致
                CloudSyncIndicator.Initialize();
                NavigationButton.Create();
                RoomHudController.Refresh();
                RoomHudController.RequestRefresh();
                VersionCheckManager.CheckOnce();
            }
            else if (sceneName == "GameMain")
            {
                WindowStackController.ForceUnlock();
                CustomAlbumsWindowGuard.CloseIfOpen("GameMain scene load");
                NativeInputBlocker.ClearAll();
                BattleResultBannerDisplay.ClearAll();
                BattleHealthBarController.Reset();
                RoomHudController.SetBattleSceneActive(true);
                PerfTrace.SetGameMain(true);
                Patches.BattleFlowPatch.SceneLoaded();
            }
            else
            {
                PerfTrace.SetGameMain(false);
            }
        }

        public override void OnUpdate()
        {
            PerfTrace.BeginFrame();
            MainThreadDispatcher.ProcessQueue();

            if (!BattleManager.IsActiveMultiplayerBattle)
            {
                PlayerManager.SyncCurrentSelectionIfChanged();
            }

            Patches.BattleFlowPatch.UpdateBattleUiState();
            BattleResultBannerDisplay.Update();
            SettlementResultDialog.Update();
            RoomHudController.Update();

            PerfTrace.UpdateReport();
        }

        public override void OnDeinitializeMelon()
        {
            NativeInputBlocker.ClearAll();
            BattleResultBannerDisplay.ClearAll();
            PerfTrace.SetGameMain(false);
            RoomHudController.SetBattleSceneActive(false);
            BattleHudController.Deinitialize();
            SettlementHudController.Deinitialize();
            UiNotificationController.Deinitialize();
            RoomHudController.Deinitialize();
            ConnectionManager.Disconnect();
        }
    }
}
