using System;
using System.Collections.Generic;
using System.IO;
using Accertone.CarSetup;
using UnityEngine;

namespace Accertone.Garage
{
    [Serializable]
    public class GarageLiverySlot
    {
        public string name;
        public string savedAt;
        public GarageAppearance appearance;
    }

    [Serializable]
    class GarageLiveryList
    {
        public int version = 1;
        public List<GarageLiverySlot> slots = new List<GarageLiverySlot>();
    }

    [Serializable]
    class GarageSetupFile
    {
        public int version = 1;
        public F1CarSetupData setup;
    }

    /// <summary>
    /// Garage persistence under <c>persistentDataPath/Garage</c>: the car's appearance and its active performance setup
    /// in separate files (so a livery can change without touching the setup and vice versa), plus saved liveries.
    /// Named performance setups stay with <see cref="CarSetupPresetManager"/> (<c>persistentDataPath/CarSetups</c>).
    /// Files are written to a temporary file first and then swapped in, so a crash never leaves a half-written save.
    /// </summary>
    public static class GarageSave
    {
        public const string AppearanceFile = "appearance.json";
        public const string SetupFile = "active_setup.json";
        public const string LiveriesFile = "liveries.json";
        public const int MaxLiveries = 6;

        public static string Directory
        {
            get { return Path.Combine(Application.persistentDataPath, "Garage"); }
        }

        public static GarageAppearance LoadAppearance()
        {
            var a = Read<GarageAppearance>(AppearanceFile);
            if (a == null) return GarageAppearance.Factory();
            a.Validate();
            return a;
        }

        public static bool SaveAppearance(GarageAppearance appearance)
        {
            return appearance != null && Write(AppearanceFile, appearance);
        }

        /// <summary>The setup saved by the Garage, or null when the player has never saved one.</summary>
        public static F1CarSetupData LoadActiveSetup()
        {
            var file = Read<GarageSetupFile>(SetupFile);
            if (file == null || file.setup == null) return null;
            file.setup.ValidateAndClamp();
            return file.setup;
        }

        public static bool SaveActiveSetup(F1CarSetupData setup)
        {
            if (setup == null) return false;
            var copy = setup.Clone();
            copy.dateModified = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            return Write(SetupFile, new GarageSetupFile { setup = copy });
        }

        public static List<GarageLiverySlot> LoadLiveries()
        {
            var list = Read<GarageLiveryList>(LiveriesFile);
            var slots = list != null && list.slots != null ? list.slots : new List<GarageLiverySlot>();
            slots.RemoveAll(s => s == null || s.appearance == null);
            foreach (var s in slots) s.appearance.Validate();
            return slots;
        }

        public static bool SaveLiveries(List<GarageLiverySlot> slots)
        {
            return Write(LiveriesFile, new GarageLiveryList { slots = slots ?? new List<GarageLiverySlot>() });
        }

        public static void DeleteAll()
        {
            foreach (var f in new[] { AppearanceFile, SetupFile })
            {
                try
                {
                    string path = Path.Combine(Directory, f);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Accert one] Garage: could not remove " + f + ": " + e.Message);
                }
            }
        }

        static T Read<T>(string file) where T : class
        {
            try
            {
                string path = Path.Combine(Directory, file);
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Accert one] Garage: could not read " + file + ": " + e.Message);
                return null;
            }
        }

        static bool Write(string file, object data)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string path = Path.Combine(Directory, file);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Accert one] Garage: could not save " + file + ": " + e.Message);
                return false;
            }
        }
    }
}
