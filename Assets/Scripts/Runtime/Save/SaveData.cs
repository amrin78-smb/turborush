using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurboRush.Save
{
    public enum SpeedUnit { Kmh = 0, Mph = 1 }
    public enum QualityChoice { Auto = 0, Low = 1, Medium = 2, High = 3 }

    [Serializable]
    public sealed class SettingsData
    {
        public bool music = true;
        public bool sfx = true;
        [Range(0f, 1f)] public float musicVolume = 0.6f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        public bool vibration = true;
        [Tooltip("Steering sensitivity multiplier 0.6..1.4")] public float steeringSensitivity = 1f;
        public SpeedUnit units = SpeedUnit.Kmh;
        public QualityChoice quality = QualityChoice.Auto;
        [Tooltip("AI difficulty (Easy = the original 0.1–0.3 field). Saves without this field load as Normal.")]
        public AI.Difficulty difficulty = AI.Difficulty.Normal;
    }

    [Serializable]
    public sealed class TrackRecord
    {
        public string trackId;
        public float bestTime;      // seconds, 0 = none
        public int bestPosition;    // 0 = none
        public int bestStars;
        public int races;
    }

    /// <summary>Versioned local save (JSON). Unknown/corrupt data falls back to defaults (see SaveDataService).</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public List<string> unlockedTracks = new List<string>();
        public List<string> unlockedCars = new List<string>();
        public string selectedCar = "";
        public string selectedTrack = "";
        public List<TrackRecord> records = new List<TrackRecord>();
        public SettingsData settings = new SettingsData();
        public bool steerHintShown;
        public int racesCompleted;

        // v1 field kept only so old files can be migrated (v1 stored sensitivity 0..10 and a single mph flag).
        public float legacySteering = -1f;
        public bool legacyUseMph;

        public TrackRecord Record(string trackId, bool create)
        {
            foreach (TrackRecord r in records) if (r.trackId == trackId) return r;
            if (!create) return null;
            var n = new TrackRecord { trackId = trackId };
            records.Add(n);
            return n;
        }

        public int TotalStars()
        {
            int s = 0;
            foreach (TrackRecord r in records) s += Mathf.Clamp(r.bestStars, 0, 3);
            return s;
        }

        /// <summary>Clamps every value into a valid range (defends against hand-edited or damaged files).</summary>
        public void Sanitize()
        {
            if (unlockedTracks == null) unlockedTracks = new List<string>();
            if (unlockedCars == null) unlockedCars = new List<string>();
            if (records == null) records = new List<TrackRecord>();
            records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.trackId));
            if (settings == null) settings = new SettingsData();
            settings.musicVolume = Mathf.Clamp01(float.IsNaN(settings.musicVolume) ? 0.6f : settings.musicVolume);
            settings.sfxVolume = Mathf.Clamp01(float.IsNaN(settings.sfxVolume) ? 0.9f : settings.sfxVolume);
            settings.steeringSensitivity = Mathf.Clamp(float.IsNaN(settings.steeringSensitivity) ? 1f : settings.steeringSensitivity, 0.6f, 1.4f);
            if (!Enum.IsDefined(typeof(SpeedUnit), settings.units)) settings.units = SpeedUnit.Kmh;
            if (!Enum.IsDefined(typeof(QualityChoice), settings.quality)) settings.quality = QualityChoice.Auto;
            if (!Enum.IsDefined(typeof(AI.Difficulty), settings.difficulty)) settings.difficulty = AI.Difficulty.Normal;
            foreach (TrackRecord r in records)
            {
                r.bestStars = Mathf.Clamp(r.bestStars, 0, 3);
                if (float.IsNaN(r.bestTime) || r.bestTime < 0f) r.bestTime = 0f;
                r.bestPosition = Mathf.Max(0, r.bestPosition);
                r.races = Mathf.Max(0, r.races);
            }
            if (selectedCar == null) selectedCar = "";
            if (selectedTrack == null) selectedTrack = "";
        }
    }
}
