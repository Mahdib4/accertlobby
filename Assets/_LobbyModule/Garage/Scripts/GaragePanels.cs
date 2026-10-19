using System;
using System.Collections.Generic;
using Accertone.CarSetup;
using Accertone.Settings;
using UnityEngine;

namespace Accertone.Garage
{
    /// <summary>Camera compositions for the categories and for individual controls (car-local focus points).</summary>
    public static class GarageShots
    {
        public static GarageShot Paint() { return GarageShot.Make(-35f, true, 10.6f, 1.5f, new Vector3(0f, 0.7f, 0f), 30f); }
        public static GarageShot FrontWing() { return GarageShot.Make(32f, false, 6.4f, 0.95f, new Vector3(0f, 0.38f, 1.65f), 28f); }
        public static GarageShot RearWing() { return GarageShot.Make(148f, false, 6.6f, 1.65f, new Vector3(0f, 0.85f, -2.2f), 28f); }
        public static GarageShot SideLow() { return GarageShot.Make(90f, false, 10.2f, 0.62f, new Vector3(0f, 0.45f, -0.2f), 28f); }
        public static GarageShot FrontAxle() { return GarageShot.Make(0f, false, 6.2f, 0.55f, new Vector3(0f, 0.4f, 1.4f), 28f); }
        public static GarageShot Brakes(GarageCarRig rig) { return GarageShot.Make(64f, false, 5.0f, 0.72f, Point(rig, GaragePart.Brakes, 64f, new Vector3(0.7f, 0.36f, 1.5f)), 28f); }
        public static GarageShot Tyre(GarageCarRig rig) { return GarageShot.Make(76f, false, 5.6f, 0.8f, Point(rig, GaragePart.Tyre, 76f, new Vector3(0.95f, 0.38f, 1.5f)), 28f); }
        public static GarageShot Powertrain() { return GarageShot.Make(152f, false, 8.6f, 1.95f, new Vector3(0f, 0.75f, -1.1f), 28f); }
        public static GarageShot Setup() { return GarageShot.Make(35f, false, 10.6f, 1.5f, new Vector3(0f, 0.7f, 0f), 30f); }
        public static GarageShot Info() { return GarageShot.Make(90f, true, 10.8f, 1.3f, new Vector3(0f, 0.65f, 0f), 30f); }

        static Vector3 Point(GarageCarRig rig, GaragePart part, float view, Vector3 fallback)
        {
            Vector3 p = rig != null ? rig.PartLocal(part) : fallback;
            // Positive views turn the nose to screen left, which shows the camera the car's left (-X) side.
            p.x = Mathf.Abs(p.x) * (view > 0f ? -1f : 1f);
            return p;
        }
    }

    /// <summary>
    /// Base for the eight configuration panels. A panel builds its rows into a <see cref="GarageStack"/>, binds them to
    /// <see cref="GarageSession"/> and refreshes its read-outs when the data changes.
    /// </summary>
    public abstract class GaragePanel
    {
        public GarageScreen screen;
        public GarageStack stack;
        protected readonly List<GarageControl> m_Controls = new List<GarageControl>();
        protected readonly List<Action> m_Refreshers = new List<Action>();

        protected GarageSession Session { get { return GarageSession.Current; } }
        protected F1CarSetupData Setup { get { return GarageSession.Current.Setup; } }
        protected GarageAppearance Look { get { return GarageSession.Current.Appearance; } }
        protected GarageCarRig Rig { get { return GarageStage.Instance != null ? GarageStage.Instance.CarRig : null; } }
        protected float W { get { return stack.width; } }

        public abstract string Title { get; }
        public abstract string Description { get; }
        public abstract GarageIconKind Icon { get; }
        public abstract GaragePart Part { get; }
        public abstract GarageShot Shot { get; }
        /// <summary>One line on what this category really changes (shown under the title).</summary>
        public abstract string Effect { get; }
        public virtual bool HasReset { get { return true; } }

        /// <summary>Short status for the menu item ("ROSSO · GLOSS").</summary>
        public abstract string Status();

        public abstract void Build();
        public virtual void ResetCategory() { }
        public virtual void OnOpen() { }
        public virtual void OnClose() { }

        /// <summary>Re-reads every control and read-out from the data.</summary>
        public void Refresh()
        {
            foreach (var c in stack.controls) if (c != null) c.Refresh();
            foreach (var r in m_Refreshers) r();
        }

        // ------------------------------------------------------------------------------------------ factories
        protected GarageSlider Slider(string label, float min, float max, float step, float baseline, Func<float> get, Action<float> set,
            Func<float, string> fmt, GarageEffect effect, GaragePart part, float width = -1f)
        {
            return GarageSlider.Create(stack.Root, label, width > 0f ? width : W, min, max, step, baseline, get, set, fmt, effect, part);
        }

        protected GarageSlider SetupSlider(string label, float min, float max, float step, float baseline, Func<F1CarSetupData, float> get,
            Action<F1CarSetupData, float> set, Func<float, string> fmt, GarageEffect effect, GaragePart part, float width = -1f)
        {
            return Slider(label, min, max, step, baseline, () => get(Setup), v => Session.EditSetup(s => set(s, v)), fmt, effect, part, width);
        }

        protected static GarageControl WithShot(GarageControl c, GarageShot shot)
        {
            c.hasShot = true;
            c.shot = shot;
            return c;
        }

        protected GarageReadout Readout(string label, bool bar, GarageEffect effect = GarageEffect.Estimate, float width = -1f)
        {
            return GarageReadout.Create(stack.Root, label, width > 0f ? width : W, bar, effect);
        }

        protected void OnRefresh(Action a)
        {
            m_Refreshers.Add(a);
        }

        protected static string I(float v) { return Mathf.RoundToInt(v).ToString(); }
        protected static string OfMax(float v, int max) { return Mathf.RoundToInt(v) + " / " + max; }
        protected static string Mm(float v) { return Mathf.RoundToInt(v) + " MM"; }
        protected static string Pct(float v) { return Mathf.RoundToInt(v) + "%"; }
        protected static string Deg(float v) { return v.ToString("0.00") + "°"; }
        protected static string Psi(float v) { return v.ToString("0.0") + " PSI"; }

        protected static string PhysicsNote
        {
            get { return "No vehicle physics in the lobby: the AC-26 here has no Rigidbody or wheel colliders. Values are saved to the setup and handed to VehicleSetupApplier for a car that has them."; }
        }
    }
}
