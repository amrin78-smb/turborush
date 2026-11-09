using UnityEngine;

namespace TurboRush.UI
{
    /// <summary>Small "pop" scale animation for text/badges (unscaled time so it works while paused).</summary>
    public sealed class Pop : MonoBehaviour
    {
        private float m_T = 1f;
        private float m_Life;
        private CanvasGroup m_Group;

        public void Play(float life = 0f)
        {
            m_T = 0f;
            m_Life = life;
            gameObject.SetActive(true);
            if (m_Group == null) m_Group = GetComponent<CanvasGroup>();
            if (m_Group != null) m_Group.alpha = 1f;
            transform.localScale = Vector3.one * 0.4f;
        }

        private void Update()
        {
            if (m_T >= 1f && m_Life <= 0f) return;
            m_T += Time.unscaledDeltaTime * 4f;
            float k = Mathf.Clamp01(m_T);
            float s = k < 0.6f ? Mathf.Lerp(0.4f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f);
            transform.localScale = Vector3.one * s;
            if (m_Life > 0f)
            {
                m_Life -= Time.unscaledDeltaTime;
                if (m_Group != null && m_Life < 0.3f) m_Group.alpha = Mathf.Clamp01(m_Life / 0.3f);
                if (m_Life <= 0f) gameObject.SetActive(false);
            }
        }
    }
}
