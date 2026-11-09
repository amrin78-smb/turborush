using System;
using TurboRush.UI;
using UnityEngine;

namespace TurboRush.Core
{
    /// <summary>
    /// Command-line switches for automated test players (Linux/xvfb). Ignored on Android.
    ///  -tr-autopilot           drive menus and a full race automatically, capture screenshots, then quit
    ///  -tr-shots &lt;dir&gt;         screenshot directory
    ///  -tr-safe-inset &lt;px&gt;     simulate a notch/rounded corners (left/right/top insets)
    ///  -tr-track &lt;id&gt;          track for the autopilot race
    ///  -tr-fast                shorten the autopilot race (teleport near the finish)
    /// </summary>
    public static class DebugArgs
    {
        public static bool Autopilot { get; private set; }
        public static bool Fast { get; private set; }
        public static string ShotDir { get; private set; }
        public static string Track { get; private set; }
        public static string Tag { get; private set; } = "";
        /// <summary>-tr-quality Low|Medium|High overrides the saved quality choice (test players only).</summary>
        public static Save.QualityChoice? Quality { get; private set; }

        public static void Apply()
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                string next = i + 1 < a.Length ? a[i + 1] : "";
                switch (a[i])
                {
                    case "-tr-autopilot": Autopilot = true; break;
                    case "-tr-fast": Fast = true; break;
                    case "-tr-shots": ShotDir = next; break;
                    case "-tr-track": Track = next; break;
                    case "-tr-tag": Tag = next; break;
                    case "-tr-quality":
                        if (System.Enum.TryParse(next, true, out Save.QualityChoice q)) Quality = q;
                        break;
                    case "-tr-safe-inset":
                        if (float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float px))
                            SafeAreaFitter.SimulatedInsets = new Vector4(px, px, px * 0.5f, 0f);
                        break;
                }
            }
            if (Autopilot && Services.Instance != null && Services.Instance.GetComponent<AutoPilot>() == null)
                Services.Instance.gameObject.AddComponent<AutoPilot>();
#endif
        }
    }
}
