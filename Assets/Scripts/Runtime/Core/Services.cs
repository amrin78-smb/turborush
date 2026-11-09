using TurboRush.Audio;
using TurboRush.Platform;
using TurboRush.Save;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurboRush.Core
{
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string Menu = "Menu";
        public const string Race = "Race";
    }

    /// <summary>
    /// The single persistent service holder (DontDestroyOnLoad). <see cref="Ensure"/> is idempotent, so any scene can
    /// be opened directly in the Editor and scene changes never create duplicate managers or subscriptions.
    /// </summary>
    public sealed class Services : MonoBehaviour
    {
        public static Services Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public GameStateMachine States { get; private set; }
        public SaveDataService SaveService { get; private set; }
        public AudioManager Audio { get; private set; }
        public SaveData Save => SaveService.Data;
        public SettingsData Settings => SaveService.Data.settings;

        /// <summary>Menu screen to open when the Menu scene loads (set by Results/Pause).</summary>
        public GameState MenuEntryState = GameState.MainMenu;

        public static Services Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[TurboRush Services]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Services>();
            Instance.Init();
            return Instance;
        }

        private void Init()
        {
            Config = GameConfig.Load();
            States = new GameStateMachine();
            SaveService = new SaveDataService();
            SaveService.Load();
            Progression.EnsureDefaults(SaveService.Data, Config);
            SaveService.Save();

            Audio = gameObject.AddComponent<AudioManager>();
            Audio.Init(Config != null ? Config.audio : null, Settings);
            DebugArgs.Apply();
            ApplySettings();
            Debug.Log($"[TR] Services ready. Save: {SaveService.LastLoadOutcome}; stars={Save.TotalStars()}; v{Application.version}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplySettings()
        {
            Haptics.Enabled = Settings.vibration;
            Audio.ApplySettings();
            DeviceQualityManager.Apply(DebugArgs.Quality ?? Settings.quality);
        }

        public void Persist() => SaveService.Save();

        public CarDefinition SelectedCar => Config.FindCar(Save.selectedCar);
        public TrackDefinition SelectedTrack => Config.FindTrack(Save.selectedTrack);

        public void StartRace(TrackDefinition track)
        {
            if (track != null) Save.selectedTrack = track.id;
            Persist();
            Audio.Play(Sfx.Click, 1f, true);
            SceneManager.LoadScene(SceneNames.Race);
        }

        public void GoToMenu(GameState screen)
        {
            MenuEntryState = screen;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneNames.Menu);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Persist();
        }

        private void OnApplicationQuit() => Persist();
    }
}
