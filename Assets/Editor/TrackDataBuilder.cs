using TurboRush.Track;
using UnityEditor;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// The three original track layouts (control points + theme). This is the track-authoring entry point: add a new
    /// method here (or create a TrackDefinition asset by hand) and list it in GameConfig — no race code changes.
    /// </summary>
    public static class TrackDataBuilder
    {
        public const string Dir = "Assets/ScriptableObjects/Tracks/";

        public static TrackDefinition[] BuildAll()
        {
            var defs = new[] { Skyway(), Desert(), Coastal() };
            foreach (TrackDefinition d in defs)
            {
                d.preview = Preview(d);
                EditorUtility.SetDirty(d);
            }
            AssetDatabase.SaveAssets();
            return defs;
        }

        private static TrackDefinition Make(string id) => EditorAssets.LoadOrCreateSO<TrackDefinition>(Dir + "Track_" + id + ".asset");

        /// <summary>Tutorial track: elevated skyway, broad corners both ways, an S-bend, a gentle climb and descent.</summary>
        private static TrackDefinition Skyway()
        {
            TrackDefinition t = Make("skyway");
            t.id = "skyway";
            t.displayName = "Skyway Circuit";
            t.theme = TrackTheme.Skyway;
            t.unlockStars = 0;
            t.controlPoints = new[]
            {
                new Vector3(0, 0, 0), new Vector3(0, 0, 160), new Vector3(25, 1, 262), new Vector3(100, 3, 322), new Vector3(200, 5, 322),
                new Vector3(272, 7, 272), new Vector3(304, 9, 186), new Vector3(280, 10, 112), new Vector3(306, 10, 36), new Vector3(340, 9, -40),
                new Vector3(372, 7, -120), new Vector3(338, 5, -208), new Vector3(240, 3, -240), new Vector3(130, 2, -222), new Vector3(42, 1, -188),
                new Vector3(0, 0, -118)
            };
            t.laps = 2;
            t.autoBankFactor = 5f;
            t.maxBank = 8f;
            t.roadHalfWidth = 7.5f;
            t.curbWidth = 1.2f;
            t.shoulderWidth = 3.2f;
            t.runoffWidth = 18f;
            t.openRunoff = new LapRange[0];
            t.checkpointsPerLap = 8;
            t.groundHeight = -48f;
            t.pillarSpacing = 34f;
            t.bridgeMinHeight = 4f;
            t.bridges = new LapRange[0];
            t.tunnels = new LapRange[0];
            t.sea = false;
            t.seed = 11;
            t.treeCount = 260;
            t.rockCount = 30;
            t.cactusCount = 0;
            t.cloudCount = 34;
            t.buildingCount = 14;
            t.mountainRing = true;
            t.sceneryRadius = 620f;
            t.palette = new TrackPalette(); // defaults are the Skyway look
            return t;
        }

        /// <summary>Warm desert at ground level: broad banked turns, rock formations, a tunnel through a rock outcrop.</summary>
        private static TrackDefinition Desert()
        {
            TrackDefinition t = Make("desert");
            t.id = "desert";
            t.displayName = "Desert Dash";
            t.theme = TrackTheme.Desert;
            t.unlockStars = 2;
            t.controlPoints = new[]
            {
                new Vector3(0, 0, 0), new Vector3(0, 0, 200), new Vector3(30, 2, 322), new Vector3(130, 5, 392), new Vector3(262, 6, 382),
                new Vector3(352, 4, 302), new Vector3(382, 2, 182), new Vector3(362, 0, 62), new Vector3(392, 0, -58), new Vector3(352, 2, -170),
                new Vector3(242, 4, -222), new Vector3(122, 3, -202), new Vector3(42, 1, -142), new Vector3(0, 0, -88)
            };
            t.laps = 2;
            t.autoBankFactor = 9f;
            t.maxBank = 13f;
            t.roadHalfWidth = 7.5f;
            t.curbWidth = 1.2f;
            t.shoulderWidth = 3.4f;
            t.runoffWidth = 18f;
            t.openRunoff = new LapRange[0];
            t.checkpointsPerLap = 8;
            t.groundHeight = -0.8f;
            t.pillarSpacing = 40f;
            t.bridgeMinHeight = 50f;
            t.bridges = new LapRange[0];
            t.tunnels = new[] { new LapRange(0.405f, 0.455f) };
            t.sea = false;
            t.seed = 23;
            t.treeCount = 0;
            t.rockCount = 70;
            t.cactusCount = 90;
            t.cloudCount = 10;
            t.buildingCount = 0;
            t.mountainRing = true;
            t.sceneryRadius = 650f;
            t.palette = new TrackPalette
            {
                skyTop = new Color(0.22f, 0.5f, 0.92f), skyHorizon = new Color(1f, 0.88f, 0.7f), skyBottom = new Color(0.95f, 0.8f, 0.6f),
                fog = new Color(0.98f, 0.86f, 0.7f), fogStart = 200f, fogEnd = 950f, ambient = new Color(0.7f, 0.62f, 0.55f),
                sun = new Color(1f, 0.93f, 0.8f), sunEuler = new Vector3(42f, 30f, 0f),
                ground = new Color(0.93f, 0.74f, 0.47f), groundAlt = new Color(0.87f, 0.64f, 0.4f), verge = new Color(0.91f, 0.72f, 0.46f),
                road = new Color(0.36f, 0.33f, 0.32f), roadLine = Color.white, curbA = new Color(0.9f, 0.15f, 0.12f), curbB = Color.white,
                barrierA = new Color(1f, 0.52f, 0.12f), barrierB = Color.white, structure = new Color(0.85f, 0.62f, 0.45f),
                scenery = new Color(0.32f, 0.6f, 0.3f), sceneryAlt = new Color(0.8f, 0.47f, 0.3f), mountain = new Color(0.8f, 0.52f, 0.38f),
                water = new Color(0.2f, 0.55f, 0.75f)
            };
            return t;
        }

        /// <summary>Island loop by the sea: sweeping curves, a gentle bridge over a channel, a sandy runoff section.</summary>
        private static TrackDefinition Coastal()
        {
            TrackDefinition t = Make("coastal");
            t.id = "coastal";
            t.displayName = "Coastal Loop";
            t.theme = TrackTheme.Coastal;
            t.unlockStars = 4;
            t.controlPoints = new[]
            {
                new Vector3(0, 1, 0), new Vector3(0, 1, 170), new Vector3(-40, 2, 282), new Vector3(-140, 3, 332), new Vector3(-262, 5, 312),
                new Vector3(-332, 6, 222), new Vector3(-342, 6, 102), new Vector3(-302, 4, -10), new Vector3(-225, 3, -80), new Vector3(-150, 2, -100),
                new Vector3(-85, 1.5f, -160), new Vector3(-15, 1, -155), new Vector3(12, 1, -85)
            };
            t.laps = 2;
            t.autoBankFactor = 6f;
            t.maxBank = 9f;
            t.roadHalfWidth = 7.5f;
            t.curbWidth = 1.2f;
            t.shoulderWidth = 3.2f;
            t.runoffWidth = 18f;
            t.openRunoff = new[] { new LapRange(0.05f, 0.11f) };
            t.checkpointsPerLap = 8;
            t.groundHeight = 0.6f;
            t.pillarSpacing = 30f;
            t.bridgeMinHeight = 50f;
            t.bridges = new[] { new LapRange(0.40f, 0.50f) };
            t.tunnels = new LapRange[0];
            t.sea = true;
            t.seaLevel = -1.5f;
            t.seed = 37;
            t.treeCount = 140;
            t.rockCount = 26;
            t.cactusCount = 0;
            t.cloudCount = 22;
            t.buildingCount = 26;
            t.mountainRing = false;
            t.sceneryRadius = 560f;
            t.palette = new TrackPalette
            {
                skyTop = new Color(0.14f, 0.5f, 0.95f), skyHorizon = new Color(0.78f, 0.93f, 1f), skyBottom = new Color(0.3f, 0.6f, 0.85f),
                fog = new Color(0.78f, 0.9f, 0.98f), fogStart = 220f, fogEnd = 1100f, ambient = new Color(0.62f, 0.7f, 0.78f),
                sun = new Color(1f, 0.97f, 0.9f), sunEuler = new Vector3(50f, 150f, 0f),
                ground = new Color(0.45f, 0.75f, 0.35f), groundAlt = new Color(0.52f, 0.8f, 0.38f), verge = new Color(0.5f, 0.8f, 0.4f),
                road = new Color(0.33f, 0.34f, 0.38f), roadLine = Color.white, curbA = new Color(0.1f, 0.5f, 0.95f), curbB = Color.white,
                barrierA = new Color(0.1f, 0.75f, 0.72f), barrierB = new Color(1f, 0.84f, 0.2f), structure = new Color(0.92f, 0.92f, 0.9f),
                scenery = new Color(0.25f, 0.62f, 0.3f), sceneryAlt = new Color(0.62f, 0.6f, 0.58f), mountain = new Color(0.45f, 0.6f, 0.7f),
                water = new Color(0.08f, 0.55f, 0.82f)
            };
            return t;
        }

        /// <summary>Top-down map preview for the track select card.</summary>
        public static Sprite Preview(TrackDefinition t)
        {
            const int w = 512, h = 320;
            TrackPath path = t.BuildPath();
            Bounds b = new Bounds(path.PositionAt(0, 0), Vector3.zero);
            for (float d = 0; d < path.Length; d += 5f) b.Encapsulate(path.PositionAt(d, 0));
            float scale = Mathf.Min((w - 60) / b.size.x, (h - 60) / b.size.z);
            Vector2 ToPx(Vector3 p) => new Vector2((p.x - b.center.x) * scale + w * 0.5f, (p.z - b.center.z) * scale + h * 0.5f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            TrackPalette pal = t.palette;
            Color bgA = t.sea ? pal.water : pal.ground, bgB = t.sea ? Color.Lerp(pal.water, Color.white, 0.15f) : pal.groundAlt;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = Color.Lerp(Color.Lerp(bgA, bgB, Mathf.PerlinNoise(x * 0.02f, y * 0.02f)), pal.skyTop, (float)y / h * 0.25f);
            void Disc(Vector2 c, float r, Color col)
            {
                int x0 = Mathf.Max(0, (int)(c.x - r - 1)), x1 = Mathf.Min(w - 1, (int)(c.x + r + 1));
                int y0 = Mathf.Max(0, (int)(c.y - r - 1)), y1 = Mathf.Min(h - 1, (int)(c.y + r + 1));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0) px[y * w + x] = Color.Lerp(px[y * w + x], col, a);
                }
            }
            if (t.sea)
                for (float d = 0; d < path.Length; d += 3f) Disc(ToPx(path.PositionAt(d, 0)), 34f, Color.Lerp(pal.ground, new Color(0.93f, 0.85f, 0.62f), 0.3f));
            for (float d = 0; d < path.Length; d += 1.5f) Disc(ToPx(path.PositionAt(d, 0)), 13f, Color.white);
            for (float d = 0; d < path.Length; d += 1.5f) Disc(ToPx(path.PositionAt(d, 0)), 9.5f, t.IsTunnel(d / path.Length) ? new Color(0.25f, 0.2f, 0.2f) : new Color(0.25f, 0.27f, 0.32f));
            for (float d = 0; d < path.Length; d += 1.5f) if (Mathf.Repeat(d, 30f) < 15f) Disc(ToPx(path.PositionAt(d, 0)), 1.6f, new Color(1f, 0.85f, 0.2f));
            // start line chequer + direction arrow
            Vector2 s0 = ToPx(path.PositionAt(0, 0));
            for (int i = -2; i <= 2; i++) Disc(s0 + new Vector2(i * 4f, 0), 2.6f, i % 2 == 0 ? Color.white : Color.black);
            Disc(ToPx(path.PositionAt(25f, 0)), 7f, pal.curbA);
            tex.SetPixels(px);
            tex.Apply();
            string png = "Assets/Art/Textures/Previews/Preview_" + t.id + ".png";
            EditorAssets.SavePng(tex, png, true, Vector4.zero, false, TextureWrapMode.Clamp, 512);
            return AssetDatabase.LoadAssetAtPath<Sprite>(png);
        }
    }
}
