using System.IO;
using TMPro;
using TurboRush.Core;
using TurboRush.UI;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Renders the launcher icon in-engine: the starter car (Blaze) from a friendly front 3/4 view on a bright sky-blue
    /// background with speed lines. Writes the Android adaptive layers (background + transparent foreground inside the
    /// 66 % safe zone), the round and legacy icons, and the default icon. Run by Tools/setup.sh (and the menu).
    /// </summary>
    public static class AppIconBuilder
    {
        public const string Folder = "Assets/Art/UI/AppIcon";
        private const int Size = 1024;

        [MenuItem("Tools/Turbo Rush/Build App Icon", priority = 31)]
        public static void Build()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ProjectSetup.ConfigPath);
            Texture2D car = RenderCar(cfg.cars[0].prefab);
            Texture2D bg = Background();
            Texture2D fgAdaptive = Place(car, 0.60f, 0.53f);   // adaptive icons are cropped to the central 66 %
            Texture2D fgFull = Place(car, 0.86f, 0.54f);
            Texture2D legacy = Over(bg, fgFull);

            Texture2D tBg = Save(bg, "AppIcon_Background.png");
            Texture2D tFg = Save(fgAdaptive, "AppIcon_Foreground.png");
            Texture2D tLegacy = Save(legacy, "AppIcon.png");

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tLegacy }, IconKind.Application);
            var android = NamedBuildTarget.Android;
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in adaptive) icon.SetTextures(tBg, tFg);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            foreach (PlatformIconKind kind in new[] { AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy })
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (PlatformIcon icon in icons) icon.SetTexture(tLegacy);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            BuildSplash(cfg, car);
            AssetDatabase.SaveAssets();
            Debug.Log("[TR] App icon built (adaptive, round, legacy) from " + cfg.cars[0].displayName);
        }

        public static readonly Color SplashBackground = new Color(0.09f, 0.38f, 0.9f);

        /// <summary>Splash screen: the game logo (Blaze + "TURBO RUSH 3D" in the menu font) on the icon's blue, before Unity's logo.</summary>
        private static void BuildSplash(GameConfig cfg, Texture2D car)
        {
            const int W = 1600, H = 1000;
            Texture2D title = RenderTitle(cfg.ui, W, 360);
            var logo = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            logo.SetPixels(px);
            Texture2D carFit = Place(car, 0.95f, 0.36f);   // sit low in its square so it hugs the title
            Blit(carFit, logo, new RectInt((W - 860) / 2, 230, 860, 860));
            Blit(title, logo, new RectInt(0, 0, W, 360));
            logo.Apply();
            string path = Folder + "/SplashLogo.png";
            File.WriteAllBytes(path, logo.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.backgroundColor = SplashBackground;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, sprite) };
            Debug.Log("[TR] Splash screen: game logo, then the Unity logo, on the game blue");
        }

        private static Texture2D RenderTitle(UISkin skin, int w, int h)
        {
            var go = new GameObject("Title");
            var t = go.AddComponent<TextMeshPro>();
            if (skin.font != null) t.font = skin.font;
            if (skin.fontOutline != null) t.fontSharedMaterial = skin.fontOutline;
            t.text = "TURBO RUSH <color=#" + ColorUtility.ToHtmlStringRGB(skin.primary) + ">3D</color>";
            t.color = skin.accent;
            t.fontStyle = FontStyles.Italic;
            t.fontSize = 10f;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(40f, 6f);
            t.ForceMeshUpdate();
            Bounds b = t.textBounds;
            var cam = new GameObject("TitleCam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            float aspect = w / (float)h;
            cam.orthographicSize = Mathf.Max(b.size.y * 0.5f * 1.15f, b.size.x * 0.5f * 1.08f / aspect);
            cam.transform.position = go.transform.TransformPoint(b.center) + new Vector3(0, 0, -10f);
            cam.transform.rotation = Quaternion.identity;
            Color[] k = Shoot(cam, Color.black, w, h), wt = Shoot(cam, Color.white, w, h);
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(go);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(Unmix(k, wt));
            tex.Apply();
            return tex;
        }

        private static Color[] Unmix(Color[] onBlack, Color[] onWhite)
        {
            var px = new Color[onBlack.Length];
            for (int i = 0; i < px.Length; i++)
            {
                Color k = onBlack[i], w = onWhite[i];
                float alpha = Mathf.Clamp01(1f - ((w.r - k.r) + (w.g - k.g) + (w.b - k.b)) / 3f);
                Color c = alpha > 0.004f ? k / alpha : Color.clear;
                c.a = alpha;
                px[i] = c;
            }
            return px;
        }

        private static void Blit(Texture2D src, Texture2D dst, RectInt r)
        {
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                {
                    Color s = src.GetPixelBilinear((x + 0.5f) / r.width, (y + 0.5f) / r.height);
                    int dx = r.x + x, dy = r.y + y;
                    dst.SetPixel(dx, dy, Blend(dst.GetPixel(dx, dy), s));
                }
        }

        /// <summary>
        /// Batch entry (Tools/setup.sh runs it in its own Unity process after setup: camera rendering does not work inside
        /// the setup run, which reconfigures graphics). Fails if the icon or splash logo came out empty.
        /// </summary>
        public static void BuildBatch()
        {
            try
            {
                Build();
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/AppIcon_Foreground.png");
                string problem = null;
                if (!AndroidIconsSet(out problem)) { }
                else if (CoveredFraction(Folder + "/AppIcon_Foreground.png") < 0.05f) problem = "app icon car render is empty";
                else if (CoveredFraction(Folder + "/SplashLogo.png") < 0.05f) problem = "splash logo render is empty";
                if (problem != null) { Debug.LogError("[TR] Icon FAILED: " + problem); EditorApplication.Exit(1); return; }
                Debug.Log("[TR] Icon and splash OK");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static float CoveredFraction(string path)
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(path));
            Color32[] px = t.GetPixels32();
            int n = 0;
            foreach (Color32 c in px) if (c.a > 128 && (c.r > 40 || c.g > 40 || c.b > 40)) n++;
            return n / (float)px.Length;
        }

        /// <summary>True when every Android icon slot has a texture (used by the project validator).</summary>
        public static bool AndroidIconsSet(out string problem)
        {
            problem = null;
            foreach (PlatformIconKind kind in new[] { AndroidPlatformIconKind.Adaptive, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy })
                foreach (PlatformIcon icon in PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
                    foreach (Texture2D t in icon.GetTextures())
                        if (t == null) { problem = "Android " + kind + " icon " + icon.width + "px has no texture"; return false; }
            return true;
        }

        private static Texture2D RenderCar(GameObject prefab)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.7f, 0.78f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
            GameObject car = Object.Instantiate(prefab);
            foreach (LODGroup g in car.GetComponentsInChildren<LODGroup>()) g.ForceLOD(0);
            Transform shadow = car.transform.Find("Shadow");
            if (shadow != null) shadow.gameObject.SetActive(false);
            var view = car.GetComponent<Vehicle.RacerView>();
            if (view != null) view.SpinWheels(0f, -0.6f, 0f); // front wheels turned a little: looks lively

            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 24f;
            cam.transform.position = new Vector3(-6.2f, 3.1f, 8.4f);
            cam.transform.LookAt(new Vector3(0f, 0.55f, 0.1f));
            // The pipeline's colour buffer may not keep alpha, so derive coverage from a black and a white render.
            Color[] px = Unmix(Shoot(cam, Color.black, Size, Size), Shoot(cam, Color.white, Size, Size));
            Object.DestroyImmediate(cam.gameObject);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            Object.DestroyImmediate(car);
            return tex;
        }

        private static Color[] Shoot(Camera cam, Color background, int w, int h)
        {
            cam.backgroundColor = background;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Color[] c = tex.GetPixels();
            if (System.Environment.GetEnvironmentVariable("TR_ICON_DEBUG") == "1")
                File.WriteAllBytes($"Logs/icon_shot_{w}x{h}_{(background.r > 0.5f ? "white" : "black")}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return c;
        }

        /// <summary>Scales the opaque part of <paramref name="src"/> to <paramref name="fraction"/> of the icon width,
        /// centred at height <paramref name="centreY"/>, with a soft ground shadow underneath.</summary>
        private static Texture2D Place(Texture2D src, float fraction, float centreY)
        {
            int x0 = Size, y0 = Size, x1 = 0, y1 = 0;
            Color32[] px = src.GetPixels32();
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (px[y * Size + x].a > 40) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            float w = x1 - x0 + 1, h = y1 - y0 + 1;
            float scale = fraction * Size / Mathf.Max(w, h);
            float ox = Size * 0.5f - w * scale * 0.5f, oy = Size * centreY - h * scale * 0.5f;
            var dst = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var o = new Color[Size * Size];
            // shadow ellipse under the car
            Vector2 sc = new Vector2(Size * 0.5f, oy + h * scale * 0.06f);
            Vector2 sr = new Vector2(w * scale * 0.5f, h * scale * 0.12f);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = new Vector2((x - sc.x) / sr.x, (y - sc.y) / sr.y).magnitude;
                    float sa = Mathf.Clamp01(1f - d) * 0.35f;
                    Color c = new Color(0.05f, 0.1f, 0.25f, sa);
                    float u = (x - ox) / scale + x0, v = (y - oy) / scale + y0;
                    if (u >= x0 && u <= x1 && v >= y0 && v <= y1)
                    {
                        Color s = src.GetPixelBilinear(u / Size, v / Size);
                        c = Blend(c, s);
                    }
                    o[y * Size + x] = c;
                }
            dst.SetPixels(o);
            dst.Apply();
            return dst;
        }

        private static Texture2D Background()
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var o = new Color[Size * Size];
            Color top = new Color(0.33f, 0.74f, 1f), bottom = new Color(0.09f, 0.38f, 0.9f), glow = new Color(1f, 0.93f, 0.55f);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float fy = y / (float)Size, fx = x / (float)Size;
                    Color c = Color.Lerp(bottom, top, fy);
                    float r = new Vector2(fx - 0.5f, fy - 0.58f).magnitude;
                    c = Color.Lerp(c, glow, Mathf.Clamp01(1f - r / 0.42f) * 0.45f);   // warm sun glow behind the car
                    // diagonal speed lines
                    float s = fx * 0.55f + fy;
                    foreach (float lane in new[] { 0.36f, 0.47f, 1.02f, 1.12f })
                    {
                        float d = Mathf.Abs(s - lane);
                        float len = Mathf.Clamp01(1f - Mathf.Abs(fx - (lane < 0.6f ? 0.18f : 0.84f)) / 0.2f);
                        c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - d / 0.012f) * 0.5f * len);
                    }
                    c.a = 1f;
                    o[y * Size + x] = c;
                }
            t.SetPixels(o);
            t.Apply();
            return t;
        }

        private static Texture2D Over(Texture2D bg, Texture2D fg)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Color[] a = bg.GetPixels(), b = fg.GetPixels();
            for (int i = 0; i < a.Length; i++) { a[i] = Blend(a[i], b[i]); a[i].a = 1f; }
            t.SetPixels(a);
            t.Apply();
            return t;
        }

        private static Color Blend(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 0f) return new Color(0, 0, 0, 0);
            Color c = (over * over.a + under * under.a * (1f - over.a)) / a;
            c.a = a;
            return c;
        }

        private static Texture2D Save(Texture2D tex, string file)
        {
            string path = Folder + "/" + file;
            EditorAssets.EnsureFolder(Folder);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = Size;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
