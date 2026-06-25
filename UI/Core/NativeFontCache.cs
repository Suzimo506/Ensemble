using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class NativeFontCache
    {
        private static Font _font;
        private static Material _material;

        public static void ApplyTo(Text text)
        {
            if (text == null) return;

            if (TryResolveNativeFont())
            {
                text.font = _font;
                if (_material != null)
                {
                    text.material = _material;
                }

                return;
            }

            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static bool TryResolveNativeFont()
        {
            if (_font != null) return true;

            using (PerfTrace.Measure("MDEN.NativeFontCache.Find"))
            {
                var candidates = Resources.FindObjectsOfTypeAll<Text>();
                foreach (var text in candidates)
                {
                    if (!IsCandidate(text)) continue;

                    var fontName = text.font.name ?? string.Empty;
                    if (fontName.Contains("Arial")) continue;

                    _font = text.font;
                    _material = text.material;
                    return true;
                }
            }

            return false;
        }

        // Skip Text components that belong to the mod's own UI hierarchy.
        // These may not have their font configured yet (NativeFontCache.ApplyTo
        // hasn't run on them), so caching their font would capture a stale or
        // fallback font instead of the game's native CJK font.
        private static bool IsCandidate(Text text)
        {
            if (text == null || text.font == null || text.transform == null) return false;

            for (var current = text.transform; current != null; current = current.parent)
            {
                var name = current.name ?? string.Empty;
                if (name == "MDENListWindowRoot" ||
                    name == "MDENInputDialogRoot" ||
                    name == "MDENConfirmDialogRoot" ||
                    name == "BtnMDENMultiplayer")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
