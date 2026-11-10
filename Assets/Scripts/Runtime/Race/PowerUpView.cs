using TurboRush.Core;
using TurboRush.Track;
using UnityEngine;

namespace TurboRush.Race
{
    /// <summary>
    /// Shows the floating power-ups of <see cref="RaceSimulation.PowerUps"/>: a spinning lightning bolt (turbo) or
    /// shield crest inside a softly glowing bubble. Pickups are personal, so this shows the player's own: one the
    /// player collected shrinks away and pops back when it respawns; AI pickups do not hide it.
    /// Meshes are built once in code; per frame it only moves a handful of transforms (no allocation).
    /// </summary>
    public sealed class PowerUpView : MonoBehaviour
    {
        private const float FloatHeight = 1.15f;

        private RaceSimulation m_Sim;
        private Transform[] m_Roots, m_Icons;
        private float[] m_Scale;
        private float m_Time;

        public int SlotCount => m_Roots != null ? m_Roots.Length : 0;
        public bool IsShown(int slot) => m_Roots[slot].gameObject.activeSelf;

        public void Init(RaceSimulation sim, RaceMaterials mats)
        {
            m_Sim = sim;
            PowerUpField f = sim.PowerUps;
            Mesh bolt = BoltMesh(), crest = CrestMesh();
            Mesh haloTurbo = Halo(new Color(1f, 0.55f, 0.12f)), haloShield = Halo(new Color(0.25f, 0.75f, 1f));
            m_Roots = new Transform[f.SlotCount];
            m_Icons = new Transform[f.SlotCount];
            m_Scale = new float[f.SlotCount];
            for (int i = 0; i < f.SlotCount; i++)
            {
                bool turbo = f.Kind[i] == PowerUpKind.Turbo;
                sim.Track.Path.Sample(f.SlotDistance[i], out TrackSample s);
                var root = new GameObject((turbo ? "Turbo_" : "Shield_") + i).transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(s.position + s.right * f.SlotLateral[i] + s.up * FloatHeight, Quaternion.LookRotation(s.forward, s.up));
                Transform icon = TrackBuilder.AddRenderer(root, "Icon", turbo ? bolt : crest, mats.vertexLit).transform;
                TrackBuilder.AddRenderer(root, "Halo", turbo ? haloTurbo : haloShield, mats.bubble);
                m_Roots[i] = root;
                m_Icons[i] = icon;
                m_Scale[i] = 1f;
            }
        }

        private void LateUpdate()
        {
            if (m_Sim == null) return;
            float dt = Time.deltaTime;
            m_Time += dt;
            PowerUpField f = m_Sim.PowerUps;
            for (int i = 0; i < m_Roots.Length; i++)
            {
                bool on = f.Available(m_Sim.PlayerIndex, i); // pickups are personal: show the player's own
                // Shrink away quickly when collected; grow back with a little overshoot when it returns.
                m_Scale[i] = on ? Mathf.MoveTowards(m_Scale[i], 1f, dt * 3f) : Mathf.MoveTowards(m_Scale[i], 0f, dt * 7f);
                float s = m_Scale[i];
                bool visible = s > 0.01f;
                if (m_Roots[i].gameObject.activeSelf != visible) m_Roots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float pop = on ? 1f + 0.25f * Mathf.Sin(s * Mathf.PI) : s;
                m_Roots[i].localScale = Vector3.one * (on ? s * pop : s);
                m_Icons[i].localRotation = Quaternion.Euler(0f, m_Time * 140f + i * 40f, 0f);
                m_Icons[i].localPosition = new Vector3(0f, 0.12f * Mathf.Sin(m_Time * 2.4f + i), 0f);
            }
        }

        // ---------- meshes ----------

        /// <summary>Extrudes a convex-decomposed outline (counter-clockwise, x right, y up) into a solid badge.</summary>
        private static void Extrude(MeshKit k, Vector2[] outline, int[][] pieces, float size, float depth, Color face, Color side)
        {
            Vector3 P(Vector2 v, float z) => new Vector3((v.x - 0.5f) * size, (v.y - 0.5f) * size, z);
            float t = depth * 0.5f;
            foreach (int[] piece in pieces)
                for (int j = 1; j + 1 < piece.Length; j++)
                {
                    Vector2 a = outline[piece[0]], b = outline[piece[j]], c = outline[piece[j + 1]];
                    k.Tri(P(a, -t), P(c, -t), P(b, -t), face); // front (faces −Z)
                    k.Tri(P(a, t), P(b, t), P(c, t), face);    // back (faces +Z)
                }
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                k.Quad(P(a, -t), P(b, -t), P(b, t), P(a, t), side);
            }
        }

        private static Mesh BoltMesh()
        {
            var k = new MeshKit { Emission = 0.55f };
            Vector2[] o =
            {
                new Vector2(0.58f, 0.95f), new Vector2(0.24f, 0.47f), new Vector2(0.47f, 0.47f), new Vector2(0.38f, 0.05f),
                new Vector2(0.78f, 0.57f), new Vector2(0.55f, 0.57f), new Vector2(0.72f, 0.95f)
            };
            Extrude(k, o, new[] { new[] { 0, 1, 2, 5, 6 }, new[] { 2, 3, 4, 5 } }, 1.5f, 0.32f, new Color(1f, 0.86f, 0.18f), new Color(1f, 0.5f, 0.1f));
            return k.ToMesh("PowerUp_Turbo");
        }

        private static Mesh CrestMesh()
        {
            var k = new MeshKit { Emission = 0.55f };
            Vector2[] outer =
            {
                new Vector2(0.5f, 0.93f), new Vector2(0.17f, 0.81f), new Vector2(0.21f, 0.44f),
                new Vector2(0.5f, 0.07f), new Vector2(0.79f, 0.44f), new Vector2(0.83f, 0.81f)
            };
            int[][] fan = { new[] { 0, 1, 2, 3, 4, 5 } };
            Extrude(k, outer, fan, 1.45f, 0.3f, new Color(0.3f, 0.8f, 1f), new Color(0.1f, 0.35f, 0.85f));
            // A white inner crest on both faces so it reads as a shield badge.
            var inner = new Vector2[outer.Length];
            for (int i = 0; i < outer.Length; i++) inner[i] = new Vector2(0.5f, 0.5f) + (outer[i] - new Vector2(0.5f, 0.5f)) * 0.55f;
            Extrude(k, inner, fan, 1.45f, 0.36f, Color.white, Color.white);
            return k.ToMesh("PowerUp_Shield");
        }

        private static Mesh Halo(Color c) => GlowMeshes.Sphere("PowerUp_Halo", Vector3.zero, Vector3.one * 1.2f, c);
    }
}
