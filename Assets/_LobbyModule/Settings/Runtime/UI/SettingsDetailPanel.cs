using System.Collections.Generic;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>
    /// The right-hand panel: what the focused setting does, why it is unavailable, when it applies, its default,
    /// and a live preview (display, calibration, GPU cost, audio meters, input telemetry, camera, HUD...).
    /// </summary>
    public class SettingsDetailPanel : MonoBehaviour
    {
        const float Inner = 328f;
        static readonly string[] ChannelShort = { "MST", "MUS", "FX", "ENG", "ENV", "UI", "VOX" };

        SettingsScreen m_Screen;
        RectTransform m_Stack;
        TextMeshProUGUI m_Eyebrow, m_Title, m_Info, m_Applies, m_Default;
        Image m_AppliesDot;
        RectTransform m_Reason, m_Listen;
        TextMeshProUGUI m_ReasonText, m_ListenText;
        RectTransform m_Preview;
        [System.NonSerialized] SettingDefinition m_Def;
        SettingPreview m_Kind = (SettingPreview)(-1);
        [System.NonSerialized] SettingDefinition m_PreviewDef;

        // live preview state
        readonly List<Image> m_BusLive = new List<Image>();
        readonly List<RectTransform> m_BusSet = new List<RectTransform>();
        readonly List<Graphic> m_BusLabels = new List<Graphic>();
        RectTransform m_SteerOut, m_SteerRaw, m_SteerDz;
        Image m_Thr, m_Brk;
        TextMeshProUGUI m_SteerText, m_ThrText, m_BrkText, m_PadState;
        UIPolyline m_Curve;
        RectTransform m_CurveDot, m_CurveDz, m_CurveBox;
        float m_KbSteer;

        public void Build(SettingsScreen screen, RectTransform panel)
        {
            m_Screen = screen;
            m_Stack = SettingsUI.At(SettingsUI.Rect("Stack", panel), 28f, 28f, Inner, 560f);
            var v = m_Stack.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            v.spacing = 12f;

            m_Eyebrow = SettingsUI.Text(m_Stack, "Eyebrow", "", SettingsTheme.Label, 10.5f, SettingsTheme.Muted, 24f, TextAlignmentOptions.TopLeft, true);
            m_Title = SettingsUI.Paragraph(m_Stack, "Title", "", SettingsTheme.Title, 22f, SettingsTheme.White, 0f);
            m_Title.fontStyle = FontStyles.UpperCase; m_Title.characterSpacing = 5f;
            m_Info = SettingsUI.Paragraph(m_Stack, "Info", "", SettingsTheme.Body, 14.5f, SettingsTheme.Text2, 10f);

            m_Reason = Callout("Reason", SettingsTheme.Warning, SettingsTheme.Hex(0xFFD9A0), out m_ReasonText);
            m_Listen = Callout("Listening", SettingsTheme.Accent, SettingsTheme.Hex(0xE9C7FF), out m_ListenText);

            var applies = SettingsUI.Rect("Applies", m_Stack);
            applies.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
            m_AppliesDot = SettingsUI.Fill(applies, "Dot", SettingsTheme.Positive);
            SettingsUI.At(m_AppliesDot.rectTransform, 0f, 5f, 7f, 7f);
            m_Applies = SettingsUI.Text(applies, "Text", "", SettingsTheme.Label, 10.5f, SettingsTheme.Silver, 20f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(m_Applies.rectTransform, 17f, 0f, Inner - 17f, 18f);

            m_Preview = SettingsUI.Rect("Preview", m_Stack);
            m_Preview.gameObject.AddComponent<LayoutElement>().preferredHeight = 300f;

            var rule = SettingsUI.Fill(panel, "FootRule", SettingsTheme.Hairline);
            SettingsUI.At(rule.rectTransform, 28f, 680f - 52f, Inner, 1f);
            var cap = SettingsUI.Text(panel, "DefaultCaption", "DEFAULT", SettingsTheme.Label, 10.5f, SettingsTheme.Muted, 20f);
            SettingsUI.At(cap.rectTransform, 28f, 680f - 46f, 120f, 30f);
            m_Default = SettingsUI.Text(panel, "DefaultValue", "", SettingsTheme.Label, 12f, SettingsTheme.Silver, 6f, TextAlignmentOptions.MidlineRight, true);
            SettingsUI.At(m_Default.rectTransform, 28f + 100f, 680f - 46f, Inner - 100f, 30f);
        }

        RectTransform Callout(string name, Color bar, Color text, out TextMeshProUGUI label)
        {
            var rt = SettingsUI.Rect(name, m_Stack);
            var bg = SettingsUI.Fill(rt, "Bg", SettingsTheme.A(bar, 0.08f));
            SettingsUI.Stretch(bg.rectTransform);
            var b = SettingsUI.Fill(rt, "Bar", bar);
            b.rectTransform.anchorMin = Vector2.zero; b.rectTransform.anchorMax = new Vector2(0f, 1f); b.rectTransform.pivot = new Vector2(0f, 0.5f);
            b.rectTransform.sizeDelta = new Vector2(2f, 0f); b.rectTransform.anchoredPosition = Vector2.zero;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(14, 12, 10, 10);
            h.childControlHeight = true; h.childControlWidth = true; h.childForceExpandWidth = true;
            label = SettingsUI.Paragraph(rt, "Text", "", SettingsTheme.Body, 13f, text, 6f);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            b.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rt.gameObject.SetActive(false);
            return rt;
        }

        /// <summary>Show the focused setting (null clears).</summary>
        public void Show(SettingDefinition def, SettingCategory category, string sectionTitle)
        {
            var s = AccertoneSettings.Instance;
            m_Def = def;
            if (def == null) { m_Stack.gameObject.SetActive(false); return; }
            m_Stack.gameObject.SetActive(true);
            m_Eyebrow.text = category.title + (string.IsNullOrEmpty(sectionTitle) ? "" : "  ·  " + sectionTitle);
            m_Title.text = def.label;
            m_Info.text = def.info;

            string reason = s.DisabledReason(def);
            m_Reason.gameObject.SetActive(reason != null);
            if (reason != null) m_ReasonText.text = reason;

            bool listening = m_Screen.IsRebinding(def, -1);
            m_Listen.gameObject.SetActive(listening);
            if (listening)
                m_ListenText.text = def.bindingGroup == InputSettingsSystem.GamepadGroup
                    ? "Press a button on your controller. Menu cancels."
                    : "Press a key. Esc cancels, Backspace clears the slot.";

            bool showApplies = def.kind != SettingKind.DeviceTabs && def.kind != SettingKind.Action;
            m_Applies.transform.parent.gameObject.SetActive(showApplies);
            switch (def.applies)
            {
                case SettingApplies.Confirm: m_Applies.text = "Applied with a 15-second confirmation"; m_AppliesDot.color = SettingsTheme.Warning; break;
                case SettingApplies.OnTrack: m_Applies.text = "Takes effect on track"; m_AppliesDot.color = SettingsTheme.Accent; break;
                case SettingApplies.Online: m_Applies.text = "Takes effect in online sessions"; m_AppliesDot.color = SettingsTheme.Accent; break;
                default: m_Applies.text = "Previewed now · kept when you apply"; m_AppliesDot.color = SettingsTheme.Positive; break;
            }
            m_AppliesDot.rectTransform.localRotation = def.applies == SettingApplies.OnTrack || def.applies == SettingApplies.Online ? Quaternion.Euler(0, 0, 45f) : Quaternion.identity;

            if (def.kind == SettingKind.Binding) m_Default.text = s.Input.DefaultSummary(def);
            else if (def.StoresValue) m_Default.text = s.FormatValue(def, s.Store.GetDefault(def.key));
            else m_Default.text = "—";

            var kind = def.preview;
            if (kind != m_Kind || def != m_PreviewDef || kind == SettingPreview.Graphics || kind == SettingPreview.Monitor || kind == SettingPreview.Assists
                || kind == SettingPreview.Hud || kind == SettingPreview.CockpitCamera || kind == SettingPreview.ChaseCamera || kind == SettingPreview.Shake
                || kind == SettingPreview.Calibration || kind == SettingPreview.InterfaceScale || kind == SettingPreview.Showroom || kind == SettingPreview.Animation
                || kind == SettingPreview.AudioBus || kind == SettingPreview.Vibration)
                BuildPreview(def);
            LayoutRebuilder.MarkLayoutForRebuild(m_Stack);
        }

        // ------------------------------------------------------------------ previews
        void ClearPreview()
        {
            for (int i = m_Preview.childCount - 1; i >= 0; i--) Destroy(m_Preview.GetChild(i).gameObject);
            m_BusLive.Clear(); m_BusSet.Clear(); m_BusLabels.Clear();
            m_SteerOut = null; m_Curve = null; m_PadState = null;
        }

        void BuildPreview(SettingDefinition def)
        {
            ClearPreview();
            m_Kind = def.preview;
            m_PreviewDef = def;
            float h = 0f;
            switch (def.preview)
            {
                case SettingPreview.Graphics: h = PreviewGraphics(def); break;
                case SettingPreview.Monitor: h = PreviewMonitor(); break;
                case SettingPreview.Calibration: h = PreviewCalibration(); break;
                case SettingPreview.InterfaceScale: h = PreviewInterfaceScale(); break;
                case SettingPreview.AudioBus: h = PreviewAudio(def); break;
                case SettingPreview.Input: h = PreviewInput(0f); break;
                case SettingPreview.Vibration: h = PreviewVibration(); break;
                case SettingPreview.Assists: h = PreviewAssists(); break;
                case SettingPreview.CockpitCamera: h = PreviewCamera(false); break;
                case SettingPreview.ChaseCamera: h = PreviewCamera(true); break;
                case SettingPreview.Shake: h = PreviewShake(); break;
                case SettingPreview.Hud: h = PreviewHud(); break;
                case SettingPreview.Showroom:
                    h = Note(0f, AccertoneSettings.Instance.Bool(SettingKeys.ShowroomMotion)
                        ? "Drift and mouse follow are on. The showroom camera behind this screen is moving the same way."
                        : "The showroom camera is completely still.", "LOBBY CAMERA", true);
                    break;
                case SettingPreview.Animation:
                    string[] t = { "Full transitions: panels slide and fade and the category bar glides.", "Reduced: the same moves at roughly half the length.", "Off: every change is instant." };
                    h = Note(0f, t[Mathf.Clamp(AccertoneSettings.Instance.Int(SettingKeys.InterfaceAnimations), 0, 2)] + " Switch categories to see it.", "PREVIEW", true);
                    break;
            }
            m_Preview.GetComponent<LayoutElement>().preferredHeight = h;
        }

        RectTransform Box(float y, float h)
        {
            var rt = SettingsUI.At(SettingsUI.Rect("Box", m_Preview), 0f, y, Inner, h);
            var bg = SettingsUI.Fill(rt, "Bg", SettingsTheme.A(SettingsTheme.Graphite, 0.6f));
            SettingsUI.Stretch(bg.rectTransform);
            var border = SettingsUI.Shape(rt, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
            SettingsUI.Stretch(border.rectTransform);
            return rt;
        }

        TextMeshProUGUI Cap(Transform parent, string text, float x, float y, float w = 200f, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = SettingsUI.Text(parent, "Caption", text, SettingsTheme.Label, 10.5f, SettingsTheme.Muted, 22f, align, true);
            SettingsUI.At(t.rectTransform, x, y, w, 14f);
            return t;
        }

        TextMeshProUGUI Data(Transform parent, string text, float x, float y, float w = 200f, TextAlignmentOptions align = TextAlignmentOptions.MidlineRight, float size = 15f)
        {
            var t = SettingsUI.Text(parent, "Data", text, SettingsTheme.Data, size, SettingsTheme.White, 0f, align, true);
            SettingsUI.At(t.rectTransform, x, y, w, 18f);
            return t;
        }

        float Note(float y, string text, string caption = null, bool boxed = false)
        {
            float pad = boxed ? 16f : 0f;
            RectTransform parent = boxed ? Box(y, 10f) : m_Preview;
            float cy = boxed ? 16f : y;
            if (caption != null) { Cap(parent, caption, pad, cy); cy += 22f; }
            var p = SettingsUI.Paragraph(parent, "Note", text, SettingsTheme.Body, 13f, boxed ? SettingsTheme.Text2 : SettingsTheme.Muted, 6f);
            float th = p.GetPreferredValues(text, Inner - pad * 2f, 400f).y;
            SettingsUI.At(p.rectTransform, pad, cy, Inner - pad * 2f, th + 4f);
            float bottom = cy + th + 6f;
            if (!boxed) return bottom;
            float total = bottom + 10f;
            parent.sizeDelta = new Vector2(Inner, total);
            return y + total;
        }

        void Meter(Transform parent, float y, string label, int value, int max, bool cost, float x = 0f, float width = Inner, bool showValue = true)
        {
            Cap(parent, label, x, y + 1f, 110f);
            float segX = x + 112f, segW = width - 112f - (showValue ? 34f : 0f);
            float each = (segW - (max - 1) * 4f) / max;
            for (int i = 0; i < max; i++)
            {
                var seg = SettingsUI.Shape(parent, "Seg", i < value ? (cost ? SettingsTheme.Accent : SettingsTheme.Silver) : SettingsTheme.Hairline, 0f, ChamferCorners.None, 3f);
                SettingsUI.At(seg.rectTransform, segX + i * (each + 4f), y + 3f, each, 8f);
            }
            if (showValue) Data(parent, value + "/" + max, x + width - 34f, y - 1f, 34f, TextAlignmentOptions.MidlineRight, 12f);
        }

        float PreviewGraphics(SettingDefinition def)
        {
            var s = AccertoneSettings.Instance;
            float y = 0f;
            if (def.gpuCost > 0 || def.visualGain > 0)
            {
                Cap(m_Preview, "Impact", 0f, 0f);
                Meter(m_Preview, 24f, "GPU cost", def.gpuCost, 5, true);
                Meter(m_Preview, 46f, "Visual gain", def.visualGain, 5, false);
                y = 80f;
            }
            var box = Box(y, 128f);
            string[] names = { "Low", "Medium", "High", "Ultra", "Custom" };
            Cap(box, "Preset", 16f, 18f);
            Data(box, names[Mathf.Clamp(s.Int(SettingKeys.GraphicsPreset), 0, 4)].ToUpperInvariant(), 140f, 16f, 172f);
            int load = s.Graphics.EstimatedLoad(s.Store);
            Cap(box, "Estimated GPU load", 16f, 46f, 200f);
            Data(box, load + "%", 200f, 44f, 112f);
            for (int i = 0; i < 10; i++)
            {
                var seg = SettingsUI.Shape(box, "Seg", i < Mathf.RoundToInt(load / 10f) ? SettingsTheme.Accent : SettingsTheme.Hairline, 0f, ChamferCorners.None, 3f);
                SettingsUI.At(seg.rectTransform, 16f + i * 30f, 70f, 26f, 8f);
            }
            var note = SettingsUI.Paragraph(box, "Note", "Relative to High at 100% render scale.", SettingsTheme.Body, 12.5f, SettingsTheme.Muted);
            SettingsUI.At(note.rectTransform, 16f, 92f, Inner - 32f, 24f);
            return y + 128f;
        }

        float PreviewMonitor()
        {
            var s = AccertoneSettings.Instance;
            var box = Box(0f, 262f);
            float sw = Inner - 32f, sh = sw * 9f / 16f;
            var frame = SettingsUI.Shape(box, "Monitor", SettingsTheme.Hex(0x4A505B), 0f, ChamferCorners.None, 0f, true, 2f);
            SettingsUI.At(frame.rectTransform, 16f, 16f, sw, sh);
            var screenBg = SettingsUI.Fill(box, "Screen", SettingsTheme.Hex(0x07080A));
            SettingsUI.At(screenBg.rectTransform, 18f, 18f, sw - 4f, sh - 4f);
            int mode = s.Int(SettingKeys.WindowMode);
            bool windowed = mode == 2;
            float ix = windowed ? 60f : 26f, iy = windowed ? 40f : 26f, iw = windowed ? sw - 88f : sw - 20f, ih = windowed ? sh - 56f : sh - 20f - (mode == 1 ? 12f : 0f);
            var img = SettingsUI.Fill(box, "Image", SettingsTheme.Hex(0x221236));
            SettingsUI.At(img.rectTransform, ix, iy, iw, ih);
            if (windowed)
            {
                var title = SettingsUI.Fill(box, "TitleBar", SettingsTheme.Hairline);
                SettingsUI.At(title.rectTransform, ix, iy, iw, 10f);
                var edge = SettingsUI.Shape(box, "WindowEdge", SettingsTheme.Metallic, 0f, ChamferCorners.None, 0f, true, 1f);
                SettingsUI.At(edge.rectTransform, ix, iy, iw, ih);
            }
            if (mode == 1)
            {
                var bar = SettingsUI.Fill(box, "Taskbar", SettingsTheme.Panel);
                SettingsUI.At(bar.rectTransform, 26f, 16f + sh - 22f, sw - 20f, 10f);
            }
            var accent = SettingsUI.Fill(box, "Accent", SettingsTheme.Accent);
            SettingsUI.At(accent.rectTransform, ix + iw * 0.32f, iy + ih * 0.62f, iw * 0.36f, 3f);
            var stand = SettingsUI.Fill(box, "Stand", SettingsTheme.Hairline);
            SettingsUI.At(stand.rectTransform, 16f + sw * 0.5f - 22f, 16f + sh, 44f, 14f);
            var foot = SettingsUI.Fill(box, "Foot", SettingsTheme.Hairline);
            SettingsUI.At(foot.rectTransform, 16f + sw * 0.5f - 60f, 30f + sh, 120f, 4f);

            int w, h;
            DisplaySettingsSystem.DecodeResolution(s.Effective(SettingKeys.Resolution), out w, out h);
            if (s.IsEditing) DisplaySettingsSystem.DecodeResolution(s.Store.Get(SettingKeys.Resolution), out w, out h);
            float yy = 46f + sh;
            Data(box, w + " × " + h, 16f, yy, 200f, TextAlignmentOptions.MidlineLeft);
            Cap(box, s.FormatValue(s.Catalog.Find(SettingKeys.WindowMode), s.Store.Get(SettingKeys.WindowMode)), 160f, yy + 2f, Inner - 176f, TextAlignmentOptions.MidlineRight);
            string hz = mode == 0 ? s.FormatValue(s.Catalog.Find(SettingKeys.RefreshRate), s.Store.Get(SettingKeys.RefreshRate)) : "Desktop rate";
            string fps = s.Store.Get(SettingKeys.VSync) >= 0.5 ? "V-Sync" : s.FormatValue(s.Catalog.Find(SettingKeys.FrameRateLimit), s.Store.Get(SettingKeys.FrameRateLimit)) + " FPS cap";
            Cap(box, hz, 16f, yy + 26f);
            Cap(box, fps.Replace("Unlimited FPS cap", "Unlimited"), 160f, yy + 26f, Inner - 176f, TextAlignmentOptions.MidlineRight);
            return 262f;
        }

        float PreviewCalibration()
        {
            var s = AccertoneSettings.Instance;
            float exposure = (s.Float(SettingKeys.Brightness) - 50f) / 50f;
            float gamma = s.Float(SettingKeys.Gamma);
            float[] levels = { 6f, 14f, 28f };
            float tile = (Inner - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var bg = SettingsUI.Fill(m_Preview, "Tile", Color.black);
                SettingsUI.At(bg.rectTransform, i * (tile + 8f), 0f, tile, tile);
                float lin = Mathf.Pow(levels[i] / 255f, 2.2f) * Mathf.Pow(2f, exposure);
                float shown = Mathf.Clamp01(Mathf.Pow(lin, 1f / gamma));
                var c = new Color(shown, shown, shown, 1f);
                for (int b = 0; b < 2; b++)
                {
                    var bar = SettingsUI.Shape(bg.rectTransform, "Mark", c, 0f, ChamferCorners.None, 8f);
                    SettingsUI.At(bar.rectTransform, tile * 0.36f + b * 16f, tile * 0.22f, 10f, tile * 0.56f);
                }
            }
            string[] caps = { "Barely visible", "Visible", "Clear" };
            for (int i = 0; i < 3; i++) Cap(m_Preview, caps[i], i * (tile + 8f), tile + 10f, tile, TextAlignmentOptions.Center);
            return Note(tile + 34f, "Calibrate here, then check the showroom behind this screen. Menus are never affected.");
        }

        float PreviewInterfaceScale()
        {
            var s = AccertoneSettings.Instance;
            var box = Box(0f, 150f);
            box.gameObject.AddComponent<RectMask2D>();
            var sample = SettingsUI.At(SettingsUI.Rect("Sample", box), 18f, 16f, 300f, 130f);
            float k = (float)(s.Store.Get(SettingKeys.InterfaceScale) / 100.0);
            sample.localScale = new Vector3(k, k, 1f);
            Cap(sample, "Next session", 0f, 0f);
            var title = SettingsUI.Text(sample, "Track", "VALCORSA", SettingsTheme.Headline, 24f, SettingsTheme.White, 1f);
            SettingsUI.At(title.rectTransform, 0f, 18f, 280f, 30f);
            var sub = SettingsUI.Text(sample, "Sub", "Round 07 / 24 · 15 laps", SettingsTheme.Body, 13f, SettingsTheme.Text2);
            SettingsUI.At(sub.rectTransform, 0f, 52f, 280f, 18f);
            var key = SettingsUI.Keycap(sample, "ENTER");
            key.anchorMin = key.anchorMax = new Vector2(0f, 1f); key.pivot = new Vector2(0f, 1f); key.anchoredPosition = new Vector2(0f, -80f);
            Cap(sample, "Race", key.sizeDelta.x + 10f, 84f);
            return Note(160f, "Shown at " + s.Store.Get(SettingKeys.InterfaceScale).ToString("F0") + "%. The whole interface uses this size after you apply.");
        }

        float PreviewAudio(SettingDefinition def)
        {
            var s = AccertoneSettings.Instance;
            var box = Box(0f, 196f);
            float colW = (Inner - 28f) / 7f;
            for (int i = 0; i < AudioSettingsSystem.Channels.Length; i++)
            {
                string ch = AudioSettingsSystem.Channels[i];
                bool sel = def.audioChannel == ch;
                string key = AudioSettingsSystem.KeyForChannel(ch);
                bool muted = s.Store.Get(key + SettingKeys.MuteSuffix) >= 0.5;
                float x = 14f + i * colW + (colW - 14f) * 0.5f;
                var bar = SettingsUI.Fill(box, "Bar", SettingsTheme.A(SettingsTheme.Hairline, muted ? 0.25f : 0.6f));
                SettingsUI.At(bar.rectTransform, x, 16f, 14f, 150f);
                var live = SettingsUI.Fill(bar.transform, "Live", SettingsTheme.Accent);
                live.rectTransform.anchorMin = new Vector2(0f, 0f); live.rectTransform.anchorMax = new Vector2(1f, 0f);
                live.rectTransform.pivot = new Vector2(0.5f, 0f); live.rectTransform.sizeDelta = new Vector2(0f, 0f); live.rectTransform.anchoredPosition = Vector2.zero;
                var set = SettingsUI.Fill(bar.transform, "Set", sel ? SettingsTheme.White : SettingsTheme.Metallic);
                set.rectTransform.anchorMin = new Vector2(0f, 0f); set.rectTransform.anchorMax = new Vector2(1f, 0f);
                set.rectTransform.pivot = new Vector2(0.5f, 0.5f); set.rectTransform.sizeDelta = new Vector2(8f, 2f);
                var lbl = SettingsUI.Text(box, "Label", ChannelShort[i], SettingsTheme.Label, 9.5f, sel ? SettingsTheme.White : SettingsTheme.Muted, 14f, TextAlignmentOptions.Center);
                SettingsUI.At(lbl.rectTransform, 14f + i * colW, 172f, colW, 14f);
                m_BusLive.Add(live); m_BusSet.Add(set.rectTransform); m_BusLabels.Add(lbl);
            }
            float y = 208f;
            if (!string.IsNullOrEmpty(def.audioChannel))
            {
                var btn = SettingsUI.Rect("TestButton", m_Preview);
                SettingsUI.At(btn, 0f, y, 210f, 38f);
                var fill = SettingsUI.Shape(btn, "Fill", SettingsTheme.Field, 0f, ChamferCorners.None, 0f, false, 1f, true);
                SettingsUI.Stretch(fill.rectTransform);
                var border = SettingsUI.Shape(btn, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
                SettingsUI.Stretch(border.rectTransform);
                var kc = SettingsUI.Keycap(btn, m_Screen.Glyph("confirm"), m_Screen.UsingGamepad);
                kc.anchorMin = kc.anchorMax = new Vector2(0f, 0.5f); kc.pivot = new Vector2(0f, 0.5f); kc.anchoredPosition = new Vector2(12f, 0f);
                var t = SettingsUI.Text(btn, "Label", "Play test sound", SettingsTheme.Label, 12f, SettingsTheme.White, 14f, TextAlignmentOptions.MidlineLeft, true);
                SettingsUI.At(t.rectTransform, kc.sizeDelta.x + 22f, 0f, 180f, 38f);
                string ch = def.audioChannel;
                var p = fill.gameObject.AddComponent<SettingsPointer>();
                p.onEnter = () => border.color = SettingsTheme.Accent;
                p.onExit = () => border.color = SettingsTheme.Hairline;
                p.onClick = () => m_Screen.PlayTestSound(ch);
                y += 50f;
            }
            return Note(y, "White line: set level. Violet: what is playing right now.");
        }

        float PreviewVibration()
        {
            var s = AccertoneSettings.Instance;
            var box = Box(0f, 88f);
            Cap(box, "Controller", 16f, 16f);
            m_PadState = SettingsUI.Text(box, "State", "", SettingsTheme.Body, 13.5f, SettingsTheme.Text2);
            SettingsUI.At(m_PadState.rectTransform, 16f, 34f, Inner - 32f, 18f);
            Meter(box, 62f, "Intensity", Mathf.RoundToInt(s.Float(SettingKeys.VibrationIntensity) / 20f), 5, true, 16f, Inner - 32f);
            return PreviewInput(100f);
        }

        float PreviewInput(float y0)
        {
            var box = Box(y0, 300f);
            Cap(box, "Steering", 16f, 16f);
            m_SteerText = Data(box, "0%", 160f, 14f, Inner - 176f, TextAlignmentOptions.MidlineRight, 13f);
            var steer = SettingsUI.Fill(box, "SteerBar", SettingsTheme.A(SettingsTheme.Hairline, 0.55f));
            var srt = SettingsUI.At(steer.rectTransform, 16f, 40f, Inner - 32f, 18f);
            m_SteerDz = SettingsUI.Fill(srt, "Deadzone", SettingsTheme.A(SettingsTheme.Warning, 0.18f)).rectTransform;
            var centre = SettingsUI.Fill(srt, "Centre", SettingsTheme.Metallic);
            SettingsUI.At(centre.rectTransform, srt.rect.width * 0.5f, -3f, 1f, 24f);
            m_SteerOut = SettingsUI.Fill(srt, "Output", SettingsTheme.Accent).rectTransform;
            m_SteerRaw = SettingsUI.Fill(srt, "Raw", SettingsTheme.Metallic).rectTransform;

            m_Thr = Pedal(box, "THR", 16f, 72f, SettingsTheme.Positive, out m_ThrText);
            m_Brk = Pedal(box, "BRK", 16f + (Inner - 32f) * 0.5f + 6f, 72f, SettingsTheme.Danger, out m_BrkText);

            m_CurveBox = SettingsUI.At(SettingsUI.Rect("Curve", box), 16f, 100f, Inner - 32f, 150f);
            var cbg = SettingsUI.Shape(m_CurveBox, "Frame", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
            SettingsUI.Stretch(cbg.rectTransform);
            for (int i = 1; i < 4; i++)
            {
                var gx = SettingsUI.Fill(m_CurveBox, "Grid", SettingsTheme.Hex(0x1E2128));
                SettingsUI.At(gx.rectTransform, m_CurveBox.rect.width * i / 4f, 0f, 1f, 150f);
                var gy = SettingsUI.Fill(m_CurveBox, "Grid", SettingsTheme.Hex(0x1E2128));
                SettingsUI.At(gy.rectTransform, 0f, 150f * i / 4f, m_CurveBox.rect.width, 1f);
            }
            m_CurveDz = SettingsUI.Fill(m_CurveBox, "Deadzone", SettingsTheme.A(SettingsTheme.Warning, 0.12f)).rectTransform;
            var diag = SettingsUI.Rect("Linear", m_CurveBox);
            SettingsUI.Stretch(diag);
            var dl = diag.gameObject.AddComponent<UIPolyline>();
            dl.thickness = 1f; dl.color = SettingsTheme.Hex(0x4A505B); dl.raycastTarget = false;
            SettingsUI.SetSharp(dl, new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) });
            var crt = SettingsUI.Rect("Response", m_CurveBox);
            SettingsUI.Stretch(crt);
            m_Curve = crt.gameObject.AddComponent<UIPolyline>();
            m_Curve.thickness = 2.5f; m_Curve.color = SettingsTheme.Accent; m_Curve.raycastTarget = false;
            m_CurveDot = SettingsUI.Diamond(m_CurveBox, "Point", SettingsTheme.White, 8f).rectTransform;
            m_CurveDot.anchorMin = m_CurveDot.anchorMax = new Vector2(0f, 0f);
            Cap(box, "Input", 16f, 258f);
            Cap(box, "Response curve", 160f, 258f, Inner - 176f, TextAlignmentOptions.MidlineRight);
            m_LastCurveKey = null;
            UpdateCurve();
            UpdateTelemetry();
            return Note(y0 + 312f, SourceText(), null);
        }

        Image Pedal(RectTransform box, string label, float x, float y, Color c, out TextMeshProUGUI value)
        {
            float w = (Inner - 32f) * 0.5f - 6f;
            Cap(box, label, x, y);
            var bg = SettingsUI.Fill(box, label + "Bar", SettingsTheme.A(SettingsTheme.Hairline, 0.55f));
            SettingsUI.At(bg.rectTransform, x + 36f, y + 3f, w - 36f - 40f, 8f);
            var fill = SettingsUI.Fill(bg.transform, "Fill", c);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f); fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.sizeDelta = Vector2.zero; fill.rectTransform.anchoredPosition = Vector2.zero;
            value = Data(box, "0%", x + w - 40f, y - 1f, 40f, TextAlignmentOptions.MidlineRight, 12f);
            return fill;
        }

        string SourceText()
        {
            var s = AccertoneSettings.Instance;
            if (m_Screen.CurrentPage == 1)
                return UnityEngine.InputSystem.Gamepad.current != null ? "Live from your controller." : "Connect a controller to see live input.";
            string left = Summary("bind.kb.SteerLeft"), right = Summary("bind.kb.SteerRight"), thr = Summary("bind.kb.Throttle"), brk = Summary("bind.kb.Brake");
            return "Live from your keys: steer " + left + " and " + right + ", throttle " + thr + ", brake " + brk + ".";
        }

        static string Summary(string key)
        {
            var s = AccertoneSettings.Instance;
            var d = s.Catalog.Find(key);
            return d != null ? s.Input.BindingSummary(d) : "?";
        }

        string m_LastCurveKey;

        void CurveParams(out float sens, out float dz, out float lin)
        {
            var st = AccertoneSettings.Instance.Store;
            if (m_Screen.CurrentPage == 1)
            {
                sens = (float)st.Get(SettingKeys.PadSteerSensitivity) / 100f;
                dz = (float)st.Get(SettingKeys.PadSteerDeadzone) / 100f;
                lin = (float)st.Get(SettingKeys.PadSteerLinearity) / 100f;
            }
            else
            {
                sens = (float)st.Get(SettingKeys.KeyboardSteerSensitivity) / 100f;
                dz = 0f;
                lin = (float)st.Get(SettingKeys.KeyboardSteerLinearity) / 100f;
            }
        }

        void UpdateCurve()
        {
            if (m_Curve == null) return;
            float sens, dz, lin;
            CurveParams(out sens, out dz, out lin);
            string key = sens + "/" + dz + "/" + lin;
            if (key == m_LastCurveKey) return;
            m_LastCurveKey = key;
            var pts = new List<Vector2>();
            for (int i = 0; i <= 48; i++) { float x = i / 48f; pts.Add(new Vector2(x, DrivingInput.Shape(x, sens, dz, lin))); }
            SettingsUI.SetSharp(m_Curve, pts.ToArray());
            float w = m_CurveBox.rect.width;
            SettingsUI.At(m_CurveDz, 0f, 0f, w * dz, 150f);
            m_SteerDz.anchorMin = m_SteerDz.anchorMax = new Vector2(0.5f, 0.5f);
            m_SteerDz.sizeDelta = new Vector2((Inner - 32f) * dz, 18f);
            m_SteerDz.anchoredPosition = Vector2.zero;
        }

        void UpdateTelemetry()
        {
            if (m_SteerOut == null) return;
            var s = AccertoneSettings.Instance;
            float sens, dz, lin;
            CurveParams(out sens, out dz, out lin);
            float raw, thr, brk;
            if (m_Screen.CurrentPage == 1)
            {
                raw = s.Input.Read("Steer");
                float tdz = (float)s.Store.Get(SettingKeys.PadTriggerDeadzone) / 100f;
                thr = Trigger(s.Input.Read("Throttle"), tdz);
                brk = Trigger(s.Input.Read("Brake"), tdz);
            }
            else
            {
                float target = (s.Input.Pressed("SteerRight") ? 1f : 0f) - (s.Input.Pressed("SteerLeft") ? 1f : 0f);
                float speed = (float)s.Store.Get(SettingKeys.KeyboardSteerSpeed) * 0.9f;
                m_KbSteer = Mathf.MoveTowards(m_KbSteer, target, speed * Time.unscaledDeltaTime);
                raw = m_KbSteer;
                thr = s.Input.Pressed("Throttle") ? (float)s.Store.Get(SettingKeys.KeyboardThrottle) / 100f : 0f;
                brk = s.Input.Pressed("Brake") ? (float)s.Store.Get(SettingKeys.KeyboardBrake) / 100f : 0f;
            }
            float o = DrivingInput.Shape(raw, sens, dz, lin);
            float w = Inner - 32f;
            m_SteerRaw.anchorMin = m_SteerRaw.anchorMax = new Vector2(0.5f + raw * 0.5f, 0.5f);
            m_SteerRaw.sizeDelta = new Vector2(2f, 14f); m_SteerRaw.anchoredPosition = Vector2.zero;
            m_SteerOut.anchorMin = m_SteerOut.anchorMax = new Vector2(0.5f, 0.5f);
            m_SteerOut.pivot = new Vector2(o < 0f ? 1f : 0f, 0.5f);
            m_SteerOut.sizeDelta = new Vector2(Mathf.Abs(o) * w * 0.5f, 12f);
            m_SteerOut.anchoredPosition = Vector2.zero;
            m_SteerText.text = Mathf.RoundToInt(o * 100f) + "%";
            thr = Mathf.Clamp01(thr); brk = Mathf.Clamp01(brk);
            m_Thr.rectTransform.sizeDelta = new Vector2(((RectTransform)m_Thr.transform.parent).rect.width * thr, 0f);
            m_Brk.rectTransform.sizeDelta = new Vector2(((RectTransform)m_Brk.transform.parent).rect.width * brk, 0f);
            m_ThrText.text = Mathf.RoundToInt(thr * 100f) + "%";
            m_BrkText.text = Mathf.RoundToInt(brk * 100f) + "%";
            m_CurveDot.anchoredPosition = new Vector2(Mathf.Abs(raw) * m_CurveBox.rect.width, Mathf.Abs(o) * 150f);
            if (m_PadState != null)
            {
                var pad = UnityEngine.InputSystem.Gamepad.current;
                m_PadState.text = pad != null ? pad.displayName + " connected." : "No controller detected.";
            }
        }

        static float Trigger(float v, float dz) { return v <= dz ? 0f : (v - dz) / Mathf.Max(0.0001f, 1f - dz); }

        float PreviewAssists()
        {
            var s = AccertoneSettings.Instance;
            string[] keys = { SettingKeys.TractionControl, SettingKeys.Abs, SettingKeys.BrakingAssist, SettingKeys.SteeringAssist, SettingKeys.RacingLine, SettingKeys.Gearbox, SettingKeys.PitAssist };
            int helping = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                var d = s.Catalog.Find(keys[i]);
                double v = s.Store.Get(keys[i]);
                bool on = keys[i] == SettingKeys.Gearbox ? v < 0.5 : v >= 0.5;
                if (on) helping++;
                float y = i * 34f;
                var t = SettingsUI.Text(m_Preview, "Name", d.label, SettingsTheme.Body, 13.5f, SettingsTheme.Text2);
                SettingsUI.At(t.rectTransform, 0f, y, 200f, 34f);
                var val = SettingsUI.Text(m_Preview, "Value", s.FormatValue(d, v), SettingsTheme.Label, 11.5f, on ? SettingsTheme.White : SettingsTheme.Muted, 14f, TextAlignmentOptions.MidlineRight, true);
                SettingsUI.At(val.rectTransform, 160f, y, Inner - 160f, 34f);
                var rule = SettingsUI.Fill(m_Preview, "Rule", SettingsTheme.Hairline);
                SettingsUI.At(rule.rectTransform, 0f, y + 33f, Inner, 1f);
            }
            Meter(m_Preview, keys.Length * 34f + 14f, "Help level", Mathf.RoundToInt(helping / 7f * 5f), 5, true);
            string summary = "AI difficulty " + s.Store.Get(SettingKeys.AiDifficulty).ToString("F0") + " · damage " + s.FormatValue(s.Catalog.Find(SettingKeys.Damage), s.Store.Get(SettingKeys.Damage)).ToLowerInvariant()
                + " · rules " + s.FormatValue(s.Catalog.Find(SettingKeys.RulesAndFlags), s.Store.Get(SettingKeys.RulesAndFlags)).ToLowerInvariant() + ".";
            return Note(keys.Length * 34f + 40f, summary);
        }

        float PreviewCamera(bool chase)
        {
            var s = AccertoneSettings.Instance;
            var box = Box(0f, 236f);
            float fov = (float)s.Store.Get(chase ? SettingKeys.ChaseFov : SettingKeys.CockpitFov);
            var area = SettingsUI.At(SettingsUI.Rect("Scene", box), 12f, 12f, Inner - 24f, 190f);
            float cx = area.rect.width * 0.5f;
            float carY = chase ? 70f : 52f;
            float camY = chase ? 70f + 70f + (float)s.Store.Get(SettingKeys.ChaseDistance) * 0.45f : carY + 26f;
            camY = Mathf.Min(camY, 186f);
            var wedge = SettingsUI.Rect("FOV", area);
            SettingsUI.Stretch(wedge);
            var w = wedge.gameObject.AddComponent<UIWedge>();
            w.color = SettingsTheme.A(SettingsTheme.Accent, 0.16f);
            w.raycastTarget = false;
            w.Set(new Vector2(cx, area.rect.height - camY), 90f, fov, 230f);
            var car = SettingsUI.At(SettingsUI.Rect("Car", area), cx - 14f, carY, 28f, 56f);
            Part(car, 9f, 0f, 10f, 56f, SettingsTheme.Hex(0x3A2A55));
            Part(car, 0f, 4f, 28f, 5f, SettingsTheme.Accent);
            Part(car, 2f, 50f, 24f, 5f, SettingsTheme.AccentDeep);
            Part(car, -3f, 12f, 6f, 11f, SettingsTheme.Hex(0x555A63));
            Part(car, 25f, 12f, 6f, 11f, SettingsTheme.Hex(0x555A63));
            Part(car, -3f, 38f, 7f, 13f, SettingsTheme.Hex(0x555A63));
            Part(car, 24f, 38f, 7f, 13f, SettingsTheme.Hex(0x555A63));
            var cam = SettingsUI.Diamond(area, "Camera", SettingsTheme.White, 9f);
            cam.rectTransform.anchorMin = cam.rectTransform.anchorMax = new Vector2(0f, 1f);
            cam.rectTransform.anchoredPosition = new Vector2(cx, -camY);
            Cap(box, chase ? "Chase camera · top view" : "Cockpit · top view", 16f, 208f);
            Data(box, fov.ToString("F0") + "°", 200f, 206f, Inner - 216f);
            return 236f;
        }

        static void Part(RectTransform parent, float x, float y, float w, float h, Color c)
        {
            var img = SettingsUI.Fill(parent, "Part", c);
            SettingsUI.At(img.rectTransform, x, y, w, h);
        }

        float PreviewShake()
        {
            var s = AccertoneSettings.Instance;
            var box = Box(0f, 170f);
            var area = SettingsUI.At(SettingsUI.Rect("Wave", box), 16f, 16f, Inner - 32f, 120f);
            var mid = SettingsUI.Fill(area, "Axis", SettingsTheme.Hairline);
            SettingsUI.At(mid.rectTransform, 0f, 60f, Inner - 32f, 1f);
            var line = area.gameObject.AddComponent<UIPolyline>();
            line.thickness = 2f; line.color = SettingsTheme.Accent; line.raycastTarget = false;
            float amp = (float)s.Store.Get(SettingKeys.CameraShake) / 100f;
            var pts = new List<Vector2>();
            for (int i = 0; i <= 80; i++) pts.Add(new Vector2(i / 80f, 0.5f + Mathf.Sin(i * 0.9f) * Mathf.Sin(i * 0.13f) * 0.42f * amp));
            SettingsUI.SetSharp(line, pts.ToArray());
            Cap(box, "Shake amplitude", 16f, 144f);
            Data(box, (amp * 100f).ToString("F0") + "%", 200f, 142f, Inner - 216f);
            return 170f;
        }

        float PreviewHud()
        {
            var s = AccertoneSettings.Instance;
            var st = s.Store;
            var box = Box(0f, 196f);
            box.gameObject.AddComponent<RectMask2D>();
            if (st.Get(SettingKeys.Hud) < 0.5)
            {
                Cap(box, "HUD hidden", 0f, 90f, Inner, TextAlignmentOptions.Center);
            }
            else
            {
                var hud = SettingsUI.At(SettingsUI.Rect("Hud", box), 0f, 0f, Inner, 196f);
                hud.pivot = new Vector2(0.5f, 0.5f);
                hud.anchoredPosition = new Vector2(Inner * 0.5f, -98f);
                float k = (float)st.Get(SettingKeys.HudScale) / 100f;
                hud.localScale = new Vector3(k, k, 1f);
                bool mph = st.Get(SettingKeys.SpeedUnits) >= 0.5;
                int speedo = (int)st.Get(SettingKeys.Speedometer);
                if (st.Get(SettingKeys.PositionAndLap) >= 0.5)
                {
                    var pos = SettingsUI.Text(hud, "Position", "P4<size=11><color=#A9AFB9> /20</color></size>", SettingsTheme.Display, 22f, SettingsTheme.White);
                    SettingsUI.At(pos.rectTransform, 14f, 12f, 120f, 24f);
                    Cap(hud, "Lap 7 / 15", 14f, 38f, 120f);
                }
                if (st.Get(SettingKeys.TrackMap) >= 0.5)
                {
                    var map = SettingsUI.At(SettingsUI.Rect("Map", hud), Inner - 68f, 12f, 54f, 40f);
                    var ml = map.gameObject.AddComponent<UIPolyline>();
                    ml.thickness = 1.5f; ml.color = SettingsTheme.Silver; ml.raycastTarget = false;
                    ml.SetPoints(new[] { new Vector2(0.1f, 0.3f), new Vector2(0.3f, 0.9f), new Vector2(0.55f, 0.7f), new Vector2(0.9f, 0.85f), new Vector2(0.85f, 0.2f), new Vector2(0.45f, 0.15f) }, true);
                    var dot = SettingsUI.Diamond(map, "Car", SettingsTheme.Accent, 6f);
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.55f, 0.7f);
                    dot.rectTransform.anchoredPosition = Vector2.zero;
                }
                if (speedo > 0)
                {
                    var dial = SettingsUI.At(SettingsUI.Rect("Dial", hud), Inner - 114f, 120f, 104f, 66f);
                    var bgArc = dial.gameObject.AddComponent<UIPolyline>();
                    bgArc.thickness = 5f; bgArc.color = SettingsTheme.Accent; bgArc.raycastTarget = false;
                    var arc = new List<Vector2>();
                    for (int i = 0; i <= 20; i++) { float a = Mathf.Lerp(180f, 60f, i / 20f) * Mathf.Deg2Rad; arc.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.46f, 0.05f + Mathf.Sin(a) * 0.9f)); }
                    SettingsUI.SetSharp(bgArc, arc.ToArray());
                }
                if (speedo != 1)
                {
                    var spd = SettingsUI.Text(hud, "Speed", mph ? "178" : "287", SettingsTheme.Light, 34f, SettingsTheme.White, 0f, TextAlignmentOptions.BottomRight);
                    SettingsUI.At(spd.rectTransform, Inner - 136f, 136f, 120f, 36f);
                }
                Cap(hud, (mph ? "MPH" : "KM/H") + " · 7TH", Inner - 136f, 172f, 120f, TextAlignmentOptions.MidlineRight);
                if (st.Get(SettingKeys.Telemetry) >= 0.5)
                {
                    var t1 = SettingsUI.Fill(hud, "Throttle", SettingsTheme.Positive); SettingsUI.At(t1.rectTransform, 14f, 142f, 7f, 38f);
                    var t2 = SettingsUI.Fill(hud, "Brake", SettingsTheme.Danger); SettingsUI.At(t2.rectTransform, 25f, 174f, 7f, 6f);
                }
                if (st.Get(SettingKeys.Subtitles) >= 0.5)
                {
                    float[] sizes = { 11f, 13f, 16f };
                    var sub = SettingsUI.Text(hud, "Subtitle", "<mark=#000000AA padding=\"8,8,3,3\">Box this lap, box box.</mark>", SettingsTheme.Body, sizes[Mathf.Clamp((int)st.Get(SettingKeys.SubtitleSize), 0, 2)], SettingsTheme.White, 0f, TextAlignmentOptions.Center);
                    SettingsUI.At(sub.rectTransform, 40f, 104f, Inner - 80f, 24f);
                }
            }
            bool imperial = st.Get(SettingKeys.SpeedUnits) >= 0.5;
            return Note(206f, (imperial ? "Lobby track length shows 3.242 mi." : "Lobby track length shows 5.218 km.") + " Notifications are " + (st.Get(SettingKeys.Notifications) >= 0.5 ? "on." : "off."));
        }

        // ------------------------------------------------------------------ live update
        void Update()
        {
            if (m_Def == null) return;
            var s = AccertoneSettings.Instance;
            if (s == null) return;
            if (m_BusLive.Count > 0)
            {
                for (int i = 0; i < m_BusLive.Count; i++)
                {
                    string ch = AudioSettingsSystem.Channels[i];
                    float level = s.Audio.Player != null ? s.Audio.Player.Level(ch) : 0f;
                    var live = m_BusLive[i].rectTransform;
                    live.sizeDelta = new Vector2(0f, Mathf.Lerp(live.sizeDelta.y, level * 150f, SettingsTween.Approach(18f)));
                    float set = i == 0 ? s.Audio.ChannelLevel("Master") : s.Audio.ChannelLevel(ch);
                    m_BusSet[i].anchoredPosition = new Vector2(0f, set * 150f);
                }
            }
            if (m_SteerOut != null) { UpdateCurve(); UpdateTelemetry(); }
        }
    }

    /// <summary>A filled field-of-view wedge (apex, direction and opening angle in degrees).</summary>
    public class UIWedge : MaskableGraphic
    {
        Vector2 m_Apex;
        float m_Direction = 90f, m_Angle = 60f, m_Length = 200f;

        public void Set(Vector2 apexFromBottomLeft, float direction, float angle, float length)
        {
            m_Apex = apexFromBottomLeft; m_Direction = direction; m_Angle = angle; m_Length = length;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 apex = new Vector2(r.xMin, r.yMin) + m_Apex;
            float a0 = (m_Direction - m_Angle * 0.5f) * Mathf.Deg2Rad, a1 = (m_Direction + m_Angle * 0.5f) * Mathf.Deg2Rad;
            var v = UIVertex.simpleVert;
            v.color = color;
            v.position = apex; vh.AddVert(v);
            const int steps = 16;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps);
                v.position = apex + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m_Length;
                vh.AddVert(v);
            }
            for (int i = 1; i <= steps; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
