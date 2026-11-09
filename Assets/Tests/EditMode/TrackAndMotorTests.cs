using NUnit.Framework;
using TurboRush.AI;
using TurboRush.Race;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEngine;

namespace TurboRush.Tests
{
    public static class TestTracks
    {
        /// <summary>Simple rounded-rectangle loop (~1.1 km) with a hill, used by fast unit tests.</summary>
        public static TrackPath Loop() => TrackPath.Build(new[]
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 200), new Vector3(60, 4, 300), new Vector3(200, 6, 300),
            new Vector3(260, 4, 200), new Vector3(260, 0, 0), new Vector3(200, 0, -100), new Vector3(60, 0, -100)
        }, 6f, 10f);

        public static RaceTrack Race(int laps = 2, LapRange[] open = null) => new RaceTrack(Loop(), 7f, 3f, 18f, laps, 8, open);

        public static RaceSimulation Sim(int racers = 6, int laps = 2, LapRange[] open = null, RaceTuning tuning = null)
        {
            var cars = new CarTuning[racers];
            for (int i = 0; i < racers; i++) cars[i] = CarTuning.Default;
            return new RaceSimulation(Race(laps, open), tuning ?? new RaceTuning { racerCount = racers }, AIProfile.CreateDefault(), cars, 0);
        }
    }

    public class TrackPathTests
    {
        [Test]
        public void SamplerReturnsFinitePositionsAndUnitDirections()
        {
            TrackPath p = TestTracks.Loop();
            for (float d = -50f; d < p.Length * 2.2f; d += 3.7f)
            {
                TrackSample s = p.Sample(d);
                Assert.IsFalse(float.IsNaN(s.position.x) || float.IsInfinity(s.position.y) || float.IsNaN(s.position.z), "position at " + d);
                Assert.AreEqual(1f, s.forward.magnitude, 1e-3f);
                Assert.AreEqual(1f, s.right.magnitude, 1e-3f);
                Assert.AreEqual(1f, s.up.magnitude, 1e-3f);
                Assert.AreEqual(0f, Vector3.Dot(s.forward, s.right), 0.02f, "forward ⟂ right");
                Assert.Greater(s.up.y, 0.9f, "up stays mostly up");
                Assert.Greater(Vector3.Dot(Vector3.Cross(s.up, s.forward), s.right), 0.99f, "right-handed frame: right = up × forward");
            }
        }

        [Test]
        public void ArcLengthIsUniformSoSpeedIsConsistent()
        {
            TrackPath p = TestTracks.Loop();
            for (float d = 0; d < p.Length - 2f; d += 1f)
            {
                float step = Vector3.Distance(p.Sample(d).position, p.Sample(d + 1f).position);
                Assert.AreEqual(1f, step, 0.05f, "1 m of progress ≈ 1 m of road at " + d);
            }
        }

        [Test]
        public void LoopClosesSeamlessly()
        {
            TrackPath p = TestTracks.Loop();
            Assert.Less(Vector3.Distance(p.Sample(0f).position, p.Sample(p.Length).position), 0.01f);
            Assert.Less(Vector3.Distance(p.Sample(p.Length - 0.5f).position, p.Sample(-0.5f).position), 0.01f);
            Assert.AreEqual(5f, p.DeltaOnLoop(2f, p.Length - 3f), 1e-3f);
        }

        [Test]
        public void RightTurnsBankRightSideLower()
        {
            TrackPath p = TestTracks.Loop();
            bool found = false;
            for (float d = 0; d < p.Length; d += 2f)
            {
                TrackSample s = p.Sample(d);
                if (s.curvature > 0.01f)
                {
                    found = true;
                    Assert.Greater(s.bank, 0f);
                    Vector3 flatRight = Vector3.Cross(Vector3.up, s.forward).normalized;
                    Assert.Greater(Vector3.Dot(s.up, flatRight), 0f, "normal leans to the inside of the turn");
                    Assert.Less(s.right.y, 0f, "right (inside) edge is lower");
                }
            }
            Assert.IsTrue(found, "test loop has right turns");
        }
    }

    public class MotorTests
    {
        [Test]
        public void ProgressIncreasesWithSpeedTimesDeltaTime()
        {
            RaceSimulation sim = TestTracks.Sim(1);
            RacerState r = sim.Player;
            r.Speed = 20f;
            r.Distance = 10f;
            float before = r.Distance;
            RacerMotor.Step(r, sim.Track, sim.Tuning, 0f, 20f, 0.05f, out _);
            Assert.AreEqual(before + 20f * 0.05f, r.Distance, 1e-3f);
        }

        [Test]
        public void AutoAcceleratesSmoothlyToMaxSpeedWithoutInput()
        {
            RaceSimulation sim = TestTracks.Sim(1, tuning: new RaceTuning { racerCount = 1, cornerDrift = 0f });
            float last = 0f;
            for (int i = 0; i < 60 * 12; i++)
            {
                sim.Step(1f / 60f, 0f);
                Assert.GreaterOrEqual(sim.Player.Speed + 1e-4f, last, "speed never drops on open road without input");
                Assert.LessOrEqual(sim.Player.Speed - last, CarTuning.Default.acceleration / 60f + 1e-3f, "no sudden jumps");
                last = sim.Player.Speed;
            }
            Assert.AreEqual(CarTuning.Default.MaxSpeed, sim.Player.Speed, 0.5f);
        }

        [Test]
        public void SteeringIsBoundedAndReturnsToNeutral()
        {
            RaceSimulation sim = TestTracks.Sim(1);
            RacerState r = sim.Player;
            r.Speed = r.Tuning.MaxSpeed;
            for (int i = 0; i < 120; i++)
            {
                RacerMotor.Step(r, sim.Track, sim.Tuning, 5f, r.Tuning.MaxSpeed, 1f / 60f, out _);
                Assert.LessOrEqual(Mathf.Abs(r.Steer), 1f);
            }
            Assert.AreEqual(1f, r.Steer, 1e-4f);
            for (int i = 0; i < 60; i++) RacerMotor.Step(r, sim.Track, sim.Tuning, 0f, r.Tuning.MaxSpeed, 1f / 60f, out _);
            Assert.AreEqual(0f, r.Steer, 1e-4f, "released steering returns to centre within 1 s");
            // does not snap instantly
            RacerMotor.Step(r, sim.Track, sim.Tuning, 1f, r.Tuning.MaxSpeed, 1f / 60f, out _);
            Assert.Less(r.Steer, 0.2f);
        }

        [Test]
        public void LateralOffsetStaysWithinBarrierLimits()
        {
            RaceSimulation sim = TestTracks.Sim(1);
            sim.Player.Speed = sim.Player.Tuning.MaxSpeed;
            for (int i = 0; i < 60 * 20; i++)
            {
                sim.Step(1f / 60f, i < 600 ? 1f : -1f);
                float lim = sim.Track.LateralLimit(sim.Player.Distance) - sim.Tuning.carWidth * 0.5f + 1e-3f;
                Assert.LessOrEqual(Mathf.Abs(sim.Player.Lateral), lim);
            }
        }

        [Test]
        public void ShoulderSlowsTheCar()
        {
            RaceSimulation sim = TestTracks.Sim(1);
            RacerState r = sim.Player;
            r.Distance = 20f;
            r.Speed = r.Tuning.MaxSpeed;
            r.Lateral = sim.Track.HalfWidth + 1.5f; // on the curb/verge
            for (int i = 0; i < 120; i++) RacerMotor.Step(r, sim.Track, sim.Tuning, 0.0f, r.Tuning.MaxSpeed, 1f / 60f, out _);
            Assert.AreEqual(SurfaceZone.Shoulder, r.Zone);
            Assert.Less(r.Speed, r.Tuning.MaxSpeed * 0.95f);
        }
    }
}
