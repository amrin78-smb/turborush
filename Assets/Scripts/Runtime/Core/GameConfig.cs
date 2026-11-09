using TurboRush.AI;
using TurboRush.Audio;
using TurboRush.Race;
using TurboRush.Track;
using TurboRush.UI;
using TurboRush.Vehicle;
using UnityEngine;

namespace TurboRush.Core
{
    /// <summary>Root data asset (Resources/GameConfig). Everything content-related hangs off this.</summary>
    [CreateAssetMenu(menuName = "Turbo Rush/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        public CarDefinition[] cars = new CarDefinition[0];
        public TrackDefinition[] tracks = new TrackDefinition[0];
        public AIProfile ai;
        public RaceTuning race = new RaceTuning();
        public AudioLibrary audio;
        public UISkin ui;
        public RaceMaterials materials;

        [Header("Camera")]
        public ChaseCameraSettings camera = new ChaseCameraSettings();

        private static GameConfig s_Instance;

        public static GameConfig Load()
        {
            if (s_Instance == null) s_Instance = Resources.Load<GameConfig>(ResourcePath);
            if (s_Instance == null) Debug.LogError("[TR] Resources/GameConfig is missing. Run Tools ▸ Turbo Rush ▸ Setup Project.");
            return s_Instance;
        }

        public CarDefinition FindCar(string id)
        {
            foreach (CarDefinition c in cars) if (c != null && c.id == id) return c;
            return cars.Length > 0 ? cars[0] : null;
        }

        public TrackDefinition FindTrack(string id)
        {
            foreach (TrackDefinition t in tracks) if (t != null && t.id == id) return t;
            return tracks.Length > 0 ? tracks[0] : null;
        }

        public int TrackIndex(TrackDefinition t) => System.Array.IndexOf(tracks, t);
    }

    /// <summary>Shared materials used by generated track geometry and cars.</summary>
    [System.Serializable]
    public sealed class RaceMaterials
    {
        public Material vertexLit;      // all vertex-coloured world geometry
        public Material road;           // textured asphalt
        public Material water;
        public Material sky;            // skybox
        public Material blobShadow;
        public Material carBase;        // cloned per paint colour at runtime (cached)
    }

    [System.Serializable]
    public sealed class ChaseCameraSettings
    {
        public float distance = 8.5f;
        public float height = 3.4f;
        public float lookAhead = 14f;
        public float lookHeight = 1.2f;
        [Tooltip("Position smoothing time (s).")] public float positionDamping = 0.12f;
        [Tooltip("Rotation smoothing (higher = snappier).")] public float rotationSharpness = 8f;
        public float fov = 60f;
        [Tooltip("Extra FOV at top speed (kept small for comfort).")] public float speedFov = 6f;
        [Tooltip("How much of the car's lateral offset the camera follows (0..1).")] public float lateralFollow = 0.75f;
        [Tooltip("How much the camera rolls with track banking (0 = never).")] public float bankFollow = 0.25f;
    }
}
