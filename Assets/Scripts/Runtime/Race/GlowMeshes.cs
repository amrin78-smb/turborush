using UnityEngine;

namespace TurboRush.Race
{
    /// <summary>Smooth meshes for the additive power-up effects (welded, with radial normals so the rim glow is round).</summary>
    public static class GlowMeshes
    {
        /// <summary>Ellipsoid around <paramref name="center"/> with the given radii, in one vertex colour.</summary>
        public static Mesh Sphere(string name, Vector3 center, Vector3 radii, Color color, int rings = 10, int segments = 16)
        {
            var v = new Vector3[(rings + 1) * (segments + 1)];
            var n = new Vector3[v.Length];
            var c = new Color[v.Length];
            var t = new int[rings * segments * 6];
            int vi = 0, ti = 0;
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.PI * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float lon = 2f * Mathf.PI * s / segments;
                    var dir = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                    v[vi] = center + Vector3.Scale(dir, radii);
                    n[vi] = new Vector3(dir.x / radii.x, dir.y / radii.y, dir.z / radii.z).normalized;
                    c[vi] = color;
                    vi++;
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    // Clockwise seen from outside (Unity front face).
                    t[ti++] = a; t[ti++] = a + 1; t[ti++] = b;
                    t[ti++] = a + 1; t[ti++] = b + 1; t[ti++] = b;
                }
            var m = new Mesh { name = name, vertices = v, normals = n, colors = c, triangles = t };
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        /// <summary>Cone from a round base at the origin to a tip at (0, 0, −length): one exhaust flame.</summary>
        public static Mesh Flame(string name, float radius, float length, Color baseColor, Color tipColor, int sides = 8)
        {
            var v = new Vector3[sides * 2];
            var n = new Vector3[v.Length];
            var c = new Color[v.Length];
            var t = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i] = radial * radius;
                v[sides + i] = new Vector3(0f, 0f, -length);
                Vector3 normal = (radial * length + Vector3.forward * -radius).normalized;
                n[i] = n[sides + i] = normal;
                c[i] = baseColor;
                c[sides + i] = tipColor;
                int j = (i + 1) % sides;
                t[i * 3] = i; t[i * 3 + 1] = sides + i; t[i * 3 + 2] = j;
            }
            var m = new Mesh { name = name, vertices = v, normals = n, colors = c, triangles = t };
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }
    }
}
