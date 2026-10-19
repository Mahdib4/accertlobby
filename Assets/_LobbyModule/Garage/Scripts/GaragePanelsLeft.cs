using System;
using Accertone.CarSetup;
using Accertone.Settings;
using UnityEngine;

namespace Accertone.Garage
{
    // =========================================================================================================
    // 01 PAINT & LIVERY
    // =========================================================================================================
    public class GaragePaintPanel : GaragePanel
    {
        public override string Title { get { return "Paint & Livery"; } }
        public override string Description { get { return "Repaint the AC-26 on the actual car: livery scheme, body and accent colours, finish and glow."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Paint; } }
        public override GaragePart Part { get { return GaragePart.Body; } }
        public override GarageShot Shot { get { return GarageShots.Paint(); } }
        public override string Effect { get { return "LIVE ON CAR · MATERIALS RECOLOURED ON THE GPU"; } }

        public override string Status()
        {
            var s = GarageCatalog.FindScheme(Look.schemeId);
            string scheme = s != null && SchemeIndex() >= 0 ? s.name : "CUSTOM";
            return scheme + "  ·  " + GarageCatalog.FinishName(Look.finish);
        }

        int SchemeIndex()
        {
            for (int i = 0; i < GarageCatalog.Schemes.Length; i++)
            {
                var s = GarageCatalog.Schemes[i];
                if (Same(Look.primary, s.primary) && Same(Look.accentA, s.accentA) && Same(Look.accentB, s.accentB)) return i;
            }
            return -1;
        }

        static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        public override void Build()
        {
            var schemes = GarageCatalog.Schemes;
            var names = new string[schemes.Length];
            var a = new Color[schemes.Length];
            var b = new Color[schemes.Length];
            for (int i = 0; i < schemes.Length; i++) { names[i] = schemes[i].name; a[i] = schemes[i].primary; b[i] = schemes[i].accentB; }
            stack.Section("LIVERY SCHEME", 0f);
            stack.Place(GarageChoice.CreateTiles(stack.Root, "SCHEME", W, 4, names, a, b, SchemeIndex,
                i => Session.EditAppearance(x => GarageCatalog.ApplyScheme(x, schemes[i])), GarageEffect.Live, GaragePart.Body));

            stack.Section("COLOUR");
            var prim = GarageCatalog.Primaries;
            var pn = new string[prim.Length];
            var pc = new Color[prim.Length];
            for (int i = 0; i < prim.Length; i++) { pn[i] = prim[i].name; pc[i] = prim[i].color; }
            stack.Place(GarageChoice.CreateSwatches(stack.Root, "BODY", W, pn, pc, null,
                () => { for (int i = 0; i < prim.Length; i++) if (Same(Look.primary, prim[i].color)) return i; return -1; },
                i => Session.EditAppearance(x => { x.primary = prim[i].color; x.schemeId = GarageCatalog.CustomSchemeId; }), GarageEffect.Live, GaragePart.Body));

            var acc = GarageCatalog.Accents;
            var an = new string[acc.Length];
            var aa = new Color[acc.Length];
            var ab = new Color[acc.Length];
            for (int i = 0; i < acc.Length; i++) { an[i] = acc[i].name; aa[i] = acc[i].color; ab[i] = acc[i].colorB; }
            stack.Place(GarageChoice.CreateSwatches(stack.Root, "ACCENT", W, an, aa, ab,
                () => { for (int i = 0; i < acc.Length; i++) if (Same(Look.accentA, acc[i].color) && Same(Look.accentB, acc[i].colorB)) return i; return -1; },
                i => Session.EditAppearance(x => { x.accentA = acc[i].color; x.accentB = acc[i].colorB; x.schemeId = GarageCatalog.CustomSchemeId; }), GarageEffect.Live, GaragePart.Body));

            stack.Section("FINISH");
            stack.Place(GarageChoice.Create(stack.Root, "PAINT FINISH", W, new[] { "GLOSS", "METALLIC", "SATIN", "MATTE" },
                () => (int)Look.finish, i => Session.EditAppearance(x => x.finish = (PaintFinish)i), GarageEffect.Live, GaragePart.Body));
            stack.Place(GarageChoice.Create(stack.Root, "LETTERING", W, new[] { "AUTO", "WHITE", "DARK" },
                () => (int)Look.lettering, i => Session.EditAppearance(x => x.lettering = (LetteringMode)i), GarageEffect.Live, GaragePart.Body));
            stack.Place(Slider("ACCENT GLOW", 0f, 1.6f, 0.1f, 1f, () => Look.glow, v => Session.EditAppearance(x => x.glow = v),
                v => Mathf.RoundToInt(v * 100f) + "%", GarageEffect.Live, GaragePart.Body));

            stack.Note("Race number \"13\", sponsor marks and the livery's graphic shapes are baked into the livery textures. New graphics need the livery source layers (Editor bake); here they recolour with the scheme.");
        }

        public override void ResetCategory()
        {
            Session.EditAppearance(x =>
            {
                var f = GarageAppearance.Factory();
                x.schemeId = f.schemeId; x.primary = f.primary; x.accentA = f.accentA; x.accentB = f.accentB;
                x.finish = f.finish; x.lettering = f.lettering; x.glow = f.glow;
            });
        }
    }

    // =========================================================================================================
    // 02 AERODYNAMICS
    // =========================================================================================================
    public class GarageAeroPanel : GaragePanel
    {
        GarageButton m_Drs;

        public override string Title { get { return "Aerodynamics"; } }
        public override string Description { get { return "Wing levels and the aero balance they create. The flaps on the car move with the settings."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Aero; } }
        public override GaragePart Part { get { return GaragePart.FrontWing; } }
        public override GarageShot Shot { get { return GarageShots.FrontWing(); } }
        public override string Effect { get { return "LIVE FLAP ANGLES · ESTIMATED AERO FIGURES"; } }

        public override string Status()
        {
            return "WINGS " + Setup.frontWingAngle + " / " + Setup.rearWingAngle + "  ·  " + GaragePowertrainPanel.Speed(Session.Analysis);
        }

        public override void Build()
        {
            var f = Session.Factory;
            stack.Section("WING LEVELS", 0f);
            stack.Place(WithShot(SetupSlider("FRONT WING", 1, 50, 1, f.frontWingAngle, s => s.frontWingAngle, (s, v) => s.frontWingAngle = Mathf.RoundToInt(v),
                v => OfMax(v, 50), GarageEffect.Live, GaragePart.FrontWing), GarageShots.FrontWing()));
            stack.Place(WithShot(SetupSlider("REAR WING", 1, 50, 1, f.rearWingAngle, s => s.rearWingAngle, (s, v) => s.rearWingAngle = Mathf.RoundToInt(v),
                v => OfMax(v, 50), GarageEffect.Live, GaragePart.RearWing), GarageShots.RearWing()));
            stack.Place(WithShot(SetupSlider("DRS EFFICIENCY", 1, 10, 1, f.drsEfficiency, s => s.drsEfficiency, (s, v) => s.drsEfficiency = Mathf.RoundToInt(v),
                v => OfMax(v, 10), GarageEffect.Stored, GaragePart.RearWing), GarageShots.RearWing()));
            m_Drs = GarageButton.Create(stack.Root, "PREVIEW DRS: OPEN FLAP", W, ToggleDrs, false, false, GarageEffect.Live, GarageIconKind.Aero);
            m_Drs.part = GaragePart.RearWing;
            WithShot(m_Drs, GarageShots.RearWing());
            stack.Place(m_Drs, 14f);

            stack.Section("AERO ESTIMATES");
            var balance = stack.Place(GarageBalance.Create(stack.Root, "AERO BALANCE", W));
            var down = stack.Place(Readout("DOWNFORCE INDEX", true));
            var drag = stack.Place(Readout("DRAG INDEX", true));
            var speed = stack.Place(Readout("EST. TOP SPEED", true));
            var handling = stack.Place(Readout("HANDLING", false));
            var fa = CarSetupEngineeringEngine.Analyze(f);
            OnRefresh(() =>
            {
                var a = Session.Analysis;
                balance.Set(a.aeroBalancePercent / 100f, "FRONT " + a.aeroBalancePercent.ToString("0.0") + "%", (100f - a.aeroBalancePercent).ToString("0.0") + "% REAR", fa.aeroBalancePercent / 100f);
                down.Set(Mathf.RoundToInt(a.downforceIndex) + " / 100", a.downforceIndex / 100f, fa.downforceIndex / 100f);
                drag.Set(Mathf.RoundToInt(a.dragIndex) + " / 100", a.dragIndex / 100f, fa.dragIndex / 100f);
                speed.Set(GaragePowertrainPanel.Speed(a), Mathf.InverseLerp(310f, 362f, a.topSpeedKmh), Mathf.InverseLerp(310f, 362f, fa.topSpeedKmh));
                handling.Set(a.handlingSummary, -1f, -1f, a.handlingColor);
                if (m_Drs != null && Rig != null) m_Drs.SetLabel(Rig.DrsPreviewOpen ? "PREVIEW DRS: CLOSE FLAP" : "PREVIEW DRS: OPEN FLAP");
            });
            stack.Note("DRS efficiency is stored with the setup; nothing in this build reads it. " + PhysicsNote);
        }

        void ToggleDrs()
        {
            if (Rig == null) return;
            Rig.SetDrsPreview(!Rig.DrsPreviewOpen);
            Refresh();
        }

        public override void OnClose()
        {
            if (Rig != null) Rig.SetDrsPreview(false);
        }

        public override void ResetCategory()
        {
            var f = Session.Factory;
            Session.EditSetup(s => { s.frontWingAngle = f.frontWingAngle; s.rearWingAngle = f.rearWingAngle; s.drsEfficiency = f.drsEfficiency; });
        }
    }

    // =========================================================================================================
    // 03 SUSPENSION
    // =========================================================================================================
    public class GarageSuspensionPanel : GaragePanel
    {
        public override string Title { get { return "Suspension"; } }
        public override string Description { get { return "Ride height, geometry, springs and anti-roll bars. Ride height, rake, camber and toe move on the car."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Suspension; } }
        public override GaragePart Part { get { return GaragePart.Suspension; } }
        public override GarageShot Shot { get { return GarageShots.SideLow(); } }
        public override string Effect { get { return "LIVE RIDE HEIGHT & GEOMETRY · ESTIMATED BALANCE"; } }

        public override string Status()
        {
            return "RIDE " + Setup.frontRideHeight + " / " + Setup.rearRideHeight + " MM  ·  RAKE " + (Setup.rearRideHeight - Setup.frontRideHeight) + " MM";
        }

        public override void Build()
        {
            var f = Session.Factory;
            float h = stack.Half;
            stack.Section("RIDE HEIGHT", 0f);
            stack.PlacePair(
                WithShot(SetupSlider("FRONT", 30, 80, 1, f.frontRideHeight, s => s.frontRideHeight, (s, v) => s.frontRideHeight = Mathf.RoundToInt(v), Mm, GarageEffect.Live, GaragePart.Suspension, h), GarageShots.SideLow()),
                WithShot(SetupSlider("REAR", 30, 80, 1, f.rearRideHeight, s => s.rearRideHeight, (s, v) => s.rearRideHeight = Mathf.RoundToInt(v), Mm, GarageEffect.Live, GaragePart.Suspension, h), GarageShots.SideLow()));
            var rake = stack.Place(Readout("RAKE", false, GarageEffect.Live));

            stack.Section("GEOMETRY");
            stack.PlacePair(
                WithShot(SetupSlider("FRONT CAMBER", -3.5f, -2.5f, 0.05f, f.frontCamber, s => s.frontCamber, (s, v) => s.frontCamber = v, Deg, GarageEffect.Live, GaragePart.Tyre, h), GarageShots.FrontAxle()),
                WithShot(SetupSlider("REAR CAMBER", -2f, -1f, 0.05f, f.rearCamber, s => s.rearCamber, (s, v) => s.rearCamber = v, Deg, GarageEffect.Live, GaragePart.Tyre, h), GarageShots.FrontAxle()));
            stack.PlacePair(
                WithShot(SetupSlider("FRONT TOE OUT", 0f, 0.5f, 0.05f, f.frontToe, s => s.frontToe, (s, v) => s.frontToe = v, Deg, GarageEffect.Live, GaragePart.Tyre, h), GarageShots.FrontAxle()),
                WithShot(SetupSlider("REAR TOE IN", 0f, 0.5f, 0.05f, f.rearToe, s => s.rearToe, (s, v) => s.rearToe = v, Deg, GarageEffect.Live, GaragePart.Tyre, h), GarageShots.FrontAxle()));

            stack.Section("SPRINGS & ANTI-ROLL BARS");
            stack.PlacePair(
                SetupSlider("FRONT SPRING", 1, 41, 1, f.frontSuspension, s => s.frontSuspension, (s, v) => s.frontSuspension = Mathf.RoundToInt(v), v => OfMax(v, 41), GarageEffect.Estimate, GaragePart.Suspension, h),
                SetupSlider("REAR SPRING", 1, 41, 1, f.rearSuspension, s => s.rearSuspension, (s, v) => s.rearSuspension = Mathf.RoundToInt(v), v => OfMax(v, 41), GarageEffect.Estimate, GaragePart.Suspension, h));
            stack.PlacePair(
                SetupSlider("FRONT ARB", 1, 21, 1, f.frontAntiRollBar, s => s.frontAntiRollBar, (s, v) => s.frontAntiRollBar = Mathf.RoundToInt(v), v => OfMax(v, 21), GarageEffect.Estimate, GaragePart.Suspension, h),
                SetupSlider("REAR ARB", 1, 21, 1, f.rearAntiRollBar, s => s.rearAntiRollBar, (s, v) => s.rearAntiRollBar = Mathf.RoundToInt(v), v => OfMax(v, 21), GarageEffect.Estimate, GaragePart.Suspension, h));

            stack.Section("ESTIMATES");
            var mech = stack.Place(GarageBalance.Create(stack.Root, "MECHANICAL BALANCE", W));
            var handling = stack.Place(Readout("HANDLING", false));
            var wear = stack.Place(Readout("TYRE WEAR RATE", true));
            var fa = CarSetupEngineeringEngine.Analyze(f);
            OnRefresh(() =>
            {
                var a = Session.Analysis;
                float r = Setup.rearRideHeight - Setup.frontRideHeight;
                rake.Set((r >= 0 ? "+" : "") + Mathf.RoundToInt(r) + " MM  (REAR HIGHER)");
                mech.Set(a.mechanicalBalance / 100f, "FRONT " + Mathf.RoundToInt(a.mechanicalBalance) + "%", Mathf.RoundToInt(100f - a.mechanicalBalance) + "% REAR", fa.mechanicalBalance / 100f);
                handling.Set(a.handlingSummary, -1f, -1f, a.handlingColor);
                wear.Set("× " + a.tireWearRateIndex.ToString("0.00"), Mathf.InverseLerp(0.8f, 1.6f, a.tireWearRateIndex), Mathf.InverseLerp(0.8f, 1.6f, fa.tireWearRateIndex));
            });
            stack.Note("Toe angles are real but tiny (0.05° steps) and barely visible. Springs and anti-roll bars drive the estimates; ride height and springs also reach VehicleSetupApplier's wheel colliders when a car has them. " + PhysicsNote);
        }

        public override void ResetCategory()
        {
            var f = Session.Factory;
            Session.EditSetup(s =>
            {
                s.frontRideHeight = f.frontRideHeight; s.rearRideHeight = f.rearRideHeight; s.frontCamber = f.frontCamber; s.rearCamber = f.rearCamber;
                s.frontToe = f.frontToe; s.rearToe = f.rearToe; s.frontSuspension = f.frontSuspension; s.rearSuspension = f.rearSuspension;
                s.frontAntiRollBar = f.frontAntiRollBar; s.rearAntiRollBar = f.rearAntiRollBar;
            });
        }
    }

    // =========================================================================================================
    // 04 BRAKING SYSTEM
    // =========================================================================================================
    public class GarageBrakesPanel : GaragePanel
    {
        bool m_Heat = true;

        public override string Title { get { return "Braking System"; } }
        public override string Description { get { return "Brake balance, line pressure and cooling. The heat preview shows the front / rear split on the car."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Brakes; } }
        public override GaragePart Part { get { return GaragePart.Brakes; } }
        public override GarageShot Shot { get { return GarageShots.Brakes(Rig); } }
        public override string Effect { get { return "LIVE HEAT PREVIEW · ESTIMATED BRAKING"; } }

        public override string Status()
        {
            return "BIAS " + Setup.brakeBias + "%  ·  PRESSURE " + Setup.brakePressure + "%";
        }

        public override void Build()
        {
            var f = Session.Factory;
            var shot = GarageShots.Brakes(Rig);
            stack.Section("BRAKE SETUP", 0f);
            stack.Place(WithShot(SetupSlider("BRAKE BIAS (FRONT)", 50, 70, 1, f.brakeBias, s => s.brakeBias, (s, v) => s.brakeBias = Mathf.RoundToInt(v), Pct, GarageEffect.Estimate, GaragePart.Brakes), shot));
            stack.Place(WithShot(SetupSlider("BRAKE PRESSURE", 80, 100, 1, f.brakePressure, s => s.brakePressure, (s, v) => s.brakePressure = Mathf.RoundToInt(v), Pct, GarageEffect.Estimate, GaragePart.Brakes), shot));
            stack.Place(WithShot(SetupSlider("BRAKE DUCTS", 1, 5, 1, f.brakeDucts, s => s.brakeDucts, (s, v) => s.brakeDucts = Mathf.RoundToInt(v),
                v => new[] { "", "1 · CLOSED", "2", "3", "4", "5 · OPEN" }[Mathf.Clamp(Mathf.RoundToInt(v), 1, 5)], GarageEffect.Estimate, GaragePart.Brakes), shot));
            stack.Place(WithShot(GarageChoice.Create(stack.Root, "HEAT PREVIEW", W, new[] { "OFF", "ON" }, () => m_Heat ? 1 : 0,
                i => { m_Heat = i == 1; ApplyHeat(); Refresh(); }, GarageEffect.Live, GaragePart.Brakes), shot));

            stack.Section("ESTIMATES");
            var balance = stack.Place(GarageBalance.Create(stack.Root, "BRAKE BALANCE  ·  MARKER = IDEAL", W));
            var stop = stack.Place(Readout("STOPPING 200 → 0 KM/H", true));
            var decel = stack.Place(Readout("PEAK DECELERATION", false));
            var temp = stack.Place(Readout("DISC OPERATING WINDOW", false));
            var tendency = stack.Place(Readout("UNDER BRAKING", false));
            OnRefresh(() =>
            {
                var b = GarageEstimates.Brakes(Setup, Session.Analysis);
                balance.Set(Setup.brakeBias / 100f, "FRONT " + Setup.brakeBias + "%", (100 - Setup.brakeBias) + "% REAR", b.idealBias / 100f);
                stop.Set(b.stoppingDistance200.ToString("0.0") + " M", Mathf.InverseLerp(70f, 30f, b.stoppingDistance200));
                decel.Set(b.peakDecelG.ToString("0.0") + " G");
                temp.Set(Mathf.RoundToInt(b.discTempMin) + " – " + Mathf.RoundToInt(b.discTempMax) + " °C");
                tendency.Set(b.tendency, -1f, -1f, b.tendencyColor);
                ApplyHeat();
            });

            stack.Section("APPEARANCE");
            stack.Note("Caliper colour: the calipers are merged into the upright meshes and share the suspension material, so they cannot be recoloured on their own. This needs a separate caliper mesh and material.");
            stack.Note("Driving aids (ABS, traction control) are set in Settings › Gameplay, so they are not duplicated here. " + PhysicsNote, GarageIconKind.Lock);
        }

        void ApplyHeat()
        {
            if (Rig == null) return;
            if (!m_Heat) { Rig.SetBrakeHeat(0f, 0f); return; }
            var b = GarageEstimates.Brakes(Setup, Session.Analysis);
            float load = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(80f, 100f, Setup.brakePressure));
            // Heat follows the bias: the axle doing more of the work glows more.
            Rig.SetBrakeHeat(load * Mathf.Clamp01(b.frontHeat / 0.6f), load * Mathf.Clamp01(b.rearHeat / 0.6f));
        }

        public override void OnOpen() { ApplyHeat(); }

        public override void OnClose()
        {
            if (Rig != null) Rig.SetBrakeHeat(0f, 0f);
        }

        public override void ResetCategory()
        {
            var f = Session.Factory;
            Session.EditSetup(s => { s.brakeBias = f.brakeBias; s.brakePressure = f.brakePressure; s.brakeDucts = f.brakeDucts; });
        }
    }
}
