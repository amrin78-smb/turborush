using TurboRush.CameraRig;
using TurboRush.Core;
using TurboRush.Input;
using TurboRush.Race;
using TurboRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TurboRush.EditorTools
{
    /// <summary>Builds the three scenes. Content (track, cars, UI) is generated at runtime from GameConfig data.</summary>
    public static class SceneBuilder
    {
        public const string BootPath = "Assets/Scenes/Boot.unity";
        public const string MenuPath = "Assets/Scenes/Menu.unity";
        public const string RacePath = "Assets/Scenes/Race.unity";

        public static void BuildAll()
        {
            EditorAssets.EnsureFolder("Assets/Scenes");
            Boot();
            Menu();
            Race();
            ProjectConfigurator.ConfigureBuildScenes(BootPath, MenuPath, RacePath);
            if (AssetDatabase.LoadMainAssetAtPath("Assets/Scenes/M0Probe.unity") != null) AssetDatabase.DeleteAsset("Assets/Scenes/M0Probe.unity");
        }

        private static void Lighting()
        {
            RenderSettings.fog = true; // keeps linear-fog shader variants in builds
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 200;
            RenderSettings.fogEndDistance = 900;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(ArtBuilder.Mat + "TR_Sky.mat");
            Lightmapping.lightingDataAsset = null;
        }

        private static Camera MakeCamera(string name, CameraClearFlags clear)
        {
            var go = new GameObject(name) { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = clear;
            cam.backgroundColor = new Color(0.12f, 0.43f, 0.85f);
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1200f;
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            return cam;
        }

        private static Light MakeSun()
        {
            var go = new GameObject("Sun");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.shadows = LightShadows.None;
            l.intensity = 1.05f;
            go.transform.rotation = Quaternion.Euler(48, -35, 0);
            return l;
        }

        private static void Boot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting();
            MakeCamera("Main Camera", CameraClearFlags.SolidColor);
            new GameObject("Bootstrap").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        private static void Menu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting();
            Camera cam = MakeCamera("Main Camera", CameraClearFlags.Skybox);
            cam.fieldOfView = 40f;
            cam.transform.position = new Vector3(-1f, 2.2f, -8.6f);
            cam.transform.LookAt(new Vector3(-2.6f, 1.2f, 0f));
            Light sun = MakeSun();
            var turntable = new GameObject("Turntable").transform;
            var menu = new GameObject("Menu").AddComponent<MenuController>();
            EditorAssets.Set(menu, "m_Turntable", turntable);
            EditorAssets.Set(menu, "m_Sun", sun);
            EditorAssets.Set(menu, "m_Camera", cam);
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        private static void Race()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting();
            Camera cam = MakeCamera("Main Camera", CameraClearFlags.Skybox);
            ChaseCamera chase = cam.gameObject.AddComponent<ChaseCamera>();
            Light sun = MakeSun();
            var go = new GameObject("Race");
            var input = go.AddComponent<PlayerInputController>();
            var race = go.AddComponent<RaceManager>();
            EditorAssets.Set(race, "m_Camera", chase);
            EditorAssets.Set(race, "m_Input", input);
            EditorAssets.Set(race, "m_Sun", sun);
            EditorSceneManager.SaveScene(scene, RacePath);
        }
    }
}
