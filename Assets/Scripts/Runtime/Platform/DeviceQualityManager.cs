using TurboRush.Save;
using UnityEngine;

namespace TurboRush.Platform
{
    /// <summary>Per-preset knobs applied on top of the Unity quality level (which picks the URP asset).</summary>
    public struct QualityProfile
    {
        public int level;               // 0 Low, 1 Medium, 2 High
        public float farClip;
        public float sceneryDensity;    // 0..1 multiplier on trees/rocks/clouds
        public bool distantMountains;
        public int targetFrameRate;

        public static QualityProfile For(int level)
        {
            switch (level)
            {
                case 0: return new QualityProfile { level = 0, farClip = 650f, sceneryDensity = 0.45f, distantMountains = true, targetFrameRate = 60 };
                case 1: return new QualityProfile { level = 1, farClip = 900f, sceneryDensity = 0.75f, distantMountains = true, targetFrameRate = 60 };
                default: return new QualityProfile { level = 2, farClip = 1300f, sceneryDensity = 1f, distantMountains = true, targetFrameRate = 60 };
            }
        }
    }

    /// <summary>
    /// Chooses Low/Medium/High. Auto picks Low on the Redmi A3 class of phone (PowerVR GE-series, Mali-4xx/T7xx,
    /// old Adreno, or ≤ 4 GB RAM). The player can override it in Settings.
    /// </summary>
    public static class DeviceQualityManager
    {
        public static QualityProfile Current { get; private set; } = QualityProfile.For(1);
        public static string Reason { get; private set; } = "";

        public static int AutoLevel(string gpu, int systemMemoryMb, bool mobile, out string reason)
        {
            gpu = gpu ?? "";
            if (!mobile) { reason = "desktop"; return 2; }
            string g = gpu.ToLowerInvariant();
            if (g.Contains("powervr") || g.Contains("mali-4") || g.Contains("mali-t7") || g.Contains("mali-t8") ||
                g.Contains("adreno (tm) 3") || g.Contains("adreno (tm) 50") || g.Contains("adreno (tm) 61"))
            {
                reason = "low-end GPU (" + gpu + ")";
                return 0;
            }
            if (systemMemoryMb > 0 && systemMemoryMb <= 4096) { reason = "≤4 GB RAM (" + systemMemoryMb + " MB)"; return 0; }
            if (systemMemoryMb > 0 && systemMemoryMb <= 6144) { reason = "mid-range (" + systemMemoryMb + " MB)"; return 1; }
            reason = "capable device";
            return 2;
        }

        public static int ResolveLevel(QualityChoice choice)
        {
            if (choice != QualityChoice.Auto) { Reason = "manual " + choice; return (int)choice - 1; }
            int l = AutoLevel(SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize, Application.isMobilePlatform, out string reason);
            Reason = "auto: " + reason;
            return l;
        }

        public static void Apply(QualityChoice choice)
        {
            int level = Mathf.Clamp(ResolveLevel(choice), 0, 2);
            if (level < QualitySettings.count && QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            Current = QualityProfile.For(level);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Current.targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Debug.Log($"[TR Quality] {QualitySettings.names[Mathf.Min(level, QualitySettings.names.Length - 1)]} ({Reason}); GPU={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}; RAM={SystemInfo.systemMemorySize}MB");
        }
    }
}
