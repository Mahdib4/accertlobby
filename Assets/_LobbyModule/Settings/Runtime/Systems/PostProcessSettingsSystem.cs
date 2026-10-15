using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Accertone.Settings
{
    /// <summary>
    /// Brightness, gamma and the optional post effects (bloom, motion blur, depth of field, film grain), applied to
    /// the <b>runtime instance</b> of every volume profile in the loaded scenes (<see cref="Volume.profile"/>), so
    /// profile assets are never changed. Brightness and gamma are offsets on top of each scene's own grade.
    /// </summary>
    public class PostProcessSettingsSystem : ISettingsSystem
    {
        static readonly string[] Keys =
        {
            SettingKeys.Brightness, SettingKeys.Gamma, SettingKeys.PostProcessing, SettingKeys.Bloom, SettingKeys.MotionBlur,
            SettingKeys.DepthOfField, SettingKeys.FilmGrain, SettingKeys.GraphicsPreset
        };

        class Entry
        {
            public Volume volume;
            public VolumeProfile profile;
            public float baseExposure;
            public Vector4 baseGamma = new Vector4(1f, 1f, 1f, 0f);
            public bool bloomInScene, grainInScene;
        }

        AccertoneSettings m_S;
        readonly List<Entry> m_Entries = new List<Entry>();

        public void Initialise(AccertoneSettings settings) { m_S = settings; }

        public void OnSettingChanged(string key)
        {
            if (System.Array.IndexOf(Keys, key) >= 0) ApplyAll();
        }

        public void OnSceneLoaded(Scene scene) { ApplyAll(); }

        public void Shutdown() { m_Entries.Clear(); }

        public void ApplyAll()
        {
            Collect();
            foreach (var e in m_Entries) Apply(e);
        }

        void Collect()
        {
            m_Entries.RemoveAll(e => e.volume == null);
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!v.isGlobal || v.sharedProfile == null || m_Entries.Exists(e => e.volume == v)) continue;
                var p = v.profile; // runtime copy owned by this volume
                var e = new Entry { volume = v, profile = p };
                ColorAdjustments ca;
                if (p.TryGet(out ca) && ca.postExposure.overrideState) e.baseExposure = ca.postExposure.value;
                LiftGammaGain lgg;
                if (p.TryGet(out lgg) && lgg.gamma.overrideState) e.baseGamma = lgg.gamma.value;
                Bloom b;
                e.bloomInScene = p.TryGet(out b) && b.active;
                FilmGrain g;
                e.grainInScene = p.TryGet(out g) && g.active;
                m_Entries.Add(e);
            }
        }

        void Apply(Entry e)
        {
            var p = e.profile;
            if (p == null) return;

            var ca = Get<ColorAdjustments>(p);
            ca.postExposure.overrideState = true;
            ca.postExposure.value = e.baseExposure + (m_S.Float(SettingKeys.Brightness) - 50f) / 50f;

            var lgg = Get<LiftGammaGain>(p);
            lgg.gamma.overrideState = true;
            Vector4 gamma = e.baseGamma;
            gamma.w += (2.2f - m_S.Float(SettingKeys.Gamma)) * 1.25f;
            lgg.gamma.value = gamma;

            bool post = m_S.Bool(SettingKeys.PostProcessing);
            Bloom bloom;
            if (p.TryGet(out bloom)) bloom.active = e.bloomInScene && post && m_S.Bool(SettingKeys.Bloom);
            FilmGrain grain;
            if (p.TryGet(out grain)) grain.active = e.grainInScene && post && m_S.Bool(SettingKeys.FilmGrain);

            int blur = m_S.Int(SettingKeys.MotionBlur);
            var mb = Get<MotionBlur>(p);
            mb.active = post && blur > 0;
            mb.intensity.overrideState = true;
            mb.intensity.value = blur >= 2 ? 0.55f : 0.25f;
            mb.quality.overrideState = true;
            mb.quality.value = blur >= 2 ? MotionBlurQuality.High : MotionBlurQuality.Medium;

            var dof = Get<DepthOfField>(p);
            dof.active = post && m_S.Bool(SettingKeys.DepthOfField);
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.overrideState = true;
            dof.gaussianStart.value = 18f;
            dof.gaussianEnd.overrideState = true;
            dof.gaussianEnd.value = 60f;
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            T c;
            if (!p.TryGet(out c))
            {
                c = p.Add<T>();
                c.active = true;
            }
            return c;
        }
    }
}
