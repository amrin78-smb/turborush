using System.Collections.Generic;
using TurboRush.Core;
using TurboRush.Platform;
using UnityEngine;

namespace TurboRush.Track
{
    /// <summary>
    /// Generates all track geometry from a <see cref="TrackDefinition"/> at load time: road, curbs, verges, barriers,
    /// safety fencing, decks, pillars, tunnels, chevrons, start gantry and checkpoint flags, split into ~100 m chunks
    /// (2 draw calls each) so the camera can cull them. Scenery is built by <see cref="SceneryBuilder"/>.
    /// </summary>
    public static class TrackBuilder
    {
        public const float SegmentLength = 2f;
        public const float ChunkLength = 100f;
        public const float RoadTextureLength = 12f;
        private const float BarrierHeight = 0.95f;
        private const float BarrierThickness = 0.5f;
        private const float DeckDepth = 1.6f;

        public static GameObject Build(TrackDefinition def, TrackPath path, RaceMaterials mats, QualityProfile q, Transform parent)
        {
            var root = new GameObject("Track_" + def.id);
            root.transform.SetParent(parent, false);
            var road = new MeshKit();
            var deco = new MeshKit();
            float L = path.Length;
            int segCount = Mathf.CeilToInt(L / SegmentLength);
            float seg = L / segCount;
            int segsPerChunk = Mathf.Max(1, Mathf.RoundToInt(ChunkLength / seg));
            int chunk = 0;
            for (int i = 0; i < segCount; i++)
            {
                BuildSegment(def, path, i, seg, road, deco);
                if ((i + 1) % segsPerChunk == 0 || i == segCount - 1)
                {
                    Emit(root.transform, "Chunk" + chunk, road, deco, mats);
                    chunk++;
                }
            }
            BuildStartGantry(def, path, deco);
            BuildCheckpointFlags(def, path, deco);
            BuildJumps(def, path, deco);
            Emit(root.transform, "Landmarks", road, deco, mats);
            return root;
        }

        private static void Emit(Transform parent, string name, MeshKit road, MeshKit deco, RaceMaterials mats)
        {
            if (road.VertexCount > 0) AddRenderer(parent, name + "_Road", road.ToMesh(name + "_Road"), mats.road);
            if (deco.VertexCount > 0) AddRenderer(parent, name + "_Deco", deco.ToMesh(name + "_Deco"), mats.vertexLit);
            road.Clear();
            deco.Clear();
        }

        public static MeshRenderer AddRenderer(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            return mr;
        }

        private static Vector3 P(in TrackSample s, float lat, float h) => s.position + s.right * lat + s.up * h;
        private static Vector3 PW(in TrackSample s, float lat, float worldDown) => s.position + s.right * lat + Vector3.down * worldDown;

        private static void BuildSegment(TrackDefinition def, TrackPath path, int i, float seg, MeshKit road, MeshKit deco)
        {
            TrackPalette c = def.palette;
            float d0 = i * seg, d1 = (i + 1) * seg;
            path.Sample(d0, out TrackSample s0);
            path.Sample(d1, out TrackSample s1);
            float L = path.Length;
            float frac = (d0 + seg * 0.5f) / L;
            bool tunnel = def.IsTunnel(frac);
            bool open = def.IsOpenRunoff(frac);
            float hw = def.roadHalfWidth, cw = def.curbWidth, e = def.roadHalfWidth + def.shoulderWidth;
            bool elevated = def.IsBridge(frac) || s0.position.y - def.groundHeight > def.bridgeMinHeight;

            // Asphalt (textured).
            float v0 = d0 / RoadTextureLength, v1 = d1 / RoadTextureLength;
            road.QuadUV(P(s0, -hw, 0), P(s1, -hw, 0), P(s1, hw, 0), P(s0, hw, 0), c.road,
                new Vector2(0, v0), new Vector2(0, v1), new Vector2(1, v1), new Vector2(1, v0));

            // Curbs (red/white) and verges, both sides.
            Color curb = i % 2 == 0 ? c.curbA : c.curbB;
            deco.Quad(P(s0, -hw - cw, 0.03f), P(s1, -hw - cw, 0.03f), P(s1, -hw, 0.03f), P(s0, -hw, 0.03f), curb);
            deco.Quad(P(s0, hw, 0.03f), P(s1, hw, 0.03f), P(s1, hw + cw, 0.03f), P(s0, hw + cw, 0.03f), curb);
            float outer = open ? e + def.runoffWidth : e;
            deco.Quad(P(s0, -outer, 0.01f), P(s1, -outer, 0.01f), P(s1, -hw - cw, 0.01f), P(s0, -hw - cw, 0.01f), c.verge);
            deco.Quad(P(s0, hw + cw, 0.01f), P(s1, hw + cw, 0.01f), P(s1, outer, 0.01f), P(s0, outer, 0.01f), c.verge);

            if (tunnel)
            {
                bool first = !def.IsTunnel((d0 - seg * 0.5f) / L), last = !def.IsTunnel((d1 + seg * 0.5f) / L);
                BuildTunnelSegment(s0, s1, e + 0.6f, i, c, deco, first, last);
            }
            else if (!open)
            {
                Barrier(s0, s1, -1f, e, i, c, deco, elevated || def.theme == TrackTheme.Skyway);
                Barrier(s0, s1, 1f, e, i, c, deco, elevated || def.theme == TrackTheme.Skyway);
            }

            // Deck underside / embankment.
            float edge = open ? outer : e + BarrierThickness;
            if (elevated)
            {
                deco.Quad(PW(s1, -edge, DeckDepth), P(s1, -edge, 0), P(s0, -edge, 0), PW(s0, -edge, DeckDepth), c.structure * 0.85f);
                deco.Quad(PW(s0, edge, DeckDepth), P(s0, edge, 0), P(s1, edge, 0), PW(s1, edge, DeckDepth), c.structure * 0.85f);
                deco.Quad(PW(s0, -edge, DeckDepth), PW(s0, edge, DeckDepth), PW(s1, edge, DeckDepth), PW(s1, -edge, DeckDepth), c.structure * 0.6f);
                if (Mathf.FloorToInt(d0 / def.pillarSpacing) != Mathf.FloorToInt(d1 / def.pillarSpacing))
                {
                    float top = s0.position.y - DeckDepth;
                    float h = top - (def.groundHeight - 2f);
                    Quaternion yaw = Quaternion.LookRotation(new Vector3(s0.forward.x, 0, s0.forward.z).normalized, Vector3.up);
                    Vector3 basePos = new Vector3(s0.position.x, def.groundHeight - 2f + h * 0.5f, s0.position.z);
                    deco.Box(basePos, new Vector3(Mathf.Min(6f, edge), h, 2.6f), yaw, c.structure * 0.9f);
                    deco.Box(new Vector3(s0.position.x, top - 0.6f, s0.position.z), new Vector3(edge * 1.6f, 1.2f, 3.2f), yaw, c.structure * 0.75f);
                }
            }
            else
            {
                float drop = Mathf.Max(1.5f, s0.position.y - def.groundHeight + 1.5f);
                deco.Quad(PW(s1, -edge - drop, drop), P(s1, -edge, 0), P(s0, -edge, 0), PW(s0, -edge - drop, drop), c.verge * 0.8f);
                deco.Quad(PW(s0, edge + drop, drop), P(s0, edge, 0), P(s1, edge, 0), PW(s1, edge + drop, drop), c.verge * 0.8f);
            }

            // Chevrons on the outside of tighter corners.
            if (!tunnel && !open && Mathf.Abs(s0.curvature) > 1f / 110f && i % 6 == 0)
            {
                float side = s0.curvature > 0f ? -1f : 1f; // turning right → board on the left
                Chevron(s0, side, e, s0.curvature > 0f, deco);
            }
        }

        private static void Barrier(in TrackSample s0, in TrackSample s1, float side, float e, int i, TrackPalette c, MeshKit k, bool fence)
        {
            float a = e * side, b = (e + BarrierThickness) * side;
            Color panel = (i / 2) % 2 == 0 ? c.barrierA : c.barrierB;
            if (side < 0f)
            {
                k.Quad(P(s0, a, 0), P(s0, a, BarrierHeight), P(s1, a, BarrierHeight), P(s1, a, 0), panel);            // inner face (faces +right)
                k.Quad(P(s0, b, BarrierHeight), P(s1, b, BarrierHeight), P(s1, a, BarrierHeight), P(s0, a, BarrierHeight), c.barrierB); // top
                k.Quad(P(s1, b, -DeckDepth * 0.5f), P(s1, b, BarrierHeight), P(s0, b, BarrierHeight), P(s0, b, -DeckDepth * 0.5f), c.structure); // outer
            }
            else
            {
                k.Quad(P(s1, a, 0), P(s1, a, BarrierHeight), P(s0, a, BarrierHeight), P(s0, a, 0), panel);            // inner face (faces −right)
                k.Quad(P(s0, a, BarrierHeight), P(s1, a, BarrierHeight), P(s1, b, BarrierHeight), P(s0, b, BarrierHeight), c.barrierB);
                k.Quad(P(s0, b, -DeckDepth * 0.5f), P(s0, b, BarrierHeight), P(s1, b, BarrierHeight), P(s1, b, -DeckDepth * 0.5f), c.structure);
            }
            if (!fence) return;
            float mid = (e + BarrierThickness * 0.5f) * side;
            Color rail = Color.Lerp(c.structure, Color.white, 0.4f);
            if (i % 2 == 0)
            {
                Vector3 basePt = P(s0, mid, BarrierHeight);
                k.Prism(basePt, s0.up, 0.07f, 0.07f, 1.5f, 4, rail);
            }
            for (int r = 0; r < 2; r++)
            {
                float h0 = BarrierHeight + 0.75f + r * 0.7f, h1 = h0 + 0.09f;
                // Thin double-sided rail strip.
                k.Quad(P(s0, mid, h0), P(s0, mid, h1), P(s1, mid, h1), P(s1, mid, h0), rail);
                k.Quad(P(s1, mid, h0), P(s1, mid, h1), P(s0, mid, h1), P(s0, mid, h0), rail);
            }
        }

        private static void BuildTunnelSegment(in TrackSample s0, in TrackSample s1, float W, int i, TrackPalette c, MeshKit k, bool first, bool last)
        {
            Color inner = Color.Lerp(c.sceneryAlt, Color.black, 0.45f);
            Vector2[] prof = { new Vector2(-W, -0.2f), new Vector2(-W, 5.5f), new Vector2(-W * 0.55f, 8.5f), new Vector2(W * 0.55f, 8.5f), new Vector2(W, 5.5f), new Vector2(W, -0.2f) };
            // Rocky mound outside (jittered per segment so it reads as a rock formation, not a pipe).
            float j0 = Mathf.Sin(i * 1.7f) * 1.5f, j1 = Mathf.Sin((i + 1) * 1.7f) * 1.5f;
            Vector2[] Outer(float j) => new[] { new Vector2(-W - 16f, -2f), new Vector2(-W - 9f + j, 9f), new Vector2(-W * 0.4f - 3f, 17f + j), new Vector2(W * 0.4f + 3f, 17f - j), new Vector2(W + 9f - j, 9f), new Vector2(W + 16f, -2f) };
            Vector2[] o0 = Outer(j0), o1 = Outer(j1);
            for (int p = 0; p < prof.Length - 1; p++)
            {
                Vector2 a = prof[p], b = prof[p + 1];
                k.Quad(P(s0, a.x, a.y), P(s0, b.x, b.y), P(s1, b.x, b.y), P(s1, a.x, a.y), inner);                 // faces the road
                Color rock = (i + p) % 3 == 0 ? Color.Lerp(c.sceneryAlt, Color.white, 0.12f) : c.sceneryAlt;
                k.Quad(P(s1, o1[p].x, o1[p].y), P(s1, o1[p + 1].x, o1[p + 1].y), P(s0, o0[p + 1].x, o0[p + 1].y), P(s0, o0[p].x, o0[p].y), rock); // faces out
                if (first) k.Quad(P(s0, a.x, a.y), P(s0, o0[p].x, o0[p].y), P(s0, o0[p + 1].x, o0[p + 1].y), P(s0, b.x, b.y), c.sceneryAlt * 0.9f);   // entry portal
                if (last) k.Quad(P(s1, b.x, b.y), P(s1, o1[p + 1].x, o1[p + 1].y), P(s1, o1[p].x, o1[p].y), P(s1, a.x, a.y), c.sceneryAlt * 0.9f);    // exit portal
            }
            if (i % 4 == 0)
            {
                k.Emission = 1f;
                k.Quad(P(s0, -1.2f, 8.4f), P(s0, 1.2f, 8.4f), P(s1, 1.2f, 8.4f), P(s1, -1.2f, 8.4f), new Color(1f, 0.92f, 0.6f));
                k.Emission = 0f;
            }
        }

        private static void Chevron(in TrackSample s, float side, float e, bool turnRight, MeshKit k)
        {
            float lat = (e - 0.05f) * side;
            Vector3 f = s.forward;
            Vector3 c0 = P(s, lat, BarrierHeight + 0.15f);
            float w = 1.1f, h = 0.9f;
            Vector3 l0 = c0 - f * w, l1 = c0 + f * w;
            Vector3 u = s.up * h;
            Color yellow = new Color(1f, 0.82f, 0.1f), black = new Color(0.08f, 0.08f, 0.1f);
            if (side < 0) k.Quad(l0, l0 + u, l1 + u, l1, yellow);
            else k.Quad(l1, l1 + u, l0 + u, l0, yellow);
            Vector3 dir = f; // arrow points along the direction of travel
            Vector3 mid = c0 + u * 0.5f - s.right * side * 0.02f;
            Vector3 tip = mid + dir * 0.55f;
            Vector3 back = mid - dir * 0.35f;
            Vector3 up = s.up * 0.32f;
            if (side < 0) k.Tri(tip, back - up, back + up, black);
            else k.Tri(tip, back + up, back - up, black);
        }

        private static void BuildStartGantry(TrackDefinition def, TrackPath path, MeshKit k)
        {
            TrackPalette c = def.palette;
            path.Sample(0f, out TrackSample s);
            float hw = def.roadHalfWidth;
            // Chequered strip across the road.
            int cols = 14;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < cols; col++)
            {
                float x0 = -hw + 2f * hw * col / cols, x1 = -hw + 2f * hw * (col + 1) / cols;
                float z0 = -1f + row, z1 = z0 + 1f;
                Color cc = (row + col) % 2 == 0 ? Color.white : new Color(0.1f, 0.1f, 0.12f);
                Vector3 a = s.position + s.right * x0 + s.forward * z0 + s.up * 0.04f;
                Vector3 b = s.position + s.right * x0 + s.forward * z1 + s.up * 0.04f;
                Vector3 cpt = s.position + s.right * x1 + s.forward * z1 + s.up * 0.04f;
                Vector3 d = s.position + s.right * x1 + s.forward * z0 + s.up * 0.04f;
                k.Quad(a, b, cpt, d, cc);
            }
            // Gantry.
            float e = def.roadHalfWidth + def.shoulderWidth + 0.3f;
            Quaternion rot = Quaternion.LookRotation(s.forward, s.up);
            Color post = c.barrierA, beam = Color.Lerp(c.barrierA, Color.white, 0.15f);
            for (int side = -1; side <= 1; side += 2)
                k.Box(s.position + s.right * (e * side) + s.up * 4f, new Vector3(0.8f, 8f, 0.8f), rot, post);
            k.Box(s.position + s.up * 8.2f, new Vector3(2f * e + 0.8f, 1.6f, 0.9f), rot, beam);
            // Chequered banner faces (both sides).
            int bc = 16;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < bc; col++)
            {
                float x0 = -e + 2f * e * col / bc, x1 = -e + 2f * e * (col + 1) / bc;
                float y0 = 7.6f + row * 0.6f, y1 = y0 + 0.6f;
                Color cc = (row + col) % 2 == 0 ? Color.white : new Color(0.1f, 0.1f, 0.12f);
                Vector3 back = s.forward * -0.48f, front = s.forward * 0.48f;
                Vector3 A(float x, float y) => s.position + s.right * x + s.up * y;
                k.Quad(A(x0, y0) + back, A(x0, y1) + back, A(x1, y1) + back, A(x1, y0) + back, cc);
                k.Quad(A(x1, y0) + front, A(x1, y1) + front, A(x0, y1) + front, A(x0, y0) + front, cc);
            }
        }

        /// <summary>Kicker ramps (yellow with red arrows, striped lip) and a warning sign on both sides before each one.</summary>
        private static void BuildJumps(TrackDefinition def, TrackPath path, MeshKit k)
        {
            if (def.jumps == null) return;
            float hw = def.roadHalfWidth;
            Color yellow = new Color(1f, 0.82f, 0.12f), orange = new Color(1f, 0.6f, 0.1f), red = new Color(0.92f, 0.16f, 0.12f), dark = new Color(0.1f, 0.1f, 0.12f);
            foreach (JumpRamp j in def.jumps)
            {
                float len = Mathf.Max(1f, j.length), lip = j.lapFraction * path.Length, start = lip - len;
                int steps = Mathf.Max(2, Mathf.CeilToInt(len));
                Vector3 Top(float x, float lat, float lift = 0.03f)
                {
                    path.Sample(start + x, out TrackSample s);
                    return P(s, lat, JumpRamp.Profile(Mathf.Clamp(x / len, 1e-4f, 1f), j.height) + lift);
                }
                Vector3 Base(float x, float lat)
                {
                    path.Sample(start + x, out TrackSample s);
                    return P(s, lat, 0f);
                }
                for (int i = 0; i < steps; i++)
                {
                    float x0 = len * i / steps, x1 = len * (i + 1) / steps;
                    Color band = (i / 2) % 2 == 0 ? yellow : orange;
                    k.Quad(Top(x0, -hw), Top(x1, -hw), Top(x1, hw), Top(x0, hw), band);                       // ramp surface (faces up)
                    k.Quad(Base(x1, -hw), Top(x1, -hw), Top(x0, -hw), Base(x0, -hw), orange * 0.85f);         // left side (faces −right)
                    k.Quad(Base(x0, hw), Top(x0, hw), Top(x1, hw), Base(x1, hw), orange * 0.85f);             // right side (faces +right)
                }
                // Lip face (faces forward), striped so the drop reads from the air.
                int stripes = 8;
                for (int i = 0; i < stripes; i++)
                {
                    float a = -hw + 2f * hw * i / stripes, b = -hw + 2f * hw * (i + 1) / stripes;
                    k.Quad(Base(len, b), Top(len, b), Top(len, a), Base(len, a), i % 2 == 0 ? yellow : dark);
                }
                // Arrows painted on the ramp, pointing the way to go.
                foreach (float u in new[] { 0.22f, 0.62f })
                {
                    float xb = len * u, xt = Mathf.Min(len, xb + len * 0.28f);
                    foreach (float lat in new[] { -hw * 0.5f, hw * 0.5f })
                        k.Tri(Top(xt, lat, 0.06f), Top(xb, lat + 1.6f, 0.06f), Top(xb, lat - 1.6f, 0.06f), red);
                }
                // Warning signs 70 m before the lip, facing the oncoming cars.
                path.Sample(lip - 70f, out TrackSample sg);
                float e = def.roadHalfWidth + def.shoulderWidth + BarrierThickness * 0.5f;
                Vector3 back = -sg.forward * 0.12f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 basePt = sg.position + sg.right * (e * side) + sg.up * BarrierHeight;
                    k.Prism(basePt, sg.up, 0.08f, 0.08f, 2.2f, 4, Color.white);
                    Vector3 c = basePt + sg.up * 2.6f + back;
                    Vector3 r = sg.right * 1.05f, u = sg.up * 1.05f;
                    k.Quad(c - r, c + u, c + r, c - u, yellow);
                    Vector3 c2 = c + back * 0.2f;
                    Vector3 rr = sg.right * 0.5f, uu = sg.up * 0.32f;
                    k.Tri(c2 - rr - uu, c2 + rr + uu, c2 + rr - uu, dark);
                    // Back of the board, so it is not invisible from behind.
                    k.Quad(c - r - back * 0.5f, c - u - back * 0.5f, c + r - back * 0.5f, c + u - back * 0.5f, Color.Lerp(Color.white, yellow, 0.3f) * 0.8f);
                }
            }
        }

        private static void BuildCheckpointFlags(TrackDefinition def, TrackPath path, MeshKit k)
        {
            float spacing = path.Length / def.checkpointsPerLap;
            float e = def.roadHalfWidth + def.shoulderWidth + BarrierThickness * 0.5f;
            for (int cp = 1; cp < def.checkpointsPerLap; cp++)
            {
                float d = cp * spacing;
                if (def.IsTunnel(d / path.Length) || def.IsOpenRunoff(d / path.Length)) continue;
                path.Sample(d, out TrackSample s);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 basePt = s.position + s.right * (e * side) + s.up * BarrierHeight;
                    k.Prism(basePt, s.up, 0.06f, 0.06f, 3.2f, 4, Color.white);
                    Vector3 top = basePt + s.up * 3.1f;
                    Color flag = def.palette.curbA;
                    Vector3 tip = top - s.up * 0.45f - s.right * side * 1.3f;
                    k.Tri(top, tip, top - s.up * 0.9f, flag);
                    k.Tri(top - s.up * 0.9f, tip, top, flag);
                }
            }
        }
    }
}
