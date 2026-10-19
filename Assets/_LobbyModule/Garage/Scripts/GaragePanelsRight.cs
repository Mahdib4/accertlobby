using System;
using System.Collections.Generic;
using Accertone.CarSetup;
using Accertone.Settings;
using UnityEngine;

namespace Accertone.Garage
{
    // =========================================================================================================
    // 05 TIRES & WHEELS
    // =========================================================================================================
    public class GarageTyresPanel : GaragePanel
    {
        static readonly TireCompound[] s_Order = { TireCompound.C3_Soft, TireCompound.C2_Medium, TireCompound.C1_Hard, TireCompound.Intermediate, TireCompound.Wet };

        public override string Title { get { return "Tires & Wheels"; } }
        public override string Description { get { return "Compound, pressures and rims. Sidewall markings and rim finish change on the car."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Tyres; } }
        public override GaragePart Part { get { return GaragePart.Tyre; } }
        public override GarageShot Shot { get { return GarageShots.Tyre(Rig); } }
        public override string Effect { get { return "LIVE MARKINGS & RIMS · ESTIMATED GRIP AND WEAR"; } }

        public override string Status()
        {
            var rim = GarageCatalog.FindRim(Look.rimId);
            return GarageCatalog.CompoundName(Setup.tireCompound) + "  ·  " + (rim != null ? rim.name : "WORKS") + " RIMS";
        }

        public override void Build()
        {
            var f = Session.Factory;
            var shot = GarageShots.Tyre(Rig);
            var names = new string[s_Order.Length];
            var dots = new Color[s_Order.Length];
            for (int i = 0; i < s_Order.Length; i++) { names[i] = GarageCatalog.CompoundName(s_Order[i]); dots[i] = GarageCatalog.CompoundColor(s_Order[i]); }
            stack.Section("COMPOUND", 0f);
            stack.Place(WithShot(GarageChoice.Create(stack.Root, "TYRE COMPOUND", W, names, () => Array.IndexOf(s_Order, Setup.tireCompound),
                i => Session.EditSetup(s => s.tireCompound = s_Order[i]), GarageEffect.Live, GaragePart.Tyre, dots), shot));
            stack.Place(WithShot(GarageChoice.Create(stack.Root, "SIDEWALL MARKING", W, new[] { "COMPOUND COLOURS", "TEAM COLOUR" },
                () => (int)Look.sidewall, i => Session.EditAppearance(x => x.sidewall = (SidewallMarking)i), GarageEffect.Live, GaragePart.Tyre), shot));

            stack.Section("PRESSURES  ·  PSI");
            float h = stack.Half;
            stack.PlacePair(
                WithShot(SetupSlider("FRONT LEFT", 21.5f, 25.5f, 0.1f, f.frontLeftPressure, s => s.frontLeftPressure, (s, v) => s.frontLeftPressure = v, Psi, GarageEffect.Estimate, GaragePart.Tyre, h), shot),
                WithShot(SetupSlider("FRONT RIGHT", 21.5f, 25.5f, 0.1f, f.frontRightPressure, s => s.frontRightPressure, (s, v) => s.frontRightPressure = v, Psi, GarageEffect.Estimate, GaragePart.Tyre, h), shot));
            stack.PlacePair(
                WithShot(SetupSlider("REAR LEFT", 20f, 23.5f, 0.1f, f.rearLeftPressure, s => s.rearLeftPressure, (s, v) => s.rearLeftPressure = v, Psi, GarageEffect.Estimate, GaragePart.Tyre, h), shot),
                WithShot(SetupSlider("REAR RIGHT", 20f, 23.5f, 0.1f, f.rearRightPressure, s => s.rearRightPressure, (s, v) => s.rearRightPressure = v, Psi, GarageEffect.Estimate, GaragePart.Tyre, h), shot));

            stack.Section("RIMS");
            var rims = GarageCatalog.Rims;
            var rn = new string[rims.Length];
            var ra = new Color[rims.Length];
            var rb = new Color[rims.Length];
            for (int i = 0; i < rims.Length; i++)
            {
                rn[i] = rims[i].name;
                ra[i] = rims[i].source ? GarageCatalog.Hex(0x1A191B) : (rims[i].matchLivery ? Look.primary : rims[i].primary);
                rb[i] = rims[i].source ? GarageCatalog.SourceMagenta : (rims[i].matchLivery ? Look.accentB : rims[i].accent);
            }
            stack.Place(WithShot(GarageChoice.CreateTiles(stack.Root, "RIM FINISH", W, 3, rn, ra, rb,
                () => { for (int i = 0; i < rims.Length; i++) if (rims[i].id == Look.rimId) return i; return 0; },
                i => Session.EditAppearance(x => x.rimId = rims[i].id), GarageEffect.Live, GaragePart.Tyre), shot));

            stack.Section("ESTIMATES");
            var grip = stack.Place(Readout("DRY GRIP", true));
            var stint = stack.Place(Readout("STINT LENGTH", false));
            var window = stack.Place(Readout("OPERATING WINDOW", false));
            var best = stack.Place(Readout("BEST FOR", false));
            OnRefresh(() =>
            {
                var t = GarageEstimates.Tyre(Setup, Session.Analysis);
                grip.Set(Mathf.RoundToInt(t.gripIndex) + " / 100", t.gripIndex / 100f);
                stint.Set("≈ " + Mathf.RoundToInt(t.stintLaps) + " LAPS");
                window.Set(Mathf.RoundToInt(t.tempMin) + " – " + Mathf.RoundToInt(t.tempMax) + " °C");
                best.Set(t.bestFor);
            });
            stack.Note("Wheel design: the model carries a single rim mesh, so other wheel designs need additional rim meshes. Wheels do not spin in the showroom (the presentation system does not animate them).");
            stack.Note("Pressures drive the grip and wear estimates and are stored for the vehicle physics. " + PhysicsNote);
        }

        public override void ResetCategory()
        {
            var f = Session.Factory;
            Session.EditSetup(s =>
            {
                s.tireCompound = f.tireCompound; s.frontLeftPressure = f.frontLeftPressure; s.frontRightPressure = f.frontRightPressure;
                s.rearLeftPressure = f.rearLeftPressure; s.rearRightPressure = f.rearRightPressure;
            });
            Session.EditAppearance(x => { x.rimId = GarageCatalog.FactoryRimId; x.sidewall = SidewallMarking.Compound; });
        }
    }

    // =========================================================================================================
    // 06 POWERTRAIN
    // =========================================================================================================
    public class GaragePowertrainPanel : GaragePanel
    {
        public override string Title { get { return "Powertrain"; } }
        public override string Description { get { return "Power unit reference specification, plus the drivetrain settings stored with the setup."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Powertrain; } }
        public override GaragePart Part { get { return GaragePart.Powertrain; } }
        public override GarageShot Shot { get { return GarageShots.Powertrain(); } }
        public override string Effect { get { return "SPECIFICATION · STORED SETTINGS · ESTIMATED TOP SPEED"; } }

        public override string Status()
        {
            return "FINAL DRIVE " + Setup.finalDriveRatio.ToString("0.00") + "  ·  " + Setup.engineMix.ToString().ToUpperInvariant() + " MIX";
        }

        public override void Build()
        {
            var f = Session.Factory;
            stack.Section("POWER UNIT  ·  REFERENCE SPECIFICATION", 0f);
            stack.Place(Readout("ENGINE", false, GarageEffect.Spec)).Set("1.6 L V6 TURBO · 90°");
            stack.Place(Readout("HYBRID SYSTEM", false, GarageEffect.Spec)).Set("MGU-K 120 KW · MGU-H");
            stack.Place(Readout("REV LIMIT", false, GarageEffect.Spec)).Set("15,000 RPM");
            stack.Place(Readout("GEARBOX", false, GarageEffect.Spec)).Set("8-SPEED SEAMLESS · RWD");
            stack.Note("Regulation-class reference figures for the AC-26's power unit; the game has no engine simulation behind them.", GarageIconKind.Info);

            stack.Section("GEARING");
            stack.Place(SetupSlider("FINAL DRIVE RATIO", 2.5f, 4.5f, 0.05f, f.finalDriveRatio, s => s.finalDriveRatio, (s, v) => s.finalDriveRatio = v,
                v => v.ToString("0.00") + " : 1", GarageEffect.Estimate, GaragePart.Powertrain));
            var speed = stack.Place(Readout("EST. TOP SPEED", true));

            stack.Section("DIFFERENTIAL & ENGINE");
            float h = stack.Half;
            stack.PlacePair(
                SetupSlider("DIFF ON THROTTLE", 50, 100, 1, f.diffOnThrottle, s => s.diffOnThrottle, (s, v) => s.diffOnThrottle = Mathf.RoundToInt(v), Pct, GarageEffect.Stored, GaragePart.Powertrain, h),
                SetupSlider("DIFF OFF THROTTLE", 50, 100, 1, f.diffOffThrottle, s => s.diffOffThrottle, (s, v) => s.diffOffThrottle = Mathf.RoundToInt(v), Pct, GarageEffect.Stored, GaragePart.Powertrain, h));
            stack.PlacePair(
                SetupSlider("DIFF PRELOAD", 0, 100, 1, f.diffPreload, s => s.diffPreload, (s, v) => s.diffPreload = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v) + " NM", GarageEffect.Stored, GaragePart.Powertrain, h),
                SetupSlider("ENGINE BRAKING", 1, 10, 1, f.engineBraking, s => s.engineBraking, (s, v) => s.engineBraking = Mathf.RoundToInt(v), v => OfMax(v, 10), GarageEffect.Stored, GaragePart.Powertrain, h));
            stack.Place(GarageChoice.Create(stack.Root, "FUEL MIX", W, new[] { "LEAN", "STANDARD", "RICH" }, () => (int)Setup.engineMix,
                i => Session.EditSetup(s => s.engineMix = (EngineFuelMix)i), GarageEffect.Stored, GaragePart.Powertrain));
            stack.Note("Differential, engine braking and fuel mix are saved with the setup for a drivetrain simulation; nothing in this build reads them. " + PhysicsNote);

            stack.Section("ENERGY RECOVERY (ERS)");
            stack.Place(GarageChoice.Create(stack.Root, "ERS DEPLOYMENT", W, new[] { "NONE", "LOW", "BALANCED", "HOTLAP", "OVERTAKE" }, () => (int)Setup.ersMode,
                i => { }, GarageEffect.Unavailable, GaragePart.Powertrain));
            stack.Note("Not available: there is no energy store or MGU-K deployment simulation in this build, so ERS cannot be set here. The value shown is the one stored in the setup.");

            OnRefresh(() =>
            {
                var a = Session.Analysis;
                var fa = CarSetupEngineeringEngine.Analyze(f);
                speed.Set(Speed(a), Mathf.InverseLerp(310f, 362f, a.topSpeedKmh), Mathf.InverseLerp(310f, 362f, fa.topSpeedKmh));
            });
        }

        public static string Speed(SetupTelemetryAnalysis a)
        {
            bool imperial = false;
            try { var s = AccertoneSettings.Instance; imperial = s != null && s.Lobby != null && s.Lobby.Imperial; } catch (Exception) { }
            return imperial ? Mathf.RoundToInt(a.topSpeedMph) + " MPH" : Mathf.RoundToInt(a.topSpeedKmh) + " KM/H";
        }

        public override void ResetCategory()
        {
            var f = Session.Factory;
            Session.EditSetup(s =>
            {
                s.finalDriveRatio = f.finalDriveRatio; s.diffOnThrottle = f.diffOnThrottle; s.diffOffThrottle = f.diffOffThrottle;
                s.diffPreload = f.diffPreload; s.engineBraking = f.engineBraking; s.engineMix = f.engineMix;
            });
        }
    }

    // =========================================================================================================
    // 07 CAR SETUP
    // =========================================================================================================
    public class GarageSetupPanel : GaragePanel
    {
        public override string Title { get { return "Car Setup"; } }
        public override string Description { get { return "Presets, your saved setups and liveries, and factory resets. Everything here is saved on disk."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Setup; } }
        public override GaragePart Part { get { return GaragePart.Centre; } }
        public override GarageShot Shot { get { return GarageShots.Setup(); } }
        public override string Effect { get { return "SAVED TO DISK · APPEARANCE AND SETUP KEPT SEPARATE"; } }
        public override bool HasReset { get { return false; } }

        public override string Status()
        {
            int changes = Session.ChangesFromFactory();
            string src = PresetIndex() >= 0 ? GarageSession.FactoryPresetTitles[PresetIndex()] : (!string.IsNullOrEmpty(Session.SetupSource) ? Session.SetupSource.ToUpperInvariant() : "CUSTOM");
            return src + "  ·  " + (changes == 0 ? "FACTORY" : changes + " CHANGED");
        }

        int PresetIndex()
        {
            return GarageSession.PresetIndexOf(Session.SetupSource);
        }

        public override void Build()
        {
            stack.Section("PRESETS", 0f);
            var shades = new[] { SettingsTheme.Silver, SettingsTheme.Accent, SettingsTheme.Positive, GarageCatalog.CompoundColor(TireCompound.Wet) };
            stack.Place(GarageChoice.CreateTiles(stack.Root, "LOAD PRESET", W, 2, GarageSession.FactoryPresetTitles, shades, shades, PresetIndex,
                i => Session.LoadNamedSetup(GarageSession.FactoryPresetKeys[i]), GarageEffect.Live, GaragePart.Centre));
            var current = stack.Place(Readout("ACTIVE SETUP", false, GarageEffect.Spec));
            var changed = stack.Place(Readout("CHANGED FROM FACTORY", false, GarageEffect.Spec));

            stack.Section("SAVED SETUPS");
            var names = Session.UserSetupNames();
            names.Sort(StringComparer.Ordinal);
            int shown = 0;
            for (int i = names.Count - 1; i >= 0 && shown < 4; i--, shown++)
            {
                string n = names[i];
                var load = GarageButton.Create(stack.Root, "LOAD  " + n, W - 140f, () => { Session.LoadNamedSetup(n); screen.RebuildPanel(); }, false, false, GarageEffect.Live, GarageIconKind.Setup, 36f);
                var del = GarageButton.Create(stack.Root, "DELETE", 128f, () => { Session.DeleteNamedSetup(n); screen.RebuildPanel(); }, false, true, GarageEffect.Spec, null, 36f);
                PlaceRow(load, del);
            }
            if (names.Count == 0) stack.Note("No saved setups yet.", GarageIconKind.Save);
            stack.Place(GarageButton.Create(stack.Root, "SAVE CURRENT SETUP", W, () => { Session.SaveSetupAsNew(); screen.RebuildPanel(); }, true, false, GarageEffect.Spec, GarageIconKind.Save), 14f);

            stack.Section("SAVED LIVERIES");
            var liveries = Session.Liveries();
            for (int i = liveries.Count - 1, k = 0; i >= 0 && k < 3; i--, k++)
            {
                var slot = liveries[i];
                var scheme = GarageCatalog.FindScheme(slot.appearance.schemeId);
                string label = "APPLY  " + slot.name + "  ·  " + (scheme != null ? scheme.name : "CUSTOM") + " " + GarageCatalog.FinishName(slot.appearance.finish);
                var apply = GarageButton.Create(stack.Root, label, W - 140f, () => { Session.LoadAppearance(slot.appearance); screen.RebuildPanel(); }, false, false, GarageEffect.Live, GarageIconKind.Paint, 36f);
                var del = GarageButton.Create(stack.Root, "DELETE", 128f, () => { Session.DeleteLivery(slot.name); screen.RebuildPanel(); }, false, true, GarageEffect.Spec, null, 36f);
                PlaceRow(apply, del);
            }
            if (liveries.Count == 0) stack.Note("No saved liveries yet.", GarageIconKind.Save);
            stack.Place(GarageButton.Create(stack.Root, "SAVE CURRENT LIVERY", W, () => { Session.SaveLiveryAsNew(); screen.RebuildPanel(); }, true, false, GarageEffect.Spec, GarageIconKind.Paint), 14f);

            stack.Section("RESET");
            stack.Place(GarageButton.Create(stack.Root, "RESET SETUP TO FACTORY", W, () => { Session.ResetSetupToFactory(); screen.RebuildPanel(); }, false, true, GarageEffect.Live, GarageIconKind.Reset));
            stack.Place(GarageButton.Create(stack.Root, "RESET APPEARANCE TO FACTORY", W, () => { Session.ResetAppearanceToFactory(); screen.RebuildPanel(); }, false, true, GarageEffect.Live, GarageIconKind.Reset));
            stack.Place(GarageButton.Create(stack.Root, "OPEN ENGINEERING SHEET  (ALL PARAMETERS)", W, () => screen.OpenEngineeringSheet(), false, false, GarageEffect.Spec, GarageIconKind.Info));
            stack.Note("Your current setup and appearance are saved automatically a moment after every change and restored the next time the game starts. Saved setups also appear in the engineering sheet.", GarageIconKind.Save);

            OnRefresh(() =>
            {
                int pi = PresetIndex();
                current.Set(pi >= 0 ? GarageSession.FactoryPresetTitles[pi] : (!string.IsNullOrEmpty(Session.SetupSource) ? Session.SetupSource.ToUpperInvariant() : "CUSTOM"));
                var list = new List<string>();
                int n = Session.ChangesFromFactory(list);
                changed.Set(n == 0 ? "NONE" : string.Join(" · ", list.ToArray()));
            });
        }

        void PlaceRow(GarageButton a, GarageButton b)
        {
            var ra = (RectTransform)a.transform;
            var rb = (RectTransform)b.transform;
            SettingsUI.At(ra, 0f, stack.y, ra.sizeDelta.x, ra.sizeDelta.y);
            SettingsUI.At(rb, W - rb.sizeDelta.x, stack.y, rb.sizeDelta.x, rb.sizeDelta.y);
            stack.y += ra.sizeDelta.y + 8f;
            stack.rows.Add(ra);
            stack.rows.Add(rb);
            stack.controls.Add(a);
            stack.controls.Add(b);
        }
    }

    // =========================================================================================================
    // 08 VEHICLE INFORMATION
    // =========================================================================================================
    public class GarageInfoPanel : GaragePanel
    {
        public override string Title { get { return "Vehicle Information"; } }
        public override string Description { get { return "The AC-26's identity and specification. Dimensions are measured from the car model itself."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Info; } }
        public override GaragePart Part { get { return GaragePart.Cockpit; } }
        public override GarageShot Shot { get { return GarageShots.Info(); } }
        public override string Effect { get { return "SPECIFICATION · MEASURED FROM THE MODEL"; } }
        public override bool HasReset { get { return false; } }

        public override string Status()
        {
            return "AC-26  ·  WORKS PROTOTYPE";
        }

        public override void Build()
        {
            stack.Section("IDENTITY", 0f);
            stack.Place(Readout("MODEL", false, GarageEffect.Spec)).Set("AC-26");
            stack.Place(Readout("CLASS", false, GarageEffect.Spec)).Set(LobbyText("ChassisInfo/Description", "WORKS PROTOTYPE · HYBRID V6"));
            stack.Place(Readout("CHASSIS", false, GarageEffect.Spec)).Set(LobbyText("ChassisInfo/Index", "01 / 01"));
            stack.Place(Readout("RACE NUMBER", false, GarageEffect.Spec)).Set("13");

            stack.Section("DIMENSIONS  ·  MEASURED FROM THE MODEL");
            var rig = Rig;
            if (rig != null)
            {
                var b = rig.LocalBounds;
                stack.Place(Readout("LENGTH  ·  WIDTH  ·  HEIGHT", false, GarageEffect.Spec)).Set(b.size.z.ToString("0.00") + "  ·  " + b.size.x.ToString("0.00") + "  ·  " + b.size.y.ToString("0.00") + " M");
                stack.Place(Readout("WHEELBASE", false, GarageEffect.Spec)).Set(rig.Wheelbase.ToString("0.00") + " M");
                stack.Place(Readout("TRACK  FRONT  ·  REAR", false, GarageEffect.Spec)).Set(rig.TrackFront.ToString("0.00") + "  ·  " + rig.TrackRear.ToString("0.00") + " M");
                stack.Place(Readout("WHEEL DIAMETER", false, GarageEffect.Spec)).Set((rig.WheelRadius * 2f).ToString("0.00") + " M");
            }

            stack.Section("RATINGS  ·  LOBBY CHASSIS CARD");
            AddRating("POWER");
            AddRating("AERO");
            AddRating("GRIP");

            stack.Section("POWER & MASS  ·  REFERENCE");
            stack.Place(Readout("POWER UNIT", false, GarageEffect.Spec)).Set("1.6 L V6 TURBO HYBRID");
            stack.Place(Readout("DRIVETRAIN", false, GarageEffect.Spec)).Set("RWD · 8-SPEED SEAMLESS");
            stack.Place(Readout("MINIMUM MASS", false, GarageEffect.Spec)).Set("800 KG  (REGULATION, WITH DRIVER)");

            stack.Section("CURRENT CONFIGURATION");
            var livery = stack.Place(Readout("LIVERY", false, GarageEffect.Spec));
            var tyres = stack.Place(Readout("TYRES  ·  RIMS", false, GarageEffect.Spec));
            var setup = stack.Place(Readout("SETUP", false, GarageEffect.Spec));
            var speed = stack.Place(Readout("EST. TOP SPEED  ·  DOWNFORCE", false, GarageEffect.Estimate));
            OnRefresh(() =>
            {
                var scheme = GarageCatalog.FindScheme(Look.schemeId);
                livery.Set((scheme != null ? scheme.name : "CUSTOM") + "  ·  " + GarageCatalog.FinishName(Look.finish));
                var rim = GarageCatalog.FindRim(Look.rimId);
                tyres.Set(GarageCatalog.CompoundName(Setup.tireCompound) + "  ·  " + (rim != null ? rim.name : "WORKS"));
                setup.Set(string.IsNullOrEmpty(Session.SetupSource) ? "CUSTOM" : Session.SetupSource.ToUpperInvariant());
                speed.Set(GaragePowertrainPanel.Speed(Session.Analysis) + "  ·  " + Session.Analysis.downforceIndex.ToString("0.0"));
            });

            stack.Note("Base 3D model by Abu Saif (sketchfab.com/abuhossain844), licensed CC BY 4.0, with the Accert one livery and materials.", GarageIconKind.Info);
        }

        void AddRating(string key)
        {
            string value = LobbyText("ChassisInfo/Value_" + key, "");
            float v;
            var r = stack.Place(Readout(key, true, GarageEffect.Spec));
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) r.Set(v.ToString("0.0") + " / 10", v / 10f);
            else r.Set(string.IsNullOrEmpty(value) ? "—" : value);
        }

        static string LobbyText(string path, string fallback)
        {
            var canvas = GameObject.Find("UI_Lobby");
            if (canvas == null) return fallback;
            var t = canvas.transform.Find(path);
            var text = t != null ? t.GetComponent<TMPro.TMP_Text>() : null;
            return text != null && !string.IsNullOrEmpty(text.text) ? text.text.ToUpperInvariant() : fallback;
        }
    }
}
