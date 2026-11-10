using System;
using System.Collections.Generic;
using TurboRush.AI;
using TurboRush.Audio;
using TurboRush.Core;
using TurboRush.Track;
using TurboRush.UI;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// One-click (or batch: Tools/setup.sh) generation of all content: settings, materials, textures, UI sprites,
    /// font, audio, cars, tracks, config and scenes; then validation. Re-runnable (asset GUIDs are kept).
    /// </summary>
    public static class ProjectSetup
    {
        public const string ConfigPath = "Assets/Resources/GameConfig.asset";

        [MenuItem("Tools/Turbo Rush/Setup Project (generate everything)", priority = 1)]
        public static void Run()
        {
            try
            {
                Step("Project settings", 0.02f);
                ProjectConfigurator.ConfigurePlayerSettings();
                ProjectConfigurator.ConfigureQuality();
                ConfigureFogStripping();
                Step("Materials & textures", 0.1f);
                RaceMaterials mats = ArtBuilder.BuildMaterials();
                Step("UI skin & icons", 0.2f);
                UISkin skin = ArtBuilder.BuildUISkin();
                Step("Audio", 0.35f);
                AudioLibrary audio = AudioSynth.BuildAll();
                Step("Cars", 0.5f);
                CarDefinition[] cars = CarBuilder.BuildAll(mats);
                Step("Tracks", 0.65f);
                TrackDefinition[] tracks = TrackDataBuilder.BuildAll();
                Step("Config", 0.75f);
                AIProfile ai = EditorAssets.LoadOrCreateSO<AIProfile>("Assets/ScriptableObjects/AI/AIProfile.asset");
                EditorUtility.SetDirty(ai);
                GameConfig cfg = EditorAssets.LoadOrCreateSO<GameConfig>(ConfigPath);
                cfg.cars = cars;
                cfg.tracks = tracks;
                cfg.ai = ai;
                cfg.audio = audio;
                cfg.ui = skin;
                cfg.materials = mats;
                if (cfg.race == null) cfg.race = new Race.RaceTuning();
                if (cfg.camera == null) cfg.camera = new ChaseCameraSettings();
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
                Step("Scenes", 0.85f);
                SceneBuilder.BuildAll();
                AssetDatabase.SaveAssets();
                Step("Validate", 0.95f);
                bool ok = ProjectValidator.Validate();
                Debug.Log("[TR] Setup finished. Validation " + (ok ? "passed" : "FAILED"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(ProjectValidator.Validate() ? 0 : 2);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Scenes set fog at runtime, so keep the linear-fog variants explicitly (automatic stripping would drop them).</summary>
        public static void ConfigureFogStripping()
        {
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            void SetB(string n, bool v) { SerializedProperty p = gs.FindProperty(n); if (p == null) throw new Exception("GraphicsSettings field " + n); p.boolValue = v; }
            SerializedProperty strip = gs.FindProperty("m_FogStripping");
            if (strip == null) throw new Exception("GraphicsSettings.m_FogStripping not found");
            strip.intValue = 1; // custom
            SetB("m_FogKeepLinear", true);
            SetB("m_FogKeepExp", false);
            SetB("m_FogKeepExp2", false);
            gs.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Step(string label, float p)
        {
            Debug.Log("[TR] Setup: " + label);
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("Turbo Rush 3D", label, p);
        }
    }

    /// <summary>Checks generated content and the release-blocking Android settings. Menu: Tools ▸ Turbo Rush ▸ Validate Project.</summary>
    public static class ProjectValidator
    {
        [MenuItem("Tools/Turbo Rush/Validate Project", priority = 2)]
        public static bool Validate()
        {
            var errors = new List<string>();
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ProjectSetup.ConfigPath);
            if (cfg == null) errors.Add("GameConfig missing at " + ProjectSetup.ConfigPath);
            else
            {
                if (cfg.cars.Length < 6) errors.Add("Need at least 6 cars (got " + cfg.cars.Length + ")");
                foreach (CarDefinition c in cfg.cars)
                {
                    if (c == null || c.prefab == null || c.prefab.GetComponent<RacerView>() == null) errors.Add("Car missing prefab/RacerView: " + (c != null ? c.id : "null"));
                    else if (c.tuning.maxSpeedKmh <= 0) errors.Add("Car tuning invalid: " + c.id);
                }
                if (cfg.cars.Length > 0 && cfg.cars[0].unlockStars != 0) errors.Add("First car must be available immediately");
                if (cfg.tracks.Length < 3) errors.Add("Need 3 tracks");
                foreach (TrackDefinition t in cfg.tracks)
                {
                    if (t == null) { errors.Add("Null track"); continue; }
                    if (t.preview == null) errors.Add("Track preview missing: " + t.id);
                    try
                    {
                        TrackPath path = t.BuildPath();
                        if (path.Length < 800f || path.Length > 3000f) errors.Add($"Track {t.id} lap length {path.Length:0} m out of range");
                        float minR = float.MaxValue;
                        for (float d = 0; d < path.Length; d += 2f)
                        {
                            float k = Mathf.Abs(path.Sample(d).curvature);
                            if (k > 1e-4f) minR = Mathf.Min(minR, 1f / k);
                        }
                        if (minR < 30f) errors.Add($"Track {t.id} has a corner tighter than 30 m (min radius {minR:0} m)");
                        if (t.jumps != null)
                            foreach (JumpRamp j in t.jumps)
                            {
                                if (j.lapFraction < 0f || j.lapFraction >= 1f || j.length < 6f || j.length > 20f || j.height < 0.2f || j.height > 1.2f)
                                    errors.Add($"Track {t.id} jump at {j.lapFraction:0.000} out of range (length 6–20 m, height 0.2–1.2 m)");
                                float lip = j.lapFraction * path.Length;
                                for (float d = lip - j.length; d <= lip + 60f; d += 2f)
                                {
                                    float k = Mathf.Abs(path.Sample(d).curvature);
                                    if (k > 1f / 150f) { errors.Add($"Track {t.id} jump at {j.lapFraction:0.000} is not on a straight (radius {1f / k:0} m at {d:0} m); it needs straight road from the ramp to 60 m past the lip"); break; }
                                }
                                if (t.IsTunnel(Mathf.Repeat(lip, path.Length) / path.Length)) errors.Add($"Track {t.id} jump at {j.lapFraction:0.000} is inside a tunnel");
                            }
                        Debug.Log($"[TR Validate] {t.displayName}: lap {path.Length:0} m × {t.laps} laps, tightest radius {minR:0} m, jumps {(t.jumps != null ? t.jumps.Length : 0)}");
                    }
                    catch (Exception e) { errors.Add($"Track {t.id} path error: {e.Message}"); }
                }
                if (cfg.tracks.Length > 0 && cfg.tracks[0].unlockStars != 0) errors.Add("First track must be unlocked from the start");
                if (cfg.audio == null || cfg.audio.engineLoop == null || cfg.audio.menuMusic == null)
                    errors.Add($"Audio library incomplete (lib={cfg.audio != null}, engine={cfg.audio != null && cfg.audio.engineLoop != null}, music={cfg.audio != null && cfg.audio.menuMusic != null})");
                else for (int i = 0; i < Enum.GetValues(typeof(Sfx)).Length; i++) if (cfg.audio.Get((Sfx)i) == null) errors.Add("Missing SFX " + (Sfx)i);
                if (cfg.ui == null || cfg.ui.font == null || cfg.ui.rounded == null)
                    errors.Add($"UI skin incomplete (skin={cfg.ui != null}, font={cfg.ui != null && cfg.ui.font != null}, rounded={cfg.ui != null && cfg.ui.rounded != null})");
                else for (int i = 0; i < (int)Icon.Count; i++) if (cfg.ui.Get((Icon)i) == null) errors.Add("Missing icon " + (Icon)i);
                if (cfg.materials == null || cfg.materials.vertexLit == null || cfg.materials.road == null || cfg.materials.sky == null || cfg.materials.blobShadow == null)
                    errors.Add("Race materials incomplete");
                if (cfg.ai == null) errors.Add("AI profile missing");
                if (cfg.race.racerCount < 2 || cfg.race.racerCount > 8) errors.Add("racerCount must be 2..8");
            }
            string android = ProjectConfigurator.CheckAndroidSettings();
            if (android != null) errors.Add(android);
            foreach (string s in new[] { SceneBuilder.BootPath, SceneBuilder.MenuPath, SceneBuilder.RacePath })
                if (AssetDatabase.LoadMainAssetAtPath(s) == null) errors.Add("Missing scene " + s);

            foreach (string e in errors) Debug.LogError("[TR Validate] " + e);
            Debug.Log("[TR Validate] " + (errors.Count == 0 ? "OK" : errors.Count + " problem(s)"));
            return errors.Count == 0;
        }
    }
}
