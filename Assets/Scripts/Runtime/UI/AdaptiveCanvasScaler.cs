using UnityEngine;
using UnityEngine.UI;

namespace TurboRush.UI
{
    /// <summary>
    /// Tablet/phone scaling rule: when the screen is squarer than 16:9 (tablets, 4:3, 16:10) match WIDTH (0) so nothing
    /// is clipped horizontally; otherwise (16:9, 20:9 phones) match HEIGHT (1). Recomputed on resolution/orientation change.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        private const float WideAspect = 16f / 9f;

        private CanvasScaler m_Scaler;
        private int m_LastW, m_LastH;

        public static float MatchFor(int width, int height)
        {
            if (width <= 0 || height <= 0) return 1f;
            float aspect = (float)Mathf.Max(width, height) / Mathf.Min(width, height);
            return aspect < WideAspect - 0.001f ? 0f : 1f;
        }

        private void Awake()
        {
            m_Scaler = GetComponent<CanvasScaler>();
            m_Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            m_Scaler.referenceResolution = ReferenceResolution;
            m_Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            Apply();
        }

        private void LateUpdate()
        {
            if (Screen.width != m_LastW || Screen.height != m_LastH) Apply();
        }

        public void Apply()
        {
            m_LastW = Screen.width;
            m_LastH = Screen.height;
            m_Scaler.matchWidthOrHeight = MatchFor(m_LastW, m_LastH);
        }
    }
}
