using System.Collections;
using NUnit.Framework;
using TurboRush.Core;
using TurboRush.Race;
using TurboRush.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TurboRush.Tests.PlayMode
{
    /// <summary>Smoke tests on the real Race scene: start, countdown, racing, pause, resume, restart, finish, results, HUD.</summary>
    public class RaceFlowPlayTests
    {
        private RaceManager m_Race;

        private static IEnumerator WaitFor(System.Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnitySetUp]
        public IEnumerator LoadRace()
        {
            Services.Ensure();
            yield return SceneManager.LoadSceneAsync(SceneNames.Race); // wait until the previous race scene is gone
            yield return WaitFor(() => Object.FindAnyObjectByType<RaceManager>() != null, 30f);
            m_Race = Object.FindAnyObjectByType<RaceManager>();
            Assert.NotNull(m_Race, "race scene loads");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CountdownThenRacingThenPauseResume()
        {
            Services s = Services.Instance;
            Assert.AreEqual(GameState.Countdown, s.States.State);
            m_Race.Input.Override = 1f; // holding right during countdown must do nothing
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0f, m_Race.Sim.Clock, "clock frozen in countdown");
            Assert.AreEqual(0f, m_Race.Sim.Player.Speed, "no acceleration in countdown");
            Assert.AreEqual(0f, m_Race.Sim.Player.Steer, "no steering in countdown");
            m_Race.Input.Override = float.NaN;

            yield return WaitFor(() => s.States.State == GameState.Racing, 6f);
            Assert.AreEqual(GameState.Racing, s.States.State);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(m_Race.Sim.Player.Speed, 5f, "car accelerates automatically after GO");
            Assert.Greater(m_Race.Sim.Clock, 1f);

            m_Race.Pause();
            Assert.AreEqual(GameState.Paused, s.States.State);
            float clock = m_Race.Sim.Clock;
            float[] dist = new float[m_Race.Sim.Racers.Length];
            for (int i = 0; i < dist.Length; i++) dist[i] = m_Race.Sim.Racers[i].Distance;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(clock, m_Race.Sim.Clock, "pause stops race time");
            for (int i = 0; i < dist.Length; i++) Assert.AreEqual(dist[i], m_Race.Sim.Racers[i].Distance, "pause stops racer " + i);
            Assert.IsTrue(m_Race.UI.PauseRoot.activeSelf, "pause menu visible");

            m_Race.Resume();
            Assert.AreEqual(GameState.Racing, s.States.State);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(m_Race.Sim.Clock, clock);
            Assert.IsFalse(m_Race.UI.PauseRoot.activeSelf);
        }

        [UnityTest]
        public IEnumerator HudShowsSimulationValues()
        {
            Services s = Services.Instance;
            yield return WaitFor(() => s.States.State == GameState.Racing, 6f);
            yield return new WaitForSeconds(2f);
            yield return null;
            RacerState p = m_Race.Sim.Player;
            Assert.AreEqual(Mathf.RoundToInt(s.Settings.units == Save.SpeedUnit.Mph ? p.Speed * 2.23694f : p.SpeedKmh), m_Race.UI.ShownSpeed, 1);
            Assert.AreEqual(p.Position, m_Race.UI.ShownPosition);
            Assert.AreEqual(m_Race.Sim.Progress(0), m_Race.UI.ShownProgress, 0.01f);
        }

        [UnityTest]
        public IEnumerator FinishShowsResultsAndRestartResetsEverything()
        {
            Services s = Services.Instance;
            yield return WaitFor(() => s.States.State == GameState.Racing, 6f);
            yield return new WaitForSeconds(3f); // let the field leave the grid (the finish line is the start line)
            m_Race.DebugJumpNearFinish(150f);
            yield return WaitFor(() => s.States.State == GameState.Finishing, 15f);
            Assert.AreEqual(GameState.Finishing, s.States.State);
            Assert.IsTrue(m_Race.Sim.Player.Finished);
            Assert.NotNull(m_Race.LastReward);
            yield return WaitFor(() => s.States.State == GameState.Results, 8f);
            Assert.AreEqual(GameState.Results, s.States.State);
            Assert.NotNull(m_Race.UI.ResultsRoot);

            m_Race.Restart();
            yield return null;
            Assert.AreEqual(GameState.Countdown, s.States.State);
            Assert.AreEqual(0f, m_Race.Sim.Clock);
            Assert.IsFalse(m_Race.Sim.Player.Finished);
            foreach (RacerState r in m_Race.Sim.Racers) Assert.Less(r.Distance, 0f, "every racer back on the grid");
            Assert.IsTrue(m_Race.UI.ResultsRoot == null, "results removed");
            Assert.AreEqual(1, Object.FindObjectsByType<RaceManager>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<Services>(FindObjectsSortMode.None).Length);
            yield return WaitFor(() => s.States.State == GameState.Racing, 6f);
            Assert.AreEqual(GameState.Racing, s.States.State, "second race starts");
        }
    }
}
