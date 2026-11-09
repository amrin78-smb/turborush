using UnityEngine;

namespace TurboRush.UI
{
    /// <summary>
    /// Fits its RectTransform to <see cref="Screen.safeArea"/> (notches, rounded corners, gesture bars) plus a small
    /// extra margin, and refits whenever the screen size, orientation or safe area changes. For layout tests on
    /// desktop, <see cref="SimulatedInsets"/> (or the command-line flag -tr-safe-inset N) fakes a notch.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField, Tooltip("Extra margin inside the safe area, as a fraction of screen height.")]
        private float m_ExtraMargin = 0.02f;

        /// <summary>Left/right/top/bottom insets in pixels added to the device safe area (testing only).</summary>
        public static Vector4 SimulatedInsets;

        private RectTransform m_Rect;
        private Rect m_LastSafe;
        private Vector2Int m_LastSize;
        private Vector4 m_LastSim;

        public static Rect ComputeSafeRect(Rect deviceSafe, int width, int height, Vector4 simulated, float extraMarginFraction)
        {
            float m = height * extraMarginFraction;
            float xMin = Mathf.Max(deviceSafe.xMin, simulated.x) + m;
            float xMax = Mathf.Min(deviceSafe.xMax, width - simulated.y) - m;
            float yMax = Mathf.Min(deviceSafe.yMax, height - simulated.z) - m;
            float yMin = Mathf.Max(deviceSafe.yMin, simulated.w) + m;
            if (xMax <= xMin || yMax <= yMin) return new Rect(0, 0, width, height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void Awake()
        {
            m_Rect = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void LateUpdate()
        {
            if (Screen.safeArea != m_LastSafe || Screen.width != m_LastSize.x || Screen.height != m_LastSize.y || SimulatedInsets != m_LastSim) Apply();
        }

        public void Apply()
        {
            if (m_Rect == null) m_Rect = (RectTransform)transform;
            int w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return;
            m_LastSafe = Screen.safeArea;
            m_LastSize = new Vector2Int(w, h);
            m_LastSim = SimulatedInsets;
            Rect r = ComputeSafeRect(m_LastSafe, w, h, SimulatedInsets, m_ExtraMargin);
            m_Rect.anchorMin = new Vector2(r.xMin / w, r.yMin / h);
            m_Rect.anchorMax = new Vector2(r.xMax / w, r.yMax / h);
            m_Rect.offsetMin = Vector2.zero;
            m_Rect.offsetMax = Vector2.zero;
        }
    }
}
