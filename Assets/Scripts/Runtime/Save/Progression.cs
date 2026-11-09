using System.Collections.Generic;
using TurboRush.Core;
using TurboRush.Track;
using TurboRush.Vehicle;

namespace TurboRush.Save
{
    /// <summary>Result of applying a finished race to the save.</summary>
    public sealed class RaceReward
    {
        public int stars;
        public bool newBestTime;
        public bool newBestStars;
        public int totalStars;
        public readonly List<string> unlockedCars = new List<string>();
        public readonly List<string> unlockedTracks = new List<string>();
    }

    /// <summary>Predictable, non-random rewards: stars per finishing position; stars unlock tracks and cars.</summary>
    public static class Progression
    {
        public static int StarsFor(int position)
        {
            if (position <= 0) return 0;
            if (position == 1) return 3;
            if (position <= 3) return 2;
            return 1; // finishing always earns a star
        }

        /// <summary>Makes sure the starting content is unlocked and selections are valid.</summary>
        public static void EnsureDefaults(SaveData save, GameConfig config)
        {
            if (config == null) return;
            UnlockEarned(save, config, null);
            if (config.cars.Length > 0 && (string.IsNullOrEmpty(save.selectedCar) || !save.unlockedCars.Contains(save.selectedCar)))
                save.selectedCar = config.cars[0].id;
            if (config.tracks.Length > 0 && (string.IsNullOrEmpty(save.selectedTrack) || !save.unlockedTracks.Contains(save.selectedTrack)))
                save.selectedTrack = config.tracks[0].id;
        }

        public static bool IsTrackUnlocked(SaveData save, TrackDefinition t) => t.unlockStars <= 0 || save.unlockedTracks.Contains(t.id);
        public static bool IsCarUnlocked(SaveData save, CarDefinition c) => c.unlockStars <= 0 || save.unlockedCars.Contains(c.id);

        public static RaceReward ApplyResult(SaveData save, GameConfig config, string trackId, int position, float time)
        {
            var reward = new RaceReward { stars = StarsFor(position) };
            TrackRecord rec = save.Record(trackId, true);
            rec.races++;
            save.racesCompleted++;
            if (position > 0)
            {
                if (rec.bestTime <= 0f || time < rec.bestTime) { rec.bestTime = time; reward.newBestTime = true; }
                if (rec.bestPosition <= 0 || position < rec.bestPosition) rec.bestPosition = position;
                if (reward.stars > rec.bestStars) { rec.bestStars = reward.stars; reward.newBestStars = true; }
            }
            UnlockEarned(save, config, reward);
            reward.totalStars = save.TotalStars();
            return reward;
        }

        private static void UnlockEarned(SaveData save, GameConfig config, RaceReward reward)
        {
            int stars = save.TotalStars();
            foreach (TrackDefinition t in config.tracks)
            {
                if (t == null || save.unlockedTracks.Contains(t.id) || stars < t.unlockStars) continue;
                save.unlockedTracks.Add(t.id);
                if (t.unlockStars > 0) reward?.unlockedTracks.Add(t.displayName);
            }
            foreach (CarDefinition c in config.cars)
            {
                if (c == null || save.unlockedCars.Contains(c.id) || stars < c.unlockStars) continue;
                save.unlockedCars.Add(c.id);
                if (c.unlockStars > 0) reward?.unlockedCars.Add(c.displayName);
            }
        }
    }
}
