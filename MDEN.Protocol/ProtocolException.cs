using System;

namespace MDEN.Protocol
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }
}
