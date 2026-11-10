using System.Text;
using NUnit.Framework;
using TurboRush.AI;
using TurboRush.Core;
using TurboRush.Race;
using TurboRush.Save;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.Tests
{
    public class PowerUpTests
    {
        private const float Row = 100f;  // metres from the line, on the 200 m start straight of TestTracks.Loop()
        private const float Pad = 140f;

        private static RaceSimulation Sim(int racers = 1, Difficulty difficulty = Difficulty.Easy)
        {
            TrackPath path = TestTracks.Loop();
            var rows = new[] { new PowerUpRow(Row / path.Length, PowerUpKind.Turbo, PowerUpKind.Shield, PowerUpKind.Turbo) };
            var pads = new[] { new BoostPad(Pad / path.Length, 0f) };
            var track = new RaceTrack(path, 7f, 3f, 18f, 2, 8, null, null, rows, pads);
            var cars = new CarTuning[racers];
            for (int i = 0; i < racers; i++) cars[i] = CarTuning.Default;
            return new RaceSimulation(track, new RaceTuning { racerCount = racers }, AIProfile.CreateDefault(), cars, 0, difficulty);
        }

        /// <summary>Drives the player straight along a lane (player steering only) for the given time.</summary>
        private static void Drive(RaceSimulation sim, float seconds, float lane)
        {
            RacerState p = sim.Player;
            for (float t = 0f; t < seconds; t += RaceSimulation.MaxSubStep)
            {
                float steer = Mathf.Clamp((lane - p.Lateral) * 0.6f - p.LateralVelocity * 0.15f, -1f, 1f);
                sim.Step(RaceSimulation.MaxSubStep, steer);
            }
        }

        private static float Lane(int slot) => PowerUpRow.SlotLane(slot) * 7f;

        [Test]
        public void TurboPickupLiftsSpeedAboveTopSpeedThenSettlesBack()
        {
            RaceSimulation sim = Sim();
            RacerState p = sim.Player;
            float max = p.Tuning.MaxSpeed;
            sim.Teleport(0, Row - 30f, Lane(0));
            p.Speed = max;
            int collected = -1;
            sim.PowerUpCollected += (r, slot) => collected = slot;
            float peak = 0f;
            for (float t = 0f; t < 3f; t += RaceSimulation.MaxSubStep)
            {
                sim.Step(RaceSimulation.MaxSubStep, 0f);
                peak = Mathf.Max(peak, p.Speed);
            }
            Assert.AreEqual(0, collected, "drove through the left turbo");
            Assert.AreEqual(1, p.PowerUpsCollected);
            Assert.Greater(peak, max * 1.2f, "turbo goes well past top speed");
            Assert.LessOrEqual(peak, max * sim.Tuning.turboSpeedFactor * 1.02f, "but never beyond the turbo factor (+ the gentle downhill)");
            Assert.IsFalse(sim.PowerUps.Available(0, 0), "collected power-up is gone for now");
            Drive(sim, 6f, Lane(0));
            Assert.Less(p.Speed, max * 1.01f, "back to normal top speed");
            Assert.IsTrue(sim.PowerUps.Available(0, 0), "it comes back after the respawn time");
        }

        [Test]
        public void PickupsNeedTheRightLaneAndArePersonal()
        {
            RaceSimulation sim = Sim(2);
            RacerState a = sim.Racers[0], b = sim.Racers[1];
            // Between the centre and right slots (2 m from each): misses both.
            float prev = Row - 0.3f;
            a.Distance = Row + 0.2f;
            a.Lateral = (Lane(1) + Lane(2)) * 0.5f;
            Assert.AreEqual(-1, sim.PowerUps.Collect(a, prev, out _), "nothing in between the lanes");

            // Racer 0 takes the centre shield. It is gone for racer 0, but racer 1 right behind still gets its own.
            a.Lateral = Lane(1);
            int slot = sim.PowerUps.Collect(a, prev, out _);
            Assert.AreEqual(PowerUpKind.Shield, sim.PowerUps.Kind[slot]);
            Assert.IsTrue(a.Shielded);
            Assert.AreEqual(-1, sim.PowerUps.Collect(a, prev, out _), "racer 0 already took it");
            b.Distance = Row + 0.2f;
            b.Lateral = Lane(1);
            Assert.AreEqual(slot, sim.PowerUps.Collect(b, prev, out _), "the car behind still gets its own");
            sim.PowerUps.Tick(sim.Tuning.pickupRespawnSeconds + 0.01f);
            Assert.AreEqual(slot, sim.PowerUps.Collect(a, prev, out _), "back for racer 0 after the respawn time");
        }

        [Test]
        public void ACarFlyingHighOverAPickupMissesIt()
        {
            RaceSimulation sim = Sim();
            RacerState p = sim.Player;
            p.Lateral = Lane(0);
            p.Distance = Row + 0.5f;
            p.Height = 2f;
            Assert.AreEqual(-1, sim.PowerUps.Collect(p, Row - 0.5f, out _));
            p.Height = 0.4f;
            Assert.AreEqual(0, sim.PowerUps.Collect(p, Row - 0.5f, out _));
        }

        [Test]
        public void BoostPadGivesAShortBurstOnlyOnThePad()
        {
            RaceSimulation sim = Sim();
            RacerState p = sim.Player;
            float max = p.Tuning.MaxSpeed;
            int pads = 0;
            sim.PlayerBoostPad += () => pads++;
            sim.Teleport(0, Pad - 20f, 0f);
            p.Speed = max;
            float peak = 0f;
            for (float t = 0f; t < 2f; t += RaceSimulation.MaxSubStep)
            {
                sim.Step(RaceSimulation.MaxSubStep, 0f);
                peak = Mathf.Max(peak, p.Speed);
            }
            Assert.AreEqual(1, pads, "one pad event per pass");
            Assert.AreEqual(1, p.PadsHit);
            Assert.Greater(peak, max * 1.12f);
            Assert.LessOrEqual(peak, max * sim.Tuning.padSpeedFactor * 1.02f);

            RaceSimulation miss = Sim();
            miss.Teleport(0, Pad - 20f, 4.5f);
            miss.Player.Speed = max;
            Drive(miss, 2f, 4.5f);
            Assert.AreEqual(0, miss.Player.PadsHit, "a car beside the pad gets nothing");
        }

        [Test]
        public void ShieldKeepsSpeedOnBarrierScrapes()
        {
            float Scrape(bool shield)
            {
                RaceSimulation sim = Sim();
                RacerState p = sim.Player;
                sim.Teleport(0, 20f, 4f);
                p.Speed = p.Tuning.MaxSpeed;
                if (shield) p.ShieldTimer = 5f;
                int blocks = 0;
                sim.PlayerShieldBlock += () => blocks++;
                for (int i = 0; i < 75; i++) sim.Step(1f / 60f, 1f); // steer hard into the right barrier
                if (shield) Assert.Greater(blocks, 0, "shield block event");
                return p.Speed;
            }
            float plain = Scrape(false), shielded = Scrape(true);
            Assert.Greater(shielded, plain + 1f, $"shield keeps speed on the barrier ({shielded:0.0} vs {plain:0.0} m/s)");
        }

        [Test]
        public void ShieldedCarNudgesTheCarAheadInsteadOfSlowingDown()
        {
            float RearSpeedAfterBump(bool shield, out float frontKick)
            {
                RaceSimulation sim = Sim(2);
                RacerState rear = sim.Racers[0], front = sim.Racers[1];
                front.Autopilot = false; // keep both on a fixed line for the test
                sim.Teleport(0, 60f, 0f);
                sim.Teleport(1, 63.5f, 0.3f);
                rear.Speed = 40f;
                front.Speed = 25f;
                if (shield) rear.ShieldTimer = 5f;
                sim.Step(1f / 60f, 0f);
                frontKick = Mathf.Abs(front.LateralVelocity);
                return rear.Speed;
            }
            float plain = RearSpeedAfterBump(false, out float kickPlain);
            float shielded = RearSpeedAfterBump(true, out float kickShield);
            Assert.Less(plain, 25f * 0.95f, "normal bump: the rear car drops to the front car's speed");
            Assert.Greater(shielded, 39f, "shielded: the rear car keeps its speed");
            Assert.Greater(kickShield, kickPlain + 1f, "and the car ahead is nudged aside");
        }

        [Test]
        public void RestartAndRespawnClearBoosts()
        {
            RaceSimulation sim = Sim();
            RacerState p = sim.Player;
            p.TurboTimer = 2f;
            p.PadTimer = 1f;
            sim.Respawn(p);
            Assert.IsFalse(p.Boosting, "no boost straight after a respawn");
            p.ShieldTimer = 3f;
            p.TurboTimer = 1f;
            sim.PowerUps.Cooldown[0] = 3f;
            sim.Reset();
            Assert.IsFalse(p.Boosting || p.Shielded);
            Assert.AreEqual(0, p.PowerUpsCollected);
            Assert.IsTrue(sim.PowerUps.Available(0, 0), "every power-up is back on restart");
        }

        [Test]
        public void DifficultyRaisesAISpeedAndEasyKeepsTheOriginalField()
        {
            AIProfile ai = AIProfile.CreateDefault();
            RaceSimulation easy = Sim(6, Difficulty.Easy), normal = Sim(6, Difficulty.Normal), hard = Sim(6, Difficulty.Hard);
            for (int i = 1; i < 6; i++)
            {
                Assert.AreEqual(ai.skill[i - 1], easy.Racers[i].Skill, 1e-5f, "Easy is the original 0.1–0.3 field");
                Assert.Greater(normal.Racers[i].Skill, easy.Racers[i].Skill);
                Assert.Greater(hard.Racers[i].Skill, normal.Racers[i].Skill);
                Assert.Less(hard.Racers[i].Skill, 1.06f, "even Hard stays within a few % of the player's top speed");
            }
            Assert.AreEqual(0f, ai.hard.maxSlowdown, "Hard: leaders never wait for the player");
            Assert.Greater(ai.easy.maxSlowdown, ai.normal.maxSlowdown);
        }

        [Test]
        public void OldSavesLoadAsNormalAndBadValuesAreRepaired()
        {
            SaveData d = SaveDataService.Parse("{\"version\":2,\"settings\":{\"music\":true}}", out _);
            Assert.AreEqual(Difficulty.Normal, d.settings.difficulty);
            d.settings.difficulty = (Difficulty)7;
            d.Sanitize();
            Assert.AreEqual(Difficulty.Normal, d.settings.difficulty);
        }

        [Test]
        public void ShippedTracksHavePowerUpsTheAICanReach()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            Assert.NotNull(cfg, "run setup first");
            foreach (TrackDefinition def in cfg.tracks)
            {
                Assert.GreaterOrEqual(def.powerUps.Length, 2, def.displayName + ": power-up rows");
                Assert.GreaterOrEqual(def.boostPads.Length, 2, def.displayName + ": boost pads");
                int easy = Balance.Run(cfg, def, Difficulty.Easy, Balance.Bot.None).aiPowerUps;
                int hard = Balance.Run(cfg, def, Difficulty.Hard, Balance.Bot.None).aiPowerUps;
                Assert.Greater(hard, easy, $"{def.displayName}: AI goes for more power-ups on Hard ({hard} vs {easy})");
            }
        }

        /// <summary>
        /// Balance report: a scripted "skilled player" bot (full speed, clean lines) races every track at every
        /// difficulty, once ignoring power-ups and once going for them. Writes Logs/balance_report.txt.
        /// </summary>
        [Test]
        public void BalanceEasyIsWinnableAndHardIsHarder()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            Assert.NotNull(cfg, "run setup first");
            var sb = new StringBuilder("track,difficulty,bot,player_pos,player_time,winner_time,ai_mean_time,player_powerups,player_pads,ai_powerups,player_bumps\n");
            var checks = new System.Collections.Generic.List<System.Action>();
            foreach (TrackDefinition def in cfg.tracks)
            {
                Balance.Result easySeek = default, hardSeek = default, easyNone = default, hardNone = default;
                foreach (Difficulty d in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
                    foreach (Balance.Bot bot in new[] { Balance.Bot.Lanes, Balance.Bot.PowerUps })
                    {
                        Balance.Result r = Balance.Run(cfg, def, d, bot);
                        sb.AppendLine($"{def.id},{d},{bot},{r.position},{r.playerTime:0.0},{r.winnerTime:0.0},{r.aiMeanTime:0.0},{r.playerPowerUps},{r.playerPads},{r.aiPowerUps},{r.playerBumps}");
                        if (d == Difficulty.Easy && bot == Balance.Bot.PowerUps) easySeek = r;
                        if (d == Difficulty.Hard && bot == Balance.Bot.PowerUps) hardSeek = r;
                        if (d == Difficulty.Easy && bot == Balance.Bot.Lanes) easyNone = r;
                        if (d == Difficulty.Hard && bot == Balance.Bot.Lanes) hardNone = r;
                    }
                string name = def.displayName;
                Balance.Result es = easySeek, hs = hardSeek, en = easyNone, hn = hardNone;
                checks.Add(() => Assert.AreEqual(1, es.position, name + ": a good driver wins on Easy"));
                checks.Add(() => Assert.Less(hs.aiMeanTime, es.aiMeanTime - 1f, name + ": the Hard field is faster"));
                checks.Add(() => Assert.GreaterOrEqual(hn.position, en.position, name + ": Hard is never easier"));
                checks.Add(() => Assert.Less(hs.playerTime, hn.playerTime, name + ": power-ups make the player faster"));
            }
            System.IO.File.WriteAllText("Logs/balance_report.txt", sb.ToString());
            Debug.Log("[TR] Balance report\n" + sb);
            foreach (System.Action c in checks) c();
        }
    }

    /// <summary>Runs complete races with a scripted player for balance checks.</summary>
    public static class Balance
    {
        public enum Bot { None, Lanes, PowerUps }

        public struct Result
        {
            public int position, playerPowerUps, playerPads, aiPowerUps, playerBumps;
            public float playerTime, winnerTime, aiMeanTime;
        }

        public static Result Run(GameConfig cfg, TrackDefinition def, Difficulty d, Bot bot)
        {
            var cars = new CarTuning[cfg.race.racerCount];
            for (int i = 0; i < cars.Length; i++) cars[i] = cfg.cars[i % cfg.cars.Length].tuning;
            var sim = new RaceSimulation(RaceTrack.From(def), cfg.race, cfg.ai, cars, 0, d);
            RacerState p = sim.Player;
            if (bot == Bot.None) p.Autopilot = true;
            int bumps = 0;
            sim.PlayerBumped += _ => bumps++;
            float hw = sim.Track.HalfWidth;
            float[] lanes = { -0.62f * hw, -0.22f * hw, 0.22f * hw, 0.62f * hw };
            float lane = lanes[1];
            bool Free(float lat, float behind, float ahead)
            {
                foreach (RacerState o in sim.Racers)
                {
                    if (o.IsPlayer) continue;
                    float dd = sim.Track.Path.DeltaOnLoop(o.Distance, p.Distance);
                    if (dd > behind && dd < ahead && Mathf.Abs(o.Lateral - lat) < 2.6f) return false;
                }
                return true;
            }
            for (float t = 0f; t < 400f && sim.FinishedCount < cars.Length; t += RaceSimulation.MaxSubStep)
            {
                float steer = 0f;
                if (!p.Autopilot)
                {
                    // Like a good young player: keep a lane, go round slower cars, and (PowerUps bot) steer for power-ups.
                    float target = lane;
                    if (bot == Bot.PowerUps && sim.PowerUps.Seek(p, 1f, out float seek) && Free(seek, 2f, 30f)) target = seek;
                    else if (!Free(lane, 0f, 30f))
                    {
                        float best = float.MaxValue;
                        foreach (float l in lanes)
                            if (Free(l, -5f, 30f) && Mathf.Abs(l - p.Lateral) < best) { best = Mathf.Abs(l - p.Lateral); lane = l; }
                        target = lane;
                    }
                    // Hold the line: P on lateral error, damping, and feed-forward against the corner push.
                    sim.Track.Path.Sample(p.Distance, out TrackSample s);
                    float latMax = p.Tuning.lateralSpeed * p.SteerScale;
                    float ff = latMax > 0.01f ? s.curvature * p.Speed * p.Speed * sim.Tuning.cornerDrift / latMax : 0f;
                    steer = Mathf.Clamp((target - p.Lateral) * 0.5f - p.LateralVelocity * 0.12f + ff, -1f, 1f);
                }
                sim.Step(RaceSimulation.MaxSubStep, steer);
            }
            var r = new Result { position = p.FinishOrder, playerTime = p.FinishTime, playerPowerUps = p.PowerUpsCollected, playerPads = p.PadsHit, playerBumps = bumps };
            float sum = 0f;
            int n = 0;
            r.winnerTime = float.MaxValue;
            foreach (RacerState o in sim.Racers)
            {
                r.winnerTime = Mathf.Min(r.winnerTime, o.FinishTime);
                if (o.IsPlayer) continue;
                sum += o.FinishTime;
                n++;
                r.aiPowerUps += o.PowerUpsCollected + o.PadsHit;
            }
            r.aiMeanTime = sum / Mathf.Max(1, n);
            return r;
        }
    }
}
