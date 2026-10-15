using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Accertone.Settings
{
    /// <summary>Outcome of an interactive rebind.</summary>
    public struct RebindResult
    {
        public bool cancelled;
        public bool conflict;
        public string path;                 // the control the player pressed
        public SettingDefinition other;     // conflicting action (when conflict)
        public int otherSlot;
    }

    /// <summary>
    /// Controls: the "Driving" action map of the project-wide Input Actions. Rebinding uses the Input System's
    /// interactive rebinding and binding overrides (saved with the settings). A pending/applied snapshot gives the
    /// same Apply / Discard behaviour as every other setting. Steering response curves live in <see cref="DrivingInput"/>.
    /// </summary>
    public class InputSettingsSystem : ISettingsSystem
    {
        public const string MapName = "Driving";
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        AccertoneSettings m_S;
        readonly Dictionary<string, string> m_AppliedPaths = new Dictionary<string, string>();
        readonly List<SettingDefinition> m_BindingDefs = new List<SettingDefinition>();
        InputActionRebindingExtensions.RebindingOperation m_Rebind;
        bool m_PreviewEnabled;

        public InputActionMap Driving
        {
            get { var a = InputSystem.actions; return a != null ? a.FindActionMap(MapName) : null; }
        }

        public bool IsRebinding { get { return m_Rebind != null; } }

        public void Initialise(AccertoneSettings settings)
        {
            m_S = settings;
            foreach (var d in settings.Catalog.AllDefinitions()) if (d.kind == SettingKind.Binding) m_BindingDefs.Add(d);
            if (Driving == null) Debug.LogWarning("[Accert one] Input Actions have no '" + MapName + "' map. Run Accert one > Settings > Build.");
        }

        public void OnSettingChanged(string key)
        {
            if (key == SettingKeys.PadSteerStick) ApplySteerStick();
            DrivingInput.Refresh(m_S);
        }

        public void ApplyAll()
        {
            ApplySteerStick();
            DrivingInput.Refresh(m_S);
        }

        public void OnSceneLoaded(Scene scene) { }

        public void Shutdown()
        {
            CancelRebind();
            SetPreview(false);
        }

        // ------------------------------------------------------------------ overrides / snapshots
        public void LoadOverrides(string json)
        {
            var map = Driving;
            if (map == null) return;
            map.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(json))
            {
                try { map.LoadBindingOverridesFromJson(json); }
                catch (Exception e) { Debug.LogWarning("[Accert one] Saved bindings ignored: " + e.Message); }
            }
            ApplySteerStick();
            Snapshot();
        }

        public string SaveOverrides()
        {
            var map = Driving;
            return map != null ? map.SaveBindingOverridesAsJson() : "";
        }

        void Snapshot()
        {
            m_AppliedPaths.Clear();
            var map = Driving;
            if (map == null) return;
            foreach (var b in map.bindings) m_AppliedPaths[b.id.ToString()] = b.effectivePath ?? "";
        }

        public void CommitBindings() { Snapshot(); }

        public void RevertBindings()
        {
            var map = Driving;
            if (map == null) return;
            for (int a = 0; a < map.actions.Count; a++)
            {
                var action = map.actions[a];
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    string applied;
                    if (!m_AppliedPaths.TryGetValue(b.id.ToString(), out applied)) continue;
                    if ((b.effectivePath ?? "") == applied) continue;
                    if (applied == (b.path ?? "")) action.RemoveBindingOverride(i);
                    else action.ApplyBindingOverride(i, applied);
                }
            }
            m_S.RaiseBindingChanged("bind.*");
        }

        public void ResetBindingsToDefaults()
        {
            var map = Driving;
            if (map == null) return;
            map.RemoveAllBindingOverrides();
            ApplySteerStick();
            m_S.RaiseBindingChanged("bind.*");
        }

        void ApplySteerStick()
        {
            var map = Driving;
            var steer = map != null ? map.FindAction("Steer") : null;
            if (steer == null) return;
            bool right = m_S.Int(SettingKeys.PadSteerStick) == 1;
            for (int i = 0; i < steer.bindings.Count; i++)
            {
                var b = steer.bindings[i];
                if (b.groups == null || !b.groups.Contains(GamepadGroup)) continue;
                if (right) steer.ApplyBindingOverride(i, "<Gamepad>/rightStick/x");
                else steer.RemoveBindingOverride(i);
            }
        }

        // ------------------------------------------------------------------ binding rows
        InputAction ActionFor(SettingDefinition d)
        {
            var map = Driving;
            return map != null && !string.IsNullOrEmpty(d.actionName) ? map.FindAction(d.actionName) : null;
        }

        /// <summary>Index in action.bindings of the slot-th binding in the definition's group, or -1.</summary>
        public int BindingIndex(SettingDefinition d, int slot)
        {
            var action = ActionFor(d);
            if (action == null) return -1;
            int n = 0;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (b.groups == null || !b.groups.Contains(d.bindingGroup)) continue;
                if (n == slot) return i;
                n++;
            }
            return -1;
        }

        public int SlotCount(SettingDefinition d)
        {
            int n = 0;
            while (BindingIndex(d, n) >= 0) n++;
            return n;
        }

        public string SlotPath(SettingDefinition d, int slot)
        {
            var action = ActionFor(d);
            int i = BindingIndex(d, slot);
            return action != null && i >= 0 ? action.bindings[i].effectivePath ?? "" : "";
        }

        public string SlotLabel(SettingDefinition d, int slot) { return InputGlyphs.Label(SlotPath(d, slot)); }

        public bool IsUnbound(SettingDefinition d)
        {
            int n = SlotCount(d);
            for (int s = 0; s < n; s++) if (!string.IsNullOrEmpty(SlotPath(d, s))) return false;
            return n > 0;
        }

        public string BindingSummary(SettingDefinition d)
        {
            var parts = new List<string>();
            int n = SlotCount(d);
            for (int s = 0; s < n; s++) { string l = SlotLabel(d, s); if (!string.IsNullOrEmpty(l)) parts.Add(l); }
            return parts.Count > 0 ? string.Join(" / ", parts.ToArray()) : "Unbound";
        }

        public string DefaultSummary(SettingDefinition d)
        {
            var action = ActionFor(d);
            if (action == null) return "";
            var parts = new List<string>();
            int n = SlotCount(d);
            for (int s = 0; s < n; s++) { string l = InputGlyphs.Label(action.bindings[BindingIndex(d, s)].path); if (!string.IsNullOrEmpty(l)) parts.Add(l); }
            return parts.Count > 0 ? string.Join(" / ", parts.ToArray()) : "Unbound";
        }

        public bool IsBindingDirty(SettingDefinition d)
        {
            var action = ActionFor(d);
            if (action == null) return false;
            int n = SlotCount(d);
            for (int s = 0; s < n; s++)
            {
                var b = action.bindings[BindingIndex(d, s)];
                string applied;
                if (m_AppliedPaths.TryGetValue(b.id.ToString(), out applied) && applied != (b.effectivePath ?? "")) return true;
            }
            return false;
        }

        public int DirtyBindingCount()
        {
            int n = 0;
            foreach (var d in m_BindingDefs) if (IsBindingDirty(d)) n++;
            return n;
        }

        public int NonDefaultBindingCount()
        {
            int n = 0;
            foreach (var d in m_BindingDefs)
            {
                var action = ActionFor(d);
                if (action == null) continue;
                int slots = SlotCount(d);
                for (int s = 0; s < slots; s++) { var b = action.bindings[BindingIndex(d, s)]; if ((b.effectivePath ?? "") != (b.path ?? "")) { n++; break; } }
            }
            return n;
        }

        public int UnboundCount(string group)
        {
            int n = 0;
            foreach (var d in m_BindingDefs) if (!d.locked && d.bindingGroup == group && IsUnbound(d)) n++;
            return n;
        }

        public void SetSlot(SettingDefinition d, int slot, string path)
        {
            var action = ActionFor(d);
            int i = BindingIndex(d, slot);
            if (action == null || i < 0) return;
            var b = action.bindings[i];
            if ((path ?? "") == (b.path ?? "")) action.RemoveBindingOverride(i);
            else action.ApplyBindingOverride(i, path ?? "");
            m_S.RaiseBindingChanged(d.key);
        }

        /// <summary>Other binding (same device group) already using <paramref name="path"/>.</summary>
        public bool FindConflict(SettingDefinition d, int slot, string path, out SettingDefinition other, out int otherSlot)
        {
            other = null; otherSlot = -1;
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var o in m_BindingDefs)
            {
                if (o.bindingGroup != d.bindingGroup) continue;
                int n = SlotCount(o);
                for (int s = 0; s < n; s++)
                {
                    if (o == d && s == slot) continue;
                    if (string.Equals(SlotPath(o, s), path, StringComparison.OrdinalIgnoreCase)) { other = o; otherSlot = s; return true; }
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ interactive rebinding
        public void StartRebind(SettingDefinition d, int slot, Action<RebindResult> done)
        {
            CancelRebind();
            var action = ActionFor(d);
            int index = BindingIndex(d, slot);
            if (action == null || index < 0 || d.locked) { done(new RebindResult { cancelled = true }); return; }

            bool pad = d.bindingGroup == GamepadGroup;
            bool wasEnabled = action.enabled;
            action.Disable();
            string before = action.bindings[index].overridePath;
            string reserved = pad ? "<Gamepad>/start" : "<Keyboard>/escape";

            m_Rebind = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath(pad ? "<Gamepad>" : "<Keyboard>")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough(reserved)
                .WithMatchingEventsBeingSuppressed()
                .OnMatchWaitForAnother(0.08f)
                .OnCancel(op => Finish(action, wasEnabled, done, new RebindResult { cancelled = true }))
                .OnComplete(op =>
                {
                    string path = action.bindings[index].effectivePath;
                    // Backspace clears the slot instead of binding it.
                    if (!pad && path == "<Keyboard>/backspace")
                    {
                        RestoreOverride(action, index, before);
                        SetSlot(d, slot, "");
                        Finish(action, wasEnabled, done, new RebindResult { path = "" });
                        return;
                    }
                    SettingDefinition other; int otherSlot;
                    if (FindConflict(d, slot, path, out other, out otherSlot))
                    {
                        RestoreOverride(action, index, before);
                        Finish(action, wasEnabled, done, new RebindResult { conflict = true, path = path, other = other, otherSlot = otherSlot });
                        return;
                    }
                    m_S.RaiseBindingChanged(d.key);
                    Finish(action, wasEnabled, done, new RebindResult { path = path });
                });
            m_Rebind.Start();
        }

        static void RestoreOverride(InputAction action, int index, string before)
        {
            if (before == null) action.RemoveBindingOverride(index);
            else action.ApplyBindingOverride(index, before);
        }

        void Finish(InputAction action, bool wasEnabled, Action<RebindResult> done, RebindResult result)
        {
            if (m_Rebind != null) { m_Rebind.Dispose(); m_Rebind = null; }
            if (wasEnabled || m_PreviewEnabled) action.Enable();
            if (done != null) done(result);
        }

        public void CancelRebind()
        {
            if (m_Rebind != null) m_Rebind.Cancel();
        }

        /// <summary>Resolves a conflict: Swap gives the other action this slot's previous control; Replace unbinds it.</summary>
        public void ResolveConflict(SettingDefinition d, int slot, string path, SettingDefinition other, int otherSlot, bool swap)
        {
            string mine = SlotPath(d, slot);
            SetSlot(other, otherSlot, swap ? mine : "");
            SetSlot(d, slot, path);
        }

        // ------------------------------------------------------------------ live preview
        /// <summary>Enables the Driving map so the settings screen can show live input.</summary>
        public void SetPreview(bool on)
        {
            m_PreviewEnabled = on;
            var map = Driving;
            if (map == null) return;
            if (on) map.Enable(); else map.Disable();
        }

        public float Read(string actionName)
        {
            var map = Driving;
            var a = map != null ? map.FindAction(actionName) : null;
            return a != null && a.enabled ? a.ReadValue<float>() : 0f;
        }

        public bool Pressed(string actionName)
        {
            var map = Driving;
            var a = map != null ? map.FindAction(actionName) : null;
            return a != null && a.enabled && a.IsPressed();
        }

        /// <summary>Short rumble on the current controller at the given intensity (0..1).</summary>
        public bool TestVibration(float intensity)
        {
            var pad = Gamepad.current;
            if (pad == null) return false;
            pad.SetMotorSpeeds(intensity * 0.6f, intensity);
            m_S.StartCoroutine(StopRumble(pad));
            return true;
        }

        static IEnumerator StopRumble(Gamepad pad)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (pad != null) pad.ResetHaptics();
        }
    }

    /// <summary>
    /// Steering / pedal shaping from Settings &gt; Controls, for the car controller:
    /// <c>float steer = DrivingInput.ShapeSteering(rawStickX, gamepad: true);</c>
    /// </summary>
    public static class DrivingInput
    {
        public static float PadSensitivity = 1f, PadDeadzone = 0.08f, PadLinearity = 0.4f, TriggerDeadzone = 0.05f;
        public static float KeyboardSensitivity = 1f, KeyboardLinearity = 0.3f, KeyboardSteerSpeed = 6f, KeyboardThrottle = 1f, KeyboardBrake = 1f;
        public static bool Vibration = true;
        public static float VibrationIntensity = 0.7f;

        public static void Refresh(AccertoneSettings s)
        {
            var st = s.Store;
            PadSensitivity = (float)st.GetApplied(SettingKeys.PadSteerSensitivity) / 100f;
            PadDeadzone = (float)st.GetApplied(SettingKeys.PadSteerDeadzone) / 100f;
            PadLinearity = (float)st.GetApplied(SettingKeys.PadSteerLinearity) / 100f;
            TriggerDeadzone = (float)st.GetApplied(SettingKeys.PadTriggerDeadzone) / 100f;
            KeyboardSensitivity = (float)st.GetApplied(SettingKeys.KeyboardSteerSensitivity) / 100f;
            KeyboardLinearity = (float)st.GetApplied(SettingKeys.KeyboardSteerLinearity) / 100f;
            KeyboardSteerSpeed = (float)st.GetApplied(SettingKeys.KeyboardSteerSpeed);
            KeyboardThrottle = (float)st.GetApplied(SettingKeys.KeyboardThrottle) / 100f;
            KeyboardBrake = (float)st.GetApplied(SettingKeys.KeyboardBrake) / 100f;
            Vibration = st.GetApplied(SettingKeys.Vibration) >= 0.5;
            VibrationIntensity = (float)st.GetApplied(SettingKeys.VibrationIntensity) / 100f;
        }

        /// <summary>Deadzone, then a power curve (linearity), then sensitivity. Input and output are -1..1.</summary>
        public static float Shape(float raw, float sensitivity, float deadzone, float linearity)
        {
            float a = Mathf.Abs(raw);
            if (a <= deadzone) return 0f;
            float n = (a - deadzone) / Mathf.Max(0.0001f, 1f - deadzone);
            return Mathf.Sign(raw) * Mathf.Clamp01(Mathf.Pow(n, 1f + linearity * 1.6f) * sensitivity);
        }

        public static float ShapeSteering(float raw, bool gamepad)
        {
            return gamepad ? Shape(raw, PadSensitivity, PadDeadzone, PadLinearity) : Shape(raw, KeyboardSensitivity, 0f, KeyboardLinearity);
        }

        public static float ShapeTrigger(float raw)
        {
            return raw <= TriggerDeadzone ? 0f : Mathf.Clamp01((raw - TriggerDeadzone) / Mathf.Max(0.0001f, 1f - TriggerDeadzone));
        }
    }

    /// <summary>Short glyph labels for control paths (W, ↑, RT, A...).</summary>
    public static class InputGlyphs
    {
        static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "<Keyboard>/upArrow", "↑" }, { "<Keyboard>/downArrow", "↓" }, { "<Keyboard>/leftArrow", "←" }, { "<Keyboard>/rightArrow", "→" },
            { "<Keyboard>/escape", "ESC" }, { "<Keyboard>/space", "SPACE" }, { "<Keyboard>/enter", "ENTER" }, { "<Keyboard>/tab", "TAB" },
            { "<Keyboard>/leftShift", "L SHIFT" }, { "<Keyboard>/rightShift", "R SHIFT" }, { "<Keyboard>/leftCtrl", "L CTRL" }, { "<Keyboard>/rightCtrl", "R CTRL" },
            { "<Keyboard>/leftAlt", "L ALT" }, { "<Keyboard>/rightAlt", "R ALT" }, { "<Keyboard>/backspace", "BKSP" }, { "<Keyboard>/capsLock", "CAPS" },
            { "<Gamepad>/buttonSouth", "A" }, { "<Gamepad>/buttonEast", "B" }, { "<Gamepad>/buttonWest", "X" }, { "<Gamepad>/buttonNorth", "Y" },
            { "<Gamepad>/leftShoulder", "LB" }, { "<Gamepad>/rightShoulder", "RB" }, { "<Gamepad>/leftTrigger", "LT" }, { "<Gamepad>/rightTrigger", "RT" },
            { "<Gamepad>/leftStickPress", "LS" }, { "<Gamepad>/rightStickPress", "RS" }, { "<Gamepad>/start", "MENU" }, { "<Gamepad>/select", "VIEW" },
            { "<Gamepad>/dpad/up", "D-UP" }, { "<Gamepad>/dpad/down", "D-DOWN" }, { "<Gamepad>/dpad/left", "D-LEFT" }, { "<Gamepad>/dpad/right", "D-RIGHT" },
            { "<Gamepad>/leftStick/x", "LEFT STICK" }, { "<Gamepad>/rightStick/x", "RIGHT STICK" }
        };

        public static string Label(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string l;
            if (Map.TryGetValue(path, out l)) return l;
            string human = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrEmpty(human) ? path : human.ToUpperInvariant();
        }
    }
}
