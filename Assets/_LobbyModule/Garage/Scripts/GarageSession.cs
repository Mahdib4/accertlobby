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
        const float SaveDelay = 0.8f, RetryDelay = 5f;

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
        float m_SaveAt = -1f, m_RetryAt = -1f;
        bool m_SetupDirty, m_AppearanceDirty, m_Syncing;
        // The only setup value the car's appearance depends on (the sidewall marking colour).
        TireCompound m_AppliedCompound;

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
            SetupSource = SourceOf(Setup);
            Appearance = GarageSave.LoadAppearance();
            Analysis = CarSetupEngineeringEngine.Analyze(Setup);
            // The preset manager always starts from Balanced; give it the player's setup.
            m_Syncing = true;
            manager.SetActiveSetup(Setup);
            m_Syncing = false;
            manager.OnSetupChanged += OnManagerSetupChanged;
            // Static handler, removed first: with domain reload off it must not stack up across Play sessions.
            Application.quitting -= FlushOnQuit;
            Application.quitting += FlushOnQuit;
        }

        static void FlushOnQuit()
        {
            if (s_Current != null && s_Current.SavePending) s_Current.SaveNow();
        }

        void Attach(GarageCarRig rig)
        {
            m_Rig = rig;
            if (m_Rig == null) return;
            m_Rig.ApplyAppearance(Appearance, Setup.tireCompound);
            m_AppliedCompound = Setup.tireCompound;
            m_Rig.ApplySetupVisuals(Setup, true);
            PushToCar();
        }

        // Another screen (the engineering sheet) applied a setup: adopt it.
        void OnManagerSetupChanged(F1CarSetupData setup)
        {
            if (m_Syncing || setup == null) return;
            Setup = setup.Clone();
            SetupSource = SourceOf(Setup);
            AfterSetupChange(true);
            // Applied from the engineering sheet, while the Garage (the only thing that ticks the save timer) is closed.
            SaveNow();
        }

        // =====================================================================================================
        // CHANGES
        // =====================================================================================================
        /// <summary>Edits the setup in place; it is clamped, analysed, shown on the car and saved.</summary>
        public void EditSetup(Action<F1CarSetupData> edit)
        {
            if (edit == null) return;
            edit(Setup);
            // Edited values are no longer the preset or saved setup they came from.
            Setup.setupName = "";
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
                // Re-colouring is a full set of GPU blits: only when the compound (the sidewall colour) changed.
                if (Setup.tireCompound != m_AppliedCompound)
                {
                    m_Rig.ApplyAppearance(Appearance, Setup.tireCompound);
                    m_AppliedCompound = Setup.tireCompound;
                }
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
            if (m_Rig != null)
            {
                m_Rig.ApplyAppearance(Appearance, Setup.tireCompound);
                m_AppliedCompound = Setup.tireCompound;
            }
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

        /// <summary>Call every frame (the Garage screen and stage do): writes a pending save once edits pause, and
        /// retries a failed one.</summary>
        public void Tick()
        {
            float now = Time.unscaledTime;
            if ((m_SaveAt > 0f && now >= m_SaveAt) || (m_RetryAt > 0f && now >= m_RetryAt)) SaveNow();
        }

        public void SaveNow()
        {
            m_SaveAt = -1f;
            m_RetryAt = -1f;
            // Each flag clears only when its own write succeeded, so failed data stays queued and is retried.
            if (m_SetupDirty && GarageSave.SaveActiveSetup(Setup)) m_SetupDirty = false;
            if (m_AppearanceDirty && GarageSave.SaveAppearance(Appearance)) m_AppearanceDirty = false;
            LastSaveFailed = m_SetupDirty || m_AppearanceDirty;
            if (LastSaveFailed) m_RetryAt = Time.unscaledTime + RetryDelay;
            else LastSavedAt = Time.unscaledTime;
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

        /// <summary>Loads a saved or factory setup by name; false (and nothing changes) when it no longer exists.</summary>
        public bool LoadNamedSetup(string name)
        {
            // The manager falls back to Balanced for a missing file; never label that fallback with the user's name.
            if (Array.IndexOf(FactoryPresetKeys, name) < 0 && !UserSetupNames().Contains(name)) { Raise(); return false; }
            LoadSetup(CarSetupPresetManager.Instance.LoadSetupByName(name), name);
            return true;
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
            // Full: the player deletes a slot first; nothing is evicted behind their back.
            if (slots.Count >= GarageSave.MaxLiveries) return null;
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
            return ChangesBetween(Setup, Factory, names);
        }

        /// <summary>A setup's own name, unless it names a factory preset whose values it no longer matches.</summary>
        static string SourceOf(F1CarSetupData setup)
        {
            if (setup == null) return "";
            int preset = PresetIndexOf(setup.setupName);
            if (preset >= 0 && ChangesBetween(setup, CarSetupPresetManager.Instance.LoadSetupByName(FactoryPresetKeys[preset]), null) > 0) return "";
            return setup.setupName ?? "";
        }

        static int ChangesBetween(F1CarSetupData a, F1CarSetupData b, List<string> names)
        {
            int n = 0;
            if (a == null || b == null) return 0;
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
            Diff(ref n, names, "ERS", a.ersMode != b.ersMode || a.ersRecovery != b.ersRecovery);
            Diff(ref n, names, "ASSISTS", a.tractionControl != b.tractionControl || a.antiLockBrakes != b.antiLockBrakes);
            Diff(ref n, names, "FORCE FEEDBACK", a.ffbStrength != b.ffbStrength || a.ffbDamper != b.ffbDamper || a.ffbCurbDetail != b.ffbCurbDetail ||
                                                 a.understeerEnhance != b.understeerEnhance);
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
