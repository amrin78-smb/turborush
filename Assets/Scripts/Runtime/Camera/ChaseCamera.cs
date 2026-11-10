using TurboRush.Core;
using TurboRush.Race;
using TurboRush.Track;
using UnityEngine;

namespace TurboRush.CameraRig
{
    /// <summary>
    /// Third-person chase camera. The follow point is taken from the TRACK a few metres behind the car (not from the
    /// car's yaw), so it stays over the road on curves, hills and banking and never swings into barriers. Looks ahead
    /// along the track. Smooth damping, small speed-based FOV, almost no roll, no shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        private ChaseCameraSettings m_S;
        private RaceSimulation m_Sim;
        private Camera m_Cam;
        private Vector3 m_Vel;
        private bool m_Snap = true;

        public Camera Cam => m_Cam;

        public void Init(RaceSimulation sim, ChaseCameraSettings settings, float farClip)
        {
            m_Sim = sim;
            m_S = settings ?? new ChaseCameraSettings();
            m_Cam = GetComponent<Camera>();
            m_Cam.fieldOfView = m_S.fov;
            m_Cam.nearClipPlane = 0.3f;
            m_Cam.farClipPlane = farClip;
            Snap();
        }

        public void Snap() => m_Snap = true;

        private void LateUpdate()
        {
            if (m_Sim == null) return;
            RacerState p = m_Sim.Player;
            TrackPath path = m_Sim.Track.Path;

            path.Sample(p.Distance - m_S.distance, out TrackSample back);
            path.Sample(p.Distance + m_S.lookAhead, out TrackSample ahead);
            path.Sample(p.Distance, out TrackSample here);

            float lat = p.Lateral * m_S.lateralFollow;
            Vector3 up = Vector3.Slerp(Vector3.up, back.up, m_S.bankFollow);
            Vector3 desired = back.position + back.right * lat + up * m_S.height;
            // Keep the camera above the road surface under it (hills/crests).
            float minY = back.position.y + 1.2f;
            if (desired.y < minY) desired.y = minY;

            // Follow jumps gently (no shake): the camera rises a little and keeps the car framed.
            desired += Vector3.up * (p.Height * 0.35f);
            Vector3 carPos = here.position + here.right * p.Lateral + Vector3.up * (p.Height * 0.6f);
            Vector3 lookTarget = Vector3.Lerp(carPos, ahead.position + ahead.right * (p.Lateral * 0.5f), 0.55f) + Vector3.Slerp(Vector3.up, here.up, m_S.bankFollow) * m_S.lookHeight;
            Quaternion desiredRot = Quaternion.LookRotation((lookTarget - desired).normalized, up);

            float dt = Time.deltaTime;
            if (m_Snap || dt <= 0f)
            {
                transform.SetPositionAndRotation(desired, desiredRot);
                m_Vel = Vector3.zero;
                m_Snap = false;
            }
            else
            {
                Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref m_Vel, m_S.positionDamping, Mathf.Infinity, dt);
                Quaternion rot = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-m_S.rotationSharpness * dt));
                transform.SetPositionAndRotation(pos, rot);
            }

            float speed01 = Mathf.Clamp01(p.Speed / Mathf.Max(1f, p.Tuning.MaxSpeed));
            float fov = m_S.fov + m_S.speedFov * speed01 * speed01 + (p.Boosting ? m_S.boostFov : 0f);
            m_Cam.fieldOfView = Mathf.MoveTowards(m_Cam.fieldOfView, fov, 8f * dt);
        }
    }
}
