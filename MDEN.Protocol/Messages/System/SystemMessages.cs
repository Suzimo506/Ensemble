namespace MDEN.Protocol.Messages.System
{
    public class ServerInfoRequest
    {
    }

    public class ServerInfoResponse
    {
        public string NodeId { get; set; }
        public int PlayerCount { get; set; }
        public int RoomCount { get; set; }
    }
}
