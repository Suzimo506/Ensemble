namespace MDEN.Protocol.Messages.Auth
{
    public class LoginRequest
    {
        public string Uid { get; set; }
        public string Name { get; set; }
        public bool IsReconnect { get; set; }
    }

    public class LoginResponse
    {
        public string Version { get; set; }
        public string Token { get; set; }
    }
}
