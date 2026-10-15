using System.Collections.Generic;
using UnityEngine;

namespace Accertone.Settings
{
    /// <summary>
    /// The settings menu's content: categories, pages, sections and every setting with its default, control,
    /// preview and dependencies, plus the presets. The settings screen is generated from this asset, so labels,
    /// help text, defaults and ranges can be tuned here without touching code.
    /// Lives at Resources/AccertoneSettingsCatalog (rebuilt by Accert one &gt; Settings &gt; Build).
    /// </summary>
    [CreateAssetMenu(menuName = "Accert one/Settings Catalog", fileName = "AccertoneSettingsCatalog")]
    public class SettingsCatalog : ScriptableObject
    {
        public const string ResourcePath = "AccertoneSettingsCatalog";

        public List<SettingCategory> categories = new List<SettingCategory>();
        public List<SettingPreset> presets = new List<SettingPreset>();

        public struct Location
        {
            public SettingDefinition definition;
            public SettingCategory category;
            public SettingPage page;
            public SettingSection section;
        }

        Dictionary<string, Location> m_Index;

        void OnEnable() { m_Index = null; }
        void OnValidate() { m_Index = null; }

        void BuildIndex()
        {
            m_Index = new Dictionary<string, Location>();
            foreach (var c in categories)
            {
                if (c.HasHeader) m_Index[c.header.key] = new Location { definition = c.header, category = c };
                foreach (var p in c.pages)
                    foreach (var s in p.sections)
                        foreach (var d in s.settings)
                            if (d != null && !string.IsNullOrEmpty(d.key)) m_Index[d.key] = new Location { definition = d, category = c, page = p, section = s };
            }
        }

        public bool TryLocate(string key, out Location location)
        {
            if (m_Index == null) BuildIndex();
            return m_Index.TryGetValue(key, out location);
        }

        public SettingDefinition Find(string key)
        {
            Location l;
            return TryLocate(key, out l) ? l.definition : null;
        }

        public SettingCategory CategoryOf(string key)
        {
            if (key.EndsWith(SettingKeys.MuteSuffix)) key = key.Substring(0, key.Length - SettingKeys.MuteSuffix.Length);
            Location l;
            return TryLocate(key, out l) ? l.category : null;
        }

        public IEnumerable<SettingDefinition> AllDefinitions()
        {
            if (m_Index == null) BuildIndex();
            foreach (var l in m_Index.Values) yield return l.definition;
        }

        public SettingPreset PresetFor(string presetKey)
        {
            foreach (var p in presets) if (p.presetKey == presetKey) return p;
            return null;
        }

        /// <summary>The preset (if any) that a setting belongs to.</summary>
        public SettingPreset PresetContaining(string key)
        {
            foreach (var p in presets)
                foreach (var k in p.keys)
                    if (k == key) return p;
            return null;
        }
    }
}
