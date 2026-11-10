using TurboRush.Core;
using UnityEngine;

namespace TurboRush.Race
{
    /// <summary>
    /// Power-up effects on one car: two exhaust flames while a turbo or boost pad is active and a rim-lit bubble
    /// while the shield is up. The bubble fades out over its last second (no blinking). No allocation per frame.
    /// </summary>
    public sealed class RacerEffects : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Transform m_FlameRoot;
        private Transform[] m_Flames;
        private Renderer m_Bubble;
        private MaterialPropertyBlock m_Block;
        private float m_Flame, m_Seed;
        private float m_LastAlpha = -1f;

        public bool FlamesVisible => m_FlameRoot != null && m_FlameRoot.gameObject.activeSelf;
        public bool BubbleVisible => m_Bubble != null && m_Bubble.enabled;

        /// <summary>Builds the effects around the car's current renderers (call while the car is at the origin).</summary>
        public void Init(RaceMaterials mats, Renderer[] carRenderers, int seed)
        {
            m_Seed = seed * 1.37f;
            Bounds b = LocalBounds(carRenderers);
            float rear = b.min.z + 0.05f, y = b.min.y + Mathf.Max(0.35f, b.size.y * 0.32f), x = Mathf.Min(0.5f, b.extents.x * 0.45f);

            m_FlameRoot = new GameObject("TurboFlames").transform;
            m_FlameRoot.SetParent(transform, false);
            Mesh outer = GlowMeshes.Flame("Flame_Outer", 0.24f, 1.3f, new Color(1f, 0.45f, 0.08f), new Color(0.5f, 0.05f, 0f));
            Mesh inner = GlowMeshes.Flame("Flame_Inner", 0.13f, 0.8f, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.1f));
            m_Flames = new Transform[4];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -x : x;
                var pos = new Vector3(side, y, rear);
                m_Flames[i * 2] = Part(m_FlameRoot, "Outer" + i, outer, mats.glow, pos);
                m_Flames[i * 2 + 1] = Part(m_FlameRoot, "Inner" + i, inner, mats.glow, pos);
            }
            m_FlameRoot.gameObject.SetActive(false);

            Vector3 radii = new Vector3(b.extents.x * 1.35f + 0.25f, b.extents.y * 1.25f + 0.25f, b.extents.z * 1.15f + 0.25f);
            Mesh bubble = GlowMeshes.Sphere("ShieldBubble", b.center, radii, new Color(0.3f, 0.75f, 1f));
            m_Bubble = Part(transform, "ShieldBubble", bubble, mats.bubble, Vector3.zero).GetComponent<Renderer>();
            m_Bubble.enabled = false;
            m_Block = new MaterialPropertyBlock();
        }

        private static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        private Bounds LocalBounds(Renderer[] rs)
        {
            var b = new Bounds(Vector3.up * 0.6f, new Vector3(1.9f, 1.3f, 4.2f));
            bool any = false;
            if (rs != null)
                foreach (Renderer r in rs)
                {
                    if (r == null) continue;
                    Bounds wb = r.bounds;
                    Vector3 lo = transform.InverseTransformPoint(wb.min), hi = transform.InverseTransformPoint(wb.max);
                    var lb = new Bounds((lo + hi) * 0.5f, new Vector3(Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y), Mathf.Abs(hi.z - lo.z)));
                    if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
                }
            return b;
        }

        public void Apply(RacerState r, float dt)
        {
            if (m_Flames == null) return;
            // Flames: grow in fast, shrink out, with a gentle size flutter (not a brightness flash).
            float target = r.TurboTimer > 0f ? 1f : r.PadTimer > 0f ? 0.75f : 0f;
            m_Flame = Mathf.MoveTowards(m_Flame, target, dt * (target > m_Flame ? 8f : 3f));
            bool flames = m_Flame > 0.01f;
            if (m_FlameRoot.gameObject.activeSelf != flames) m_FlameRoot.gameObject.SetActive(flames);
            if (flames)
            {
                float t = Time.time * 23f + m_Seed;
                for (int i = 0; i < m_Flames.Length; i++)
                {
                    float flutter = 0.85f + 0.15f * Mathf.Sin(t + i * 1.9f) + 0.08f * Mathf.Sin(t * 2.3f + i);
                    m_Flames[i].localScale = new Vector3(m_Flame, m_Flame, m_Flame * flutter);
                }
            }

            bool shield = r.ShieldTimer > 0f;
            if (m_Bubble.enabled != shield) m_Bubble.enabled = shield;
            if (shield)
            {
                float alpha = Mathf.Clamp01(r.ShieldTimer) * 0.85f;
                if (!Mathf.Approximately(alpha, m_LastAlpha))
                {
                    m_LastAlpha = alpha;
                    m_Block.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
                    m_Bubble.SetPropertyBlock(m_Block);
                }
            }
        }
    }
}
