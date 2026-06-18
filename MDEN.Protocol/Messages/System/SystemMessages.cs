namespace MDEN.Protocol.Messages.System
{
    public class ServerInfoRequest
    {
    }

    public class ServerInfoResponse
    {
        public string NodeId { get; set; }
        public string Version { get; set; }
        public int PlayerCount { get; set; }
        public int RoomCount { get; set; }
    }

    public class PingReportNotify
    {
        public ushort PingMS { get; set; }
    }
}
