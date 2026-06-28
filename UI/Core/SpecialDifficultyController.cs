using Il2CppAssets.Scripts.Database;
using MDEN.Protocol.Rules;

namespace MDEN.UI.Core
{
    internal static class SpecialDifficultyController
    {
        private const int BarrageNativeDifficulty = DifficultyDisplayRules.Hard;

        public static int ResolveCurrentDifficulty(MusicInfo musicInfo, int nativeDifficulty)
        {
            if (!IsSpecialModeActive(musicInfo)) return nativeDifficulty;

            return nativeDifficulty switch
            {
                DifficultyDisplayRules.Easy => DifficultyDisplayRules.SpellEasy,
                DifficultyDisplayRules.Hard => DifficultyDisplayRules.SpellHard,
                DifficultyDisplayRules.Master => DifficultyDisplayRules.SpellMaster,
                _ => DifficultyDisplayRules.Spell
            };
        }

        public static int Sync(MusicInfo musicInfo, int difficulty)
        {
            var isSpecial = DifficultyDisplayRules.IsSpellDifficulty(difficulty) &&
                            HasSpecialDifficulty(musicInfo);
            var dbUiSpecial = GlobalDataBase.s_DbUISpecial;
            if (dbUiSpecial != null)
            {
                dbUiSpecial.isBarrageMode = isSpecial;
            }

            return isSpecial
                ? DifficultyDisplayRules.GetSpellNativeDifficulty(difficulty)
                : difficulty;
        }

        public static bool IsSpecialDifficultySelected(MusicInfo musicInfo, int nativeDifficulty)
        {
            if (musicInfo == null || nativeDifficulty != BarrageNativeDifficulty) return false;

            return IsSpecialModeActive(musicInfo);
        }

        public static bool IsSpecialDifficultySelected(MusicInfo musicInfo, int nativeDifficulty, int difficulty)
        {
            if (!DifficultyDisplayRules.IsSpellDifficulty(difficulty)) return false;
            if (!IsSpecialModeActive(musicInfo)) return false;

            return nativeDifficulty == DifficultyDisplayRules.GetSpellNativeDifficulty(difficulty);
        }

        public static int NormalizePlaylistDifficulty(int difficulty)
        {
            return DifficultyDisplayRules.IsSpellDifficulty(difficulty)
                ? DifficultyDisplayRules.Spell
                : difficulty;
        }

        public static int GetChartIdentityDifficulty(int difficulty)
        {
            return DifficultyDisplayRules.IsSpellDifficulty(difficulty)
                ? DifficultyDisplayRules.Spell
                : difficulty;
        }

        public static int GetLevelDisplayDifficulty(int difficulty)
        {
            if (difficulty == DifficultyDisplayRules.Spell) return DifficultyDisplayRules.Spell;
            return DifficultyDisplayRules.IsSpellDifficulty(difficulty)
                ? DifficultyDisplayRules.GetSpellNativeDifficulty(difficulty)
                : difficulty;
        }

        private static bool IsSpecialModeActive(MusicInfo musicInfo)
        {
            if (musicInfo == null) return false;

            var dbUiSpecial = GlobalDataBase.s_DbUISpecial;
            return dbUiSpecial != null &&
                   dbUiSpecial.isBarrageMode &&
                   HasSpecialDifficulty(musicInfo);
        }

        public static bool HasSpecialDifficulty(MusicInfo musicInfo)
        {
            if (musicInfo == null) return false;

            return !string.IsNullOrWhiteSpace(musicInfo.difficulty5) &&
                   musicInfo.difficulty5 != "0";
        }
    }
}
