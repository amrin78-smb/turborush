using System;
using System.IO;
using UnityEngine;

namespace TurboRush.Save
{
    /// <summary>
    /// Local JSON save with versioning, migration, atomic writes and corruption recovery. A missing, corrupt or
    /// future-version file never blocks start-up: the game falls back to defaults and logs what happened.
    /// </summary>
    public sealed class SaveDataService
    {
        public const string FileName = "turborush_save.json";

        private readonly string m_Directory;
        public SaveData Data { get; private set; }
        /// <summary>What happened on the last Load (for logs/tests): "new", "loaded", "migrated", "corrupt", "future".</summary>
        public string LastLoadOutcome { get; private set; } = "";

        public SaveDataService(string directory = null)
        {
            m_Directory = directory ?? Application.persistentDataPath;
        }

        public string FilePath => Path.Combine(m_Directory, FileName);

        public SaveData Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
            {
                string bak = path + ".bak";
                if (File.Exists(bak))
                {
                    Debug.LogWarning("[TR Save] Save missing; restoring from backup.");
                    path = bak;
                }
                else
                {
                    Data = new SaveData();
                    LastLoadOutcome = "new";
                    Debug.Log("[TR Save] No save found; using defaults.");
                    return Data;
                }
            }

            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception e)
            {
                Debug.LogWarning("[TR Save] Could not read save (" + e.Message + "); using defaults.");
                return Recover(path);
            }
            Data = Parse(json, out string outcome);
            LastLoadOutcome = outcome;
            if (outcome == "corrupt") return Recover(path);
            if (outcome == "migrated") Save();
            return Data;
        }

        /// <summary>Parses JSON into a sanitised, migrated SaveData. outcome: loaded | migrated | corrupt | future.</summary>
        public static SaveData Parse(string json, out string outcome)
        {
            SaveData d = null;
            outcome = "loaded";
            if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{') { outcome = "corrupt"; return new SaveData(); }
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception) { d = null; }
            if (d == null) { outcome = "corrupt"; return new SaveData(); }

            // JsonUtility leaves version at the class default when the field is absent: detect real v1 files.
            if (!json.Contains("\"version\"")) d.version = 1;
            if (d.version < SaveData.CurrentVersion)
            {
                Migrate(d);
                outcome = "migrated";
            }
            else if (d.version > SaveData.CurrentVersion)
            {
                outcome = "future";
                Debug.LogWarning($"[TR Save] Save is from a newer version ({d.version}); loading known fields only.");
            }
            d.Sanitize();
            return d;
        }

        /// <summary>v1 → v2: sensitivity was 0..10 (5 = normal), units was a bool.</summary>
        public static void Migrate(SaveData d)
        {
            if (d.version <= 1)
            {
                if (d.settings == null) d.settings = new SettingsData();
                if (d.legacySteering >= 0f) d.settings.steeringSensitivity = Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(d.legacySteering / 10f));
                d.settings.units = d.legacyUseMph ? SpeedUnit.Mph : SpeedUnit.Kmh;
                d.legacySteering = -1f;
                d.legacyUseMph = false;
                Debug.Log("[TR Save] Migrated save v1 → v2.");
            }
            d.version = SaveData.CurrentVersion;
        }

        private SaveData Recover(string path)
        {
            try
            {
                string corrupt = FilePath + ".corrupt";
                if (File.Exists(path)) File.Copy(path, corrupt, true);
                Debug.LogWarning("[TR Save] Save was corrupt; kept a copy at " + corrupt + " and reset to defaults.");
            }
            catch (Exception e) { Debug.LogWarning("[TR Save] Could not keep corrupt save: " + e.Message); }
            Data = new SaveData();
            LastLoadOutcome = "corrupt";
            Save();
            return Data;
        }

        /// <summary>Atomic write: temp file → replace, previous file kept as .bak.</summary>
        public bool Save()
        {
            if (Data == null) return false;
            try
            {
                Directory.CreateDirectory(m_Directory);
                Data.version = SaveData.CurrentVersion;
                string path = FilePath, tmp = path + ".tmp", bak = path + ".bak";
                File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
                if (File.Exists(path))
                {
                    File.Copy(path, bak, true);
                    File.Delete(path);
                }
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[TR Save] Save failed: " + e.Message);
                return false;
            }
        }

        public void ReplaceData(SaveData d)
        {
            Data = d ?? new SaveData();
        }
    }
}
