using System;
using TurboRush.Audio;
using TurboRush.CameraRig;
using TurboRush.Core;
using TurboRush.Input;
using TurboRush.Platform;
using TurboRush.Save;
using TurboRush.Track;
using TurboRush.UI;
using TurboRush.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TurboRush.Race
{
    /// <summary>
    /// Race scene orchestrator. Owns the <see cref="RaceSimulation"/>, drives Countdown → Racing → Finishing → Results,
    /// pause/resume/restart, and forwards simulation events to HUD, audio and haptics. Subscriptions are made once in
    /// Awake and removed in OnDestroy; restart only resets state (no new objects, no new subscriptions).
    /// </summary>
    public sealed class RaceManager : MonoBehaviour
    {
        [SerializeField] private ChaseCamera m_Camera;
        [SerializeField] private PlayerInputController m_Input;
        [SerializeField] private Light m_Sun;

        public RaceSimulation Sim { get; private set; }
        public TrackDefinition Track { get; private set; }
        public CarDefinition PlayerCar { get; private set; }
        public CarDefinition[] RacerCars { get; private set; }
        public RaceUI UI { get; private set; }
        public PlayerInputController Input => m_Input;
        public RaceReward LastReward { get; private set; }
        public int RestartCount { get; private set; }
        public bool ShowSteerHint { get; private set; }

        public event Action RaceReset;

        private Services m_Services;
        private RacerView[] m_Views;
        private float m_Countdown;
        private int m_LastCount;
        private float m_FinishTimer;
        private GameState m_ResumeState = GameState.Racing;
        private bool m_Snap;
        private float m_HintTimer;

        private GameState State => m_Services.States.State;

        private void Awake()
        {
            m_Services = Services.Ensure();
            GameConfig cfg = m_Services.Config;
            UIKit.Skin = cfg.ui;

            Track = !string.IsNullOrEmpty(DebugArgs.Track) ? cfg.FindTrack(DebugArgs.Track) : m_Services.SelectedTrack;
            PlayerCar = m_Services.SelectedCar;
            TrackPath path = Track.BuildPath();
            var raceTrack = RaceTrack.From(Track, path);

            QualityProfile q = DeviceQualityManager.Current;
            RaceEnvironment.Apply(Track, cfg.materials, m_Sun);
            var world = new GameObject("World").transform;
            TrackBuilder.Build(Track, path, cfg.materials, q, world);
            SceneryBuilder.Build(Track, path, cfg.materials, q, world, out _, out _);

            int n = Mathf.Clamp(cfg.race.racerCount, 2, 8);
            RacerCars = new CarDefinition[n];
            var tunings = new CarTuning[n];
            RacerCars[0] = PlayerCar;
            int ci = 0;
            for (int i = 1; i < n; i++)
            {
                CarDefinition c;
                do { c = cfg.cars[ci % cfg.cars.Length]; ci++; } while (c == PlayerCar && cfg.cars.Length > 1);
                RacerCars[i] = c;
            }
            for (int i = 0; i < n; i++) tunings[i] = RacerCars[i].tuning;
            Sim = new RaceSimulation(raceTrack, cfg.race, cfg.ai != null ? cfg.ai : AI.AIProfile.CreateDefault(), tunings, 0);

            var cars = new GameObject("Racers").transform;
            m_Views = new RacerView[n];
            for (int i = 0; i < n; i++)
            {
                GameObject go = Instantiate(RacerCars[i].prefab, cars);
                go.name = (i == 0 ? "Player_" : "AI_") + RacerCars[i].displayName;
                m_Views[i] = go.GetComponent<RacerView>();
            }

            Sim.RacerFinished += OnRacerFinished;
            Sim.PlayerPositionChanged += OnPositionChanged;
            Sim.PlayerCheckpoint += OnCheckpoint;
            Sim.PlayerLap += OnLap;
            Sim.PlayerHitBarrier += OnBarrier;
            Sim.PlayerBumped += OnBump;
            Sim.Respawned += OnRespawned;
            Sim.PlayerTookOff += OnTookOff;
            Sim.PlayerLanded += OnLanded;

            m_Camera.Init(Sim, cfg.camera, q.farClip);
            UI = gameObject.AddComponent<RaceUI>();
            UI.Init(this, cfg.ui);
            m_Input.RegisterBlocker(UI.PauseButtonRect);
            ShowSteerHint = !m_Services.Save.steerHintShown;
        }

        private void Start()
        {
            m_Services.Audio.PlayMusic(true);
            BeginCountdown();
        }

        private void OnDestroy()
        {
            if (Sim != null)
            {
                Sim.RacerFinished -= OnRacerFinished;
                Sim.PlayerPositionChanged -= OnPositionChanged;
                Sim.PlayerCheckpoint -= OnCheckpoint;
                Sim.PlayerLap -= OnLap;
                Sim.PlayerHitBarrier -= OnBarrier;
                Sim.PlayerBumped -= OnBump;
                Sim.Respawned -= OnRespawned;
                Sim.PlayerTookOff -= OnTookOff;
                Sim.PlayerLanded -= OnLanded;
            }
            if (m_Services != null)
            {
                m_Services.Audio.StopEngine();
                m_Services.Audio.SetPaused(false);
            }
        }

        private void Go(GameState s)
        {
            if (!m_Services.States.TryTransition(s)) m_Services.States.Force(s);
        }

        /// <summary>Start or restart: every racer, timer, checkpoint and HUD value back to the grid.</summary>
        public void BeginCountdown()
        {
            Sim.Reset();
            LastReward = null;
            m_Countdown = m_Services.Config.race.countdownSeconds + 0.7f;
            m_LastCount = -1;
            m_Snap = true;
            m_Camera.Snap();
            m_Services.Audio.SetPaused(false);
            Go(GameState.Countdown);
            RaceReset?.Invoke();
        }

        public void Restart()
        {
            RestartCount++;
            BeginCountdown();
        }

        public void Pause()
        {
            GameState s = State;
            if (s != GameState.Racing && s != GameState.Countdown) return;
            m_ResumeState = s;
            Go(GameState.Paused);
            m_Services.Audio.SetPaused(true);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            m_Services.Audio.SetPaused(false);
            Go(m_ResumeState);
        }

        public void QuitToMenu(GameState screen = GameState.MainMenu)
        {
            m_Services.Audio.StopEngine();
            m_Services.Audio.SetPaused(false);
            if (!m_Services.States.TryTransition(GameState.MainMenu)) m_Services.States.Force(GameState.MainMenu);
            m_Services.GoToMenu(screen);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            GameState s = State;
            m_Input.Enabled = GameStateMachine.AcceptsSteering(s);
            Sim.Player.SteerScale = m_Services.Settings.steeringSensitivity;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (s == GameState.Paused) Resume();
                else Pause();
                s = State;
            }

            if (s == GameState.Countdown)
            {
                m_Countdown -= dt;
                int count = Mathf.CeilToInt(m_Countdown);
                if (count != m_LastCount && count >= 1 && count <= 3)
                {
                    UI.ShowCountdown(count);
                    m_Services.Audio.Play(Sfx.Countdown);
                }
                m_LastCount = count;
                if (m_Countdown <= 0f)
                {
                    Go(GameState.Racing);
                    UI.ShowGo();
                    m_Services.Audio.Play(Sfx.Go);
                    Haptics.Pulse(30);
                    if (ShowSteerHint) { UI.SetSteerHint(true); m_HintTimer = 7f; }
                }
            }
            else if (GameStateMachine.SimulationRuns(s))
            {
                Sim.Step(dt, m_Input.Steer);
                if (ShowSteerHint && s == GameState.Racing)
                {
                    m_HintTimer -= dt;
                    if (m_HintTimer <= 0f || (m_Input.AnyInputThisFrame && m_HintTimer < 5f)) DismissHint();
                }
                if (s == GameState.Finishing)
                {
                    m_FinishTimer -= dt;
                    if (m_FinishTimer <= 0f) ShowResults();
                }
            }

            bool engine = s == GameState.Countdown || s == GameState.Racing || s == GameState.Finishing;
            RacerState p = Sim.Player;
            float speed01 = s == GameState.Countdown ? 0.12f : Mathf.Clamp01(p.Speed / Mathf.Max(1f, p.Tuning.MaxSpeed));
            if (s != GameState.Paused) m_Services.Audio.SetEngine(engine, speed01, p.Zone != SurfaceZone.Road && s == GameState.Racing);
        }

        private void LateUpdate()
        {
            float dt = State == GameState.Paused ? 0f : Time.deltaTime;
            for (int i = 0; i < m_Views.Length; i++) m_Views[i].Apply(Sim, Sim.Racers[i], dt, m_Snap);
            m_Snap = false;
        }

        private void DismissHint()
        {
            ShowSteerHint = false;
            UI.SetSteerHint(false);
            m_Services.Save.steerHintShown = true;
            m_Services.Persist();
        }

        private void OnRacerFinished(int index)
        {
            if (index != Sim.PlayerIndex) return;
            RacerState p = Sim.Player;
            Go(GameState.Finishing);
            m_FinishTimer = m_Services.Config.race.finishingSeconds;
            LastReward = Progression.ApplyResult(m_Services.Save, m_Services.Config, Track.id, p.FinishOrder, p.FinishTime);
            m_Services.Persist();
            UI.ShowFinish(p.FinishOrder);
            m_Services.Audio.Play(Sfx.Finish);
            Haptics.Pulse(60);
            if (ShowSteerHint) DismissHint();
        }

        private void ShowResults()
        {
            Go(GameState.Results);
            m_Services.Audio.StopEngine();
            UI.ShowResults(LastReward);
            if (LastReward != null && (LastReward.unlockedCars.Count > 0 || LastReward.unlockedTracks.Count > 0)) m_Services.Audio.Play(Sfx.Unlock, 1f, true);
        }

        private void OnPositionChanged(int oldPos, int newPos)
        {
            UI.OnPositionChanged(oldPos, newPos);
            if (newPos < oldPos && State == GameState.Racing) m_Services.Audio.Play(Sfx.Overtake, 0.8f);
        }

        private void OnCheckpoint(int cp)
        {
            if (cp % Track.checkpointsPerLap != 0) m_Services.Audio.Play(Sfx.Checkpoint, 0.5f);
        }

        private void OnLap(int lap)
        {
            UI.ShowLap(lap, Track.laps);
            m_Services.Audio.Play(Sfx.Lap, 0.9f);
        }

        private void OnBarrier(float impact)
        {
            m_Services.Audio.Play(Sfx.Barrier, Mathf.Clamp(impact / 8f, 0.3f, 0.8f));
            Haptics.Pulse(20);
        }

        private void OnBump(int other)
        {
            m_Services.Audio.Play(Sfx.Bump, 0.7f);
            Haptics.Pulse(25);
        }

        private void OnTookOff()
        {
            m_Services.Audio.Play(Sfx.Jump, 0.7f);
        }

        private void OnLanded(float airTime)
        {
            m_Services.Audio.Play(Sfx.Land, Mathf.Clamp(airTime, 0.35f, 0.8f));
            Haptics.Pulse(35);
            if (airTime >= 0.4f && State == GameState.Racing) UI.ShowJump();
        }

        private void OnRespawned(int index)
        {
            if (index == Sim.PlayerIndex) m_Camera.Snap();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && Application.isMobilePlatform) Pause();
        }

        /// <summary>Debug/test: move the player close to the finish line.</summary>
        public void DebugJumpNearFinish(float metresBefore = 60f)
        {
            Sim.Teleport(Sim.PlayerIndex, Sim.Track.RaceDistance - metresBefore, 0f);
            m_Camera.Snap();
            m_Snap = true;
        }
    }
}
