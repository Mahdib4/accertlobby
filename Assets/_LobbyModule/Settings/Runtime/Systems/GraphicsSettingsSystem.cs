using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Accertone.LobbyModule;
using Object = UnityEngine.Object;

namespace Accertone.Settings
{
    /// <summary>
    /// Graphics: works on a <b>runtime copy</b> of the active URP asset (and its renderer + features), so the
    /// project's pipeline asset is never modified. Covers render scale, upscaling, MSAA / camera anti-aliasing,
    /// shadows, lighting, SSAO, texture mip limit, anisotropic filtering and the showroom floor reflection.
    /// The original pipeline and quality values are restored on shutdown (leaving Play mode).
    /// </summary>
    public class GraphicsSettingsSystem : ISettingsSystem
    {
        static readonly string[] Keys =
        {
            SettingKeys.RenderScale, SettingKeys.Upscaling, SettingKeys.AntiAliasing, SettingKeys.TextureQuality, SettingKeys.Anisotropic,
            SettingKeys.ShadowQuality, SettingKeys.LightingQuality, SettingKeys.AmbientOcclusion, SettingKeys.Reflections, SettingKeys.GraphicsPreset
        };
        static readonly int[] ShadowResolution = { 1024, 1024, 2048, 2048, 4096 };
        static readonly int[] ShadowCascades = { 1, 1, 2, 4, 4 };
        static readonly int[] AdditionalLights = { 2, 4, 8 };
        static readonly int[] AdditionalShadowResolution = { 512, 1024, 2048 };
        static readonly int[] AnisoLevels = { 0, 2, 4, 8, 16 };
        static readonly float[] ReflectionScale = { 0.25f, 0.5f, 0.75f };

        AccertoneSettings m_S;
        RenderPipelineAsset m_OriginalLevelPipeline;
        UniversalRenderPipelineAsset m_Source;
        UniversalRenderPipelineAsset m_Runtime;
        readonly List<Object> m_Owned = new List<Object>();
        ScriptableRendererFeature m_Ssao;
        object m_SsaoSettings;
        readonly Dictionary<string, object> m_SsaoDefaults = new Dictionary<string, object>();
        float m_SourceShadowDistance = 50f;
        int m_OriginalMipLimit;
        AnisotropicFiltering m_OriginalAniso;
        bool m_HasOriginals;

        public UniversalRenderPipelineAsset RuntimeAsset { get { return m_Runtime; } }

        public void Initialise(AccertoneSettings settings)
        {
            m_S = settings;
            m_OriginalMipLimit = QualitySettings.globalTextureMipmapLimit;
            m_OriginalAniso = QualitySettings.anisotropicFiltering;
            m_HasOriginals = true;
            CreateRuntimePipeline();
        }

        void CreateRuntimePipeline()
        {
            m_Source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (m_Source == null) { Debug.LogWarning("[Accert one] Graphics settings need a URP pipeline asset."); return; }
            m_SourceShadowDistance = m_Source.shadowDistance;
            m_OriginalLevelPipeline = QualitySettings.renderPipeline;

            m_Runtime = Object.Instantiate(m_Source);
            m_Runtime.name = m_Source.name + " (Settings)";
            m_Owned.Add(m_Runtime);

            // Copy the renderer data and its features too, so toggling SSAO never touches the renderer asset.
            var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = listField != null ? listField.GetValue(m_Runtime) as ScriptableRendererData[] : null;
            if (list != null)
            {
                var copy = new ScriptableRendererData[list.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i] == null) continue;
                    var data = Object.Instantiate(list[i]);
                    data.name = list[i].name + " (Settings)";
                    m_Owned.Add(data);
                    var features = data.rendererFeatures;
                    for (int f = 0; f < features.Count; f++)
                    {
                        if (features[f] == null) continue;
                        var feature = Object.Instantiate(features[f]);
                        feature.name = features[f].name;
                        m_Owned.Add(feature);
                        features[f] = feature;
                        if (m_Ssao == null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") m_Ssao = feature;
                    }
                    data.SetDirty();
                    copy[i] = data;
                }
                listField.SetValue(m_Runtime, copy);
            }

            if (m_Ssao != null)
            {
                var sf = m_Ssao.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance);
                m_SsaoSettings = sf != null ? sf.GetValue(m_Ssao) : null;
                if (m_SsaoSettings != null)
                    foreach (var n in new[] { "Downsample", "Samples", "BlurQuality", "Intensity" })
                    {
                        var fi = m_SsaoSettings.GetType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fi != null) m_SsaoDefaults[n] = fi.GetValue(m_SsaoSettings);
                    }
            }

            QualitySettings.renderPipeline = m_Runtime;
        }

        public void OnSettingChanged(string key)
        {
            if (Array.IndexOf(Keys, key) >= 0) ApplyAll();
        }

        public void ApplyAll()
        {
            ApplyPipeline();
            ApplyCameras();
            ApplyTextures();
            ApplyReflections();
        }

        public void OnSceneLoaded(Scene scene)
        {
            ApplyCameras();
            ApplyReflections();
        }

        public void Shutdown()
        {
            if (m_Runtime != null && QualitySettings.renderPipeline == m_Runtime) QualitySettings.renderPipeline = m_OriginalLevelPipeline;
            if (m_HasOriginals)
            {
                QualitySettings.globalTextureMipmapLimit = m_OriginalMipLimit;
                QualitySettings.anisotropicFiltering = m_OriginalAniso;
            }
            foreach (var o in m_Owned) if (o != null) Object.Destroy(o);
            m_Owned.Clear();
            m_Runtime = null;
        }

        // ------------------------------------------------------------------ pipeline
        void ApplyPipeline()
        {
            var a = m_Runtime;
            if (a == null) return;
            a.renderScale = Mathf.Clamp(m_S.Float(SettingKeys.RenderScale) / 100f, 0.1f, 2f);
            UpscalingFilterSelection filter;
            switch (m_S.Int(SettingKeys.Upscaling))
            {
                case 0: filter = UpscalingFilterSelection.Linear; break;
                case 2: filter = UpscalingFilterSelection.STP; break;
                default: filter = UpscalingFilterSelection.FSR; break;
            }
            a.upscalingFilter = filter;
            int aa = m_S.Int(SettingKeys.AntiAliasing);
            a.msaaSampleCount = aa == 4 ? 2 : aa == 5 ? 4 : 1;

            int s = Mathf.Clamp(m_S.Int(SettingKeys.ShadowQuality), 0, 4);
            SetField(a, "m_MainLightShadowsSupported", s > 0);
            a.mainLightShadowmapResolution = ShadowResolution[s];
            a.shadowCascadeCount = ShadowCascades[s];
            float[] distance = { 0f, m_SourceShadowDistance * 0.5f, m_SourceShadowDistance * 0.8f, m_SourceShadowDistance, m_SourceShadowDistance * 1.6f };
            a.shadowDistance = distance[s];
            SetField(a, "m_SoftShadowsSupported", s >= 2);

            int l = Mathf.Clamp(m_S.Int(SettingKeys.LightingQuality), 0, 2);
            a.maxAdditionalLightsCount = AdditionalLights[l];
            SetField(a, "m_AdditionalLightShadowsSupported", l >= 1 && s > 0);
            a.additionalLightsShadowmapResolution = AdditionalShadowResolution[l];

            ApplyAmbientOcclusion(m_S.Int(SettingKeys.AmbientOcclusion));
        }

        void ApplyAmbientOcclusion(int level)
        {
            if (m_Ssao == null) return;
            m_Ssao.SetActive(level > 0);
            if (m_SsaoSettings == null) return;
            bool low = level == 1;
            SetSsao("Downsample", low ? (object)true : null);
            SetSsaoEnum("Samples", low ? "Low" : null);
            SetSsaoEnum("BlurQuality", low ? "Low" : null);
        }

        void SetSsao(string field, object value)
        {
            var fi = m_SsaoSettings.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return;
            object v = value ?? (m_SsaoDefaults.ContainsKey(field) ? m_SsaoDefaults[field] : null);
            if (v != null) fi.SetValue(m_SsaoSettings, v);
        }

        void SetSsaoEnum(string field, string enumName)
        {
            var fi = m_SsaoSettings.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return;
            if (enumName == null) { if (m_SsaoDefaults.ContainsKey(field)) fi.SetValue(m_SsaoSettings, m_SsaoDefaults[field]); return; }
            if (fi.FieldType.IsEnum && Enum.IsDefined(fi.FieldType, enumName)) fi.SetValue(m_SsaoSettings, Enum.Parse(fi.FieldType, enumName));
        }

        static void SetField(object target, string name, bool value)
        {
            var fi = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null && fi.FieldType == typeof(bool)) fi.SetValue(target, value);
        }

        // ------------------------------------------------------------------ cameras / textures / reflections
        void ApplyCameras()
        {
            int aa = m_S.Int(SettingKeys.AntiAliasing);
            AntialiasingMode mode;
            switch (aa)
            {
                case 1: mode = AntialiasingMode.FastApproximateAntialiasing; break;
                case 2: mode = AntialiasingMode.SubpixelMorphologicalAntiAliasing; break;
                case 3: mode = AntialiasingMode.TemporalAntiAliasing; break;
                default: mode = AntialiasingMode.None; break;
            }
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.targetTexture != null || cam.cameraType != CameraType.Game) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.antialiasing = mode;
                data.antialiasingQuality = AntialiasingQuality.High;
                cam.allowMSAA = aa >= 4;
            }
        }

        void ApplyTextures()
        {
            int q = Mathf.Clamp(m_S.Int(SettingKeys.TextureQuality), 0, 2);
            QualitySettings.globalTextureMipmapLimit = 2 - q;
            int a = AnisoLevels[Mathf.Clamp(m_S.Int(SettingKeys.Anisotropic), 0, AnisoLevels.Length - 1)];
            if (a == 0) QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            else
            {
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                Texture.SetGlobalAnisotropicFilteringLimits(a, a);
            }
        }

        void ApplyReflections()
        {
            float scale = ReflectionScale[Mathf.Clamp(m_S.Int(SettingKeys.Reflections), 0, 2)];
            foreach (var r in Object.FindObjectsByType<PlanarFloorReflection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                r.resolutionScale = scale;
        }

        /// <summary>Rough GPU load (percent of the High preset at 100% render scale) for the detail panel.</summary>
        public int EstimatedLoad(SettingsStore s)
        {
            double rs = s.Get(SettingKeys.RenderScale) / 100.0;
            int aa = (int)s.Get(SettingKeys.AntiAliasing);
            double load = rs * rs * 30 + s.Get(SettingKeys.ShadowQuality) * 9 + s.Get(SettingKeys.AmbientOcclusion) * 6 + s.Get(SettingKeys.Reflections) * 4
                + s.Get(SettingKeys.LightingQuality) * 5 + new[] { 0, 2, 3, 7, 6, 10 }[Mathf.Clamp(aa, 0, 5)]
                + (s.Get(SettingKeys.PostProcessing) > 0.5 ? 4 : 0) + s.Get(SettingKeys.MotionBlur) * 2 + (s.Get(SettingKeys.DepthOfField) > 0.5 ? 4 : 0);
            return (int)Math.Round(load);
        }
    }
}
