using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurboRush.Track
{
    /// <summary>
    /// Tiny flat-shaded mesh builder (vertex colours, optional UVs and an emission channel in UV2.x).
    /// Used at load time for track/scenery and in the Editor for cars. Not for per-frame use.
    /// </summary>
    public sealed class MeshKit
    {
        public readonly List<Vector3> V = new List<Vector3>(4096);
        public readonly List<Color32> C = new List<Color32>(4096);
        public readonly List<Vector2> UV = new List<Vector2>(4096);
        public readonly List<Vector2> UV2 = new List<Vector2>(4096);
        public readonly List<int> T = new List<int>(8192);
        public float Emission;
        public bool PaintMask; // when true vertex alpha = 1 (painted with the material's _Paint colour)

        public int VertexCount => V.Count;

        public void Clear()
        {
            V.Clear(); C.Clear(); UV.Clear(); UV2.Clear(); T.Clear();
            Emission = 0f;
            PaintMask = false;
        }

        private void Add(Vector3 p, Color col, Vector2 uv)
        {
            V.Add(p);
            Color32 c = col;
            c.a = PaintMask ? (byte)255 : (byte)0;
            C.Add(c);
            UV.Add(uv);
            UV2.Add(new Vector2(Emission, 0f));
        }

        /// <summary>Quad a-b-c-d, clockwise when seen from the visible side.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            int i = V.Count;
            Add(a, col, Vector2.zero); Add(b, col, Vector2.up); Add(c, col, Vector2.one); Add(d, col, Vector2.right);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
            T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        public void QuadUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count;
            Add(a, col, ua); Add(b, col, ub); Add(c, col, uc); Add(d, col, ud);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
            T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            int i = V.Count;
            Add(a, col, Vector2.zero); Add(b, col, Vector2.up); Add(c, col, Vector2.one);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        /// <summary>Oriented box. rot rotates the local axes; size is full extents.</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion rot, Color col, Color? top = null)
        {
            Vector3 x = rot * new Vector3(size.x * 0.5f, 0, 0), y = rot * new Vector3(0, size.y * 0.5f, 0), z = rot * new Vector3(0, 0, size.z * 0.5f);
            Vector3 p000 = center - x - y - z, p100 = center + x - y - z, p010 = center - x + y - z, p110 = center + x + y - z;
            Vector3 p001 = center - x - y + z, p101 = center + x - y + z, p011 = center - x + y + z, p111 = center + x + y + z;
            Quad(p010, p011, p111, p110, top ?? col);   // top
            Quad(p001, p000, p100, p101, col);          // bottom
            Quad(p000, p010, p110, p100, col);          // back (-z)
            Quad(p101, p111, p011, p001, col);          // front (+z)
            Quad(p001, p011, p010, p000, col);          // left
            Quad(p100, p110, p111, p101, col);          // right
        }

        /// <summary>Vertical prism/cylinder from base centre (axis = up) with n sides; top radius may differ (cone/frustum).</summary>
        public void Prism(Vector3 baseCenter, Vector3 up, float r0, float r1, float height, int sides, Color col, float twist = 0f, Color? capColor = null)
        {
            up.Normalize();
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(side, up);
            Vector3 topC = baseCenter + up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + twist;
                Vector3 d0 = side * Mathf.Cos(a0) + fwd * Mathf.Sin(a0), d1 = side * Mathf.Cos(a1) + fwd * Mathf.Sin(a1);
                Vector3 b0 = baseCenter + d0 * r0, b1 = baseCenter + d1 * r0;
                if (r1 <= 0.001f) Tri(b0, topC, b1, col);
                else
                {
                    Vector3 t0 = topC + d0 * r1, t1 = topC + d1 * r1;
                    Quad(b0, t0, t1, b1, col);
                    Tri(topC, t1, t0, capColor ?? col);
                }
            }
        }

        /// <summary>Low-poly blob (octahedron subdivided once) squashed by scale and roughened deterministically.</summary>
        public void Blob(Vector3 center, Vector3 scale, Color col, Color? colB, int seed, float rough = 0.18f)
        {
            Vector3[] o = { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            int[] f = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
            var rng = new System.Random(seed);
            Vector3 J(Vector3 v)
            {
                // deterministic per-direction jitter so shared corners stay shared
                float h = Mathf.Sin(v.x * 12.9898f + v.y * 78.233f + v.z * 37.719f + seed * 0.618f) * 43758.5453f;
                h -= Mathf.Floor(h);
                return v.normalized * (1f + (h - 0.5f) * 2f * rough);
            }
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = o[f[i]], b = o[f[i + 1]], c = o[f[i + 2]];
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                Vector3[] tri = { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca };
                for (int k = 0; k < tri.Length; k += 3)
                {
                    Color cc = colB.HasValue && rng.NextDouble() < 0.35 ? colB.Value : col;
                    Tri(center + Vector3.Scale(J(tri[k]), scale), center + Vector3.Scale(J(tri[k + 1]), scale), center + Vector3.Scale(J(tri[k + 2]), scale), cc);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetColors(C);
            m.SetUVs(0, UV);
            m.SetUVs(1, UV2);
            m.SetTriangles(T, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(false);
            return m;
        }
    }
}
