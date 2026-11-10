using NUnit.Framework;
using TurboRush.AI;
using TurboRush.Core;
using TurboRush.Race;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.Tests
{
    public class JumpAndSlopeTests
    {
        private const float Lip = 120f; // on the 200 m start straight of TestTracks.Loop()

        private static RaceSimulation JumpSim(int racers = 1, RaceTuning tuning = null)
        {
            TrackPath path = TestTracks.Loop();
            var jumps = new[] { new JumpRamp(Lip / path.Length, 10f, 0.7f) };
            var track = new RaceTrack(path, 7f, 3f, 18f, 2, 8, null, jumps);
            var cars = new CarTuning[racers];
            for (int i = 0; i < racers; i++) cars[i] = CarTuning.Default;
            return new RaceSimulation(track, tuning ?? new RaceTuning { racerCount = racers }, AIProfile.CreateDefault(), cars, 0);
        }

        private struct Flight { public bool tookOff, landed; public float maxHeight, airTime, takeoffAt, landAt, speedAfter; }

        private static Flight Fly(RaceSimulation sim, RacerState r, float startSpeed, float lateral, float steer = 0f, float seconds = 3f)
        {
            sim.Teleport(r.Index, Lip - 40f, lateral);
            r.Speed = startSpeed;
            var f = new Flight();
            for (int i = 0; i < seconds * 60; i++)
            {
                RacerMotor.Step(r, sim.Track, sim.Tuning, steer, startSpeed, 1f / 60f, out _);
                Assert.IsFalse(float.IsNaN(r.Height) || float.IsInfinity(r.Height), "height stays finite");
                f.maxHeight = Mathf.Max(f.maxHeight, r.Height);
                if (r.TookOff) { f.tookOff = true; f.takeoffAt = r.Distance; }
                if (r.LandingImpact > 0f) { f.landed = true; f.landAt = r.Distance; f.airTime = r.AirTime; f.speedAfter = r.Speed; }
            }
            return f;
        }

        [Test]
        public void RampProfileRisesSmoothlyToTheLip()
        {
            Assert.AreEqual(0f, JumpRamp.Profile(0f, 0.7f), 1e-5f);
            Assert.AreEqual(0.7f, JumpRamp.Profile(1f, 0.7f), 1e-5f);
            Assert.AreEqual(0f, JumpRamp.Profile(1.01f, 0.7f), 1e-5f, "flat again past the lip");
            float prev = 0f;
            for (float u = 0.05f; u <= 1f; u += 0.05f)
            {
                float h = JumpRamp.Profile(u, 0.7f);
                Assert.Greater(h, prev, "monotonic at " + u);
                prev = h;
            }
        }

        [Test]
        public void SurfaceHeightCoversOnlyTheAsphaltOfTheRamp()
        {
            RaceTrack t = JumpSim().Track;
            Assert.AreEqual(1, t.JumpCount);
            Assert.AreEqual(Lip, t.JumpLip(0), 0.01f);
            Assert.Greater(t.SurfaceHeight(Lip - 1f, 0f), 0.5f, "on the ramp");
            Assert.Greater(t.SurfaceHeight(Lip - 1f + t.LapLength, 3f), 0.5f, "on the ramp on lap 2");
            Assert.AreEqual(0f, t.SurfaceHeight(Lip - 1f, 8f), "the shoulder is not ramped");
            Assert.AreEqual(0f, t.SurfaceHeight(Lip + 1f, 0f), "flat after the lip");
            Assert.AreEqual(0f, t.SurfaceHeight(Lip - 20f, 0f), "flat before the ramp");
        }

        [Test]
        public void FullSpeedJumpIsGentleAndLandsSafely()
        {
            RaceSimulation sim = JumpSim();
            RacerState r = sim.Player;
            float v = r.Tuning.MaxSpeed;
            Flight f = Fly(sim, r, v, 0f);
            Assert.IsTrue(f.tookOff, "takes off at the lip");
            Assert.AreEqual(Lip, f.takeoffAt, 2f, "take-off happens at the lip");
            Assert.IsTrue(f.landed, "lands again");
            Assert.That(f.airTime, Is.InRange(0.4f, 1.0f), "short, gentle air time");
            Assert.That(f.maxHeight, Is.InRange(0.8f, 2f), "gentle height");
            Assert.That(f.landAt - Lip, Is.InRange(12f, 45f), "lands on the road ahead");
            Assert.Greater(f.speedAfter, v * 0.95f, "landing barely slows the car");
            Assert.IsFalse(r.Airborne);
            Assert.AreEqual(0f, r.Height, 1e-4f, "back on the road");
        }

        [Test]
        public void SlowCarOnlyHops()
        {
            RaceSimulation sim = JumpSim();
            Flight f = Fly(sim, sim.Player, 10f, 0f, 0f, 6f);
            Assert.IsTrue(f.landed);
            Assert.Less(f.airTime, 0.4f, "slow cars just drop off the lip");
        }

        [Test]
        public void CarOnTheShoulderBesideTheRampDoesNotJump()
        {
            RaceSimulation sim = JumpSim();
            RacerState r = sim.Player;
            Flight f = Fly(sim, r, r.Tuning.MaxSpeed, 8.5f);
            Assert.IsFalse(f.tookOff);
            Assert.AreEqual(0f, f.maxHeight, 1e-4f);
        }

        [Test]
        public void SteeringIsSoftenedInTheAir()
        {
            RaceSimulation sim = JumpSim();
            RacerState r = sim.Player;
            sim.Teleport(0, 20f, 0f);
            r.Speed = r.Tuning.MaxSpeed;
            r.LateralVelocity = 0f;
            r.Steer = 0f;
            for (int i = 0; i < 12; i++) RacerMotor.Step(r, sim.Track, sim.Tuning, 1f, r.Speed, 1f / 60f, out _);
            float ground = r.LateralVelocity;

            sim.Teleport(0, 20f, 0f);
            r.Speed = r.Tuning.MaxSpeed;
            r.LateralVelocity = 0f;
            r.Steer = 0f;
            r.Airborne = true;
            r.Height = 5f;
            r.VerticalSpeed = 20f; // stays up for the whole measurement
            for (int i = 0; i < 12; i++) RacerMotor.Step(r, sim.Track, sim.Tuning, 1f, r.Speed, 1f / 60f, out _);
            Assert.IsTrue(r.Airborne);
            Assert.Less(Mathf.Abs(r.LateralVelocity), Mathf.Abs(ground) * 0.6f, "less steering authority in the air");
            Assert.Greater(Mathf.Abs(r.LateralVelocity), 0f, "but still some");
        }

        [Test]
        public void CarsFlyingOverEachOtherDoNotCollide()
        {
            RaceSimulation sim = JumpSim(2);
            RacerState a = sim.Racers[0], b = sim.Racers[1];
            sim.Teleport(0, 60f, 0f);
            sim.Teleport(1, 60f, 0.2f);
            a.Speed = b.Speed = 30f;
            a.Airborne = true;
            a.Height = 2.5f;
            a.VerticalSpeed = 3f;
            sim.Step(1f / 60f, 0f);
            Assert.Less(Mathf.Abs(a.Lateral - b.Lateral), 0.6f, "no sideways push between a flying car and one below");

            a.Airborne = false;
            a.Height = 0f;
            a.VerticalSpeed = 0f;
            sim.Teleport(0, 60f, 0f);
            sim.Teleport(1, 60f, 0.2f);
            sim.Step(1f / 60f, 0f);
            Assert.Greater(Mathf.Abs(a.Lateral - b.Lateral), 1.5f, "cars at the same height still collide");
        }

        [Test]
        public void RespawnAndRestartClearJumpState()
        {
            RaceSimulation sim = JumpSim();
            RacerState r = sim.Player;
            r.Airborne = true;
            r.Height = 1.5f;
            r.VerticalSpeed = 2f;
            sim.Respawn(r);
            Assert.IsFalse(r.Airborne);
            Assert.AreEqual(0f, r.Height);
            r.Airborne = true;
            r.Height = 1f;
            sim.Reset();
            Assert.IsFalse(r.Airborne);
            Assert.AreEqual(0f, r.Height);
        }

        [Test]
        public void UphillIsSlowerAndDownhillFasterThanFlat()
        {
            float Settle(float distance)
            {
                RaceSimulation sim = TestTracks.Sim(1);
                RacerState r = sim.Player;
                r.Speed = r.Tuning.MaxSpeed;
                for (int i = 0; i < 600; i++)
                {
                    sim.Teleport(0, distance, 0f);
                    RacerMotor.Step(r, sim.Track, sim.Tuning, 0f, r.Tuning.MaxSpeed, 1f / 60f, out _);
                }
                return r.Speed;
            }
            RaceSimulation probe = TestTracks.Sim(1);
            TrackPath p = probe.Track.Path;
            float up = 0f, down = 0f, flat = 100f, gUp = 0f, gDown = 0f;
            for (float d = 0; d < p.Length; d += 2f)
            {
                float g = p.Sample(d).forward.y;
                if (g > gUp) { gUp = g; up = d; }
                if (g < gDown) { gDown = g; down = d; }
            }
            Assert.Greater(gUp, 0.015f, "test track has an uphill");
            Assert.Less(gDown, -0.015f, "test track has a downhill");
            Assert.AreEqual(0f, p.Sample(flat).forward.y, 0.005f, "flat reference point");
            float vFlat = Settle(flat), vUp = Settle(up), vDown = Settle(down);
            Assert.Less(vUp, vFlat * 0.995f, "uphill slower");
            Assert.Greater(vDown, vFlat * 1.005f, "downhill faster");
            Assert.Greater(vUp, vFlat * 0.8f, "but never a crawl");
        }

        [Test]
        public void ShippedJumpsAreOnStraightsAndAITakesThemCleanly()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            Assert.NotNull(cfg, "run setup first");
            int tracksWithJumps = 0;
            foreach (TrackDefinition def in cfg.tracks)
            {
                if (def.jumps == null || def.jumps.Length == 0) continue;
                tracksWithJumps++;
                var cars = new CarTuning[cfg.race.racerCount];
                for (int i = 0; i < cars.Length; i++) cars[i] = cfg.cars[i % cfg.cars.Length].tuning;
                var sim = new RaceSimulation(RaceTrack.From(def), cfg.race, cfg.ai, cars, 0);
                sim.Player.Autopilot = true;
                int landings = 0;
                float maxHeight = 0f;
                // Step at the simulation's sub-step size: LandingImpact only lives for the sub-step the car lands in.
                for (float t = 0f; t < 400f && sim.FinishedCount < cars.Length; t += RaceSimulation.MaxSubStep)
                {
                    sim.Step(RaceSimulation.MaxSubStep, 0f);
                    foreach (RacerState r in sim.Racers)
                    {
                        maxHeight = Mathf.Max(maxHeight, r.Height);
                        if (r.LandingImpact > 0f && r.AirTime > 0.3f) landings++;
                    }
                }
                Assert.AreEqual(cars.Length, sim.FinishedCount, def.displayName + ": everyone finishes");
                int expected = def.jumps.Length * def.laps * cars.Length;
                Assert.GreaterOrEqual(landings, expected * 0.8f, $"{def.displayName}: most racers fly every jump ({landings}/{expected})");
                Assert.Less(maxHeight, 2.5f, def.displayName + ": jumps stay gentle");
            }
            Assert.GreaterOrEqual(tracksWithJumps, 1, "at least one shipped track has jumps");
        }
    }
}
