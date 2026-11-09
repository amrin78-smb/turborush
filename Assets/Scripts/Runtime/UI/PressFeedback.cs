using UnityEngine;
using UnityEngine.EventSystems;

namespace TurboRush.UI
{
    /// <summary>Scales a button down while pressed and plays the click sound. Large targets, instant feedback.</summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 m_Base = Vector3.one;
        private float m_Target = 1f;

        private void Awake() => m_Base = transform.localScale;
        public void OnPointerDown(PointerEventData e) => m_Target = 0.92f;
        public void OnPointerUp(PointerEventData e) => m_Target = 1f;
        public void OnPointerExit(PointerEventData e) => m_Target = 1f;

        private void Update()
        {
            float s = transform.localScale.x / Mathf.Max(0.0001f, m_Base.x);
            if (Mathf.Abs(s - m_Target) < 0.001f) return;
            s = Mathf.MoveTowards(s, m_Target, Time.unscaledDeltaTime * 3f);
            transform.localScale = m_Base * s;
        }
    }
}
