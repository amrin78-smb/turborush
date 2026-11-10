using System.IO;
using TurboRush.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Renders every car prefab with the game's own shader and a sun light into one lineup image (rear 3/4 and front 3/4).
    /// Menu: Tools ▸ Turbo Rush ▸ Render Car Lineup. Batch: -executeMethod TurboRush.EditorTools.CarLineupRenderer.RenderBatch
    /// </summary>
    public static class CarLineupRenderer
    {
        public const string Output = "Docs/screenshots/m7_cars_in_engine.png";

        [MenuItem("Tools/Turbo Rush/Render Car Lineup", priority = 30)]
        public static void Render()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ProjectSetup.ConfigPath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.74f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            const int W = 640, H = 400;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.92f, 0.98f);
            cam.fieldOfView = 17f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            int n = cfg.cars.Length;
            var sheet = new Texture2D(W * n / 2, H * 4, TextureFormat.RGB24, false);
            var read = new Texture2D(W, H, TextureFormat.RGB24, false);
            for (int i = 0; i < n; i++)
            {
                GameObject car = Object.Instantiate(cfg.cars[i].prefab);
                foreach (LODGroup g in car.GetComponentsInChildren<LODGroup>()) g.ForceLOD(0);
                for (int view = 0; view < 2; view++)
                {
                    // view 0: the chase-camera side (behind, +Z is forward); view 1: front 3/4.
                    Vector3 eye = view == 0 ? new Vector3(4.5f, 4.2f, -10.5f) : new Vector3(-6.5f, 3.0f, 9.0f);
                    cam.transform.position = eye;
                    cam.transform.LookAt(new Vector3(0f, 0.6f, 0f));
                    cam.Render();
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    read.Apply();
                    int col = i % (n / 2), row = (i / (n / 2)) * 2 + view;
                    sheet.SetPixels(col * W, (3 - row) * H, W, H, read.GetPixels());
                }
                Object.DestroyImmediate(car);
            }
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllBytes(Output, sheet.EncodeToPNG());
            Debug.Log("[TR] Car lineup written to " + Output);
        }

        public static void RenderBatch()
        {
            try { Render(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
