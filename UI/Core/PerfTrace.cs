using System;

namespace MDEN.UI.Core
{
    internal static class PerfTrace
    {
        public static bool Enabled => false;

        public static void SetGameMain(bool enabled) { }

        public static void BeginFrame() { }

        public static void UpdateReport() { }

        public static Scope Measure(string name) => default;

        public readonly struct Scope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
