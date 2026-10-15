using System;
using System.Collections.Generic;
using UnityEngine;

namespace Accertone.Settings
{
    /// <summary>The control a setting is edited with.</summary>
    public enum SettingKind
    {
        Selector,   // arrows + pips, for ordered levels and short lists
        Segmented,  // 2-5 visible choices
        Dropdown,   // long lists (resolutions, displays)
        Toggle,
        Slider,
        Binding,    // an Input System action binding (primary + secondary slot)
        Action,     // a button that does something (test vibration)
        DeviceTabs  // page switcher inside a category (keyboard / controller)
    }

    /// <summary>When a change reaches the game.</summary>
    public enum SettingApplies
    {
        Instant,  // previewed while editing, kept on Apply
        Confirm,  // applied on Apply, then confirmed within a countdown (display mode changes)
        OnTrack,  // stored; read by race systems
        Online    // stored; read by online sessions
    }

    /// <summary>What the detail panel shows for the focused setting.</summary>
    public enum SettingPreview
    {
        None, Monitor, Calibration, InterfaceScale, Graphics, AudioBus, Input, Vibration,
        Assists, CockpitCamera, ChaseCamera, Shake, Showroom, Hud, Animation
    }

    /// <summary>Where a setting's option list comes from.</summary>
    public enum SettingOptionSource { Static, Displays, Resolutions, RefreshRates }

    public enum ConditionOp { Equals, NotEquals, LessThan, AtLeast }

    /// <summary>Disables a setting while another setting has a given value, and explains why.</summary>
    [Serializable]
    public class SettingCondition
    {
        public string key;
        public ConditionOp op;
        public double value;
        public string reason;

        public bool IsMet(double current)
        {
            switch (op)
            {
                case ConditionOp.Equals: return Math.Abs(current - value) < 1e-6;
                case ConditionOp.NotEquals: return Math.Abs(current - value) >= 1e-6;
                case ConditionOp.LessThan: return current < value - 1e-6;
                default: return current >= value - 1e-6;
            }
        }
    }

    /// <summary>One setting: what it is, how it is edited, its default and how it reaches the game.</summary>
    [Serializable]
    public class SettingDefinition
    {
        public string key;
        public string label;
        public SettingKind kind;
        [TextArea(2, 4)] public string info;

        [Header("Options (Selector / Segmented / Dropdown / DeviceTabs)")]
        public SettingOptionSource optionSource;
        public string[] options = new string[0];
        [Tooltip("Ordered levels (Low..Ultra): the pips fill up to the current level.")]
        public bool ordinal;

        [Header("Slider")]
        public double min;
        public double max = 100;
        public double step = 1;
        public int decimals;
        public string unit;

        [Header("Value")]
        public double defaultValue;
        [Tooltip("Optional mute switch stored under this key (audio channels).")]
        public string muteKey;
        public string audioChannel;

        [Header("Binding")]
        public string actionName;
        [Tooltip("Input System binding group: Keyboard&Mouse or Gamepad.")]
        public string bindingGroup;
        public bool locked;

        [Header("Behaviour")]
        public SettingApplies applies;
        public SettingPreview preview;
        [Range(0, 5)] public int gpuCost;
        [Range(0, 5)] public int visualGain;
        public SettingCondition[] disabledWhen = new SettingCondition[0];

        public bool StoresValue { get { return kind != SettingKind.Binding && kind != SettingKind.Action && kind != SettingKind.DeviceTabs; } }
        public bool IsLivePreview { get { return applies == SettingApplies.Instant; } }
        public bool IsDynamic { get { return optionSource != SettingOptionSource.Static; } }
    }

    [Serializable]
    public class SettingSection
    {
        public string title;
        [Tooltip("Shows the Action / Primary / Secondary column header (binding lists).")]
        public bool bindingColumns;
        public List<SettingDefinition> settings = new List<SettingDefinition>();
    }

    /// <summary>A page inside a category. Most categories have one; Controls has Keyboard &amp; mouse and Controller.</summary>
    [Serializable]
    public class SettingPage
    {
        public string id;
        public string label;
        public List<SettingSection> sections = new List<SettingSection>();
    }

    [Serializable]
    public class SettingCategory
    {
        public string id;
        public string title;
        public string blurb;
        [Tooltip("Optional control shown above the list (quality preset, assist preset, device tabs).")]
        public SettingDefinition header;
        public List<SettingPage> pages = new List<SettingPage>();
        public bool HasHeader { get { return header != null && !string.IsNullOrEmpty(header.key); } }
    }

    /// <summary>A preset setting that writes a group of other settings at once (Low..Ultra, assist levels).</summary>
    [Serializable]
    public class SettingPreset
    {
        public string presetKey;
        public string[] keys = new string[0];
        [Tooltip("One row per preset option (except the last option, Custom), one value per key.")]
        public List<PresetValues> levels = new List<PresetValues>();
    }

    [Serializable]
    public class PresetValues
    {
        public double[] values = new double[0];
    }

    /// <summary>A displayable option and the value it stores.</summary>
    public struct SettingOption
    {
        public string label;
        public double value;
        public SettingOption(string label, double value) { this.label = label; this.value = value; }
    }
}
