using TurboRush.Race;
using UnityEngine;

namespace TurboRush.Vehicle
{
    /// <summary>
    /// Visual side of a racer: places the car from the simulation pose, leans the body into steering, pitches
    /// slightly under acceleration, lights up brake lights when slowing and flickers while respawn-ghosted.
    /// No physics, no allocation.
    /// </summary>
    public sealed class RacerView : MonoBehaviour
    {
        [SerializeField] private Transform m_Body;
        [SerializeField] private Transform m_Shadow;
        [SerializeField] private Renderer m_BodyRenderer;
        [SerializeField] private float m_LeanDegrees = 4.5f;
        [SerializeField] private float m_PitchDegrees = 1.5f;

        private float m_Lean, m_Pitch, m_LastSpeed;
        private bool m_Visible = true;

        public void Bind(Transform body, Transform shadow, Renderer bodyRenderer)
        {
            m_Body = body;
            m_Shadow = shadow;
            m_BodyRenderer = bodyRenderer;
        }

        public void Apply(RaceSimulation sim, RacerState r, float dt, bool snap)
        {
            sim.GetPose(r, out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);

            float accel = dt > 0f ? (r.Speed - m_LastSpeed) / dt : 0f;
            m_LastSpeed = r.Speed;
            float k = snap ? 1f : 1f - Mathf.Exp(-10f * dt);
            m_Lean = Mathf.Lerp(m_Lean, -r.Steer * m_LeanDegrees, k);
            m_Pitch = Mathf.Lerp(m_Pitch, Mathf.Clamp(-accel * 0.15f, -m_PitchDegrees, m_PitchDegrees), k);
            if (m_Body != null) m_Body.localRotation = Quaternion.Euler(m_Pitch, 0f, m_Lean);

            bool show = r.GhostTimer <= 0f || Mathf.Repeat(r.GhostTimer * 8f, 1f) > 0.35f;
            if (show != m_Visible && m_BodyRenderer != null)
            {
                m_Visible = show;
                m_BodyRenderer.enabled = show;
            }
        }
    }
}
