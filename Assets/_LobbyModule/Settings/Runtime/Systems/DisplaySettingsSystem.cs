using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Accertone.Settings
{
    /// <summary>
    /// Display: monitor, window mode, resolution and refresh rate (applied through <see cref="Screen"/> with the
    /// confirmation countdown), V-Sync, the frame-rate cap and the interface scale of every registered canvas.
    /// Resolution and window changes only take effect in a built player; the Editor's Game view keeps its size.
    /// </summary>
    public class DisplaySettingsSystem : ISettingsSystem
    {
        static readonly int[] FrameCaps = { 30, 60, 120, 144, 165, 240, -1 };
        static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

        AccertoneSettings m_S;
        readonly List<DisplayInfo> m_Layout = new List<DisplayInfo>();
        readonly List<SettingOption> m_Displays = new List<SettingOption>();
        readonly List<SettingOption> m_Resolutions = new List<SettingOption>();
        readonly List<SettingOption> m_Refresh = new List<SettingOption>();
        int m_OriginalVSync = -1;
        bool m_LoggedEditor;
        string m_LastMode;

        public static double EncodeResolution(int w, int h) { return w * 100000.0 + h; }
        public static void DecodeResolution(double v, out int w, out int h)
        {
            w = (int)Math.Floor(v / 100000.0);
            h = (int)Math.Round(v - w * 100000.0);
        }

        /// <summary>Native resolution, desktop refresh rate and the current display become the defaults.</summary>
        public void ResolveHardwareDefaults(SettingsStore store)
        {
            var cur = Screen.currentResolution;
            if (cur.width > 0 && cur.height > 0) store.SetDefault(SettingKeys.Resolution, EncodeResolution(cur.width, cur.height));
            double hz = Math.Round(cur.refreshRateRatio.value);
            if (hz > 0) store.SetDefault(SettingKeys.RefreshRate, hz);
        }

        public void Initialise(AccertoneSettings settings)
        {
            m_S = settings;
            m_OriginalVSync = QualitySettings.vSyncCount;
        }

        public void OnSettingChanged(string key)
        {
            switch (key)
            {
                case SettingKeys.VSync:
                case SettingKeys.FrameRateLimit: ApplySync(); break;
                case SettingKeys.Monitor:
                case SettingKeys.WindowMode:
                case SettingKeys.Resolution:
                case SettingKeys.RefreshRate: ApplyMode(); break;
                case SettingKeys.InterfaceScale: ApplyInterfaceScale(); break;
            }
        }

        public void ApplyAll()
        {
            ApplySync();
            ApplyMode();
            ApplyInterfaceScale();
        }

        public void OnSceneLoaded(Scene scene) { ApplyInterfaceScale(); }

        public void Shutdown()
        {
#if UNITY_EDITOR
            // QualitySettings edits made in Play mode would otherwise stick to the project.
            if (m_OriginalVSync >= 0) QualitySettings.vSyncCount = m_OriginalVSync;
#endif
        }

        void ApplySync()
        {
            bool vsync = m_S.Bool(SettingKeys.VSync);
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            int cap = FrameCaps[Mathf.Clamp(m_S.Int(SettingKeys.FrameRateLimit), 0, FrameCaps.Length - 1)];
            Application.targetFrameRate = vsync ? -1 : cap;
        }

        void ApplyMode()
        {
            int w, h;
            DecodeResolution(m_S.Effective(SettingKeys.Resolution), out w, out h);
            var mode = Modes[Mathf.Clamp(m_S.Int(SettingKeys.WindowMode), 0, Modes.Length - 1)];
            int hz = m_S.Int(SettingKeys.RefreshRate);
            int monitor = m_S.Int(SettingKeys.Monitor);
            string desc = w + "x" + h + " " + mode + " " + hz + "Hz display " + monitor;
            if (desc == m_LastMode || w <= 0 || h <= 0) return;
            m_LastMode = desc;

            if (Application.isEditor)
            {
                if (!m_LoggedEditor) Debug.Log("[Accert one] Display mode changes (" + desc + ") are applied in a built player. The Editor Game view keeps its own size.");
                m_LoggedEditor = true;
                return;
            }

            Screen.GetDisplayLayout(m_Layout);
            if (monitor >= 0 && monitor < m_Layout.Count && !m_Layout[monitor].Equals(Screen.mainWindowDisplayInfo))
                Screen.MoveMainWindowTo(m_Layout[monitor], Vector2Int.zero);

            var rate = Screen.currentResolution.refreshRateRatio;
            foreach (var r in Screen.resolutions)
                if (r.width == w && r.height == h && Math.Abs(Math.Round(r.refreshRateRatio.value) - hz) < 0.5) { rate = r.refreshRateRatio; break; }
            Screen.SetResolution(w, h, mode, rate);
        }

        void ApplyInterfaceScale()
        {
            // The interface only rescales on Apply, so the menu doesn't move under the pointer while editing.
            SettingsCanvasScale.ApplyAll((float)(m_S.Store.GetApplied(SettingKeys.InterfaceScale) / 100.0));
        }

        // ------------------------------------------------------------------ option lists
        public IList<SettingOption> DisplayOptions()
        {
            m_Displays.Clear();
            Screen.GetDisplayLayout(m_Layout);
            var main = Screen.mainWindowDisplayInfo;
            for (int i = 0; i < m_Layout.Count; i++)
            {
                var d = m_Layout[i];
                string label = "Display " + (i + 1) + " · " + d.width + " × " + d.height + (d.Equals(main) ? " · Current" : "");
                m_Displays.Add(new SettingOption(label, i));
            }
            if (m_Displays.Count == 0) m_Displays.Add(new SettingOption("Display 1 · " + Screen.currentResolution.width + " × " + Screen.currentResolution.height, 0));
            return m_Displays;
        }

        public IList<SettingOption> ResolutionOptions()
        {
            m_Resolutions.Clear();
            var seen = new HashSet<double>();
            var all = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
                if (seen.Add(EncodeResolution(r.width, r.height))) all.Add(new Vector2Int(r.width, r.height));
            int cw, ch;
            DecodeResolution(m_S.Store.Get(SettingKeys.Resolution), out cw, out ch);
            if (cw > 0 && seen.Add(EncodeResolution(cw, ch))) all.Add(new Vector2Int(cw, ch));
            var native = Screen.currentResolution;
            if (native.width > 0 && seen.Add(EncodeResolution(native.width, native.height))) all.Add(new Vector2Int(native.width, native.height));
            all.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            foreach (var r in all) m_Resolutions.Add(new SettingOption(r.x + " × " + r.y, EncodeResolution(r.x, r.y)));
            return m_Resolutions;
        }

        public IList<SettingOption> RefreshOptions()
        {
            m_Refresh.Clear();
            int w, h;
            DecodeResolution(m_S.Store.Get(SettingKeys.Resolution), out w, out h);
            var rates = new SortedSet<int>();
            foreach (var r in Screen.resolutions)
                if (r.width == w && r.height == h) rates.Add((int)Math.Round(r.refreshRateRatio.value));
            if (rates.Count == 0) foreach (var r in Screen.resolutions) rates.Add((int)Math.Round(r.refreshRateRatio.value));
            rates.Add((int)Math.Round(m_S.Store.Get(SettingKeys.RefreshRate)));
            rates.Add((int)Math.Round(Screen.currentResolution.refreshRateRatio.value));
            foreach (var hz in rates) if (hz > 0) m_Refresh.Add(new SettingOption(hz + " Hz", hz));
            return m_Refresh;
        }
    }
}
