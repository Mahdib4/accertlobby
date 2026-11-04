using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Accertone.Settings;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Accertone.Garage
{
    public enum PhotoTarget { Car, Nose, Cockpit, RearWing, FrontWheel }
    public enum PhotoAspect { Screen, Cinema, Square, Portrait }
    public enum PhotoGuides { Off, Thirds, Centre }

    /// <summary>Everything Photo Mode lets the player set. Camera values are targets; the stage eases toward them.</summary>
    [Serializable]
    public class GaragePhotoSettings
    {
        public const float MinDistance = 2.6f, MaxDistance = 12f, MinPitch = -4f, MaxPitch = 80f, MinFocal = 16f, MaxFocal = 200f;

        // Camera: orbit around the target (yaw and pitch in degrees, distance in metres), lens in mm (full frame).
        public float yaw, pitch = 8f, distance = 7.5f, focal = 45f, roll, lift;
        public PhotoTarget target = PhotoTarget.Car;
        public float wheelSide = -1f;
        public bool spin;
        // Focus.
        public bool dof = true;
        public float aperture = 2.8f, focusAdjust;
        // Look: the preset sets these; the sliders adjust on top.
        public int look;
        public float exposure, contrast, saturation, vignette = 0.28f, grain;
        // Frame and output.
        public PhotoAspect aspect = PhotoAspect.Screen;
        public PhotoGuides guides = PhotoGuides.Thirds;
        public int supersample = 2;
        // Car.
        public bool drs, brakeGlow;

        public void ApplyLook(int index)
        {
            look = Mathf.Clamp(index, 0, GaragePhotoLooks.All.Length - 1);
            var l = GaragePhotoLooks.All[look];
            exposure = l.exposure;
            contrast = l.contrast;
            saturation = l.saturation;
            vignette = l.vignette;
            grain = l.grain;
        }

        /// <summary>Lens, grade, frame and car back to defaults. The camera position is left where it is.</summary>
        public void ResetAll()
        {
            focal = 45f;
            roll = 0f;
            lift = 0f;
            target = PhotoTarget.Car;
            spin = false;
            dof = true;
            aperture = 2.8f;
            focusAdjust = 0f;
            aspect = PhotoAspect.Screen;
            guides = PhotoGuides.Thirds;
            supersample = 2;
            drs = false;
            brakeGlow = false;
            ApplyLook(0);
        }

        /// <summary>Vertical field of view for a focal length on a 36 x 24 mm frame.</summary>
        public static float FovFromFocal(float mm) { return 2f * Mathf.Atan(12f / Mathf.Max(1f, mm)) * Mathf.Rad2Deg; }
        public static float FocalFromFov(float fov) { return 12f / Mathf.Tan(Mathf.Clamp(fov, 1f, 170f) * 0.5f * Mathf.Deg2Rad); }

        public static float AspectValue(PhotoAspect aspect, float screen)
        {
            switch (aspect)
            {
                case PhotoAspect.Cinema: return 2.39f;
                case PhotoAspect.Square: return 1f;
                case PhotoAspect.Portrait: return 0.8f;
            }
            return screen;
        }
    }

    /// <summary>A Photo Mode look: grade values for the sliders, a fixed grade part, and how the garage lights change.</summary>
    public struct GaragePhotoLook
    {
        public string name;
        public Color swatchA, swatchB;
        public float exposure, contrast, saturation, vignette, grain;
        public float chroma, temperature, tint;
        public Color filter;
        public float key, fill, kick, backdrop;
        public Color keyColor, fillColor, kickColor;
    }

    public static class GaragePhotoLooks
    {
        static readonly Color KeyStudio = new Color(1f, 0.97f, 0.93f);
        static readonly Color FillStudio = new Color(0.88f, 0.93f, 1f);
        static readonly Color KickStudio = new Color(0.62f, 0.34f, 1f);

        public static readonly GaragePhotoLook[] All =
        {
            new GaragePhotoLook
            {
                name = "STUDIO", swatchA = new Color(0.78f, 0.8f, 0.84f), swatchB = new Color(0.62f, 0.34f, 1f),
                exposure = 0f, contrast = 6f, saturation = 0f, vignette = 0.28f, grain = 0f, chroma = 0f, temperature = 0f, tint = 0f, filter = Color.white,
                key = 1f, fill = 1f, kick = 1f, backdrop = 1f, keyColor = KeyStudio, fillColor = FillStudio, kickColor = KickStudio,
            },
            new GaragePhotoLook
            {
                name = "MIDNIGHT", swatchA = new Color(0.12f, 0.16f, 0.36f), swatchB = new Color(0.55f, 0.3f, 1f),
                exposure = -0.35f, contrast = 18f, saturation = -8f, vignette = 0.42f, grain = 0.08f, chroma = 0f, temperature = -18f, tint = 6f, filter = new Color(0.9f, 0.93f, 1f),
                key = 0.65f, fill = 0.5f, kick = 1.9f, backdrop = 0.35f, keyColor = new Color(0.78f, 0.84f, 1f), fillColor = new Color(0.7f, 0.8f, 1f), kickColor = new Color(0.55f, 0.3f, 1f),
            },
            new GaragePhotoLook
            {
                name = "GOLDEN HOUR", swatchA = new Color(1f, 0.72f, 0.38f), swatchB = new Color(0.95f, 0.45f, 0.25f),
                exposure = 0.1f, contrast = 10f, saturation = 8f, vignette = 0.3f, grain = 0.05f, chroma = 0f, temperature = 28f, tint = 4f, filter = new Color(1f, 0.94f, 0.86f),
                key = 1.25f, fill = 0.7f, kick = 0.6f, backdrop = 0.8f, keyColor = new Color(1f, 0.78f, 0.52f), fillColor = new Color(1f, 0.88f, 0.75f), kickColor = new Color(1f, 0.55f, 0.3f),
            },
            new GaragePhotoLook
            {
                name = "NOIR", swatchA = new Color(0.92f, 0.92f, 0.92f), swatchB = new Color(0.08f, 0.08f, 0.08f),
                exposure = 0f, contrast = 38f, saturation = -100f, vignette = 0.5f, grain = 0.32f, chroma = 0f, temperature = 0f, tint = 0f, filter = Color.white,
                key = 1.35f, fill = 0.35f, kick = 1.2f, backdrop = 0.25f, keyColor = Color.white, fillColor = Color.white, kickColor = Color.white,
            },
            new GaragePhotoLook
            {
                name = "NEON", swatchA = new Color(0.85f, 0.25f, 1f), swatchB = new Color(0.25f, 0.7f, 1f),
                exposure = 0.05f, contrast = 14f, saturation = 32f, vignette = 0.36f, grain = 0f, chroma = 0.08f, temperature = -6f, tint = 12f, filter = Color.white,
                key = 0.7f, fill = 0.6f, kick = 2.4f, backdrop = 0.6f, keyColor = new Color(0.9f, 0.85f, 1f), fillColor = new Color(0.35f, 0.7f, 1f), kickColor = new Color(0.85f, 0.25f, 1f),
            },
            new GaragePhotoLook
            {
                name = "FILM", swatchA = new Color(0.86f, 0.78f, 0.62f), swatchB = new Color(0.36f, 0.42f, 0.4f),
                exposure = 0.05f, contrast = -6f, saturation = -18f, vignette = 0.34f, grain = 0.38f, chroma = 0.14f, temperature = 10f, tint = -4f, filter = new Color(1f, 0.97f, 0.92f),
                key = 1f, fill = 1.1f, kick = 0.8f, backdrop = 0.9f, keyColor = new Color(1f, 0.93f, 0.84f), fillColor = new Color(0.9f, 0.95f, 0.95f), kickColor = new Color(0.75f, 0.5f, 0.9f),
            },
        };

        public static GaragePhotoLook Get(int index) { return All[Mathf.Clamp(index, 0, All.Length - 1)]; }
    }

    /// <summary>
    /// The rendering half of Photo Mode: a global post-processing volume that fades in with the photo camera (depth of
    /// field, grade, vignette, grain), and the capture: the camera renders to an off-screen target (never the
    /// interface), the frame is cropped to the chosen aspect and a worker thread writes the PNG.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class GaragePhotoMode : MonoBehaviour
    {
        public static readonly GaragePhotoSettings Settings = new GaragePhotoSettings();
        /// <summary>When set, photos are written here instead of Pictures/Accert one.</summary>
        public static string FolderOverride;

        public struct Result
        {
            public bool ok;
            public string path, error;
            public int width, height;
        }

        public Action<Result> Captured;

        Volume m_Volume;
        VolumeProfile m_Profile;
        DepthOfField m_Dof;
        ColorAdjustments m_Color;
        Vignette m_Vignette;
        FilmGrain m_Grain;
        ChromaticAberration m_ChromaFx;
        WhiteBalance m_White;

        // The grade the scene has without Photo Mode; the photo values are blended from these.
        float m_BaseExposure, m_BaseContrast, m_BaseSaturation, m_BaseVignette, m_BaseSmooth = 0.4f, m_BaseGrain, m_BaseChroma, m_BaseTemp, m_BaseTint;
        Color m_BaseFilter = Color.white;
        // Fixed look parts, eased so switching looks dissolves.
        float m_Chroma, m_Temp, m_Tint;
        Color m_Filter = Color.white;

        RenderTexture m_Thumb;
        bool m_Busy;
        readonly object m_Lock = new object();
        bool m_HasResult;
        Result m_Result;

        public bool Busy { get { return m_Busy; } }
        public Texture Thumbnail { get { return m_Thumb; } }

        public static GaragePhotoMode Ensure(GameObject host)
        {
            var mode = host.GetComponent<GaragePhotoMode>();
            return mode != null ? mode : host.AddComponent<GaragePhotoMode>();
        }

        /// <summary>Reads the scene's grade before the photo volume fades in.</summary>
        public void Begin()
        {
            if (m_Volume != null && m_Volume.enabled) return;
            var stack = VolumeManager.instance.stack;
            if (stack == null) return;
            var ca = stack.GetComponent<ColorAdjustments>();
            if (ca != null) { m_BaseExposure = ca.postExposure.value; m_BaseContrast = ca.contrast.value; m_BaseSaturation = ca.saturation.value; m_BaseFilter = ca.colorFilter.value; }
            var vg = stack.GetComponent<Vignette>();
            if (vg != null) { m_BaseVignette = vg.active ? vg.intensity.value : 0f; m_BaseSmooth = vg.smoothness.value; }
            var gr = stack.GetComponent<FilmGrain>();
            if (gr != null) m_BaseGrain = gr.active ? gr.intensity.value : 0f;
            var ch = stack.GetComponent<ChromaticAberration>();
            if (ch != null) m_BaseChroma = ch.active ? ch.intensity.value : 0f;
            var wb = stack.GetComponent<WhiteBalance>();
            if (wb != null) { m_BaseTemp = wb.active ? wb.temperature.value : 0f; m_BaseTint = wb.active ? wb.tint.value : 0f; }
            var look = GaragePhotoLooks.Get(Settings.look);
            m_Chroma = look.chroma;
            m_Temp = look.temperature;
            m_Tint = look.tint;
            m_Filter = look.filter;
        }

        void EnsureVolume()
        {
            if (m_Volume != null) return;
            var go = new GameObject("GaragePhotoVolume");
            go.transform.SetParent(transform, false);
            m_Volume = go.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 60f;
            m_Volume.weight = 1f;
            m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Profile.name = "GaragePhotoProfile";
            // A runtime profile only: with no shared profile, the post-processing settings system leaves this volume alone.
            m_Volume.profile = m_Profile;

            m_Dof = m_Profile.Add<DepthOfField>(false);
            m_Dof.mode.overrideState = true;
            m_Dof.focusDistance.overrideState = true;
            m_Dof.aperture.overrideState = true;
            m_Dof.focalLength.overrideState = true;
            m_Dof.bladeCount.Override(7);
            m_Dof.bladeCurvature.Override(1f);
            m_Color = m_Profile.Add<ColorAdjustments>(false);
            m_Color.postExposure.overrideState = true;
            m_Color.contrast.overrideState = true;
            m_Color.saturation.overrideState = true;
            m_Color.colorFilter.overrideState = true;
            m_Vignette = m_Profile.Add<Vignette>(false);
            m_Vignette.intensity.overrideState = true;
            m_Vignette.smoothness.overrideState = true;
            m_Grain = m_Profile.Add<FilmGrain>(false);
            m_Grain.type.Override(FilmGrainLookup.Medium3);
            m_Grain.intensity.overrideState = true;
            m_Grain.response.Override(0.75f);
            m_ChromaFx = m_Profile.Add<ChromaticAberration>(false);
            m_ChromaFx.intensity.overrideState = true;
            m_White = m_Profile.Add<WhiteBalance>(false);
            m_White.temperature.overrideState = true;
            m_White.tint.overrideState = true;
        }

        void LateUpdate()
        {
            var stage = GarageStage.Instance;
            float pw = stage != null ? stage.PhotoWeight : 0f;
            if (m_Volume == null && pw <= 0f) return;
            EnsureVolume();
            bool on = pw > 0.0005f;
            if (m_Volume.enabled != on) m_Volume.enabled = on;
            if (!on) return;

            var s = Settings;
            var look = GaragePhotoLooks.Get(s.look);
            float k = GarageTheme.Approach(5f);
            m_Chroma = Mathf.Lerp(m_Chroma, look.chroma, k);
            m_Temp = Mathf.Lerp(m_Temp, look.temperature, k);
            m_Tint = Mathf.Lerp(m_Tint, look.tint, k);
            m_Filter = Color.Lerp(m_Filter, look.filter, k);

            // The volume always sits at full weight; every value blends from the scene's own by the photo weight,
            // so nothing pops as Photo Mode fades in and out.
            float e = pw * pw * (3f - 2f * pw);
            m_Color.postExposure.value = m_BaseExposure + s.exposure * e;
            m_Color.contrast.value = Mathf.Clamp(m_BaseContrast + s.contrast * e, -100f, 100f);
            m_Color.saturation.value = Mathf.Clamp(m_BaseSaturation + s.saturation * e, -100f, 100f);
            m_Color.colorFilter.value = Color.Lerp(m_BaseFilter, m_BaseFilter * m_Filter, e);
            m_Vignette.intensity.value = Mathf.Lerp(m_BaseVignette, s.vignette, e);
            m_Vignette.smoothness.value = Mathf.Lerp(m_BaseSmooth, 0.42f, e);
            m_Grain.intensity.value = Mathf.Lerp(m_BaseGrain, s.grain, e);
            m_ChromaFx.intensity.value = Mathf.Lerp(m_BaseChroma, m_Chroma, e);
            m_White.temperature.value = Mathf.Clamp(m_BaseTemp + m_Temp * e, -100f, 100f);
            m_White.tint.value = Mathf.Clamp(m_BaseTint + m_Tint * e, -100f, 100f);
            if (s.dof && stage != null)
            {
                m_Dof.mode.value = DepthOfFieldMode.Bokeh;
                m_Dof.focusDistance.value = Mathf.Max(0.1f, stage.PhotoFocusDistance + s.focusAdjust);
                m_Dof.focalLength.value = Mathf.Clamp(stage.PhotoFocal, 1f, 300f);
                // Opens up from f/32 (no visible blur) so the focus falls off gently as Photo Mode fades in.
                m_Dof.aperture.value = Mathf.Lerp(32f, Mathf.Clamp(s.aperture, 1f, 32f), e);
            }
            else m_Dof.mode.value = DepthOfFieldMode.Off;
        }

        void Update()
        {
            Result r;
            lock (m_Lock)
            {
                if (!m_HasResult) return;
                m_HasResult = false;
                r = m_Result;
            }
            m_Busy = false;
            if (!r.ok) Debug.LogWarning("[Accert one] Photo Mode: could not save the photo: " + r.error);
            if (Captured != null) Captured(r);
        }

        // =====================================================================================================
        // CAPTURE
        // =====================================================================================================
        public static Vector2Int OutputSize(Camera cam, GaragePhotoSettings s)
        {
            int sw = cam != null ? Mathf.Max(16, cam.pixelWidth) : Screen.width;
            int sh = cam != null ? Mathf.Max(16, cam.pixelHeight) : Screen.height;
            int ss = Supersample(sw, sh, s.supersample);
            var crop = CropRect(sw * ss, sh * ss, s.aspect);
            return new Vector2Int(crop.width, crop.height);
        }

        static int Supersample(int sw, int sh, int wanted)
        {
            int max = Mathf.Min(SystemInfo.maxTextureSize, 8192);
            int ss = Mathf.Clamp(wanted, 1, 3);
            while (ss > 1 && (sw * ss > max || sh * ss > max)) ss--;
            return ss;
        }

        static RectInt CropRect(int w, int h, PhotoAspect aspect)
        {
            float screen = (float)w / h;
            float a = GaragePhotoSettings.AspectValue(aspect, screen);
            int cw = w, ch = h;
            if (a > screen + 1e-3f) ch = Mathf.RoundToInt(w / a);
            else if (a < screen - 1e-3f) cw = Mathf.RoundToInt(h * a);
            return new RectInt((w - cw) / 2, (h - ch) / 2, cw, ch);
        }

        /// <summary>Renders the camera off-screen and saves a PNG; <see cref="Captured"/> reports the result.</summary>
        public bool Capture(Camera cam)
        {
            if (m_Busy || cam == null) return false;
            m_Busy = true;
            var s = Settings;
            int sw = Mathf.Max(16, cam.pixelWidth), sh = Mathf.Max(16, cam.pixelHeight);
            int ss = Supersample(sw, sh, s.supersample);
            int w = sw * ss, h = sh * ss;
            var crop = CropRect(w, h, s.aspect);

            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1, useMipMap = false };
            var rt = RenderTexture.GetTemporary(desc);
            bool rendered = false;
            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    request.destination = rt;
                    RenderPipeline.SubmitRenderRequest(cam, request);
                    rendered = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Accert one] Photo Mode: off-screen render failed, using a screen capture instead: " + e.Message);
            }
            if (!rendered)
            {
                RenderTexture.ReleaseTemporary(rt);
                StartCoroutine(CaptureScreen(s.aspect));
                return true;
            }

            MakeThumbnail(rt, crop, w, h);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(crop.x, crop.y, crop.width, crop.height), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var bytes = tex.GetRawTextureData<byte>().ToArray();
            Destroy(tex);
            Save(bytes, crop.width, crop.height);
            return true;
        }

        /// <summary>Fallback for pipelines without render requests: hides the interface for one frame and grabs the screen.</summary>
        IEnumerator CaptureScreen(PhotoAspect aspect)
        {
            var hidden = new List<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.enabled = false; hidden.Add(c); }
            }
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var c in hidden) if (c != null) c.enabled = true;
            var crop = CropRect(shot.width, shot.height, aspect);
            var pixels = shot.GetPixels32();
            var bytes = new byte[crop.width * crop.height * 4];
            for (int y = 0; y < crop.height; y++)
            {
                int src = (crop.y + y) * shot.width + crop.x;
                int dst = y * crop.width * 4;
                for (int x = 0; x < crop.width; x++)
                {
                    var p = pixels[src + x];
                    bytes[dst++] = p.r; bytes[dst++] = p.g; bytes[dst++] = p.b; bytes[dst++] = 255;
                }
            }
            var rt = RenderTexture.GetTemporary(shot.width, shot.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(shot, rt);
            MakeThumbnail(rt, crop, shot.width, shot.height);
            RenderTexture.ReleaseTemporary(rt);
            Destroy(shot);
            Save(bytes, crop.width, crop.height);
        }

        void MakeThumbnail(RenderTexture src, RectInt crop, int w, int h)
        {
            int tw = 400;
            int th = Mathf.Max(1, Mathf.RoundToInt(tw * (float)crop.height / crop.width));
            if (m_Thumb != null && (m_Thumb.width != tw || m_Thumb.height != th)) { m_Thumb.Release(); Destroy(m_Thumb); m_Thumb = null; }
            if (m_Thumb == null)
            {
                m_Thumb = new RenderTexture(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "GaragePhotoThumb" };
                m_Thumb.Create();
            }
            Graphics.Blit(src, m_Thumb, new Vector2((float)crop.width / w, (float)crop.height / h), new Vector2((float)crop.x / w, (float)crop.y / h));
        }

        void Save(byte[] rgba, int w, int h)
        {
            string folder = Folder();
            string path = UniquePath(folder);
            Task.Run(() =>
            {
                var r = new Result { width = w, height = h, path = path };
                try
                {
                    // The camera's alpha is not meant for a photo: write it opaque.
                    for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
                    var png = ImageConversion.EncodeArrayToPNG(rgba, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h, (uint)(w * 4));
                    Directory.CreateDirectory(folder);
                    File.WriteAllBytes(path, png);
                    r.ok = true;
                }
                catch (Exception e)
                {
                    r.error = e.Message;
                }
                lock (m_Lock)
                {
                    m_Result = r;
                    m_HasResult = true;
                }
            });
        }

        public static string Folder()
        {
            if (!string.IsNullOrEmpty(FolderOverride)) return FolderOverride;
            string pictures = null;
            try { pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); }
            catch (Exception) { }
            return !string.IsNullOrEmpty(pictures) ? Path.Combine(pictures, "Accert one") : Path.Combine(Application.persistentDataPath, "Photos");
        }

        static string UniquePath(string folder)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(folder, "AccertOne_AC-26_" + stamp + ".png");
            for (int i = 2; File.Exists(path) && i < 100; i++) path = Path.Combine(folder, "AccertOne_AC-26_" + stamp + "_" + i + ".png");
            return path;
        }

        public static void OpenFolder()
        {
            string folder = Folder();
            try
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL("file:///" + folder.Replace('\\', '/'));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Accert one] Photo Mode: could not open " + folder + ": " + e.Message);
            }
        }

        void OnDestroy()
        {
            if (m_Thumb != null) { m_Thumb.Release(); Destroy(m_Thumb); }
            if (m_Profile != null) Destroy(m_Profile);
        }
    }

    // =========================================================================================================
    // PANEL
    // =========================================================================================================
    /// <summary>Photo Mode controls: camera, focus, look, frame, car and capture.</summary>
    public class GaragePhotoPanel : GaragePanel
    {
        static readonly string[] s_Targets = { "CAR", "NOSE", "COCKPIT", "REAR WING", "WHEEL" };
        GarageReadout m_Output;

        GaragePhotoSettings S { get { return GaragePhotoMode.Settings; } }

        public override string Title { get { return "Photo Mode"; } }
        public override string Description { get { return "Frame the AC-26 like a press shot: orbit the camera, choose the lens and focus, grade the image, then capture a clean PNG without the interface."; } }
        public override GarageIconKind Icon { get { return GarageIconKind.Camera; } }
        public override GaragePart Part { get { return GaragePart.Centre; } }
        public override GarageShot Shot { get { return GarageStage.HeroShot(); } }
        public override string Effect { get { return "LIVE CAMERA, LENS AND GRADE  ·  PNG CAPTURE"; } }
        public override string Status() { return ""; }
        public override string IndexLabel { get { return "CAPTURE"; } }
        public override string ResetLabel { get { return "RESET PHOTO SETTINGS"; } }

        public override void Build()
        {
            var capture = GarageButton.Create(stack.Root, "CAPTURE PHOTO  ·  SPACE", W, () => screen.CapturePhoto(), true, false, GarageEffect.Live, GarageIconKind.Camera);
            stack.Place(capture, 10f);
            m_Output = Readout("OUTPUT", false, GarageEffect.Spec);
            stack.Place(m_Output, 6f);
            OnRefresh(() =>
            {
                var cam = GarageStage.Instance != null ? GarageStage.Instance.Camera : null;
                var size = GaragePhotoMode.OutputSize(cam, S);
                m_Output.Set(size.x + " × " + size.y + "  PNG");
            });

            stack.Section("CAMERA");
            stack.Place(Slider("FOCAL LENGTH", GaragePhotoSettings.MinFocal, GaragePhotoSettings.MaxFocal, 1f, 45f, () => S.focal, v => S.focal = v,
                v => Mathf.RoundToInt(v) + " MM", GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("DISTANCE", GaragePhotoSettings.MinDistance, GaragePhotoSettings.MaxDistance, 0.1f, 7.5f, () => S.distance, v => S.distance = v,
                v => v.ToString("0.0") + " M", GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("ORBIT", 0f, 359f, 1f, 0f, () => Mathf.Repeat(S.yaw, 360f), v => S.yaw = v,
                v => Mathf.RoundToInt(v).ToString("000") + "°", GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("ELEVATION", GaragePhotoSettings.MinPitch, GaragePhotoSettings.MaxPitch, 1f, 8f, () => S.pitch, v => S.pitch = v,
                v => Mathf.RoundToInt(v) + "°", GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("ROLL", -25f, 25f, 0.5f, 0f, () => S.roll, v => S.roll = v,
                v => v.ToString("0.0") + "°", GarageEffect.Live, GaragePart.Centre));
            stack.Place(GarageChoice.Create(stack.Root, "TARGET", W, s_Targets, () => (int)S.target, i =>
            {
                if (i == (int)PhotoTarget.FrontWheel && GarageStage.Instance != null) S.wheelSide = GarageStage.Instance.CameraSideOfCar();
                S.target = (PhotoTarget)i;
                Refresh();
            }, GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("TARGET HEIGHT", -0.4f, 1.2f, 0.05f, 0f, () => S.lift, v => S.lift = v,
                v => (v >= 0f ? "+" : "") + v.ToString("0.00") + " M", GarageEffect.Live, GaragePart.Centre));

            stack.Section("FOCUS");
            stack.Place(GarageChoice.Create(stack.Root, "DEPTH OF FIELD", W, new[] { "OFF", "ON" }, () => S.dof ? 1 : 0, i => { S.dof = i == 1; Refresh(); },
                GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("APERTURE", 1.4f, 16f, 0.1f, 2.8f, () => S.aperture, v => S.aperture = v,
                v => "f/" + v.ToString("0.0"), GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("FOCUS ADJUST", -3f, 3f, 0.05f, 0f, () => S.focusAdjust, v => S.focusAdjust = v,
                v => (v >= 0f ? "+" : "") + v.ToString("0.00") + " M", GarageEffect.Live, GaragePart.Centre));

            stack.Section("LOOK");
            var looks = GaragePhotoLooks.All;
            var names = new string[looks.Length];
            var a = new Color[looks.Length];
            var b = new Color[looks.Length];
            for (int i = 0; i < looks.Length; i++) { names[i] = looks[i].name; a[i] = looks[i].swatchA; b[i] = looks[i].swatchB; }
            stack.Place(GarageChoice.CreateTiles(stack.Root, "LOOK", W, 3, names, a, b, () => S.look, i => { S.ApplyLook(i); Refresh(); },
                GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("EXPOSURE", -2f, 2f, 0.1f, 0f, () => S.exposure, v => S.exposure = v,
                v => (v >= 0f ? "+" : "") + v.ToString("0.0") + " EV", GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("CONTRAST", -50f, 50f, 1f, 6f, () => S.contrast, v => S.contrast = v, I, GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("SATURATION", -100f, 50f, 1f, 0f, () => S.saturation, v => S.saturation = v, I, GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("VIGNETTE", 0f, 1f, 0.01f, 0.28f, () => S.vignette, v => S.vignette = v, v => Mathf.RoundToInt(v * 100f) + "%",
                GarageEffect.Live, GaragePart.Centre));
            stack.Place(Slider("FILM GRAIN", 0f, 1f, 0.01f, 0f, () => S.grain, v => S.grain = v, v => Mathf.RoundToInt(v * 100f) + "%",
                GarageEffect.Live, GaragePart.Centre));

            stack.Section("FRAME");
            stack.Place(GarageChoice.Create(stack.Root, "ASPECT", W, new[] { "SCREEN", "2.39 : 1", "1 : 1", "4 : 5" }, () => (int)S.aspect,
                i => { S.aspect = (PhotoAspect)i; Refresh(); }, GarageEffect.Live, GaragePart.Centre));
            stack.Place(GarageChoice.Create(stack.Root, "GUIDES", W, new[] { "OFF", "THIRDS", "CENTRE" }, () => (int)S.guides,
                i => { S.guides = (PhotoGuides)i; Refresh(); }, GarageEffect.Live, GaragePart.Centre));
            stack.Place(GarageChoice.Create(stack.Root, "SUPERSAMPLING", W, new[] { "1×", "2×", "3×" }, () => S.supersample - 1,
                i => { S.supersample = i + 1; Refresh(); }, GarageEffect.Spec, GaragePart.Centre));

            stack.Section("CAR");
            stack.Place(GarageChoice.Create(stack.Root, "TURNTABLE", W, new[] { "HOLD", "SPIN" }, () => S.spin ? 1 : 0, i => { S.spin = i == 1; Refresh(); },
                GarageEffect.Live, GaragePart.Centre));
            stack.Place(GarageChoice.Create(stack.Root, "DRS FLAP", W, new[] { "CLOSED", "OPEN" }, () => S.drs ? 1 : 0, i => { S.drs = i == 1; ApplyCar(); Refresh(); },
                GarageEffect.Live, GaragePart.RearWing));
            stack.Place(GarageChoice.Create(stack.Root, "BRAKE GLOW", W, new[] { "OFF", "ON" }, () => S.brakeGlow ? 1 : 0, i => { S.brakeGlow = i == 1; ApplyCar(); Refresh(); },
                GarageEffect.Live, GaragePart.Brakes));

            stack.Section("FILES");
            stack.Place(GarageButton.Create(stack.Root, "OPEN PHOTOS FOLDER", W, GaragePhotoMode.OpenFolder, false, false, GarageEffect.Spec, GarageIconKind.Save));
            stack.Note("Photos are rendered from the game camera without the interface and saved as PNG to " + FolderLabel() + ". The look, depth of field and lights apply in Photo Mode only; leaving it restores the garage.",
                GarageIconKind.Info);
        }

        static string FolderLabel()
        {
            if (!string.IsNullOrEmpty(GaragePhotoMode.FolderOverride)) return GaragePhotoMode.FolderOverride;
            string pictures = null;
            try { pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures); }
            catch (Exception) { }
            return !string.IsNullOrEmpty(pictures) ? "Pictures › Accert one" : "the game's Photos folder";
        }

        public void ApplyCar()
        {
            var rig = Rig;
            if (rig == null) return;
            rig.SetDrsPreview(S.drs);
            rig.SetBrakeHeat(S.brakeGlow ? 0.85f : 0f, S.brakeGlow ? 0.55f : 0f);
        }

        public override void OnOpen() { ApplyCar(); }

        public override void OnClose()
        {
            var rig = Rig;
            if (rig == null) return;
            rig.SetDrsPreview(false);
            rig.SetBrakeHeat(0f, 0f);
        }

        public override void ResetCategory()
        {
            S.ResetAll();
            ApplyCar();
        }
    }

    // =========================================================================================================
    // VIEWFINDER
    // =========================================================================================================
    /// <summary>
    /// Photo Mode's screen furniture. Under the panel: the aspect mask and composition guides. Over everything: the
    /// lens read-out, key prompts, the shutter flash and the saved-photo card with its thumbnail.
    /// </summary>
    public class GaragePhotoOverlay : MonoBehaviour
    {
        RectTransform m_Under, m_Over;
        CanvasGroup m_UnderGroup, m_OverGroup, m_ToastGroup, m_PromptGroup;
        readonly RectTransform[] m_Bars = new RectTransform[4];
        readonly RectTransform[] m_Thirds = new RectTransform[4];
        readonly RectTransform[] m_Cross = new RectTransform[2];
        RectTransform m_CropFrame;
        readonly RectTransform[] m_Corners = new RectTransform[8];
        Image m_Flash;
        TextMeshProUGUI m_Readout, m_ToastTitle, m_ToastFile, m_ToastFolder;
        RawImage m_Thumb;
        RectTransform m_ToastRt, m_ThumbRt;
        float m_Show, m_ShowT, m_FlashAt = -10f, m_ToastShow, m_ToastUntil;
        int m_ShownFocal = -1, m_ShownAperture = -1, m_ShownYaw = -1, m_ShownDistance = -1, m_ShownDof = -1;

        public void Build(RectTransform under, RectTransform over)
        {
            m_Under = under;
            m_Over = over;
            m_UnderGroup = under.gameObject.AddComponent<CanvasGroup>();
            m_UnderGroup.blocksRaycasts = false;
            m_UnderGroup.interactable = false;
            m_OverGroup = over.gameObject.AddComponent<CanvasGroup>();
            m_OverGroup.blocksRaycasts = false;
            m_OverGroup.interactable = false;

            // Aspect mask: four bars that close in around the crop.
            for (int i = 0; i < 4; i++)
            {
                var bar = SettingsUI.Fill(under, "Mask" + i, new Color(0.02f, 0.02f, 0.03f, 0.82f));
                bar.raycastTarget = false;
                m_Bars[i] = bar.rectTransform;
                m_Bars[i].anchorMin = m_Bars[i].anchorMax = m_Bars[i].pivot = Vector2.zero;
            }
            m_CropFrame = SettingsUI.Rect("Crop", under);
            m_CropFrame.anchorMin = m_CropFrame.anchorMax = m_CropFrame.pivot = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                var line = SettingsUI.Fill(m_CropFrame, "Third" + i, new Color(1f, 1f, 1f, 0.2f));
                line.raycastTarget = false;
                m_Thirds[i] = line.rectTransform;
                m_Thirds[i].anchorMin = m_Thirds[i].anchorMax = m_Thirds[i].pivot = Vector2.zero;
            }
            for (int i = 0; i < 2; i++)
            {
                var line = SettingsUI.Fill(m_CropFrame, "Cross" + i, new Color(1f, 1f, 1f, 0.45f));
                line.raycastTarget = false;
                m_Cross[i] = line.rectTransform;
                m_Cross[i].anchorMin = m_Cross[i].anchorMax = new Vector2(0.5f, 0.5f);
                m_Cross[i].pivot = new Vector2(0.5f, 0.5f);
                m_Cross[i].sizeDelta = i == 0 ? new Vector2(28f, 1f) : new Vector2(1f, 28f);
            }
            for (int i = 0; i < 4; i++)
            {
                var h = SettingsUI.Fill(m_CropFrame, "CornerH" + i, SettingsTheme.A(SettingsTheme.White, 0.7f));
                var v = SettingsUI.Fill(m_CropFrame, "CornerV" + i, SettingsTheme.A(SettingsTheme.White, 0.7f));
                h.raycastTarget = v.raycastTarget = false;
                var corner = new Vector2(i % 2, i / 2);
                h.rectTransform.anchorMin = h.rectTransform.anchorMax = h.rectTransform.pivot = corner;
                v.rectTransform.anchorMin = v.rectTransform.anchorMax = v.rectTransform.pivot = corner;
                h.rectTransform.sizeDelta = new Vector2(30f, 2f);
                v.rectTransform.sizeDelta = new Vector2(2f, 30f);
                h.rectTransform.anchoredPosition = v.rectTransform.anchoredPosition = Vector2.zero;
                m_Corners[i * 2] = h.rectTransform;
                m_Corners[i * 2 + 1] = v.rectTransform;
            }

            // Lens read-out, top centre.
            var dot = SettingsUI.Diamond(over, "RecDot", SettingsTheme.Accent, 7f);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            dot.rectTransform.anchoredPosition = new Vector2(-330f, -46f);
            m_Readout = GarageTheme.Text(over, "Readout", "", SettingsTheme.Data, 12f, SettingsTheme.Silver, 6f, TextAlignmentOptions.Center);
            m_Readout.rectTransform.anchorMin = m_Readout.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            m_Readout.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m_Readout.rectTransform.anchoredPosition = new Vector2(0f, -46f);
            m_Readout.rectTransform.sizeDelta = new Vector2(640f, 20f);
            m_Readout.raycastTarget = false;

            // Key prompts, bottom left (under the panel's footprint, where the garage footer sits).
            var prompts = SettingsUI.Rect("Prompts", over);
            SettingsUI.Stretch(prompts);
            m_PromptGroup = prompts.gameObject.AddComponent<CanvasGroup>();
            float x = 48f;
            x = Prompt(prompts, x, new[] { "W", "A", "S", "D" }, "ORBIT");
            x = Prompt(prompts, x, new[] { "R", "F" }, "DOLLY");
            x = Prompt(prompts, x, new[] { "Z", "X" }, "LENS");
            x = Prompt(prompts, x, new[] { "Q", "E" }, "ROLL");
            x = Prompt(prompts, x, new[] { "SPACE" }, "CAPTURE");
            x = Prompt(prompts, x, new[] { "H" }, "PANEL");
            Prompt(prompts, x, new[] { "ESC" }, "EXIT");

            // Saved-photo card, bottom right.
            m_ToastRt = SettingsUI.Rect("Saved", over);
            m_ToastRt.anchorMin = m_ToastRt.anchorMax = m_ToastRt.pivot = new Vector2(1f, 0f);
            m_ToastRt.sizeDelta = new Vector2(320f, 240f);
            m_ToastRt.anchoredPosition = new Vector2(-48f, 96f);
            m_ToastGroup = m_ToastRt.gameObject.AddComponent<CanvasGroup>();
            m_ToastGroup.alpha = 0f;
            var card = SettingsUI.Shape(m_ToastRt, "Card", SettingsTheme.A(SettingsTheme.Graphite, 0.94f), 10f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
            SettingsUI.Stretch(card.rectTransform);
            var rule = SettingsUI.Fill(m_ToastRt, "Accent", SettingsTheme.Accent);
            rule.raycastTarget = false;
            SettingsUI.At(rule.rectTransform, 0f, 0f, 2f, 240f);
            m_ThumbRt = SettingsUI.Rect("Thumb", m_ToastRt);
            m_Thumb = m_ThumbRt.gameObject.AddComponent<RawImage>();
            m_Thumb.raycastTarget = false;
            SettingsUI.At(m_ThumbRt, 16f, 16f, 288f, 162f);
            m_ToastTitle = GarageTheme.Text(m_ToastRt, "Title", "PHOTO SAVED", SettingsTheme.Label, 11f, SettingsTheme.Positive, 22f);
            SettingsUI.At(m_ToastTitle.rectTransform, 16f, 186f, 288f, 16f);
            m_ToastFile = GarageTheme.Text(m_ToastRt, "File", "", SettingsTheme.Data, 10f, SettingsTheme.White, 0f);
            SettingsUI.At(m_ToastFile.rectTransform, 16f, 204f, 288f, 14f);
            m_ToastFolder = GarageTheme.Text(m_ToastRt, "Folder", "", SettingsTheme.Label, 9f, SettingsTheme.Muted, 10f);
            SettingsUI.At(m_ToastFolder.rectTransform, 16f, 220f, 288f, 14f);
            foreach (var t in m_ToastRt.GetComponentsInChildren<Graphic>(true)) t.raycastTarget = false;

            // Shutter flash over everything.
            m_Flash = SettingsUI.Fill(over, "Flash", new Color(1f, 1f, 1f, 0f));
            m_Flash.raycastTarget = false;
            SettingsUI.Stretch(m_Flash.rectTransform);

            m_Show = m_ShowT = 0f;
            Apply(0f);
        }

        static float Prompt(RectTransform parent, float x, string[] keys, string label)
        {
            foreach (var k in keys)
            {
                var cap = SettingsUI.Keycap(parent, k);
                cap.anchorMin = cap.anchorMax = new Vector2(0f, 0f);
                cap.pivot = new Vector2(0f, 0.5f);
                cap.anchoredPosition = new Vector2(x, 53f);
                x += cap.sizeDelta.x + 4f;
            }
            var t = GarageTheme.Text(parent, "Prompt_" + label, label, SettingsTheme.Label, 10f, SettingsTheme.Text2, 22f);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 0f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            float w = Mathf.Ceil(t.GetPreferredValues(label, 400f, 16f).x);
            t.rectTransform.sizeDelta = new Vector2(w + 4f, 16f);
            t.rectTransform.anchoredPosition = new Vector2(x + 4f, 53f);
            t.raycastTarget = false;
            return x + w + 30f;
        }

        public void SetVisible(bool on)
        {
            m_ShowT = on ? 1f : 0f;
            if (!on) m_ToastUntil = 0f;
        }

        public void Flash()
        {
            m_FlashAt = Time.unscaledTime;
        }

        public void ShowResult(GaragePhotoMode.Result r, Texture thumb)
        {
            m_Thumb.texture = thumb;
            if (thumb != null)
            {
                float h = Mathf.Min(162f, 288f * thumb.height / Mathf.Max(1f, thumb.width));
                float w = h * thumb.width / Mathf.Max(1f, thumb.height);
                SettingsUI.At(m_ThumbRt, 16f + (288f - w) * 0.5f, 16f + (162f - h) * 0.5f, w, h);
            }
            m_ToastTitle.text = r.ok ? "PHOTO SAVED  ·  " + r.width + " × " + r.height : "COULD NOT SAVE THE PHOTO";
            m_ToastTitle.color = r.ok ? SettingsTheme.Positive : SettingsTheme.Danger;
            m_ToastFile.text = r.ok ? Path.GetFileName(r.path) : (r.error ?? "");
            m_ToastFolder.text = r.ok ? ShortFolder(Path.GetDirectoryName(r.path)) : "";
            m_ToastUntil = Time.unscaledTime + 4.5f;
        }

        static string ShortFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return "";
            folder = folder.Replace('\\', '/');
            var parts = folder.Split('/');
            return parts.Length >= 2 ? (parts[parts.Length - 2] + " › " + parts[parts.Length - 1]).ToUpperInvariant() : folder.ToUpperInvariant();
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            m_Show = Mathf.MoveTowards(m_Show, m_ShowT, dt / Mathf.Max(0.01f, AccertoneMotion.Duration(0.35f)));
            if (AccertoneMotion.Scale <= 0.01f) m_Show = m_ShowT;
            Apply(m_Show);
            if (m_Show <= 0.001f && m_ToastShow <= 0.001f && m_Flash.color.a <= 0.001f) return;
            UpdateMask();
            UpdateReadout();

            float since = Time.unscaledTime - m_FlashAt;
            float flash = since < 0f ? 0f : Mathf.Clamp01(1f - since / 0.4f);
            if (AccertoneMotion.Scale <= 0.01f) flash = 0f;
            m_Flash.color = new Color(1f, 1f, 1f, 0.75f * flash * flash);

            float toastT = Time.unscaledTime < m_ToastUntil && m_Show > 0.5f ? 1f : 0f;
            m_ToastShow = Mathf.MoveTowards(m_ToastShow, toastT, dt / 0.3f);
            m_ToastGroup.alpha = m_ToastShow;
            m_ToastRt.anchoredPosition = new Vector2(-48f + 24f * (1f - m_ToastShow), 96f);
        }

        void Apply(float show)
        {
            m_UnderGroup.alpha = show;
            m_OverGroup.alpha = show > 0f || m_ToastShow > 0f ? 1f : 0f;
            float s = show * show * (3f - 2f * show);
            m_Readout.alpha = s;
            m_PromptGroup.alpha = s;
        }

        void UpdateMask()
        {
            var size = m_Under.rect.size;
            if (size.x <= 1f || size.y <= 1f) return;
            float screen = size.x / size.y;
            float a = GaragePhotoSettings.AspectValue(GaragePhotoMode.Settings.aspect, screen);
            float cw = size.x, ch = size.y;
            if (a > screen + 1e-3f) ch = size.x / a;
            else if (a < screen - 1e-3f) cw = size.y * a;
            float x0 = (size.x - cw) * 0.5f, y0 = (size.y - ch) * 0.5f;

            // Bars: left, right, bottom, top (bottom-left anchored).
            Place(m_Bars[0], 0f, 0f, x0, size.y);
            Place(m_Bars[1], x0 + cw, 0f, size.x - x0 - cw, size.y);
            Place(m_Bars[2], x0, 0f, cw, y0);
            Place(m_Bars[3], x0, y0 + ch, cw, size.y - y0 - ch);
            Place(m_CropFrame, x0, y0, cw, ch);

            var guides = GaragePhotoMode.Settings.guides;
            bool thirds = guides == PhotoGuides.Thirds;
            Place(m_Thirds[0], cw / 3f, 0f, 1f, ch);
            Place(m_Thirds[1], cw * 2f / 3f, 0f, 1f, ch);
            Place(m_Thirds[2], 0f, ch / 3f, cw, 1f);
            Place(m_Thirds[3], 0f, ch * 2f / 3f, cw, 1f);
            for (int i = 0; i < 4; i++) if (m_Thirds[i].gameObject.activeSelf != thirds) m_Thirds[i].gameObject.SetActive(thirds);
            bool cross = guides != PhotoGuides.Off;
            for (int i = 0; i < 2; i++) if (m_Cross[i].gameObject.activeSelf != cross) m_Cross[i].gameObject.SetActive(cross);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        void UpdateReadout()
        {
            var stage = GarageStage.Instance;
            var s = GaragePhotoMode.Settings;
            int focal = Mathf.RoundToInt(stage != null ? stage.PhotoFocal : s.focal);
            int aperture = Mathf.RoundToInt(s.aperture * 10f);
            int yaw = Mathf.RoundToInt(Mathf.Repeat(s.yaw, 360f));
            int distance = Mathf.RoundToInt(s.distance * 10f);
            int dof = s.dof ? 1 : 0;
            if (focal == m_ShownFocal && aperture == m_ShownAperture && yaw == m_ShownYaw && distance == m_ShownDistance && dof == m_ShownDof) return;
            m_ShownFocal = focal; m_ShownAperture = aperture; m_ShownYaw = yaw; m_ShownDistance = distance; m_ShownDof = dof;
            m_Readout.text = "PHOTO MODE    " + focal + " MM    " + (s.dof ? "f/" + (aperture / 10f).ToString("0.0") : "DOF OFF") + "    ORBIT " + yaw.ToString("000") + "°    " + (distance / 10f).ToString("0.0") + " M";
        }
    }
}
