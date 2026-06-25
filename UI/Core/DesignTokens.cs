using UnityEngine;

namespace MDEN.UI.Core
{
    /// <summary>
    /// Single source of truth for all visual design tokens (colors, typography, spacing, layout).
    /// Values are derived from DESIGN.md and must be used by all native UI construction code.
    /// </summary>
    internal static class DesignTokens
    {
        // Colors (from DESIGN.md, converted to Unity Color 0-1 range)
        public static readonly Color Primary = new(0.961f, 0.259f, 0.678f);         // #f542ad
        public static readonly Color OnPrimary = Color.white;                         // #ffffff
        public static readonly Color PrimaryContainer = new(0.239f, 0.102f, 0.180f); // #3d1a2e
        public static readonly Color Secondary = new(0.271f, 0.392f, 1.0f);          // #4564ff
        public static readonly Color SecondaryContainer = new(0.102f, 0.114f, 0.239f); // #1a1d3d
        public static readonly Color Tertiary = new(0f, 1f, 1f);                      // #00ffff
        public static readonly Color Surface = new(0.102f, 0.102f, 0.180f);          // #1a1a2e
        public static readonly Color SurfaceBright = new(0.176f, 0.176f, 0.306f);    // #2d2d4e
        public static readonly Color SurfaceContainer = new(0.145f, 0.145f, 0.251f); // #252540
        public static readonly Color SurfaceContainerHigh = new(0.188f, 0.188f, 0.314f); // #303050
        public static readonly Color SurfaceContainerHighest = new(0.227f, 0.227f, 0.369f); // #3a3a5e
        public static readonly Color OnSurface = Color.white;                         // #ffffff
        public static readonly Color OnSurfaceVariant = new(0.690f, 0.690f, 0.816f); // #b0b0d0
        public static readonly Color Outline = new(0.314f, 0.314f, 0.439f);          // #505070
        public static readonly Color Success = new(0f, 1f, 0f);                       // #00ff00
        public static readonly Color Warning = new(1f, 0.969f, 0f);                   // #fff700
        public static readonly Color Error = new(1f, 0.333f, 0.333f);                // #ff5555
        public static readonly Color Accent = new(0.773f, 0.549f, 1f);               // #c58cff
        public static readonly Color Shade = new(0f, 0f, 0f, 0.86f);                 // rgba(0,0,0,0.86)

        // Typography (font sizes in Unity units)
        public const int HeadlineLg = 32;
        public const int HeadlineMd = 24;
        public const int BodyLg = 20;
        public const int BodyMd = 18;
        public const int BodySm = 14;
        public const int LabelMd = 16;

        // Spacing (pixels)
        public const float SpacingXs = 4f;
        public const float SpacingSm = 8f;
        public const float SpacingMd = 16f;
        public const float SpacingLg = 24f;
        public const float SpacingXl = 32f;

        // Rounded corners (pixels)
        public const float RoundedSm = 4f;
        public const float RoundedMd = 8f;
        public const float RoundedLg = 12f;

        // Layout
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float PanelWidth = 1100f;   // ~57% of 1920
        public const float PanelHeight = 680f;    // ~63% of 1080
        public const float ListItemHeight = 56f;
        public const float ThumbnailSize = 48f;
        public const int WindowSortingOrder = 32762;
        public const int DialogSortingOrder = 32763;
    }
}
