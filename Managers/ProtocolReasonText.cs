namespace MDEN.Managers
{
    internal static class ProtocolReasonText
    {
        public static bool TryReadLastPositiveInt(string value, out int parsed)
        {
            parsed = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var multiplier = 1;
            for (var i = value.Length - 1; i >= 0; i--)
            {
                var ch = value[i];
                if (ch < '0' || ch > '9')
                {
                    if (parsed > 0) return true;
                    continue;
                }

                parsed += (ch - '0') * multiplier;
                multiplier *= 10;
            }

            return parsed > 0;
        }
    }
}
