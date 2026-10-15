using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Accertone.Settings
{
    /// <summary>A part of the game that settings drive (display, graphics, audio, input, lobby).</summary>
    public interface ISettingsSystem
    {
        void Initialise(AccertoneSettings settings);
        /// <summary>The effective value of <paramref name="key"/> may have changed.</summary>
        void OnSettingChanged(string key);
        /// <summary>Re-apply every value this system owns (startup, scene loads).</summary>
        void ApplyAll();
        void OnSceneLoaded(Scene scene);
        /// <summary>Undo anything that would otherwise leak into project assets (Editor) or outlive the game.</summary>
        void Shutdown();
    }

    /// <summary>
    /// The settings service. Created automatically before the first scene loads (no scene setup needed), it loads
    /// the player's saved settings, applies them to the game, and runs the edit / apply / discard / reset and
    /// display-confirmation workflow the settings screen uses.
    ///
    /// Game systems read values with <see cref="Effective(string)"/> (or <see cref="Bool"/> / <see cref="Int"/>)
    /// using the constants in <see cref="SettingKeys"/>, and listen to <see cref="SettingChanged"/> / <see cref="Applied"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AccertoneSettings : MonoBehaviour
    {
        public const string SaveFileName = "accertone_settings_v2.json";
        public const int ConfirmSeconds = 15;

        static AccertoneSettings s_Instance;

        /// <summary>The running service (created on demand in Play mode).</summary>
        public static AccertoneSettings Instance
        {
            get
            {
                if (s_Instance == null && Application.isPlaying) Boot();
                return s_Instance;
            }
        }

        public SettingsCatalog Catalog { get; private set; }
        public SettingsStore Store { get; private set; }
        public DisplaySettingsSystem Display { get; private set; }
        public GraphicsSettingsSystem Graphics { get; private set; }
        public PostProcessSettingsSystem PostProcess { get; private set; }
        public AudioSettingsSystem Audio { get; private set; }
        public InputSettingsSystem Input { get; private set; }
        public LobbySettingsSystem Lobby { get; private set; }

        /// <summary>True while the settings screen is open: live-preview settings use their pending value.</summary>
        public bool IsEditing { get; private set; }
        /// <summary>True during the display confirmation countdown.</summary>
        public bool IsConfirming { get { return m_ConfirmKeys != null; } }
        public int ConfirmSecondsLeft { get; private set; }

        /// <summary>A pending value (or binding) changed. Key is a setting key or "bind.*".</summary>
        public event Action<string> SettingChanged;
        /// <summary>Changes were applied and saved.</summary>
        public event Action Applied;
        /// <summary>Seconds left in the display confirmation.</summary>
        public event Action<int> ConfirmTick;
        /// <summary>The display confirmation ended: true = kept, false = reverted.</summary>
        public event Action<bool> ConfirmEnded;

        readonly List<ISettingsSystem> m_Systems = new List<ISettingsSystem>();
        readonly Dictionary<SettingDefinition, List<SettingOption>> m_StaticOptions = new Dictionary<SettingDefinition, List<SettingOption>>();
        List<string> m_ConfirmKeys;
        Coroutine m_Countdown;
        string m_LoadedBindings = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (s_Instance != null) return;
            var go = new GameObject("AccertoneSettings");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<AccertoneSettings>();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this) { Destroy(gameObject); return; }
            s_Instance = this;

            Catalog = Resources.Load<SettingsCatalog>(SettingsCatalog.ResourcePath);
            if (Catalog == null)
            {
                Catalog = ScriptableObject.CreateInstance<SettingsCatalog>();
                SettingsCatalogContent.Fill(Catalog);
            }
            Store = new SettingsStore(Catalog);

            Display = new DisplaySettingsSystem();
            Graphics = new GraphicsSettingsSystem();
            PostProcess = new PostProcessSettingsSystem();
            Audio = new AudioSettingsSystem();
            Input = new InputSettingsSystem();
            Lobby = new LobbySettingsSystem();
            m_Systems.AddRange(new ISettingsSystem[] { Display, Graphics, PostProcess, Audio, Input, Lobby });

            Display.ResolveHardwareDefaults(Store);
            Load();
            foreach (var s in m_Systems) Safe(() => s.Initialise(this));
            Input.LoadOverrides(m_LoadedBindings);

            Store.PendingChanged += OnPendingChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            foreach (var s in m_Systems) Safe(s.ApplyAll);
        }

        IEnumerator Start()
        {
            // Some engine state (Audio Mixer parameters in particular) ignores changes made before the first frame.
            yield return null;
            foreach (var s in m_Systems) Safe(s.ApplyAll);
        }

        void OnDestroy()
        {
            if (s_Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Store != null) Store.PendingChanged -= OnPendingChanged;
            foreach (var s in m_Systems) Safe(s.Shutdown);
            s_Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var s in m_Systems) Safe(() => s.OnSceneLoaded(scene));
        }

        void OnApplicationFocus(bool focused)
        {
            if (Audio != null) Audio.OnFocusChanged(focused);
        }

        static void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ reading values
        /// <summary>The value the game should use right now.</summary>
        public double Effective(string key)
        {
            if (m_ConfirmKeys != null && m_ConfirmKeys.Contains(key)) return Store.Get(key);
            if (IsEditing)
            {
                var d = Catalog.Find(BaseKey(key));
                if (d != null && d.IsLivePreview) return Store.Get(key);
            }
            return Store.GetApplied(key);
        }

        public bool Bool(string key) { return Effective(key) >= 0.5; }
        public int Int(string key) { return (int)Math.Round(Effective(key)); }
        public float Float(string key) { return (float)Effective(key); }

        public static string BaseKey(string key)
        {
            return key.EndsWith(SettingKeys.MuteSuffix) ? key.Substring(0, key.Length - SettingKeys.MuteSuffix.Length) : key;
        }

        // ------------------------------------------------------------------ editing workflow
        public void BeginEditing()
        {
            if (IsEditing) return;
            IsEditing = true;
        }

        /// <summary>Leaves editing. Anything not applied is discarded.</summary>
        public void EndEditing()
        {
            if (!IsEditing) return;
            if (IsConfirming) RevertDisplayChanges();
            if (HasChanges) Discard();
            IsEditing = false;
            foreach (var s in m_Systems) Safe(s.ApplyAll);
        }

        public bool HasChanges { get { return ChangeCount > 0; } }

        public int ChangeCount { get { return Store.DirtyKeys().Count + Input.DirtyBindingCount(); } }

        public bool IsDirty(SettingDefinition d)
        {
            if (d == null) return false;
            if (d.kind == SettingKind.Binding) return Input.IsBindingDirty(d);
            return Store.IsDirty(d.key) || (!string.IsNullOrEmpty(d.muteKey) && Store.IsDirty(d.muteKey));
        }

        public bool CategoryHasChanges(SettingCategory c)
        {
            foreach (var k in Store.DirtyKeys()) if (Catalog.CategoryOf(k) == c) return true;
            return c.id == "controls" && Input.DirtyBindingCount() > 0;
        }

        /// <summary>Edits a pending value.</summary>
        public void Set(string key, double value) { Store.Set(key, value); }

        void OnPendingChanged(string key)
        {
            if (SettingChanged != null) SettingChanged(key);
            if (!IsEditing) { Notify(key); return; }
            var d = Catalog.Find(BaseKey(key));
            if (d != null && d.IsLivePreview) Notify(key);
        }

        /// <summary>Called by the input system when a binding override changes.</summary>
        public void RaiseBindingChanged(string bindingKey)
        {
            if (SettingChanged != null) SettingChanged(bindingKey);
        }

        void Notify(string key)
        {
            foreach (var s in m_Systems) { var sys = s; Safe(() => sys.OnSettingChanged(key)); }
        }

        /// <summary>
        /// Applies and saves every change. Returns true if display changes started the confirmation countdown
        /// (listen to <see cref="ConfirmTick"/> / <see cref="ConfirmEnded"/>).
        /// </summary>
        public bool Apply()
        {
            var dirty = Store.DirtyKeys();
            var confirm = new List<string>();
            var rest = new List<string>();
            foreach (var k in dirty)
            {
                var d = Catalog.Find(BaseKey(k));
                if (d != null && d.applies == SettingApplies.Confirm) confirm.Add(k); else rest.Add(k);
            }
            Store.Commit(rest);
            Input.CommitBindings();
            Save();
            foreach (var k in rest) Notify(k);
            if (Applied != null) Applied();

            if (confirm.Count == 0) return false;
            m_ConfirmKeys = confirm;
            foreach (var k in confirm) Notify(k);
            ConfirmSecondsLeft = ConfirmSeconds;
            if (m_Countdown != null) StopCoroutine(m_Countdown);
            m_Countdown = StartCoroutine(Countdown());
            return true;
        }

        IEnumerator Countdown()
        {
            while (ConfirmSecondsLeft > 0)
            {
                if (ConfirmTick != null) ConfirmTick(ConfirmSecondsLeft);
                yield return new WaitForSecondsRealtime(1f);
                ConfirmSecondsLeft--;
            }
            m_Countdown = null;
            RevertDisplayChanges();
        }

        public void KeepDisplayChanges()
        {
            if (m_ConfirmKeys == null) return;
            if (m_Countdown != null) { StopCoroutine(m_Countdown); m_Countdown = null; }
            Store.Commit(m_ConfirmKeys);
            m_ConfirmKeys = null;
            Save();
            if (ConfirmEnded != null) ConfirmEnded(true);
            if (Applied != null) Applied();
        }

        public void RevertDisplayChanges()
        {
            if (m_ConfirmKeys == null) return;
            if (m_Countdown != null) { StopCoroutine(m_Countdown); m_Countdown = null; }
            var keys = m_ConfirmKeys;
            m_ConfirmKeys = null;
            Store.Revert(keys);
            foreach (var k in keys) Notify(k);
            if (ConfirmEnded != null) ConfirmEnded(false);
        }

        /// <summary>Throws away every change since the last Apply.</summary>
        public void Discard()
        {
            Store.RevertAll();
            Input.RevertBindings();
        }

        public void ResetCategory(SettingCategory c)
        {
            var keys = new List<string>();
            foreach (var k in Store.Keys) if (Catalog.CategoryOf(k) == c) keys.Add(k);
            Store.ResetToDefaults(keys);
            if (c.id == "controls") Input.ResetBindingsToDefaults();
        }

        public void ResetAll()
        {
            Store.ResetToDefaults(new List<string>(Store.Keys));
            Input.ResetBindingsToDefaults();
        }

        /// <summary>Settings in a category that differ from their defaults (for the reset dialog).</summary>
        public int CountNonDefault(SettingCategory c)
        {
            int n = 0;
            foreach (var k in Store.Keys) if ((c == null || Catalog.CategoryOf(k) == c) && !Store.IsAtDefault(k)) n++;
            if (c == null || c.id == "controls") n += Input.NonDefaultBindingCount();
            return n;
        }

        // ------------------------------------------------------------------ options and formatting
        public IList<SettingOption> GetOptions(SettingDefinition d)
        {
            switch (d.optionSource)
            {
                case SettingOptionSource.Displays: return Display.DisplayOptions();
                case SettingOptionSource.Resolutions: return Display.ResolutionOptions();
                case SettingOptionSource.RefreshRates: return Display.RefreshOptions();
            }
            List<SettingOption> list;
            if (!m_StaticOptions.TryGetValue(d, out list))
            {
                list = new List<SettingOption>();
                for (int i = 0; i < d.options.Length; i++) list.Add(new SettingOption(d.options[i], i));
                m_StaticOptions[d] = list;
            }
            return list;
        }

        /// <summary>Index of the option closest to <paramref name="value"/>.</summary>
        public int OptionIndex(SettingDefinition d, double value)
        {
            var opts = GetOptions(d);
            int best = 0; double bestDist = double.MaxValue;
            for (int i = 0; i < opts.Count; i++)
            {
                double dist = Math.Abs(opts[i].value - value);
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        public string FormatValue(SettingDefinition d, double v)
        {
            if (d == null) return "";
            switch (d.kind)
            {
                case SettingKind.Toggle: return v >= 0.5 ? "On" : "Off";
                case SettingKind.Slider: return v.ToString("F" + d.decimals, System.Globalization.CultureInfo.InvariantCulture) + (d.unit ?? "");
                case SettingKind.Binding: return Input.BindingSummary(d);
                case SettingKind.Action: return "";
                default:
                    var opts = GetOptions(d);
                    return opts.Count == 0 ? "" : opts[OptionIndex(d, v)].label;
            }
        }

        /// <summary>Why a setting is unavailable right now, or null.</summary>
        public string DisabledReason(SettingDefinition d)
        {
            if (d == null || d.disabledWhen == null) return null;
            foreach (var c in d.disabledWhen)
                if (c != null && Store.Has(c.key) && c.IsMet(Store.Get(c.key))) return c.reason;
            return null;
        }

        // ------------------------------------------------------------------ persistence
        public static string SavePath { get { return Path.Combine(Application.persistentDataPath, SaveFileName); } }

        void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return;
                var data = JsonUtility.FromJson<SettingsSaveData>(File.ReadAllText(SavePath));
                if (data == null || data.version != SettingsStore.SaveVersion) return;
                Store.Load(data);
                m_LoadedBindings = data.bindingOverrides ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Accert one] Settings file could not be read, using defaults: " + e.Message);
            }
        }

        void Save()
        {
            try
            {
                var data = Store.ToSaveData(Input.SaveOverrides());
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError("[Accert one] Settings could not be saved: " + e.Message);
            }
        }
    }
}
