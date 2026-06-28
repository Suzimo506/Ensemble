namespace MDEN.Protocol.Rules
{
    public static class DifficultyDisplayRules
    {
        public const int Easy = 1;
        public const int Hard = 2;
        public const int Master = 3;
        public const int Hidden = 4;
        public const int Spell = 5;
        public const int SpellEasy = 6;
        public const int SpellHard = 7;
        public const int SpellMaster = 8;

        public static bool IsKnownDifficulty(int difficulty)
        {
            return difficulty >= Easy && difficulty <= SpellMaster;
        }

        public static bool IsSpellDifficulty(int difficulty)
        {
            return difficulty == Spell ||
                   difficulty == SpellEasy ||
                   difficulty == SpellHard ||
                   difficulty == SpellMaster;
        }

        public static int GetSpellNativeDifficulty(int difficulty)
        {
            return difficulty switch
            {
                SpellEasy => Easy,
                SpellHard => Hard,
                SpellMaster => Master,
                _ => Hard
            };
        }

        public static bool IsFearlessDifficulty(int difficulty)
        {
            return difficulty == Master || difficulty == Hidden || IsSpellDifficulty(difficulty);
        }
    }
}
