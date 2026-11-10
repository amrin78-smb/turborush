using System.Collections;
using System.IO;
using System.Text;
using TurboRush.Race;
using TurboRush.UI;
using UnityEngine;

namespace TurboRush.Core
{
    /// <summary>
    /// Automated play-through for desktop test players (-tr-autopilot): visits every menu screen, races (steering
    /// left/right like a child would), pauses, resumes, finishes, restarts, returns to the menu, captures
    /// screenshots and writes autopilot_report.txt. Never active on Android.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        private readonly StringBuilder m_Report = new StringBuilder();
        private int m_Errors;
        private int m_Fail;
        private string m_Dir;

        private void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                m_Errors++;
                m_Report.AppendLine("LOG-ERROR " + msg);
            }
        }

        private void Check(bool ok, string what)
        {
            if (!ok) m_Fail++;
            m_Report.AppendLine((ok ? "PASS " : "FAIL ") + what);
            Debug.Log("[TR AutoPilot] " + (ok ? "PASS " : "FAIL ") + what);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string tag = string.IsNullOrEmpty(DebugArgs.Tag) ? "" : DebugArgs.Tag + "_";
            string path = Path.Combine(m_Dir, tag + name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            m_Report.AppendLine("SHOT " + path);
        }

        private static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private IEnumerator Start()
        {
            m_Dir = string.IsNullOrEmpty(DebugArgs.ShotDir) ? Path.Combine(Application.persistentDataPath, "autopilot") : DebugArgs.ShotDir;
            Directory.CreateDirectory(m_Dir);
            Services s = Services.Instance;
            m_Report.AppendLine($"Turbo Rush 3D autopilot  {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}  screen {Screen.width}x{Screen.height}  " +
                                $"aspect {(float)Screen.width / Screen.height:0.000}  match={AdaptiveCanvasScaler.MatchFor(Screen.width, Screen.height)}  safe-inset={SafeAreaFitter.SimulatedInsets}");

            MenuController menu = null;
            float t0 = Time.realtimeSinceStartup;
            while ((menu = FindAnyObjectByType<MenuController>()) == null && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            Check(menu != null, "menu scene loaded");
            if (menu == null) { Finish(); yield break; }
            yield return Wait(1.5f);
            Check(s.States.State == GameState.MainMenu, "state MainMenu");
            yield return Shot("01_main_menu");
            menu.Show(GameState.VehicleSelect);
            yield return Wait(1f);
            Check(s.States.State == GameState.VehicleSelect, "state VehicleSelect");
            yield return Shot("02_garage");
            menu.Show(GameState.TrackSelect);
            yield return Wait(1f);
            Check(s.States.State == GameState.TrackSelect, "state TrackSelect");
            yield return Shot("03_tracks");
            menu.Show(GameState.MainMenu);
            menu.OpenSettings();
            yield return Wait(0.8f);
            yield return Shot("04_settings");
            menu.SettingsRoot.SetActive(false);

            var track = string.IsNullOrEmpty(DebugArgs.Track) ? s.SelectedTrack : s.Config.FindTrack(DebugArgs.Track);
            s.StartRace(track);
            RaceManager race = null;
            t0 = Time.realtimeSinceStartup;
            while ((race = FindAnyObjectByType<RaceManager>()) == null && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            Check(race != null, "race scene loaded (" + (track != null ? track.displayName : "?") + ")");
            if (race == null) { Finish(); yield break; }
            yield return Wait(1.3f);
            Check(s.States.State == GameState.Countdown, "state Countdown");
            Check(race.Sim.Player.Speed == 0f && race.Sim.Clock == 0f, "no progress during countdown");
            yield return Shot("05_countdown");
            t0 = Time.realtimeSinceStartup;
            while (s.States.State != GameState.Racing && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Check(s.States.State == GameState.Racing, "state Racing after GO");

            // Drive: weave between lanes, steering with the same -1/0/+1 intent as touch input.
            float raceStart = Time.realtimeSinceStartup;
            int frames = 0;
            float worst = 0f;
            bool shotRacing = false, paused = false, jumped = false, airShot = false, rampShot = false;
            int takeoffs = 0, landings = 0;
            float maxAir = 0f, maxHeight = 0f;
            System.Action onTakeoff = () => takeoffs++;
            System.Action<float> onLanded = a => { landings++; maxAir = Mathf.Max(maxAir, a); };
            race.Sim.PlayerTookOff += onTakeoff;
            race.Sim.PlayerLanded += onLanded;
            float targetLane = 2.5f;
            while (s.States.State == GameState.Racing && Time.realtimeSinceStartup - raceStart < 400f)
            {
                float el = Time.realtimeSinceStartup - raceStart;
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                if (Mathf.FloorToInt(el / 4f) % 2 == 0) targetLane = 3f; else targetLane = -3f;
                float err = targetLane - race.Sim.Player.Lateral;
                race.Input.Override = Mathf.Abs(err) > 0.8f ? Mathf.Sign(err) : 0f;
                if (!shotRacing && el > 7f)
                {
                    shotRacing = true;
                    yield return Shot("06_racing");
                }
                if (!paused && el > 9f)
                {
                    paused = true;
                    float clock = race.Sim.Clock, dist = race.Sim.Player.Distance;
                    race.Pause();
                    yield return Wait(1.2f);
                    Check(s.States.State == GameState.Paused, "state Paused");
                    Check(Mathf.Approximately(clock, race.Sim.Clock) && Mathf.Approximately(dist, race.Sim.Player.Distance), "pause freezes clock and progress");
                    yield return Shot("07_pause");
                    race.Resume();
                    yield return null;
                    Check(s.States.State == GameState.Racing, "resume returns to Racing");
                }
                if (DebugArgs.Fast && !jumped && el > 14f)
                {
                    jumped = true;
                    race.DebugJumpNearFinish(150f);
                }
                if (el > 12f && el < 12.1f) yield return Shot("06b_racing");
                maxHeight = Mathf.Max(maxHeight, race.Sim.Player.Height);
                if (!rampShot && race.Sim.Track.JumpCount > 0)
                {
                    float toLip = race.Sim.Track.Path.DeltaOnLoop(race.Sim.Track.JumpLip(0), race.Sim.Player.Distance);
                    if (toLip > 30f && toLip < 42f)
                    {
                        rampShot = true;
                        yield return Shot("06d_ramp");
                    }
                }
                if (!airShot && race.Sim.Player.Airborne && race.Sim.Player.AirTime > 0.15f && race.Sim.Player.Height > 0.9f)
                {
                    airShot = true;
                    yield return Shot("06c_jump");
                }
                yield return null;
            }
            race.Input.Override = float.NaN;
            race.Sim.PlayerTookOff -= onTakeoff;
            race.Sim.PlayerLanded -= onLanded;
            if (race.Sim.Track.JumpCount > 0)
            {
                m_Report.AppendLine($"Jumps: {takeoffs} take-offs, {landings} landings, longest air time {maxAir:0.00}s, max height {maxHeight:0.00} m");
                if (!DebugArgs.Fast) Check(takeoffs > 0 && landings == takeoffs && maxHeight < 3f, "player jumped and landed safely");
            }
            float raceSeconds = Time.realtimeSinceStartup - raceStart;
            m_Report.AppendLine($"Race wall time {raceSeconds:0.0}s, sim clock {race.Sim.Clock:0.0}s, frames {frames}, avg FPS {frames / Mathf.Max(0.01f, raceSeconds):0.0}, worst frame {worst * 1000f:0}ms (desktop xvfb software GL: NOT representative of phones)");
            Check(s.States.State == GameState.Finishing, "player finished → Finishing");
            Check(race.Sim.Player.Finished && race.Sim.Player.FinishTime > 0f, $"finish time recorded ({race.Sim.Player.FinishTime:0.00}s, place {race.Sim.Player.FinishOrder}, respawns {race.Sim.Player.RespawnCount})");
            t0 = Time.realtimeSinceStartup;
            while (s.States.State != GameState.Results && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Check(s.States.State == GameState.Results, "state Results");
            yield return Wait(1.5f);
            yield return Shot("08_results");
            int aiFinished = 0;
            foreach (RacerState r in race.Sim.Racers) if (!r.IsPlayer && r.Finished) aiFinished++;
            m_Report.AppendLine($"AI finished by results screen: {aiFinished}/{race.Sim.Racers.Length - 1}");

            race.Restart();
            yield return Wait(0.5f);
            Check(s.States.State == GameState.Countdown && race.Sim.Clock == 0f && race.Sim.Player.Distance < 0f && !race.Sim.Player.Finished, "restart resets race to countdown");
            Check(FindObjectsByType<RaceManager>(FindObjectsSortMode.None).Length == 1, "no duplicate race managers");
            Check(GameObject.Find("Results") == null, "results screen removed on restart");
            yield return Wait(4.5f);
            Check(s.States.State == GameState.Racing, "racing again after restart");
            race.Pause();
            yield return Wait(0.3f);
            race.QuitToMenu();
            t0 = Time.realtimeSinceStartup;
            while (FindAnyObjectByType<MenuController>() == null && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return Wait(1f);
            Check(s.States.State == GameState.MainMenu, "back at MainMenu");
            Check(FindObjectsByType<Services>(FindObjectsSortMode.None).Length == 1, "single Services instance");
            Finish();
        }

        private void Finish()
        {
            Check(m_Errors == 0, "no errors/exceptions logged (" + m_Errors + ")");
            m_Report.AppendLine(m_Fail == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + m_Fail + ")");
            string tag = string.IsNullOrEmpty(DebugArgs.Tag) ? "" : DebugArgs.Tag + "_";
            File.WriteAllText(Path.Combine(m_Dir, tag + "autopilot_report.txt"), m_Report.ToString());
            Debug.Log("[TR AutoPilot] done: " + (m_Fail == 0 ? "PASS" : "FAIL"));
            Application.Quit(m_Fail == 0 ? 0 : 3);
        }
    }
}
