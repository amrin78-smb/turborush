using TMPro;
using TurboRush.Core;
using TurboRush.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TurboRush.EditorTools
{
    /// <summary>Shaders → materials, the asphalt texture, UI sprites/icons, the TMP font and the UI skin.</summary>
    public static class ArtBuilder
    {
        public const string Mat = "Assets/Art/Materials/";
        public const string Tex = "Assets/Art/Textures/";
        public const string UiDir = "Assets/Art/UI/";
        public const string FontPath = "Assets/Art/UI/Fonts/Fredoka-SemiBold.ttf";
        public const string FontAssetPath = "Assets/Art/UI/Fonts/Fredoka SDF.asset";
        public const string FontOutlinePath = "Assets/Art/UI/Fonts/Fredoka SDF Outline.mat";

        public static Shader VertexLit => Find("TurboRush/VertexLit");

        private static Shader Find(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null) throw new System.Exception("Shader not found: " + name);
            return s;
        }

        public static RaceMaterials BuildMaterials()
        {
            var m = new RaceMaterials();
            m.vertexLit = EditorAssets.Material(Mat + "TR_World.mat", VertexLit);
            m.road = EditorAssets.Material(Mat + "TR_Road.mat", VertexLit);
            m.road.SetTexture("_BaseMap", RoadTexture());
            m.road.SetColor("_BaseColor", Color.white);
            m.water = EditorAssets.Material(Mat + "TR_Water.mat", VertexLit);
            m.water.SetFloat("_Gloss", 0.8f);
            m.sky = EditorAssets.Material(Mat + "TR_Sky.mat", Find("TurboRush/Sky"));
            m.blobShadow = EditorAssets.Material(Mat + "TR_BlobShadow.mat", Find("TurboRush/BlobShadow"));
            m.blobShadow.SetFloat("_Strength", 0.55f);
            m.carBase = EditorAssets.Material(Mat + "TR_CarBase.mat", VertexLit);
            m.carBase.SetFloat("_Gloss", 0.55f);
            foreach (Material mat in new[] { m.vertexLit, m.road, m.water, m.sky, m.blobShadow, m.carBase }) EditorUtility.SetDirty(mat);
            return m;
        }

        /// <summary>Asphalt: subtle grain, white edge lines, dashed lane lines (3 lanes) — tiles every 12 m.</summary>
        private static Texture2D RoadTexture()
        {
            const int w = 256, h = 512;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];
            var rng = new System.Random(3);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float grain = 0.9f + (float)rng.NextDouble() * 0.12f + Mathf.PerlinNoise(u * 18f, v * 36f) * 0.08f;
                Color c = new Color(0.42f, 0.43f, 0.47f) * grain;
                // edge lines
                if (u > 0.025f && u < 0.06f || u > 0.94f && u < 0.975f) c = new Color(0.97f, 0.97f, 0.97f);
                // two dashed lane dividers (3 lanes), dashes 50% of the 12 m tile
                bool dash = v < 0.5f;
                if (dash && (Mathf.Abs(u - 1f / 3f) < 0.013f || Mathf.Abs(u - 2f / 3f) < 0.013f)) c = new Color(0.95f, 0.95f, 0.95f);
                // slightly darker wheel tracks
                float lane = Mathf.Repeat(u * 3f, 1f);
                if (Mathf.Abs(lane - 0.3f) < 0.06f || Mathf.Abs(lane - 0.7f) < 0.06f) c *= 0.96f;
                c.a = 1f;
                px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            Texture2D saved = EditorAssets.SavePng(t, Tex + "Road_Asphalt.png", false, default, true, TextureWrapMode.Repeat, 512);
            var imp = (TextureImporter)AssetImporter.GetAtPath(Tex + "Road_Asphalt.png");
            imp.anisoLevel = 2;
            imp.SaveAndReimport();
            return saved;
        }

        public static UISkin BuildUISkin()
        {
            UISkin skin = EditorAssets.LoadOrCreateSO<UISkin>("Assets/ScriptableObjects/UISkin.asset");
            skin.font = Font();
            skin.fontOutline = FontOutline(skin.font);
            skin.rounded = Sprite("UI_Rounded", 128, p => IconPainter.Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0.3f), new Vector4(44, 44, 44, 44));
            skin.circle = Sprite("UI_Circle", 128, p => IconPainter.Circle(p, new Vector2(0.5f, 0.5f), 0.48f), Vector4.zero);
            skin.softCircle = SoftCircle();
            skin.gradient = Gradient();
            skin.icons = new Sprite[(int)Icon.Count];
            for (int i = 0; i < (int)Icon.Count; i++) skin.icons[i] = Sprite("Icon_" + (Icon)i, 128, IconPainter.For((Icon)i), Vector4.zero);
            EditorUtility.SetDirty(skin);
            return skin;
        }

        private static Sprite Sprite(string name, int size, IconPainter.Sdf sdf, Vector4 border)
        {
            string path = UiDir + "Sprites/" + name + ".png";
            EditorAssets.SavePng(IconPainter.Render(size, sdf, Color.white), path, true, border, false, TextureWrapMode.Clamp, 256);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite SoftCircle()
        {
            const int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = new Vector2((x + 0.5f) / s - 0.5f, (y + 0.5f) / s - 0.5f).magnitude * 2f;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1f, 0f, d)));
            }
            t.Apply();
            string path = UiDir + "Sprites/UI_SoftCircle.png";
            EditorAssets.SavePng(t, path, true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite Gradient()
        {
            const int w = 8, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(y / (h - 1f), 1.5f)));
            t.Apply();
            string path = UiDir + "Sprites/UI_Gradient.png";
            EditorAssets.SavePng(t, path, true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static TMP_FontAsset Font()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fa != null) return fa;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) throw new System.Exception("Font missing at " + FontPath);
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "Fredoka SDF";
            AssetDatabase.CreateAsset(fa, FontAssetPath);
            fa.material.name = "Fredoka SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (Texture2D t in fa.atlasTextures)
            {
                t.name = "Fredoka SDF Atlas";
                AssetDatabase.AddObjectToAsset(t, fa);
            }
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~•★");
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        private static Material FontOutline(TMP_FontAsset fa)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(FontOutlinePath);
            if (m == null)
            {
                m = new Material(fa.material) { name = "Fredoka SDF Outline" };
                AssetDatabase.CreateAsset(m, FontOutlinePath);
            }
            m.shader = fa.material.shader;
            m.CopyPropertiesFromMaterial(fa.material);
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.22f);
            m.SetColor("_OutlineColor", new Color(0.05f, 0.08f, 0.2f, 1f));
            m.SetFloat("_FaceDilate", 0.12f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0.1f, 0.45f));
            m.SetFloat("_UnderlayOffsetY", -0.9f);
            m.SetFloat("_UnderlaySoftness", 0.3f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
