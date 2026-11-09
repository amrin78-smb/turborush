using System.Collections.Generic;
using TurboRush.Core;
using TurboRush.Platform;
using UnityEngine;

namespace TurboRush.Track
{
    /// <summary>
    /// Deterministic scenery from the track's seed and theme: ground (flattened under ground-level road), sea,
    /// distant mountain ring, clouds, trees/rocks/cacti/houses. Everything is vertex-coloured and merged into a few
    /// chunked meshes (one shared material) to keep draw calls low on the Redmi A3.
    /// </summary>
    public static class SceneryBuilder
    {
        private sealed class Proximity
        {
            private readonly Dictionary<long, List<Vector4>> m_Cells = new Dictionary<long, List<Vector4>>();
            private const float Cell = 30f;

            public Proximity(TrackDefinition def, TrackPath path)
            {
                for (float d = 0; d < path.Length; d += 4f)
                {
                    Vector3 p = path.PositionAt(d, 0f);
                    bool bridge = def.IsBridge(d / path.Length) || p.y - def.groundHeight > def.bridgeMinHeight;
                    Add(new Vector4(p.x, p.y, p.z, bridge ? 1f : 0f));
                }
            }

            private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            private void Add(Vector4 p)
            {
                long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
                if (!m_Cells.TryGetValue(k, out List<Vector4> l)) m_Cells[k] = l = new List<Vector4>();
                l.Add(p);
            }

            public float Nearest(float x, float z, float range, out float y) => Nearest(x, z, range, out y, out _);

            /// <summary>Horizontal distance to the nearest centreline point (capped at range), its height and whether it is a bridge.</summary>
            public float Nearest(float x, float z, float range, out float y, out bool bridge)
            {
                int cx = Mathf.FloorToInt(x / Cell), cz = Mathf.FloorToInt(z / Cell);
                int r = Mathf.CeilToInt(range / Cell);
                float best = range * range;
                y = 0f;
                bridge = false;
                for (int i = -r; i <= r; i++)
                for (int j = -r; j <= r; j++)
                {
                    if (!m_Cells.TryGetValue(Key(cx + i, cz + j), out List<Vector4> l)) continue;
                    foreach (Vector4 p in l)
                    {
                        float dx = p.x - x, dz = p.z - z, sq = dx * dx + dz * dz;
                        if (sq < best) { best = sq; y = p.y; bridge = p.w > 0.5f; }
                    }
                }
                return Mathf.Sqrt(best);
            }
        }

        public static void Build(TrackDefinition def, TrackPath path, RaceMaterials mats, QualityProfile q, Transform parent, out Vector3 center, out float extent)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            var prox = new Proximity(def, path);
            Bounds b = new Bounds(path.PositionAt(0, 0), Vector3.zero);
            for (float d = 0; d < path.Length; d += 10f) b.Encapsulate(path.PositionAt(d, 0));
            Vector3 ctr = new Vector3(b.center.x, def.groundHeight, b.center.z);
            float ext = Mathf.Max(b.extents.x, b.extents.z);
            center = ctr;
            extent = ext;
            float radius = Mathf.Max(def.sceneryRadius, ext + 150f);
            var rng = new System.Random(def.seed);
            TrackPalette c = def.palette;
            float clear = def.roadHalfWidth + def.shoulderWidth + 6f;

            BuildGround(def, path, mats, root, prox, ctr, ext, radius);
            if (def.sea) BuildSea(def, mats, root, ctr);

            // Props, grouped into a 3×3 grid of meshes for culling.
            var kits = new MeshKit[9];
            for (int i = 0; i < kits.Length; i++) kits[i] = new MeshKit();
            MeshKit KitAt(Vector3 p)
            {
                int gx = Mathf.Clamp(Mathf.FloorToInt((p.x - ctr.x + radius) / (2f * radius) * 3f), 0, 2);
                int gz = Mathf.Clamp(Mathf.FloorToInt((p.z - ctr.z + radius) / (2f * radius) * 3f), 0, 2);
                return kits[gx * 3 + gz];
            }
            float density = q.sceneryDensity;

            bool TryPlace(float minClear, out Vector3 pos)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rad = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                    float x = ctr.x + Mathf.Cos(ang) * rad, z = ctr.z + Mathf.Sin(ang) * rad;
                    float dist = prox.Nearest(x, z, minClear + 40f, out float ty);
                    if (dist < minClear) continue;
                    float gy = GroundHeight(def, x, z, prox, ctr, ext, out _);
                    if (def.sea && gy < def.seaLevel + 0.8f) continue;
                    pos = new Vector3(x, gy, z);
                    return true;
                }
                pos = Vector3.zero;
                return false;
            }

            int trees = Mathf.RoundToInt(def.treeCount * density);
            for (int i = 0; i < trees; i++)
            {
                if (!TryPlace(clear + 4f, out Vector3 p)) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.7f;
                if (def.theme == TrackTheme.Skyway) s *= 1.4f; // seen from above, a bit larger reads better
                MeshKit k = KitAt(p);
                if (def.theme == TrackTheme.Coastal) Palm(k, p, s, rng, c);
                else if (rng.NextDouble() < 0.45) Pine(k, p, s, c, rng);
                else RoundTree(k, p, s, c, rng);
            }
            int rocks = Mathf.RoundToInt(def.rockCount * density);
            for (int i = 0; i < rocks; i++)
            {
                if (!TryPlace(clear + 6f, out Vector3 p)) continue;
                float s = def.theme == TrackTheme.Desert ? 6f + (float)rng.NextDouble() * 18f : 2f + (float)rng.NextDouble() * 5f;
                Vector3 scale = new Vector3(s, s * (def.theme == TrackTheme.Desert ? 0.9f + (float)rng.NextDouble() * 1.4f : 0.6f), s * 0.9f);
                KitAt(p).Blob(p + Vector3.up * scale.y * 0.35f, scale, c.sceneryAlt, Color.Lerp(c.sceneryAlt, Color.white, 0.2f), rng.Next(), 0.22f);
            }
            int cacti = Mathf.RoundToInt(def.cactusCount * density);
            for (int i = 0; i < cacti; i++)
            {
                if (!TryPlace(clear + 3f, out Vector3 p)) continue;
                Cactus(KitAt(p), p, 0.8f + (float)rng.NextDouble() * 0.8f, c);
            }
            int houses = Mathf.RoundToInt(def.buildingCount * density);
            for (int i = 0; i < houses; i++)
            {
                if (!TryPlace(clear + 12f, out Vector3 p)) continue;
                House(KitAt(p), p, rng);
            }
            for (int i = 0; i < kits.Length; i++)
                if (kits[i].VertexCount > 0) TrackBuilder.AddRenderer(root, "Props" + i, kits[i].ToMesh("Props" + i), mats.vertexLit);

            // Mountains and clouds: single far meshes, inside the far clip and softened by fog.
            var far = new MeshKit();
            if (def.mountainRing && q.distantMountains)
            {
                float ringR = Mathf.Min(radius + 250f, q.farClip * 0.82f);
                int peaks = 34;
                for (int i = 0; i < peaks; i++)
                {
                    float ang = i / (float)peaks * Mathf.PI * 2f + (float)rng.NextDouble() * 0.12f;
                    float rr = ringR * (0.92f + (float)rng.NextDouble() * 0.16f);
                    Vector3 p = new Vector3(ctr.x + Mathf.Cos(ang) * rr, def.groundHeight - 25f, ctr.z + Mathf.Sin(ang) * rr);
                    float h = 110f + (float)rng.NextDouble() * 170f;
                    float br = 110f + (float)rng.NextDouble() * 110f;
                    if (def.theme == TrackTheme.Desert)
                    {
                        // Mesas: flat-topped buttes.
                        far.Prism(p, Vector3.up, br, br * 0.72f, h * 0.6f, 7, c.mountain, (float)rng.NextDouble(), Color.Lerp(c.mountain, Color.white, 0.15f));
                    }
                    else
                    {
                        far.Prism(p, Vector3.up, br, br * 0.33f, h * 0.72f, 6, c.mountain, (float)rng.NextDouble(), c.mountain);
                        far.Prism(p + Vector3.up * h * 0.72f, Vector3.up, br * 0.33f, 0f, h * 0.28f, 6, def.theme == TrackTheme.Skyway ? Color.white : Color.Lerp(c.mountain, Color.white, 0.25f), (float)rng.NextDouble());
                    }
                }
            }
            int clouds = Mathf.RoundToInt(def.cloudCount * Mathf.Lerp(0.6f, 1f, density));
            for (int i = 0; i < clouds; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rr = radius * (0.5f + (float)rng.NextDouble() * 0.9f);
                float alt = def.theme == TrackTheme.Skyway ? def.groundHeight + 25f + (float)rng.NextDouble() * 140f : 120f + (float)rng.NextDouble() * 120f;
                Vector3 p = new Vector3(ctr.x + Mathf.Cos(ang) * rr, alt, ctr.z + Mathf.Sin(ang) * rr);
                if (prox.Nearest(p.x, p.z, 60f, out float ty) < 50f && Mathf.Abs(ty - alt) < 30f) continue; // never on the road
                int puffs = 3 + rng.Next(3);
                far.Emission = 0.55f; // soft, bright clouds rather than grey rocks
                for (int k = 0; k < puffs; k++)
                {
                    Vector3 off = new Vector3(((float)rng.NextDouble() - 0.5f) * 50f, ((float)rng.NextDouble() - 0.3f) * 6f, ((float)rng.NextDouble() - 0.5f) * 22f);
                    float s = 14f + (float)rng.NextDouble() * 16f;
                    far.Blob(p + off, new Vector3(s * 1.5f, s * 0.45f, s), Color.white, new Color(0.92f, 0.95f, 1f), rng.Next(), 0.08f);
                }
                far.Emission = 0f;
            }
            if (far.VertexCount > 0) TrackBuilder.AddRenderer(root, "FarScenery", far.ToMesh("FarScenery"), mats.vertexLit);
        }

        private static float Noise(float x, float z, float scale) => Mathf.PerlinNoise(x * scale + 1000f, z * scale + 1000f);

        private static float GroundHeight(TrackDefinition def, float x, float z, Proximity prox, Vector3 center, float extent, out float trackDist)
        {
            float amp = def.theme == TrackTheme.Skyway ? 14f : def.theme == TrackTheme.Desert ? 7f : 4f;
            float h = def.groundHeight + (Noise(x, z, 0.006f) - 0.4f) * amp + (Noise(x, z, 0.03f) - 0.5f) * amp * 0.25f;
            if (def.sea)
            {
                float dx = x - center.x, dz = z - center.z;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                float island = extent + 90f + (Noise(x, z, 0.004f) - 0.5f) * 120f;
                if (r > island) h -= (r - island) * 0.12f;
            }
            trackDist = prox.Nearest(x, z, 70f, out float ty, out bool bridge);
            float hw = def.roadHalfWidth + def.shoulderWidth;
            if (bridge && def.sea && trackDist < 70f)
            {
                // Carve a channel under coastal bridges so they span water.
                float t = Mathf.InverseLerp(30f, 70f, trackDist);
                return Mathf.Lerp(def.seaLevel - 4f, h, t * t);
            }
            if (!bridge && trackDist < 60f)
            {
                // Ground-level road: settle the ground just under it, blending back to the natural height by 60 m.
                float flat = ty - 0.6f;
                float t = Mathf.InverseLerp(hw + 4f, 60f, trackDist);
                h = Mathf.Lerp(flat, Mathf.Min(h, flat + (trackDist - hw) * 0.35f), t * t);
            }
            return h;
        }

        private static void BuildGround(TrackDefinition def, TrackPath path, RaceMaterials mats, Transform root, Proximity prox, Vector3 center, float extent, float radius)
        {
            TrackPalette c = def.palette;
            float size = radius * 2f + 900f;
            int n = 72;
            float step = size / n;
            var heights = new float[(n + 1) * (n + 1)];
            for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
            {
                float x = center.x - size * 0.5f + i * step, z = center.z - size * 0.5f + j * step;
                heights[i * (n + 1) + j] = GroundHeight(def, x, z, prox, center, extent, out _);
            }
            int chunks = 4, per = n / chunks;
            var k = new MeshKit();
            for (int cx = 0; cx < chunks; cx++)
            for (int cz = 0; cz < chunks; cz++)
            {
                for (int i = cx * per; i < (cx + 1) * per; i++)
                for (int j = cz * per; j < (cz + 1) * per; j++)
                {
                    float x0 = center.x - size * 0.5f + i * step, z0 = center.z - size * 0.5f + j * step;
                    Vector3 a = new Vector3(x0, heights[i * (n + 1) + j], z0);
                    Vector3 b = new Vector3(x0, heights[i * (n + 1) + j + 1], z0 + step);
                    Vector3 cc = new Vector3(x0 + step, heights[(i + 1) * (n + 1) + j + 1], z0 + step);
                    Vector3 d = new Vector3(x0 + step, heights[(i + 1) * (n + 1) + j], z0);
                    float avgH = (a.y + b.y + cc.y + d.y) * 0.25f;
                    Color col = Color.Lerp(c.ground, c.groundAlt, Noise(x0, z0, 0.02f));
                    if (def.sea && avgH < def.seaLevel + 2.2f) col = Color.Lerp(new Color(0.93f, 0.85f, 0.62f), new Color(0.85f, 0.76f, 0.55f), Noise(x0, z0, 0.05f));
                    k.Quad(a, b, cc, d, col);
                }
                TrackBuilder.AddRenderer(root, $"Ground{cx}{cz}", k.ToMesh($"Ground{cx}{cz}"), mats.vertexLit);
                k.Clear();
            }
        }

        private static void BuildSea(TrackDefinition def, RaceMaterials mats, Transform root, Vector3 center)
        {
            var k = new MeshKit();
            float size = 4000f;
            int n = 8;
            float step = size / n;
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x0 = center.x - size * 0.5f + i * step, z0 = center.z - size * 0.5f + j * step;
                Vector3 a = new Vector3(x0, def.seaLevel, z0), b = new Vector3(x0, def.seaLevel, z0 + step);
                Vector3 c = new Vector3(x0 + step, def.seaLevel, z0 + step), d = new Vector3(x0 + step, def.seaLevel, z0);
                float far = Mathf.Clamp01(new Vector2(x0 + step * 0.5f - center.x, z0 + step * 0.5f - center.z).magnitude / (size * 0.5f));
                k.Quad(a, b, c, d, Color.Lerp(def.palette.water, def.palette.water * 0.75f, far));
            }
            TrackBuilder.AddRenderer(root, "Sea", k.ToMesh("Sea"), mats.water != null ? mats.water : mats.vertexLit);
        }

        private static void Pine(MeshKit k, Vector3 p, float s, TrackPalette c, System.Random rng)
        {
            Color trunk = new Color(0.45f, 0.3f, 0.2f);
            Color leaf = Color.Lerp(c.scenery, Color.black, 0.15f + (float)rng.NextDouble() * 0.1f);
            k.Prism(p - Vector3.up * 0.5f, Vector3.up, 0.35f * s, 0.25f * s, 2.5f * s, 5, trunk);
            k.Prism(p + Vector3.up * 2f * s, Vector3.up, 2.6f * s, 0f, 4.5f * s, 6, leaf, (float)rng.NextDouble());
            k.Prism(p + Vector3.up * 4.2f * s, Vector3.up, 1.9f * s, 0f, 3.8f * s, 6, Color.Lerp(leaf, Color.white, 0.08f), (float)rng.NextDouble());
        }

        private static void RoundTree(MeshKit k, Vector3 p, float s, TrackPalette c, System.Random rng)
        {
            Color trunk = new Color(0.5f, 0.34f, 0.22f);
            Color leaf = Color.Lerp(c.scenery, new Color(0.55f, 0.78f, 0.25f), (float)rng.NextDouble() * 0.5f);
            k.Prism(p - Vector3.up * 0.5f, Vector3.up, 0.4f * s, 0.3f * s, 3f * s, 5, trunk);
            k.Blob(p + Vector3.up * 4.2f * s, new Vector3(2.8f, 2.4f, 2.8f) * s, leaf, Color.Lerp(leaf, Color.white, 0.12f), rng.Next(), 0.15f);
        }

        private static void Palm(MeshKit k, Vector3 p, float s, System.Random rng, TrackPalette c)
        {
            Color trunk = new Color(0.62f, 0.48f, 0.3f);
            Vector3 lean = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.5f, 1f, ((float)rng.NextDouble() - 0.5f) * 0.5f).normalized;
            Vector3 cur = p - Vector3.up * 0.3f;
            for (int i = 0; i < 4; i++)
            {
                k.Prism(cur, lean, 0.32f * s, 0.27f * s, 2f * s, 5, i % 2 == 0 ? trunk : trunk * 0.9f);
                cur += lean * 2f * s;
            }
            Color leaf = Color.Lerp(c.scenery, new Color(0.2f, 0.65f, 0.25f), 0.5f);
            for (int f = 0; f < 6; f++)
            {
                float a = f / 6f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.3f;
                Vector3 dir = new Vector3(Mathf.Cos(a), -0.35f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * 0.9f * s;
                Vector3 tip = cur + dir * 4.2f * s;
                Vector3 mid = cur + dir * 2f * s + Vector3.up * 0.6f * s;
                k.Tri(cur, mid + side, tip, leaf);
                k.Tri(cur, tip, mid - side, leaf * 0.9f);
                k.Tri(cur, tip, mid + side, leaf);
                k.Tri(cur, mid - side, tip, leaf * 0.9f);
            }
        }

        private static void Cactus(MeshKit k, Vector3 p, float s, TrackPalette c)
        {
            Color g = new Color(0.3f, 0.6f, 0.3f);
            k.Prism(p - Vector3.up * 0.3f, Vector3.up, 0.45f * s, 0.4f * s, 5f * s, 6, g);
            k.Prism(p + new Vector3(0.45f * s, 2f * s, 0), Vector3.right, 0.3f * s, 0.3f * s, 1.1f * s, 5, g);
            k.Prism(p + new Vector3(1.4f * s, 1.9f * s, 0), Vector3.up, 0.3f * s, 0.28f * s, 1.8f * s, 5, g);
            k.Prism(p + new Vector3(-0.45f * s, 2.8f * s, 0), Vector3.left, 0.28f * s, 0.28f * s, 0.9f * s, 5, g);
            k.Prism(p + new Vector3(-1.25f * s, 2.7f * s, 0), Vector3.up, 0.28f * s, 0.25f * s, 1.4f * s, 5, g);
        }

        private static void House(MeshKit k, Vector3 p, System.Random rng)
        {
            Color[] walls = { new Color(1f, 0.95f, 0.85f), new Color(0.98f, 0.8f, 0.7f), new Color(0.75f, 0.9f, 1f), new Color(1f, 0.92f, 0.6f) };
            Color[] roofs = { new Color(0.85f, 0.3f, 0.25f), new Color(0.25f, 0.5f, 0.8f), new Color(0.3f, 0.65f, 0.55f) };
            Color wall = walls[rng.Next(walls.Length)], roof = roofs[rng.Next(roofs.Length)];
            float w = 8f + (float)rng.NextDouble() * 6f, d = 7f + (float)rng.NextDouble() * 4f, h = 5f + (float)rng.NextDouble() * 3f;
            Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            k.Box(p + Vector3.up * (h * 0.5f - 0.5f), new Vector3(w, h, d), rot, wall);
            // Gable roof
            Vector3 r = rot * Vector3.right * (w * 0.55f), f = rot * Vector3.forward * (d * 0.55f), top = Vector3.up * (h - 0.5f);
            Vector3 ridge = Vector3.up * (h + 2.8f);
            Vector3 a = p + top - r - f, b = p + top - r + f, cc = p + top + r + f, dd = p + top + r - f;
            Vector3 ra = p + ridge - r, rb = p + ridge + r;
            k.Quad(a, ra, rb, dd, roof);
            k.Quad(cc, rb, ra, b, roof);
            k.Tri(a, b, ra, wall * 0.95f);
            k.Tri(cc, dd, rb, wall * 0.95f);
        }
    }
}
