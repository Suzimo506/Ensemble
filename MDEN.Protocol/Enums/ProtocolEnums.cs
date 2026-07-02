namespace MDEN.Protocol.Enums
{
    public enum PlayerStatus : byte
    {
        Offline = 0,
        Online = 1,
        InLobby = 2,
        InBattle = 3,
        SinglePlaying = 4
    }

    public enum LobbyGoal : byte
    {
        Accuracy = 0,
        Score = 1,
        Custom = 2
    }

    public enum LobbyPlayType : byte
    {
        All = 0,
        VanillaOnly = 1,
        CustomOnly = 2
    }

    public enum LobbyChartSelection : byte
    {
        HostPlaylist = 0,
        Playlist = 1,
        Random = 2
    }

    public enum LobbyPlayMode : byte
    {
        Normal = 0,
        Rookie = 1,
        Fearless = 2,
        Tenzi = 3
    }
}
