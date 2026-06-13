using System.Text.Json;
using System.Text.Json.Serialization;

namespace MDEN.Protocol
{
    public static class ProtocolJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = null // Ensure PascalCase
        };
    }
}
