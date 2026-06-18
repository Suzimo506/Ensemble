using MelonLoader;
using MDEN.Managers;
using MDEN.Network;
using MDEN.UI.Core;
using System;

[assembly: MelonInfo(typeof(MDEN.Main), "Ensemble", "0.3.2", "MDENTeam")]
[assembly: MelonGame("PeroPeroGames", "MuseDash")]

namespace MDEN
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Mod is initializing...");
            ModConfigManager.LoadConfig();
            PushDispatcher.Instance.Init();
            ReconnectionManager.Instance.Init();
            LobbyManager.Init();
            ChatManager.Init();
            SocialManager.Init();
            ChartManager.Initialize();
            BattleManager.Init();
            SettlementManager.Init();
            BattleHudController.Initialize();
            SettlementHudController.Initialize();
            RoomHudController.Initialize();
            MelonLogger.Msg("Initialization complete.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "UISystem_PC")
            {
                WindowStackController.ForceUnlock();
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
                Patches.BattleFlowPatch.SceneLoaded();
            }
        }

        public override void OnUpdate()
        {
            MainThreadDispatcher.ProcessQueue();
            PlayerManager.SyncCurrentSelectionIfChanged();
            RoomHudController.Update();
        }

        public override void OnDeinitializeMelon()
        {
            BattleHudController.Deinitialize();
            SettlementHudController.Deinitialize();
            RoomHudController.Deinitialize();
            ConnectionManager.Disconnect();
        }
    }
}
