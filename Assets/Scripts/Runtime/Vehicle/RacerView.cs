using TurboRush.Race;
using UnityEngine;

namespace TurboRush.Vehicle
{
    /// <summary>
    /// Visual side of a racer: places the car from the simulation pose, leans the body into steering, pitches
    /// slightly under acceleration and in the air, spins the wheels with the speed, steers the front wheels and
    /// flickers while respawn-ghosted. No physics, no allocation.
    /// </summary>
    public sealed class RacerView : MonoBehaviour
    {
        [SerializeField] private Transform m_Body;
        [SerializeField] private Transform m_Shadow;
        [SerializeField] private Renderer[] m_Renderers;
        [Tooltip("Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR; origins at the wheel centres, spin axis local X.")]
        [SerializeField] private Transform[] m_Wheels;
        [SerializeField] private float m_WheelRadius = 0.38f;
        [SerializeField] private float m_SteerDegrees = 22f;
        [SerializeField] private float m_LeanDegrees = 4.5f;
        [SerializeField] private float m_PitchDegrees = 1.5f;
        [SerializeField] private float m_AirPitchDegrees = 12f;

        private float m_Lean, m_Pitch, m_LastSpeed, m_Spin;
        private bool m_Visible = true;

        public Transform[] Wheels => m_Wheels;
        public float WheelRadius => m_WheelRadius;

        public void Bind(Transform body, Transform shadow, Renderer[] renderers, Transform[] wheels, float wheelRadius)
        {
            m_Body = body;
            m_Shadow = shadow;
            m_Renderers = renderers;
            m_Wheels = wheels;
            m_WheelRadius = Mathf.Max(0.1f, wheelRadius);
        }

        /// <summary>Rolls the wheels by the distance travelled and turns the front wheels with the steering.</summary>
        public void SpinWheels(float speed, float steer, float dt)
        {
            if (m_Wheels == null || m_Wheels.Length < 4) return;
            m_Spin = Mathf.Repeat(m_Spin + speed * dt / m_WheelRadius * Mathf.Rad2Deg, 360f);
            Quaternion spin = Quaternion.Euler(m_Spin, 0f, 0f);
            Quaternion front = Quaternion.Euler(0f, steer * m_SteerDegrees, 0f) * spin;
            m_Wheels[0].localRotation = front;
            m_Wheels[1].localRotation = front;
            m_Wheels[2].localRotation = spin;
            m_Wheels[3].localRotation = spin;
        }

        public void Apply(RaceSimulation sim, RacerState r, float dt, bool snap)
        {
            sim.GetPose(r, out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);

            float accel = dt > 0f ? (r.Speed - m_LastSpeed) / dt : 0f;
            m_LastSpeed = r.Speed;
            float k = snap ? 1f : 1f - Mathf.Exp(-10f * dt);
            m_Lean = Mathf.Lerp(m_Lean, -r.Steer * m_LeanDegrees, k);
            // Nose follows the flight path on ramps and jumps (negative pitch = nose up).
            float flight = -Mathf.Atan2(r.VerticalSpeed, Mathf.Max(r.Speed, 4f)) * Mathf.Rad2Deg;
            float pitchTarget = Mathf.Clamp(-accel * 0.15f, -m_PitchDegrees, m_PitchDegrees) + Mathf.Clamp(flight, -m_AirPitchDegrees, m_AirPitchDegrees);
            m_Pitch = Mathf.Lerp(m_Pitch, pitchTarget, k);
            if (m_Body != null) m_Body.localRotation = Quaternion.Euler(m_Pitch, 0f, m_Lean);
            if (m_Shadow != null)
            {
                // The shadow stays on the road while the car is in the air, and shrinks a little with height.
                float h = Mathf.Max(0f, r.Height);
                m_Shadow.localPosition = new Vector3(0f, 0.04f - h, 0f);
                m_Shadow.localScale = Vector3.one * (1f / (1f + h * 0.25f));
            }

            SpinWheels(r.Speed, r.Steer, dt);

            bool show = r.GhostTimer <= 0f || Mathf.Repeat(r.GhostTimer * 8f, 1f) > 0.35f;
            if (show != m_Visible && m_Renderers != null)
            {
                m_Visible = show;
                for (int i = 0; i < m_Renderers.Length; i++) m_Renderers[i].enabled = show;
            }
        }
    }
}
