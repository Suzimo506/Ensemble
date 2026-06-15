using MelonLoader;
using MDEN.Managers;
using MDEN.Network;
using MDEN.UI.Core;
using System;

[assembly: MelonInfo(typeof(MDEN.Main), "MDEN", "1.0.0", "MDEN Team")]
[assembly: MelonGame("PeroPeroGames", "MuseDash")]

namespace MDEN
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Mod is initializing...");
            PushDispatcher.Instance.Init();
            ReconnectionManager.Instance.Init();
            LobbyManager.Init();
            ChatManager.Init();
            BattleManager.Init();
            RoomHudController.Initialize();
            MelonLogger.Msg("Initialization complete.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "UISystem_PC")
            {
                string bundlePath = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MDEN", "ui.bundle");
                ResourceManager.Initialize(bundlePath);
                
                // 增加场景加载时的注入时机，确保和原版模组一致
                CloudSyncIndicator.Initialize();
                NavigationButton.Create();
                RoomHudController.Refresh();
                RoomHudController.RequestRefresh();
            }
        }

        public override void OnUpdate()
        {
            MainThreadDispatcher.ProcessQueue();
            RoomHudController.Update();
        }

        public override void OnDeinitializeMelon()
        {
            RoomHudController.Deinitialize();
            ConnectionManager.Disconnect();
        }
    }
}
