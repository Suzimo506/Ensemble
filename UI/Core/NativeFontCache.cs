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
                    if (text == null || text.font == null) continue;
                    var fontName = text.font.name ?? string.Empty;
                    if (fontName.Contains("Arial")) continue;

                    _font = text.font;
                    _material = text.material;
                    return true;
                }
            }

            return false;
        }
    }
}
