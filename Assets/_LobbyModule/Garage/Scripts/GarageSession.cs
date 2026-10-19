using System;
using System.Collections.Generic;
using Accertone.CarSetup;
using UnityEngine;

namespace Accertone.Garage
{
    /// <summary>
    /// The Garage's live data: the car's appearance and its performance setup. Every change is applied to the car
    /// straight away and saved shortly after (debounced), so nothing is lost when the player switches category,
    /// leaves the Garage or quits. The setup is mirrored into <see cref="CarSetupPresetManager"/> (the rest of the
    /// game reads it from there) and handed to the car's <see cref="VehicleSetupApplier"/>.
    /// </summary>
    public class GarageSession
    {
        const float SaveDelay = 0.8f;

        static GarageSession s_Current;
        public static GarageSession Current { get { return s_Current; } }

        public F1CarSetupData Setup { get; private set; }
        public GarageAppearance Appearance { get; private set; }
        public SetupTelemetryAnalysis Analysis { get; private set; }
        public F1CarSetupData Factory { get; private set; }
        /// <summary>Name of the preset or saved setup the current values came from ("" once edited).</summary>
        public string SetupSource { get; private set; }

        public bool SavePending { get { return m_SaveAt > 0f; } }
        public float LastSavedAt { get; private set; }
        public bool LastSaveFailed { get; private set; }

        /// <summary>Raised after any change (setup or appearance).</summary>
        public event Action Changed;

        GarageCarRig m_Rig;
        float m_SaveAt = -1f;
        bool m_SetupDirty, m_AppearanceDirty, m_Syncing;

        /// <summary>Domain reload is off in this project, so statics outlive a Play session: start clean each time.</summary>
        public static void ResetStatics()
        {
            // The preset manager that held the old subscription is destroyed with the previous session's scene.
            s_Current = null;
        }

        /// <summary>Loads the saved Garage state and applies it to the car (called when the lobby starts).</summary>
        public static GarageSession Boot(GarageCarRig rig)
        {
            if (s_Current == null) s_Current = new GarageSession();
            s_Current.Attach(rig);
            return s_Current;
        }

        GarageSession()
        {
            var manager = CarSetupPresetManager.Instance;
            Factory = manager.CreateBalancedPreset();
            var saved = GarageSave.LoadActiveSetup();
            Setup = (saved ?? manager.ActiveSetup ?? Factory).Clone();
            Setup.ValidateAndClamp();
            SetupSource = saved != null ? saved.setupName : Setup.setupName;
            Appearance = GarageSave.LoadAppearance();
            Analysis = CarSetupEngineeringEngine.Analyze(Setup);
            // The preset manager always starts from Balanced; give it the player's setup.
            m_Syncing = true;
            manager.SetActiveSetup(Setup);
            m_Syncing = false;
            manager.OnSetupChanged += OnManagerSetupChanged;
        }

        void Attach(GarageCarRig rig)
        {
            m_Rig = rig;
            if (m_Rig == null) return;
            m_Rig.ApplyAppearance(Appearance, Setup.tireCompound);
            m_Rig.ApplySetupVisuals(Setup, true);
            PushToCar();
        }

        // Another screen (the engineering sheet) applied a setup: adopt it.
        void OnManagerSetupChanged(F1CarSetupData setup)
        {
            if (m_Syncing || setup == null) return;
            Setup = setup.Clone();
            SetupSource = setup.setupName;
            AfterSetupChange(true);
        }

        // =====================================================================================================
        // CHANGES
        // =====================================================================================================
        /// <summary>Edits the setup in place; it is clamped, analysed, shown on the car and saved.</summary>
        public void EditSetup(Action<F1CarSetupData> edit)
        {
            if (edit == null) return;
            edit(Setup);
            SetupSource = "";
            AfterSetupChange(false);
        }

        public void LoadSetup(F1CarSetupData setup, string source)
        {
            if (setup == null) return;
            Setup = setup.Clone();
            SetupSource = source ?? setup.setupName;
            AfterSetupChange(false);
        }

        void AfterSetupChange(bool fromManager)
        {
            Setup.ValidateAndClamp();
            Analysis = CarSetupEngineeringEngine.Analyze(Setup);
            if (m_Rig != null)
            {
                m_Rig.ApplySetupVisuals(Setup);
                m_Rig.ApplyAppearance(Appearance, Setup.tireCompound);
            }
            if (!fromManager)
            {
                m_Syncing = true;
                CarSetupPresetManager.Instance.SetActiveSetup(Setup);
                m_Syncing = false;
            }
            PushToCar();
            m_SetupDirty = true;
            ScheduleSave();
            Raise();
        }

        public void EditAppearance(Action<GarageAppearance> edit)
        {
            if (edit == null) return;
            edit(Appearance);
            Appearance.Validate();
            if (m_Rig != null) m_Rig.ApplyAppearance(Appearance, Setup.tireCompound);
            m_AppearanceDirty = true;
            ScheduleSave();
            Raise();
        }

        public void LoadAppearance(GarageAppearance appearance)
        {
            if (appearance == null) return;
            Appearance = appearance.Clone();
            EditAppearance(a => { });
        }

        public void ResetSetupToFactory()
        {
            LoadSetup(CarSetupPresetManager.Instance.CreateBalancedPreset(), "FACTORY BASELINE");
        }

        public void ResetAppearanceToFactory()
        {
            LoadAppearance(GarageAppearance.Factory());
        }

        void PushToCar()
        {
            var applier = m_Rig != null ? m_Rig.GetComponent<VehicleSetupApplier>() : null;
            if (applier != null) applier.ApplyCarSetup(Setup);
        }

        void Raise()
        {
            if (Changed != null) Changed();
        }

        // =====================================================================================================
        // SAVING
        // =====================================================================================================
        void ScheduleSave()
        {
            m_SaveAt = Time.unscaledTime + SaveDelay;
        }

        /// <summary>Call every frame (the Garage stage does): writes a pending save once edits pause.</summary>
        public void Tick()
        {
            if (m_SaveAt > 0f && Time.unscaledTime >= m_SaveAt) SaveNow();
        }

        public void SaveNow()
        {
            m_SaveAt = -1f;
            bool ok = true;
            if (m_SetupDirty) ok &= GarageSave.SaveActiveSetup(Setup);
            if (m_AppearanceDirty) ok &= GarageSave.SaveAppearance(Appearance);
            m_SetupDirty = m_AppearanceDirty = false;
            LastSaveFailed = !ok;
            LastSavedAt = Time.unscaledTime;
            Raise();
        }

        // =====================================================================================================
        // NAMED SETUPS AND LIVERIES
        // =====================================================================================================
        public static readonly string[] FactoryPresetKeys = { "Balanced (Valcorsa)", "High Downforce (Monaco)", "Low Downforce (Monza)", "Wet Weather Trim" };
        public static readonly string[] FactoryPresetTitles = { "BALANCED", "MAXIMUM DOWNFORCE", "MAXIMUM TOP SPEED", "WET WEATHER" };
        // The presets' own setupName differs from the key the preset manager lists them under.
        static readonly string[] s_FactoryPresetNames = { "Balanced Trim (Valcorsa)", "High Downforce (Monaco)", "Low Downforce (Monza)", "Wet Weather Trim" };

        /// <summary>Which factory preset a setup source names (by key or by the preset's own name), or -1.</summary>
        public static int PresetIndexOf(string source)
        {
            if (string.IsNullOrEmpty(source)) return -1;
            for (int i = 0; i < FactoryPresetKeys.Length; i++)
                if (source == FactoryPresetKeys[i] || source == s_FactoryPresetNames[i]) return i;
            return -1;
        }

        public List<string> UserSetupNames()
        {
            var names = CarSetupPresetManager.Instance.GetSavedSetupNames();
            names.RemoveAll(n => Array.IndexOf(FactoryPresetKeys, n) >= 0);
            return names;
        }

        public string SaveSetupAsNew()
        {
            var existing = UserSetupNames();
            string name;
            int n = 1;
            do { name = "AC-26 SETUP " + n.ToString("00"); n++; } while (existing.Contains(name) && n < 1000);
            var copy = Setup.Clone();
            if (!CarSetupPresetManager.Instance.SaveUserSetup(name, copy)) return null;
            SetupSource = name;
            Setup.setupName = name;
            m_SetupDirty = true;
            ScheduleSave();
            Raise();
            return name;
        }

        public void LoadNamedSetup(string name)
        {
            LoadSetup(CarSetupPresetManager.Instance.LoadSetupByName(name), name);
        }

        public bool DeleteNamedSetup(string name)
        {
            bool ok = CarSetupPresetManager.Instance.DeleteUserSetup(name);
            Raise();
            return ok;
        }

        public List<GarageLiverySlot> Liveries()
        {
            return GarageSave.LoadLiveries();
        }

        public string SaveLiveryAsNew()
        {
            var slots = GarageSave.LoadLiveries();
            if (slots.Count >= GarageSave.MaxLiveries) slots.RemoveAt(0);
            string name;
            int n = 1;
            do { name = "LIVERY " + n.ToString("00"); n++; } while (slots.Exists(s => s.name == name) && n < 100);
            slots.Add(new GarageLiverySlot { name = name, savedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"), appearance = Appearance.Clone() });
            if (!GarageSave.SaveLiveries(slots)) return null;
            Raise();
            return name;
        }

        public void DeleteLivery(string name)
        {
            var slots = GarageSave.LoadLiveries();
            slots.RemoveAll(s => s.name == name);
            GarageSave.SaveLiveries(slots);
            Raise();
        }

        /// <summary>Number of setup values that differ from the factory baseline.</summary>
        public int ChangesFromFactory(List<string> names = null)
        {
            var a = Setup;
            var b = Factory;
            int n = 0;
            Diff(ref n, names, "FRONT WING", a.frontWingAngle != b.frontWingAngle);
            Diff(ref n, names, "REAR WING", a.rearWingAngle != b.rearWingAngle);
            Diff(ref n, names, "DRS", a.drsEfficiency != b.drsEfficiency);
            Diff(ref n, names, "RIDE HEIGHT", a.frontRideHeight != b.frontRideHeight || a.rearRideHeight != b.rearRideHeight);
            Diff(ref n, names, "CAMBER", !Mathf.Approximately(a.frontCamber, b.frontCamber) || !Mathf.Approximately(a.rearCamber, b.rearCamber));
            Diff(ref n, names, "TOE", !Mathf.Approximately(a.frontToe, b.frontToe) || !Mathf.Approximately(a.rearToe, b.rearToe));
            Diff(ref n, names, "SPRINGS", a.frontSuspension != b.frontSuspension || a.rearSuspension != b.rearSuspension);
            Diff(ref n, names, "ANTI-ROLL BARS", a.frontAntiRollBar != b.frontAntiRollBar || a.rearAntiRollBar != b.rearAntiRollBar);
            Diff(ref n, names, "BRAKES", a.brakeBias != b.brakeBias || a.brakePressure != b.brakePressure || a.brakeDucts != b.brakeDucts);
            Diff(ref n, names, "COMPOUND", a.tireCompound != b.tireCompound);
            Diff(ref n, names, "PRESSURES", !Mathf.Approximately(a.frontLeftPressure, b.frontLeftPressure) || !Mathf.Approximately(a.frontRightPressure, b.frontRightPressure) ||
                                            !Mathf.Approximately(a.rearLeftPressure, b.rearLeftPressure) || !Mathf.Approximately(a.rearRightPressure, b.rearRightPressure));
            Diff(ref n, names, "DIFFERENTIAL", a.diffOnThrottle != b.diffOnThrottle || a.diffOffThrottle != b.diffOffThrottle || a.diffPreload != b.diffPreload);
            Diff(ref n, names, "FINAL DRIVE", !Mathf.Approximately(a.finalDriveRatio, b.finalDriveRatio));
            Diff(ref n, names, "ENGINE", a.engineMix != b.engineMix || a.engineBraking != b.engineBraking);
            return n;
        }

        static void Diff(ref int n, List<string> names, string label, bool changed)
        {
            if (!changed) return;
            n++;
            if (names != null) names.Add(label);
        }
    }
}
