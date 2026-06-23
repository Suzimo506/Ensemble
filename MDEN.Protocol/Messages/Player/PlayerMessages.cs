using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MDEN.Protocol.Messages.Player
{
    public class UpdatePlayerRequest
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Bio { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ChatColor { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string EntranceMessage { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Title { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AvatarName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AvatarData { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Level { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GirlIndex { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ElfinIndex { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? FavGirlIndex { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? FavElfinIndex { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] Customs { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] Hiddens { get; set; }
    }

    public class UpdatePlayerResponse
    {
    }

    public class GetPlayerRequest
    {
        public string TargetUid { get; set; }
    }

    public class GetPlayerResponse
    {
        public string Uid { get; set; }
        public string Name { get; set; }
        public byte Status { get; set; }
        public string Bio { get; set; }
        public string ChatColor { get; set; }
        public string EntranceMessage { get; set; }
        public string Title { get; set; }
        public string AvatarName { get; set; }
        public string AvatarData { get; set; }
        public int Level { get; set; }
        public ushort ELO { get; set; }
        public bool Banned { get; set; }
        public int GirlIndex { get; set; }
        public int ElfinIndex { get; set; }
        public int FavGirlIndex { get; set; }
        public int FavElfinIndex { get; set; }
        public ushort PingMS { get; set; }
        public string[] Friends { get; set; }
        public string[] FriendRequests { get; set; }
        public Dictionary<long, byte> Achievements { get; set; }
    }
}
