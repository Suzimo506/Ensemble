using Il2CppAssets.Scripts.Database;
using MDEN.Protocol.Rules;

namespace MDEN.Managers
{
    internal static class SpecialDifficultyController
    {
        private const int BarrageNativeDifficulty = DifficultyDisplayRules.Hard;

        public static int ResolveCurrentDifficulty(MusicInfo musicInfo, int nativeDifficulty)
        {
            return IsSpecialDifficultySelected(musicInfo, nativeDifficulty)
                ? DifficultyDisplayRules.Spell
                : nativeDifficulty;
        }

        public static int Sync(MusicInfo musicInfo, int difficulty)
        {
            var isSpecial = difficulty == DifficultyDisplayRules.Spell &&
                            HasSpecialDifficulty(musicInfo);
            var dbUiSpecial = GlobalDataBase.s_DbUISpecial;
            if (dbUiSpecial != null)
            {
                dbUiSpecial.isBarrageMode = isSpecial;
            }

            return isSpecial
                ? BarrageNativeDifficulty
                : difficulty;
        }

        public static bool IsSpecialDifficultySelected(MusicInfo musicInfo, int nativeDifficulty)
        {
            if (musicInfo == null || nativeDifficulty != BarrageNativeDifficulty) return false;

            return IsSpecialModeActive(musicInfo);
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
