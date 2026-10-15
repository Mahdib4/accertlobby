using System;
using System.Collections.Generic;

namespace Accertone.Settings
{
    /// <summary>Saved form of the player's settings (persistentDataPath/accertone_settings_v2.json).</summary>
    [Serializable]
    public class SettingsSaveData
    {
        public int version = SettingsStore.SaveVersion;
        public List<string> keys = new List<string>();
        public List<double> values = new List<double>();
        public string bindingOverrides = "";
    }

    /// <summary>
    /// Holds every setting value twice: <b>applied</b> (what the game uses and what is saved) and <b>pending</b>
    /// (what the player is editing). Edits go to pending; Commit makes them applied, Revert throws them away.
    /// Presets (graphics quality, assists) are kept in sync both ways here.
    /// </summary>
    public class SettingsStore
    {
        public const int SaveVersion = 2;
        const double Epsilon = 1e-6;

        readonly SettingsCatalog m_Catalog;
        readonly Dictionary<string, double> m_Defaults = new Dictionary<string, double>();
        readonly Dictionary<string, double> m_Applied = new Dictionary<string, double>();
        readonly Dictionary<string, double> m_Pending = new Dictionary<string, double>();
        bool m_Syncing;

        /// <summary>Raised whenever a pending value changes (edits, presets, resets, reverts).</summary>
        public event Action<string> PendingChanged;

        public SettingsCatalog Catalog { get { return m_Catalog; } }
        public IEnumerable<string> Keys { get { return m_Defaults.Keys; } }

        public SettingsStore(SettingsCatalog catalog)
        {
            m_Catalog = catalog;
            foreach (var d in catalog.AllDefinitions())
            {
                if (d.StoresValue) Register(d.key, d.defaultValue);
                if (!string.IsNullOrEmpty(d.muteKey)) Register(d.muteKey, 0);
            }
        }

        void Register(string key, double def)
        {
            m_Defaults[key] = def;
            m_Applied[key] = def;
            m_Pending[key] = def;
        }

        public bool Has(string key) { return m_Defaults.ContainsKey(key); }
        public double Get(string key) { double v; return m_Pending.TryGetValue(key, out v) ? v : 0; }
        public double GetApplied(string key) { double v; return m_Applied.TryGetValue(key, out v) ? v : 0; }
        public double GetDefault(string key) { double v; return m_Defaults.TryGetValue(key, out v) ? v : 0; }

        /// <summary>Replaces a default that depends on the hardware (native resolution, refresh rate). Values still at the old default follow it.</summary>
        public void SetDefault(string key, double value)
        {
            if (!m_Defaults.ContainsKey(key)) return;
            double old = m_Defaults[key];
            m_Defaults[key] = value;
            if (Same(m_Applied[key], old)) m_Applied[key] = value;
            if (Same(m_Pending[key], old)) m_Pending[key] = value;
        }

        public bool IsDirty(string key) { return Has(key) && !Same(m_Pending[key], m_Applied[key]); }
        public bool IsAtDefault(string key) { return Has(key) && Same(m_Pending[key], m_Defaults[key]); }

        public List<string> DirtyKeys()
        {
            var list = new List<string>();
            foreach (var k in m_Defaults.Keys) if (!Same(m_Pending[k], m_Applied[k])) list.Add(k);
            return list;
        }

        /// <summary>Edits a pending value. Returns false if nothing changed.</summary>
        public bool Set(string key, double value)
        {
            if (!Has(key)) return false;
            value = Normalise(key, value);
            if (Same(m_Pending[key], value)) return false;
            m_Pending[key] = value;
            if (PendingChanged != null) PendingChanged(key);
            if (!m_Syncing) SyncPresets(key);
            return true;
        }

        public void Commit(IEnumerable<string> keys)
        {
            foreach (var k in keys) if (Has(k)) m_Applied[k] = m_Pending[k];
        }

        public void CommitAll() { Commit(new List<string>(m_Defaults.Keys)); }

        public void Revert(IEnumerable<string> keys)
        {
            foreach (var k in new List<string>(keys))
            {
                if (!Has(k) || Same(m_Pending[k], m_Applied[k])) continue;
                m_Pending[k] = m_Applied[k];
                if (PendingChanged != null) PendingChanged(k);
            }
        }

        public void RevertAll() { Revert(new List<string>(m_Defaults.Keys)); }

        public void ResetToDefaults(IEnumerable<string> keys)
        {
            m_Syncing = true;
            try { foreach (var k in keys) if (Has(k)) Set(k, m_Defaults[k]); }
            finally { m_Syncing = false; }
            foreach (var p in m_Catalog.presets) RefreshPreset(p);
        }

        double Normalise(string key, double value)
        {
            var d = m_Catalog.Find(key);
            if (d == null) return value;
            switch (d.kind)
            {
                case SettingKind.Slider:
                    if (d.step > 0) value = d.min + Math.Round((value - d.min) / d.step) * d.step;
                    value = Math.Round(Math.Max(d.min, Math.Min(d.max, value)), 6);
                    break;
                case SettingKind.Toggle:
                    value = value >= 0.5 ? 1 : 0;
                    break;
                case SettingKind.Selector:
                case SettingKind.Segmented:
                case SettingKind.Dropdown:
                    if (!d.IsDynamic && d.options != null && d.options.Length > 0) value = Math.Max(0, Math.Min(d.options.Length - 1, Math.Round(value)));
                    break;
            }
            return value;
        }

        // ------------------------------------------------------------------ presets
        void SyncPresets(string key)
        {
            m_Syncing = true;
            try
            {
                foreach (var p in m_Catalog.presets)
                {
                    if (p.presetKey == key)
                    {
                        int level = (int)Math.Round(m_Pending[key]);
                        if (level >= 0 && level < p.levels.Count)
                            for (int i = 0; i < p.keys.Length && i < p.levels[level].values.Length; i++) Set(p.keys[i], p.levels[level].values[i]);
                    }
                    else if (Array.IndexOf(p.keys, key) >= 0) RefreshPreset(p);
                }
            }
            finally { m_Syncing = false; }
        }

        void RefreshPreset(SettingPreset p)
        {
            if (!Has(p.presetKey)) return;
            int match = p.levels.Count; // Custom
            for (int l = 0; l < p.levels.Count; l++)
            {
                bool all = true;
                for (int i = 0; i < p.keys.Length && all; i++) all = i < p.levels[l].values.Length && Same(Get(p.keys[i]), p.levels[l].values[i]);
                if (all) { match = l; break; }
            }
            bool was = m_Syncing; m_Syncing = true;
            try { Set(p.presetKey, match); } finally { m_Syncing = was; }
        }

        // ------------------------------------------------------------------ persistence
        public SettingsSaveData ToSaveData(string bindingOverrides)
        {
            var data = new SettingsSaveData { bindingOverrides = bindingOverrides ?? "" };
            foreach (var kv in m_Applied) { data.keys.Add(kv.Key); data.values.Add(kv.Value); }
            return data;
        }

        /// <summary>Loads saved values into both applied and pending. Unknown keys are ignored, missing keys keep their defaults.</summary>
        public void Load(SettingsSaveData data)
        {
            if (data == null || data.keys == null || data.values == null) return;
            for (int i = 0; i < data.keys.Count && i < data.values.Count; i++)
            {
                string k = data.keys[i];
                if (!Has(k)) continue;
                double v = Normalise(k, data.values[i]);
                m_Applied[k] = v;
                m_Pending[k] = v;
            }
        }

        static bool Same(double a, double b) { return Math.Abs(a - b) < Epsilon; }
    }
}
