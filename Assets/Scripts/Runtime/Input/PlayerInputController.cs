using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TurboRush.Input
{
    /// <summary>
    /// Hold-to-steer: left half of the screen = −1, right half = +1, nothing = 0 (the motor smooths it back to
    /// centre). Multi-touch safe: the most recently started valid touch wins. Touches that START on a registered
    /// UI blocker (pause button) are ignored for their whole lifetime. Editor/desktop: mouse button or arrow keys/A-D.
    /// </summary>
    public sealed class PlayerInputController : MonoBehaviour
    {
        private readonly List<RectTransform> m_Blockers = new List<RectTransform>(4);
        private readonly HashSet<int> m_IgnoredTouches = new HashSet<int>();
        private readonly HashSet<int> m_ActiveIds = new HashSet<int>();
        private readonly List<int> m_Stale = new List<int>(8);
        private bool m_MouseIgnored;

        /// <summary>When false the controller reports 0 (only the Racing state enables steering).</summary>
        public bool Enabled { get; set; }
        /// <summary>Raw steering intent this frame: −1, 0 or +1.</summary>
        public float Steer { get; private set; }
        /// <summary>Optional override (automated tests / autopilot). NaN = use real input.</summary>
        public float Override = float.NaN;
        public bool AnyInputThisFrame { get; private set; }

        public void RegisterBlocker(RectTransform rect)
        {
            if (rect != null && !m_Blockers.Contains(rect)) m_Blockers.Add(rect);
        }

        public static float SteerFromScreenX(float x, float screenWidth) => x < screenWidth * 0.5f ? -1f : 1f;

        private bool OverBlocker(Vector2 screenPoint)
        {
            for (int i = 0; i < m_Blockers.Count; i++)
            {
                RectTransform r = m_Blockers[i];
                if (r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screenPoint, null)) return true;
            }
            return false;
        }

        private void Update()
        {
            float steer = 0f;
            AnyInputThisFrame = false;
            double newest = double.MinValue;

            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                m_ActiveIds.Clear();
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    TouchControl t = touches[i];
                    if (!t.press.isPressed) continue;
                    int id = t.touchId.ReadValue();
                    m_ActiveIds.Add(id);
                    if (m_IgnoredTouches.Contains(id)) continue;
                    if (OverBlocker(t.startPosition.ReadValue())) { m_IgnoredTouches.Add(id); continue; }
                    AnyInputThisFrame = true;
                    double start = t.startTime.ReadValue();
                    if (start >= newest)
                    {
                        newest = start;
                        steer = SteerFromScreenX(t.position.ReadValue().x, Screen.width);
                    }
                }
                // Forget ignored touches that have ended (no allocation).
                m_Stale.Clear();
                foreach (int id in m_IgnoredTouches) if (!m_ActiveIds.Contains(id)) m_Stale.Add(id);
                for (int i = 0; i < m_Stale.Count; i++) m_IgnoredTouches.Remove(m_Stale[i]);
            }

            Mouse mouse = Mouse.current;
            if (!AnyInputThisFrame && mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) m_MouseIgnored = OverBlocker(mouse.position.ReadValue());
                if (mouse.leftButton.isPressed && !m_MouseIgnored)
                {
                    AnyInputThisFrame = true;
                    steer = SteerFromScreenX(mouse.position.ReadValue().x, Screen.width);
                }
            }

            Keyboard kb = Keyboard.current;
            if (!AnyInputThisFrame && kb != null)
            {
                bool l = kb.leftArrowKey.isPressed || kb.aKey.isPressed;
                bool r = kb.rightArrowKey.isPressed || kb.dKey.isPressed;
                if (l != r) { steer = l ? -1f : 1f; AnyInputThisFrame = true; }
            }

            if (!float.IsNaN(Override)) steer = Override;
            Steer = Enabled ? steer : 0f;
        }
    }
}
