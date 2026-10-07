using System;
using System.IO;
using UnityEngine;

namespace VrFsim.Settings
{
    /// <summary>
    /// Loads and saves <see cref="SimSettings"/> as JSON in the player's persistent data folder
    /// (on Windows: %USERPROFILE%\AppData\LocalLow\VrFsim\VrFsim\settings.json).
    /// A corrupt or partial file never crashes the game: missing fields keep their defaults and
    /// every value is clamped by <see cref="SimSettings.Validate"/>.
    /// </summary>
    public static class SettingsStore
    {
        const string FileName = "settings.json";
        static SimSettings current;
        static bool persist = true;

        /// <summary>Use in-memory settings that are never written to disk (automated tests).</summary>
        public static void UseTransient(SimSettings s)
        {
            persist = false;
            current = s ?? new SimSettings();
            current.Validate();
        }

        /// <summary>Raised after any change is committed with <see cref="Commit"/>.</summary>
        public static event Action<SimSettings> Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SimSettings Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        public static SimSettings Load()
        {
            var s = new SimSettings();
            if (!persist) return s;
            try
            {
                if (File.Exists(FilePath))
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), s);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VrFsim] Settings file unreadable, using defaults: {e.Message}");
                s = new SimSettings();
            }
            s.Validate();
            return s;
        }

        /// <summary>Validate, persist, and notify listeners.</summary>
        public static void Commit()
        {
            Current.Validate();
            Save(Current);
            Changed?.Invoke(Current);
        }

        public static void Replace(SimSettings s)
        {
            current = s ?? new SimSettings();
            Commit();
        }

        public static void ResetToDefaults() => Replace(new SimSettings());

        static void Save(SimSettings s)
        {
            if (!persist) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(s, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VrFsim] Could not save settings: {e.Message}");
            }
        }
    }
}
