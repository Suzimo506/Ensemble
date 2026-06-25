using MelonLoader;
using MDEN.Bootstrap;

[assembly: MelonInfo(typeof(MDEN.Main), "Ensemble", "0.3.6", "MDENTeam")]
[assembly: MelonGame("PeroPeroGames", "MuseDash")]
[assembly: MelonOptionalDependencies("FavGirl")]

namespace MDEN
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ClientBootstrapper.Initialize();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ClientBootstrapper.HandleSceneLoaded(sceneName);
        }

        public override void OnUpdate()
        {
            ClientBootstrapper.Update();
        }

        public override void OnDeinitializeMelon()
        {
            ClientBootstrapper.Shutdown();
        }
    }
}
