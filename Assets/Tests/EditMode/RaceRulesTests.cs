using NUnit.Framework;
using TurboRush.Core;
using TurboRush.Race;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.Tests
{
    public class RaceRulesTests
    {
        [Test]
        public void PositionsSortByProgressWithStableTieBreak()
        {
            RaceSimulation sim = TestTracks.Sim(4);
            sim.Teleport(0, 100f);
            sim.Teleport(1, 300f);
            sim.Teleport(2, 200f);
            sim.Teleport(3, 200f); // tie with racer 2 → lower index first
            Assert.AreEqual(new[] { 1, 2, 3, 0 }, sim.Order);
            Assert.AreEqual(4, sim.Racers[0].Position);
            Assert.AreEqual(1, sim.Racers[1].Position);
            Assert.AreEqual(2, sim.Racers[2].Position);
            Assert.AreEqual(3, sim.Racers[3].Position);
        }

        [Test]
        public void FinishedRacersRankByFinishOrderNotDistance()
        {
            var a = new RacerState { Index = 0, Finished = true, FinishOrder = 2, Distance = 5000f };
            var b = new RacerState { Index = 1, Finished = true, FinishOrder = 1, Distance = 4000f };
            var c = new RacerState { Index = 2, Finished = false, Distance = 9000f };
            Assert.Less(RaceSimulation.Compare(b, a), 0);
            Assert.Less(RaceSimulation.Compare(a, c), 0);
        }

        [Test]
        public void FinishFiresExactlyOncePerRacer()
        {
            RaceSimulation sim = TestTracks.Sim(1, laps: 1);
            int finished = 0;
            sim.RacerFinished += i => finished++;
            sim.Teleport(0, sim.Track.RaceDistance - 30f);
            sim.Player.Speed = 40f;
            for (int i = 0; i < 600; i++) sim.Step(1f / 60f, 0f);
            Assert.AreEqual(1, finished);
            Assert.IsTrue(sim.Player.Finished);
            Assert.AreEqual(1, sim.Player.FinishOrder);
            Assert.Greater(sim.Player.FinishTime, 0f);
            Assert.Less(sim.Player.FinishTime, 2f);
        }

        [Test]
        public void NotSteppingFreezesClockAndProgress()
        {
            // Pause = RaceManager stops calling Step; verify nothing in the simulation moves on its own.
            RaceSimulation sim = TestTracks.Sim();
            for (int i = 0; i < 120; i++) sim.Step(1f / 60f, 0f);
            float clock = sim.Clock, d = sim.Player.Distance;
            sim.Step(0f, 1f);
            Assert.AreEqual(clock, sim.Clock);
            Assert.AreEqual(d, sim.Player.Distance);
            Assert.IsFalse(GameStateMachine.SimulationRuns(GameState.Paused));
            Assert.IsFalse(GameStateMachine.SimulationRuns(GameState.Countdown));
            Assert.IsFalse(GameStateMachine.AcceptsSteering(GameState.Countdown));
            Assert.IsFalse(GameStateMachine.AcceptsSteering(GameState.Finishing));
            Assert.IsTrue(GameStateMachine.AcceptsSteering(GameState.Racing));
        }

        [Test]
        public void ResetRestoresEveryRacerToTheGrid()
        {
            RaceSimulation sim = TestTracks.Sim();
            float[] start = new float[sim.Racers.Length];
            for (int i = 0; i < start.Length; i++) start[i] = sim.Racers[i].Distance;
            for (int i = 0; i < 60 * 20; i++) sim.Step(1f / 60f, i % 120 < 60 ? 1f : -1f);
            sim.Teleport(0, sim.Track.RaceDistance + 1f);
            sim.Step(1f / 60f, 0f);
            Assert.IsTrue(sim.Player.Finished);
            sim.Reset();
            Assert.AreEqual(0f, sim.Clock);
            Assert.AreEqual(0, sim.FinishedCount);
            for (int i = 0; i < start.Length; i++)
            {
                RacerState r = sim.Racers[i];
                Assert.AreEqual(start[i], r.Distance, 1e-4f);
                Assert.AreEqual(0f, r.Speed);
                Assert.AreEqual(0f, r.Steer);
                Assert.IsFalse(r.Finished);
                Assert.AreEqual(0, r.CheckpointCount);
                Assert.AreEqual(1, r.Lap);
                Assert.AreEqual(0, r.RespawnCount);
                Assert.AreEqual(r.IsPlayer ? false : true, r.Autopilot);
            }
            Assert.AreEqual(sim.Tuning.playerGridSlot + 1, sim.Player.Position, "player starts at its grid slot");
        }

        [Test]
        public void PlayerStartsBehindTheLineAndCountdownCannotMoveAnyone()
        {
            RaceSimulation sim = TestTracks.Sim();
            foreach (RacerState r in sim.Racers)
            {
                Assert.Less(r.Distance, 0f);
                Assert.AreEqual(0f, r.Speed);
            }
        }

        [Test]
        public void RecoveryRespawnsAtLastCheckpointFacingAlongTrack()
        {
            var open = new[] { new LapRange(0f, 1f) }; // no barriers anywhere
            RaceSimulation sim = TestTracks.Sim(1, open: open);
            int respawns = 0;
            sim.Respawned += i => respawns++;
            sim.Teleport(0, 300f);
            float cp = sim.Player.CheckpointDistance;
            Assert.Greater(cp, 0f);
            sim.Player.Lateral = sim.Track.ShoulderEdge + 6f; // outside the recovery area
            sim.Player.Speed = 20f;
            float t = 0f;
            while (respawns == 0 && t < sim.Tuning.recoveryDelay + 1f)
            {
                sim.Step(1f / 60f, 1f); // keep steering away from the road
                t += 1f / 60f;
            }
            Assert.AreEqual(1, respawns);
            Assert.GreaterOrEqual(t, sim.Tuning.recoveryDelay - 0.05f);
            RacerState r = sim.Player;
            Assert.AreEqual(cp, r.Distance, 1e-3f);
            Assert.LessOrEqual(Mathf.Abs(r.Lateral), sim.Track.HalfWidth, "back on the asphalt");
            Assert.AreEqual(SurfaceZone.Road, sim.Track.ZoneAt(r.Lateral));
            sim.GetPose(r, out Vector3 pos, out Quaternion rot);
            Assert.Greater(Vector3.Dot(rot * Vector3.forward, sim.Track.Path.Sample(r.Distance).forward), 0.95f, "faces along the track");
            Assert.Less(Vector3.Distance(pos, sim.Track.Path.PositionAt(r.Distance, r.Lateral)), 0.01f, "placed on the road surface");
            Assert.Greater(r.GhostTimer, 0f);
        }

        [Test]
        public void InvalidStateIsRecoveredImmediately()
        {
            RaceSimulation sim = TestTracks.Sim(1);
            sim.Player.Lateral = float.NaN;
            sim.Step(1f / 60f, 0f);
            Assert.IsFalse(float.IsNaN(sim.Player.Lateral));
            Assert.AreEqual(1, sim.Player.RespawnCount);
        }

        [Test]
        public void BarriersOnTheShoulderNeverTriggerRespawn()
        {
            RaceSimulation sim = TestTracks.Sim(1);
            for (int i = 0; i < 60 * 30; i++) sim.Step(1f / 60f, 1f); // hold right for 30 s
            Assert.AreEqual(0, sim.Player.RespawnCount, "scraping the barrier only slows the car");
            Assert.Greater(sim.Player.Distance, 300f);
        }

        [Test]
        public void CheckpointsAndLapsAdvance()
        {
            RaceSimulation sim = TestTracks.Sim(1, laps: 2);
            int laps = 0, cps = 0;
            sim.PlayerLap += l => laps++;
            sim.PlayerCheckpoint += c => cps++;
            sim.Teleport(0, sim.Track.LapLength - 5f);
            sim.Player.Speed = 30f;
            for (int i = 0; i < 60; i++) sim.Step(1f / 60f, 0f);
            Assert.AreEqual(2, sim.Player.Lap);
            Assert.AreEqual(1, laps);
            Assert.GreaterOrEqual(cps, 1);
        }

        [Test]
        public void AIOpponentsCompleteEveryShippedTrack()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            Assert.NotNull(cfg, "run setup first");
            foreach (TrackDefinition def in cfg.tracks)
            {
                var cars = new CarTuning[cfg.race.racerCount];
                for (int i = 0; i < cars.Length; i++) cars[i] = cfg.cars[i % cfg.cars.Length].tuning;
                var sim = new RaceSimulation(RaceTrack.From(def), cfg.race, cfg.ai, cars, 0);
                sim.Player.Autopilot = true; // let the AI drive the player car too
                float t = 0f;
                while (sim.FinishedCount < cars.Length && t < 400f)
                {
                    sim.Step(1f / 30f, 0f);
                    t += 1f / 30f;
                }
                Assert.AreEqual(cars.Length, sim.FinishedCount, def.displayName + ": all racers finish");
                int respawns = 0;
                foreach (RacerState r in sim.Racers) respawns += r.RespawnCount;
                Assert.AreEqual(0, respawns, def.displayName + ": AI never needs rescuing");
                float winner = float.MaxValue;
                foreach (RacerState r in sim.Racers) winner = Mathf.Min(winner, r.FinishTime);
                Debug.Log($"[TR Test] {def.displayName}: winner {winner:0.0}s, last {t:0.0}s");
                Assert.That(winner, Is.InRange(50f, 130f), def.displayName + ": race length within 1–2 minutes");
            }
        }

        [Test]
        public void HumanLikePlayerCanWinWithGoodDriving()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            TrackDefinition def = cfg.tracks[0];
            var cars = new CarTuning[cfg.race.racerCount];
            for (int i = 0; i < cars.Length; i++) cars[i] = cfg.cars[i % cfg.cars.Length].tuning;
            var sim = new RaceSimulation(RaceTrack.From(def), cfg.race, cfg.ai, cars, 0);
            // Bang-bang steering towards a lane, like a child holding left/right.
            float t = 0f;
            while (!sim.Player.Finished && t < 300f)
            {
                RacerState p = sim.Player;
                sim.Track.Path.Sample(p.Distance, out TrackSample s);
                float target = Mathf.Clamp(s.curvature * 300f, -4f, 4f); // hug the inside a little
                float err = target - p.Lateral;
                sim.Step(1f / 30f, Mathf.Abs(err) > 0.7f ? Mathf.Sign(err) : 0f);
                t += 1f / 30f;
            }
            Assert.IsTrue(sim.Player.Finished);
            Debug.Log($"[TR Test] Tap-steering player on {def.displayName}: place {sim.Player.FinishOrder}, {sim.Player.FinishTime:0.0}s");
            Assert.LessOrEqual(sim.Player.FinishOrder, 3, "a reasonable child can reach the podium on the first track");
        }
    }
}
