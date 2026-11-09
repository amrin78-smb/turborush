using System.IO;
using NUnit.Framework;
using TurboRush.Core;
using TurboRush.Input;
using TurboRush.Platform;
using TurboRush.Save;
using TurboRush.Track;
using TurboRush.UI;
using TurboRush.Vehicle;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurboRush.Tests
{
    public class SaveTests
    {
        private string m_Dir;

        [SetUp]
        public void SetUp()
        {
            m_Dir = Path.Combine(Path.GetTempPath(), "tr_save_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Dir)) Directory.Delete(m_Dir, true);
        }

        [Test]
        public void MissingSaveGivesDefaults()
        {
            var s = new SaveDataService(m_Dir);
            SaveData d = s.Load();
            Assert.AreEqual("new", s.LastLoadOutcome);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsTrue(d.settings.music);
            Assert.AreEqual(SpeedUnit.Kmh, d.settings.units);
            Assert.AreEqual(QualityChoice.Auto, d.settings.quality);
        }

        [Test]
        public void RoundTripKeepsProgressAndSettings()
        {
            var s = new SaveDataService(m_Dir);
            s.Load();
            s.Data.unlockedCars.Add("zoom");
            s.Data.Record("skyway", true).bestTime = 81.5f;
            s.Data.settings.units = SpeedUnit.Mph;
            s.Data.settings.vibration = false;
            Assert.IsTrue(s.Save());
            var s2 = new SaveDataService(m_Dir);
            SaveData d = s2.Load();
            Assert.AreEqual("loaded", s2.LastLoadOutcome);
            Assert.Contains("zoom", d.unlockedCars);
            Assert.AreEqual(81.5f, d.Record("skyway", false).bestTime, 1e-4f);
            Assert.AreEqual(SpeedUnit.Mph, d.settings.units);
            Assert.IsFalse(d.settings.vibration);
        }

        [Test]
        public void CorruptSaveRecoversWithDefaultsAndKeepsACopy()
        {
            File.WriteAllText(Path.Combine(m_Dir, SaveDataService.FileName), "{ this is not json ]]]");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("corrupt"));
            var s = new SaveDataService(m_Dir);
            SaveData d = s.Load();
            Assert.AreEqual("corrupt", s.LastLoadOutcome);
            Assert.NotNull(d);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsTrue(File.Exists(Path.Combine(m_Dir, SaveDataService.FileName + ".corrupt")));
            Assert.AreEqual("loaded", new SaveDataService(m_Dir).LoadAndReport());
        }

        [Test]
        public void EmptyAndBinaryGarbageAreTreatedAsCorrupt()
        {
            SaveDataService.Parse("", out string o1);
            SaveDataService.Parse("\0\0\x01binary", out string o2);
            Assert.AreEqual("corrupt", o1);
            Assert.AreEqual("corrupt", o2);
        }

        [Test]
        public void OldVersionIsMigrated()
        {
            const string v1 = "{\"unlockedTracks\":[\"skyway\"],\"selectedCar\":\"blaze\",\"legacySteering\":10,\"legacyUseMph\":true,\"settings\":{\"music\":false}}";
            SaveData d = SaveDataService.Parse(v1, out string outcome);
            Assert.AreEqual("migrated", outcome);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.AreEqual(1.4f, d.settings.steeringSensitivity, 1e-4f);
            Assert.AreEqual(SpeedUnit.Mph, d.settings.units);
            Assert.IsFalse(d.settings.music);
            Assert.Contains("skyway", d.unlockedTracks);
        }

        [Test]
        public void FutureVersionStillLoads()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer version"));
            SaveData d = SaveDataService.Parse("{\"version\":99,\"selectedTrack\":\"desert\",\"newField\":5}", out string outcome);
            Assert.AreEqual("future", outcome);
            Assert.AreEqual("desert", d.selectedTrack);
        }

        [Test]
        public void OutOfRangeValuesAreSanitised()
        {
            SaveData d = SaveDataService.Parse("{\"version\":2,\"settings\":{\"musicVolume\":7,\"steeringSensitivity\":-3,\"units\":9,\"quality\":42},\"records\":[{\"trackId\":\"a\",\"bestStars\":12,\"bestTime\":-5}]}", out _);
            Assert.AreEqual(1f, d.settings.musicVolume);
            Assert.AreEqual(0.6f, d.settings.steeringSensitivity, 1e-4f);
            Assert.AreEqual(SpeedUnit.Kmh, d.settings.units);
            Assert.AreEqual(QualityChoice.Auto, d.settings.quality);
            Assert.AreEqual(3, d.records[0].bestStars);
            Assert.AreEqual(0f, d.records[0].bestTime);
        }
    }

    public static class SaveTestExtensions
    {
        public static string LoadAndReport(this SaveDataService s)
        {
            s.Load();
            return s.LastLoadOutcome;
        }
    }

    public class ProgressionTests
    {
        private static GameConfig Config()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var t1 = ScriptableObject.CreateInstance<TrackDefinition>(); t1.id = "a"; t1.unlockStars = 0;
            var t2 = ScriptableObject.CreateInstance<TrackDefinition>(); t2.id = "b"; t2.unlockStars = 2; t2.displayName = "B";
            var c1 = ScriptableObject.CreateInstance<CarDefinition>(); c1.id = "x"; c1.unlockStars = 0;
            var c2 = ScriptableObject.CreateInstance<CarDefinition>(); c2.id = "y"; c2.unlockStars = 1; c2.displayName = "Y";
            cfg.tracks = new[] { t1, t2 };
            cfg.cars = new[] { c1, c2 };
            return cfg;
        }

        [Test]
        public void StarsArePredictable()
        {
            Assert.AreEqual(3, Progression.StarsFor(1));
            Assert.AreEqual(2, Progression.StarsFor(2));
            Assert.AreEqual(2, Progression.StarsFor(3));
            Assert.AreEqual(1, Progression.StarsFor(6));
            Assert.AreEqual(0, Progression.StarsFor(0));
        }

        [Test]
        public void ResultsUnlockContentAndKeepBests()
        {
            GameConfig cfg = Config();
            var save = new SaveData();
            Progression.EnsureDefaults(save, cfg);
            Assert.Contains("a", save.unlockedTracks);
            Assert.Contains("x", save.unlockedCars);
            Assert.IsFalse(save.unlockedCars.Contains("y"));
            RaceReward r = Progression.ApplyResult(save, cfg, "a", 5, 90f);
            Assert.AreEqual(1, r.stars);
            Assert.Contains("Y", r.unlockedCars);
            Assert.IsFalse(save.unlockedTracks.Contains("b"));
            r = Progression.ApplyResult(save, cfg, "a", 1, 95f);
            Assert.AreEqual(3, r.stars);
            Assert.IsFalse(r.newBestTime, "slower time is not a best");
            Assert.AreEqual(90f, save.Record("a", false).bestTime);
            Assert.AreEqual(1, save.Record("a", false).bestPosition);
            Assert.AreEqual(3, save.TotalStars(), "best stars per track, not summed per race");
            Assert.Contains("b", save.unlockedTracks);
        }
    }

    public class SystemsTests
    {
        [Test]
        public void StateMachineOnlyAllowsLegalTransitions()
        {
            var m = new GameStateMachine();
            Assert.IsFalse(m.TryTransition(GameState.Racing));
            Assert.IsTrue(m.TryTransition(GameState.MainMenu));
            Assert.IsTrue(m.TryTransition(GameState.TrackSelect));
            Assert.IsTrue(m.TryTransition(GameState.Countdown));
            Assert.IsFalse(m.TryTransition(GameState.Results), "cannot skip the race");
            Assert.IsTrue(m.TryTransition(GameState.Racing));
            Assert.IsTrue(m.TryTransition(GameState.Paused));
            Assert.IsTrue(m.TryTransition(GameState.Racing));
            Assert.IsTrue(m.TryTransition(GameState.Finishing));
            Assert.IsFalse(m.TryTransition(GameState.Racing), "cannot un-finish");
            Assert.IsTrue(m.TryTransition(GameState.Results));
            Assert.IsTrue(m.TryTransition(GameState.Countdown), "race again");
            int changes = 0;
            m.Changed += (a, b) => changes++;
            m.TryTransition(GameState.Racing);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void CanvasMatchRuleFollowsAspect()
        {
            Assert.AreEqual(1f, AdaptiveCanvasScaler.MatchFor(1920, 1080), "16:9 → height");
            Assert.AreEqual(1f, AdaptiveCanvasScaler.MatchFor(2400, 1080), "20:9 → height");
            Assert.AreEqual(0f, AdaptiveCanvasScaler.MatchFor(1920, 1200), "16:10 → width");
            Assert.AreEqual(0f, AdaptiveCanvasScaler.MatchFor(2048, 1536), "4:3 → width");
            Assert.AreEqual(0f, AdaptiveCanvasScaler.MatchFor(1536, 2048), "orientation independent");
        }

        [Test]
        public void SafeAreaRespectsDeviceAndSimulatedInsets()
        {
            Rect r = SafeAreaFitter.ComputeSafeRect(new Rect(80, 0, 2240, 1080), 2400, 1080, Vector4.zero, 0f);
            Assert.AreEqual(80f, r.xMin);
            Assert.AreEqual(2320f, r.xMax);
            r = SafeAreaFitter.ComputeSafeRect(new Rect(0, 0, 1920, 1080), 1920, 1080, new Vector4(100, 50, 20, 0), 0.02f);
            Assert.AreEqual(100f + 1080 * 0.02f, r.xMin, 1e-3f);
            Assert.AreEqual(1920f - 50 - 1080 * 0.02f, r.xMax, 1e-3f);
            Assert.AreEqual(1080f - 20 - 1080 * 0.02f, r.yMax, 1e-3f);
        }

        [Test]
        public void TouchHalvesMapToSteering()
        {
            Assert.AreEqual(-1f, PlayerInputController.SteerFromScreenX(10, 1920));
            Assert.AreEqual(-1f, PlayerInputController.SteerFromScreenX(959, 1920));
            Assert.AreEqual(1f, PlayerInputController.SteerFromScreenX(960, 1920));
            Assert.AreEqual(1f, PlayerInputController.SteerFromScreenX(1900, 1920));
        }

        [Test]
        public void RedmiA3ClassDevicesGetLowQuality()
        {
            Assert.AreEqual(0, DeviceQualityManager.AutoLevel("PowerVR Rogue GE8320", 3072, true, out _));
            Assert.AreEqual(0, DeviceQualityManager.AutoLevel("Mali-G57", 4096, true, out _));
            Assert.AreEqual(1, DeviceQualityManager.AutoLevel("Mali-G57", 6144, true, out _));
            Assert.AreEqual(2, DeviceQualityManager.AutoLevel("Adreno (TM) 740", 12288, true, out _));
            Assert.AreEqual(2, DeviceQualityManager.AutoLevel("NVIDIA", 32000, false, out _));
        }

        [Test]
        public void TimeAndOrdinalFormatting()
        {
            Assert.AreEqual("1:05.25", UIKit.FormatTime(65.25f));
            Assert.AreEqual("--:--.--", UIKit.FormatTime(0f));
            Assert.AreEqual("1st", UIKit.Ordinal(1));
            Assert.AreEqual("2nd", UIKit.Ordinal(2));
            Assert.AreEqual("3rd", UIKit.Ordinal(3));
            Assert.AreEqual("6th", UIKit.Ordinal(6));
            Assert.AreEqual("11th", UIKit.Ordinal(11));
        }
    }
}
