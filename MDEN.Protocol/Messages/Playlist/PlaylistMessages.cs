namespace MDEN.Protocol.Messages.Playlist
{
    public class PlaylistAddRequest
    {
        public string Entry { get; set; }
    }

    public class PlaylistAddResponse
    {
        public int Result { get; set; }
    }

    public class PlaylistRemoveRequest
    {
        public string Entry { get; set; }
    }

    public class PlaylistRemoveResponse
    {
    }

    public class PlaylistContinueRequest
    {
    }

    public class PlaylistContinueResponse
    {
    }
}
