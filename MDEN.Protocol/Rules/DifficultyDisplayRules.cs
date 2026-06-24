namespace MDEN.Protocol.Rules
{
    public static class DifficultyDisplayRules
    {
        public const int Easy = 1;
        public const int Hard = 2;
        public const int Master = 3;
        public const int Hidden = 4;

        public static bool IsKnownDifficulty(int difficulty)
        {
            return difficulty >= Easy && difficulty <= Hidden;
        }

        public static bool IsFearlessDifficulty(int difficulty)
        {
            return difficulty == Master || difficulty == Hidden;
        }
    }
}
