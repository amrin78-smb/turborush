using UnityEngine;

namespace TurboRush.Platform
{
    /// <summary>Short, subtle vibrations behind the Settings toggle. No-op on desktop.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject s_Vibrator;
        private static bool s_Tried;
#endif

        public static void Pulse(int milliseconds)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!s_Tried)
                {
                    s_Tried = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        s_Vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (s_Vibrator != null) s_Vibrator.Call("vibrate", (long)Mathf.Clamp(milliseconds, 5, 200));
                else Handheld.Vibrate(); // also makes Unity add the VIBRATE permission
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TR Haptics] " + e.Message);
                Enabled = false;
            }
#endif
        }
    }
}
