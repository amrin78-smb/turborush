using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>M0 only: platform configuration + a minimal boot scene, used to validate the Android pipeline before gameplay exists.</summary>
    public static class M0Setup
    {
        public static void RunBatch()
        {
            try
            {
                ProjectConfigurator.ConfigurePlayerSettings();
                ProjectConfigurator.ConfigureQuality();
                ProjectConfigurator.EnsureFolder("Assets/Scenes");
                const string path = "Assets/Scenes/M0Probe.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                Camera.main.backgroundColor = new Color(0.3f, 0.6f, 0.95f);
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                EditorSceneManager.SaveScene(scene, path);
                ProjectConfigurator.ConfigureBuildScenes(path);
                AssetDatabase.SaveAssets();
                Debug.Log("[TR] M0 setup done: " + ProjectConfigurator.Describe());
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
