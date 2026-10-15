using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Accertone.CarSetup
{
    /// <summary>
    /// Manages built-in factory setups (Monaco, Monza, Balanced, Wet) and user custom setups.
    /// Handles JSON persistence, cloning, and export/import.
    /// </summary>
    public class CarSetupPresetManager : MonoBehaviour
    {
        private static CarSetupPresetManager s_Instance;
        public static CarSetupPresetManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("CarSetupPresetManager");
                    s_Instance = go.AddComponent<CarSetupPresetManager>();
                    DontDestroyOnLoad(go);
                }
                return s_Instance;
            }
        }

        [SerializeField] private F1CarSetupData m_ActiveSetup;
        [SerializeField] private F1CarSetupData m_BaselineSetup;

        public F1CarSetupData ActiveSetup => m_ActiveSetup;
        public F1CarSetupData BaselineSetup => m_BaselineSetup;

        public event Action<F1CarSetupData> OnSetupChanged;
        public event Action OnPresetsListUpdated;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSetupDirectory();
            InitializeDefaultSetups();
        }

        public string GetSetupsDirectory()
        {
            string dir = Path.Combine(Application.persistentDataPath, "CarSetups");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void EnsureSetupDirectory()
        {
            GetSetupsDirectory();
        }

        private void InitializeDefaultSetups()
        {
            m_BaselineSetup = CreateBalancedPreset();
            m_ActiveSetup = m_BaselineSetup.Clone();
        }

        // =================================================================== FACTORY PRESETS
        public F1CarSetupData CreateBalancedPreset()
        {
            var setup = new F1CarSetupData
            {
                setupName = "Balanced Trim (Valcorsa)",
                frontWingAngle = 28,
                rearWingAngle = 26,
                drsEfficiency = 6,
                diffOnThrottle = 65,
                diffOffThrottle = 55,
                diffPreload = 40,
                finalDriveRatio = 3.40f,
                frontCamber = -3.00f,
                rearCamber = -1.60f,
                frontToe = 0.10f,
                rearToe = 0.20f,
                frontSuspension = 22,
                rearSuspension = 18,
                frontAntiRollBar = 12,
                rearAntiRollBar = 8,
                frontRideHeight = 36,
                rearRideHeight = 54,
                brakeBias = 56,
                brakePressure = 96,
                brakeDucts = 3,
                tireCompound = TireCompound.C2_Medium,
                frontLeftPressure = 23.0f,
                frontRightPressure = 23.0f,
                rearLeftPressure = 21.2f,
                rearRightPressure = 21.2f
            };
            setup.ValidateAndClamp();
            return setup;
        }

        public F1CarSetupData CreateHighDownforcePreset()
        {
            var setup = new F1CarSetupData
            {
                setupName = "High Downforce (Monaco)",
                frontWingAngle = 46,
                rearWingAngle = 48,
                drsEfficiency = 5,
                diffOnThrottle = 55,
                diffOffThrottle = 50,
                diffPreload = 25,
                finalDriveRatio = 3.85f,
                frontCamber = -3.40f,
                rearCamber = -1.90f,
                frontToe = 0.15f,
                rearToe = 0.25f,
                frontSuspension = 14,
                rearSuspension = 12,
                frontAntiRollBar = 8,
                rearAntiRollBar = 6,
                frontRideHeight = 32,
                rearRideHeight = 58,
                brakeBias = 54,
                brakePressure = 98,
                brakeDucts = 5,
                tireCompound = TireCompound.C3_Soft,
                frontLeftPressure = 22.0f,
                frontRightPressure = 22.0f,
                rearLeftPressure = 20.4f,
                rearRightPressure = 20.4f
            };
            setup.ValidateAndClamp();
            return setup;
        }

        public F1CarSetupData CreateLowDownforcePreset()
        {
            var setup = new F1CarSetupData
            {
                setupName = "Low Downforce (Monza)",
                frontWingAngle = 9,
                rearWingAngle = 11,
                drsEfficiency = 8,
                diffOnThrottle = 75,
                diffOffThrottle = 65,
                diffPreload = 55,
                finalDriveRatio = 2.95f,
                frontCamber = -2.60f,
                rearCamber = -1.20f,
                frontToe = 0.05f,
                rearToe = 0.10f,
                frontSuspension = 34,
                rearSuspension = 30,
                frontAntiRollBar = 18,
                rearAntiRollBar = 15,
                frontRideHeight = 34,
                rearRideHeight = 44,
                brakeBias = 58,
                brakePressure = 94,
                brakeDucts = 1,
                tireCompound = TireCompound.C1_Hard,
                frontLeftPressure = 24.5f,
                frontRightPressure = 24.5f,
                rearLeftPressure = 22.5f,
                rearRightPressure = 22.5f
            };
            setup.ValidateAndClamp();
            return setup;
        }

        public F1CarSetupData CreateWetPreset()
        {
            var setup = new F1CarSetupData
            {
                setupName = "Wet Weather Trim",
                frontWingAngle = 38,
                rearWingAngle = 40,
                drsEfficiency = 4,
                diffOnThrottle = 50,
                diffOffThrottle = 50,
                diffPreload = 15,
                finalDriveRatio = 3.30f,
                frontCamber = -2.70f,
                rearCamber = -1.30f,
                frontToe = 0.05f,
                rearToe = 0.15f,
                frontSuspension = 10,
                rearSuspension = 8,
                frontAntiRollBar = 5,
                rearAntiRollBar = 3,
                frontRideHeight = 48,
                rearRideHeight = 68,
                brakeBias = 52,
                brakePressure = 88,
                brakeDucts = 4,
                tireCompound = TireCompound.Wet,
                frontLeftPressure = 21.8f,
                frontRightPressure = 21.8f,
                rearLeftPressure = 20.2f,
                rearRightPressure = 20.2f
            };
            setup.ValidateAndClamp();
            return setup;
        }

        // =================================================================== PERSISTENCE & MANAGEMENT
        public void SetActiveSetup(F1CarSetupData setup)
        {
            if (setup == null) return;
            m_ActiveSetup = setup.Clone();
            m_ActiveSetup.ValidateAndClamp();
            OnSetupChanged?.Invoke(m_ActiveSetup);
        }

        public List<string> GetSavedSetupNames()
        {
            var list = new List<string>
            {
                "Balanced (Valcorsa)",
                "High Downforce (Monaco)",
                "Low Downforce (Monza)",
                "Wet Weather Trim"
            };

            try
            {
                string dir = GetSetupsDirectory();
                var files = Directory.GetFiles(dir, "*.json");
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!list.Contains(name))
                    {
                        list.Add(name);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Accert one] Error loading custom setup names: {e.Message}");
            }

            return list;
        }

        public bool SaveUserSetup(string name, F1CarSetupData setup)
        {
            if (string.IsNullOrEmpty(name) || setup == null) return false;
            try
            {
                setup.setupName = name;
                setup.dateModified = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
                string path = Path.Combine(GetSetupsDirectory(), name + ".json");
                string json = JsonUtility.ToJson(setup, true);
                File.WriteAllText(path, json);
                OnPresetsListUpdated?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Accert one] Error saving user setup '{name}': {e.Message}");
                return false;
            }
        }

        public F1CarSetupData LoadSetupByName(string name)
        {
            switch (name)
            {
                case "Balanced (Valcorsa)":
                    return CreateBalancedPreset();
                case "High Downforce (Monaco)":
                    return CreateHighDownforcePreset();
                case "Low Downforce (Monza)":
                    return CreateLowDownforcePreset();
                case "Wet Weather Trim":
                    return CreateWetPreset();
            }

            string path = Path.Combine(GetSetupsDirectory(), name + ".json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonUtility.FromJson<F1CarSetupData>(json);
                    loaded.ValidateAndClamp();
                    return loaded;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Accert one] Error reading custom setup '{name}': {e.Message}");
                }
            }

            return CreateBalancedPreset();
        }

        public bool DeleteUserSetup(string name)
        {
            string path = Path.Combine(GetSetupsDirectory(), name + ".json");
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    OnPresetsListUpdated?.Invoke();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Accert one] Error deleting custom setup '{name}': {e.Message}");
                }
            }
            return false;
        }
    }
}
