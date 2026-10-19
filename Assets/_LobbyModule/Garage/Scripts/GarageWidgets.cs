using System;
using System.Collections.Generic;
using Accertone.Settings;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Garage
{
    /// <summary>What a control actually does, shown as a badge so nothing pretends to work when it does not.</summary>
    public enum GarageEffect
    {
        Live,        // changes the car you are looking at
        Estimate,    // changes the engineering estimates on screen
        Stored,      // saved into the setup for the vehicle physics; nothing in this build reads it
        Unavailable, // the system it needs does not exist in this build (locked)
        Spec,        // information only
    }

    public static class GarageTheme
    {
        public static Color Ink { get { return SettingsTheme.White; } }
        public static Color Text2 { get { return SettingsTheme.Text2; } }
        public static Color Muted { get { return SettingsTheme.Muted; } }
        public static Color Accent { get { return SettingsTheme.Accent; } }
        public static Color AccentLight { get { return SettingsTheme.AccentLight; } }
        public static Color Hairline { get { return SettingsTheme.Hairline; } }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            float spacing = 0f, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            return SettingsUI.Text(parent, name, text, font, size, color, spacing, align);
        }

        public static string EffectLabel(GarageEffect e)
        {
            switch (e)
            {
                case GarageEffect.Live: return "LIVE";
                case GarageEffect.Estimate: return "EST";
                case GarageEffect.Stored: return "STORED";
                case GarageEffect.Unavailable: return "N/A";
            }
            return "SPEC";
        }

        public static Color EffectColor(GarageEffect e)
        {
            switch (e)
            {
                case GarageEffect.Live: return SettingsTheme.AccentLight;
                case GarageEffect.Estimate: return SettingsTheme.Silver;
                case GarageEffect.Stored: return SettingsTheme.Muted;
                case GarageEffect.Unavailable: return SettingsTheme.Muted;
            }
            return SettingsTheme.Text2;
        }

        /// <summary>A small outlined tag ("LIVE", "EST"...). Returns its width.</summary>
        public static float Badge(RectTransform parent, float x, float y, GarageEffect effect)
        {
            var rt = SettingsUI.Rect("Badge", parent);
            var color = EffectColor(effect);
            var t = Text(rt, "Label", EffectLabel(effect), SettingsTheme.Label, 9f, color, 14f, TextAlignmentOptions.Center);
            float w = Mathf.Ceil(t.GetPreferredValues(t.text, 300f, 16f).x) + 10f;
            SettingsUI.At(rt, x, y, w, 15f);
            SettingsUI.Stretch(t.rectTransform);
            var box = SettingsUI.Shape(rt, "Box", SettingsTheme.A(color, effect == GarageEffect.Live ? 0.75f : 0.45f), 3f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, true, 1f);
            SettingsUI.Stretch(box.rectTransform);
            box.transform.SetAsFirstSibling();
            return w;
        }

        public static void PlaySound(UiSound sound)
        {
            try
            {
                var s = AccertoneSettings.Instance;
                if (s != null && s.Audio != null && s.Audio.Player != null) s.Audio.Player.Play(sound);
            }
            catch (Exception) { }
        }

        public static float Approach(float sharpness)
        {
            return 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
        }
    }

    /// <summary>Base for every Garage control: focus visuals, effect badge and the car part it is about.</summary>
    public abstract class GarageControl : Selectable
    {
        public GarageEffect effect = GarageEffect.Live;
        public GaragePart part = GaragePart.Centre;
        public bool hasShot;
        public GarageShot shot;
        public Action<GarageControl> focused;

        protected RectTransform m_Rect;
        protected TextMeshProUGUI m_Label;
        protected AccertoneShape m_Hover, m_Tick;
        protected float m_Focus, m_FocusTarget;
        protected bool m_Locked;

        public bool IsFocused { get { return m_FocusTarget > 0.5f; } }
        public string LabelText { get { return m_Label != null ? m_Label.text : name; } }

        protected void BuildFrame(string label, float width, float height)
        {
            m_Rect = (RectTransform)transform;
            m_Rect.sizeDelta = new Vector2(width, height);
            transition = Transition.None;
            m_Locked = effect == GarageEffect.Unavailable;

            var hit = SettingsUI.Fill(transform, "Hit", new Color(0f, 0f, 0f, 0f), true);
            SettingsUI.Stretch(hit.rectTransform, -10f, 0f, -10f, 0f);
            targetGraphic = hit;

            m_Hover = SettingsUI.Shape(transform, "Hover", SettingsTheme.A(SettingsTheme.Accent, 0.07f), 6f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
            SettingsUI.Stretch(m_Hover.rectTransform, -12f, -2f, -12f, -2f);
            m_Hover.alphaLeft = 1f;
            m_Hover.alphaRight = 0f;
            m_Hover.canvasRenderer.SetAlpha(0f);

            m_Tick = SettingsUI.Shape(transform, "Tick", SettingsTheme.Accent);
            SettingsUI.At(m_Tick.rectTransform, -12f, 6f, 2f, height - 12f);
            m_Tick.canvasRenderer.SetAlpha(0f);

            m_Label = GarageTheme.Text(transform, "Label", label, SettingsTheme.Label, 11f, m_Locked ? SettingsTheme.Muted : SettingsTheme.Text2, 22f);
            SettingsUI.At(m_Label.rectTransform, 0f, 2f, width * 0.6f, 18f);
            float lw = Mathf.Ceil(m_Label.GetPreferredValues(label, 600f, 18f).x);
            GarageTheme.Badge(m_Rect, lw + 10f, 3.5f, effect);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            m_FocusTarget = 1f;
            if (focused != null) focused(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            m_FocusTarget = 0f;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        protected virtual void Update()
        {
            m_Focus = Mathf.Lerp(m_Focus, m_FocusTarget, GarageTheme.Approach(18f));
            if (m_Hover != null) m_Hover.canvasRenderer.SetAlpha(m_Focus);
            if (m_Tick != null) m_Tick.canvasRenderer.SetAlpha(m_Focus);
            if (m_Label != null && !m_Locked)
                m_Label.canvasRenderer.SetColor(Color.Lerp(Color.white, new Color(1.25f, 1.25f, 1.25f, 1f), m_Focus));
        }

        /// <summary>Re-reads the value from the data without raising change events.</summary>
        public virtual void Refresh() { }
    }

    /// <summary>
    /// A value slider: label, live value, a thin track with the factory value marked, and the change from it.
    /// Drag or click the row, or use left/right (keyboard / d-pad); up/down move between rows.
    /// </summary>
    public class GarageSlider : GarageControl, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public float min, max, step = 1f, baseline;
        public Func<float> getter;
        public Action<float> setter;
        public Func<float, string> format;

        TextMeshProUGUI m_Value, m_Delta;
        RectTransform m_Track, m_Fill, m_Handle, m_Base;
        float m_Shown, m_TrackWidth;

        public static GarageSlider Create(RectTransform parent, string label, float width, float min, float max, float step, float baseline,
            Func<float> getter, Action<float> setter, Func<float, string> format, GarageEffect effect, GaragePart part)
        {
            var rt = SettingsUI.Rect("Slider_" + label, parent);
            var s = rt.gameObject.AddComponent<GarageSlider>();
            s.min = min; s.max = max; s.step = step; s.baseline = baseline;
            s.getter = getter; s.setter = setter; s.format = format; s.effect = effect; s.part = part;
            s.Build(label, width);
            return s;
        }

        void Build(string label, float width)
        {
            BuildFrame(label, width, 46f);
            bool dim = effect == GarageEffect.Stored || m_Locked;
            m_Value = GarageTheme.Text(transform, "Value", "", SettingsTheme.Data, 15f, m_Locked ? SettingsTheme.Muted : SettingsTheme.White, 0f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(m_Value.rectTransform, 0f, 0f, 150f, 22f);
            m_Delta = GarageTheme.Text(transform, "Delta", "", SettingsTheme.Data, 10f, SettingsTheme.Muted, 0f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(m_Delta.rectTransform, 0f, 30f, 80f, 14f);

            m_TrackWidth = width - 90f;
            m_Track = SettingsUI.Shape(transform, "Track", SettingsTheme.A(SettingsTheme.Metallic, 0.28f)).rectTransform;
            SettingsUI.At(m_Track, 0f, 36f, m_TrackWidth, 2f);
            var fill = SettingsUI.Shape(transform, "Fill", dim ? SettingsTheme.A(SettingsTheme.Metallic, 0.7f) : SettingsTheme.Accent);
            m_Fill = fill.rectTransform;
            SettingsUI.At(m_Fill, 0f, 36f, 0f, 2f);
            m_Base = SettingsUI.Shape(transform, "Factory", SettingsTheme.A(SettingsTheme.White, 0.55f)).rectTransform;
            SettingsUI.At(m_Base, 0f, 32f, 1f, 10f);
            var handle = SettingsUI.Shape(transform, "Handle", dim ? SettingsTheme.Silver : SettingsTheme.White, 2f, ChamferCorners.All);
            m_Handle = handle.rectTransform;
            SettingsUI.At(m_Handle, 0f, 30f, 3f, 14f);
            if (m_Locked) { fill.color = SettingsTheme.A(SettingsTheme.Muted, 0.4f); handle.color = SettingsTheme.Muted; }
            m_Shown = Value01(Current());
            Refresh();
            PlaceBase();
        }

        float Current() { return getter != null ? getter() : min; }
        float Value01(float v) { return Mathf.Approximately(max, min) ? 0f : Mathf.Clamp01((v - min) / (max - min)); }

        void PlaceBase()
        {
            m_Base.anchoredPosition = new Vector2(Value01(baseline) * m_TrackWidth, -32f);
        }

        public override void Refresh()
        {
            float v = Current();
            m_Value.text = format != null ? format(v) : v.ToString("0");
            float d = v - baseline;
            if (Mathf.Abs(d) < step * 0.5f) m_Delta.text = "FACTORY";
            else m_Delta.text = (d > 0f ? "+" : "") + (step < 1f ? d.ToString(step < 0.1f ? "0.00" : "0.0") : d.ToString("0"));
        }

        void SetValue(float v, bool sound)
        {
            if (m_Locked || setter == null) return;
            v = Mathf.Clamp(Mathf.Round((v - min) / step) * step + min, min, max);
            if (Mathf.Abs(v - Current()) < step * 0.25f) return;
            setter(v);
            Refresh();
            if (sound) GarageTheme.PlaySound(UiSound.Change);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                SetValue(Current() + (eventData.moveDir == MoveDirection.Right ? step : -step), true);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = false; }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left) Scrub(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Scrub(eventData);
        }

        void Scrub(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Rect, e.position, e.pressEventCamera, out local)) return;
            float x = local.x + m_Rect.pivot.x * m_Rect.rect.width;
            SetValue(Mathf.Lerp(min, max, Mathf.Clamp01(x / m_TrackWidth)), false);
        }

        protected override void Update()
        {
            base.Update();
            float target = Value01(Current());
            m_Shown = Mathf.Lerp(m_Shown, target, GarageTheme.Approach(22f));
            float x = m_Shown * m_TrackWidth;
            m_Fill.sizeDelta = new Vector2(x, 2f);
            m_Handle.anchoredPosition = new Vector2(x - 1.5f, -30f);
            m_Handle.localScale = new Vector3(1f, 1f + m_Focus * 0.35f, 1f);
        }
    }

    /// <summary>
    /// A row of choices (text chips or colour swatches). Left/right step through them; click picks one.
    /// </summary>
    public class GarageChoice : GarageControl
    {
        public Func<int> getter;
        public Action<int> setter;
        string[] m_Options;
        Color[] m_ColorsA, m_ColorsB;
        readonly List<RectTransform> m_Chips = new List<RectTransform>();
        readonly List<TextMeshProUGUI> m_ChipText = new List<TextMeshProUGUI>();
        readonly List<Graphic> m_ChipFrames = new List<Graphic>();
        RectTransform m_Marker;
        Graphic m_MarkerGraphic;
        TextMeshProUGUI m_Value;
        /// <summary>Shown as the value when nothing matches (getter returns -1).</summary>
        public string noneLabel = "CUSTOM";
        float m_MarkerX, m_MarkerW;
        bool m_Swatches;
        int m_Columns;
        float m_ChipW, m_ChipH, m_Gap;

        public int Count { get { return m_Options.Length; } }

        public static GarageChoice Create(RectTransform parent, string label, float width, string[] options, Func<int> getter, Action<int> setter,
            GarageEffect effect, GaragePart part, Color[] dots = null)
        {
            var rt = SettingsUI.Rect("Choice_" + label, parent);
            var c = rt.gameObject.AddComponent<GarageChoice>();
            c.getter = getter; c.setter = setter; c.effect = effect; c.part = part;
            c.m_Options = options;
            c.m_ColorsA = dots;
            c.BuildChips(label, width, false, 0);
            return c;
        }

        /// <summary>Colour swatches; colorsB gives a second stop (accent ramps). Names show for the selected swatch.</summary>
        public static GarageChoice CreateSwatches(RectTransform parent, string label, float width, string[] names, Color[] colorsA, Color[] colorsB,
            Func<int> getter, Action<int> setter, GarageEffect effect, GaragePart part)
        {
            var rt = SettingsUI.Rect("Swatches_" + label, parent);
            var c = rt.gameObject.AddComponent<GarageChoice>();
            c.getter = getter; c.setter = setter; c.effect = effect; c.part = part;
            c.m_Options = names;
            c.m_ColorsA = colorsA;
            c.m_ColorsB = colorsB;
            c.BuildChips(label, width, true, 0);
            return c;
        }

        /// <summary>Tiles in a grid (livery schemes, presets).</summary>
        public static GarageChoice CreateTiles(RectTransform parent, string label, float width, int columns, string[] names, Color[] colorsA, Color[] colorsB,
            Func<int> getter, Action<int> setter, GarageEffect effect, GaragePart part)
        {
            var rt = SettingsUI.Rect("Tiles_" + label, parent);
            var c = rt.gameObject.AddComponent<GarageChoice>();
            c.getter = getter; c.setter = setter; c.effect = effect; c.part = part;
            c.m_Options = names;
            c.m_ColorsA = colorsA;
            c.m_ColorsB = colorsB;
            c.BuildChips(label, width, false, columns);
            return c;
        }

        void BuildChips(string label, float width, bool swatches, int columns)
        {
            m_Swatches = swatches;
            m_Columns = columns;
            int n = m_Options.Length;
            if (swatches)
            {
                m_Gap = 8f;
                m_ChipW = m_ChipH = Mathf.Min(34f, (width - m_Gap * (n - 1)) / n);
            }
            else if (columns > 0)
            {
                m_Gap = 8f;
                m_ChipW = (width - m_Gap * (columns - 1)) / columns;
                m_ChipH = 46f;
            }
            else
            {
                m_Gap = 6f;
                m_ChipW = (width - m_Gap * (n - 1)) / n;
                m_ChipH = 30f;
            }
            int rows = columns > 0 ? Mathf.CeilToInt(n / (float)columns) : 1;
            float height = 26f + rows * m_ChipH + (rows - 1) * m_Gap + (swatches ? 4f : 0f);
            BuildFrame(label, width, height);

            m_Value = GarageTheme.Text(transform, "Value", "", SettingsTheme.Data, 12f, SettingsTheme.White, 0f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(m_Value.rectTransform, 0f, 0f, 260f, 20f);

            for (int i = 0; i < n; i++)
            {
                int index = i;
                int col = columns > 0 ? i % columns : i;
                int row = columns > 0 ? i / columns : 0;
                var chip = SettingsUI.Rect("Chip_" + i, transform);
                SettingsUI.At(chip, col * (m_ChipW + m_Gap), 26f + row * (m_ChipH + m_Gap), m_ChipW, m_ChipH);
                var hit = SettingsUI.Fill(chip, "Hit", new Color(0f, 0f, 0f, 0f), true);
                SettingsUI.Stretch(hit.rectTransform);
                var trigger = chip.gameObject.AddComponent<GarageClick>();
                trigger.onClick = () => { if (!m_Locked) Pick(index, true); };
                trigger.onEnter = () => { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject); };

                if (swatches)
                {
                    var fillA = SettingsUI.Shape(chip, "A", m_ColorsA[i], 5f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
                    SettingsUI.Stretch(fillA.rectTransform, 3f, 3f, 3f, 3f);
                    if (m_ColorsB != null && m_ColorsB[i] != m_ColorsA[i])
                    {
                        var g = fillA.gameObject.AddComponent<PlayGradientHelper>();
                        g.Set(m_ColorsA[i], m_ColorsB[i]);
                    }
                    var frame = SettingsUI.Shape(chip, "Frame", SettingsTheme.A(SettingsTheme.Metallic, 0.35f), 6f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, true, 1f);
                    SettingsUI.Stretch(frame.rectTransform);
                    m_ChipFrames.Add(frame);
                    m_ChipText.Add(null);
                }
                else
                {
                    var bg = SettingsUI.Shape(chip, "Bg", SettingsTheme.A(SettingsTheme.Carbon, 0.65f), 5f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
                    SettingsUI.Stretch(bg.rectTransform);
                    var frame = SettingsUI.Shape(chip, "Frame", SettingsTheme.A(SettingsTheme.Metallic, 0.3f), 5f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, true, 1f);
                    SettingsUI.Stretch(frame.rectTransform);
                    m_ChipFrames.Add(frame);
                    float textLeft = 10f;
                    if (m_ColorsA != null && i < m_ColorsA.Length)
                    {
                        if (columns > 0)
                        {
                            // Scheme tile: body block + accent ramp stripe.
                            var body = SettingsUI.Shape(chip, "Body", m_ColorsA[i], 3f, ChamferCorners.BottomLeft);
                            SettingsUI.At(body.rectTransform, 6f, 6f, 26f, m_ChipH - 12f);
                            var stripe = SettingsUI.Shape(chip, "Stripe", m_ColorsB != null ? m_ColorsB[i] : m_ColorsA[i], 0f);
                            SettingsUI.At(stripe.rectTransform, 6f, m_ChipH - 11f, 26f, 5f);
                            textLeft = 40f;
                        }
                        else
                        {
                            var dot = SettingsUI.Diamond(chip, "Dot", m_ColorsA[i], 7f);
                            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                            dot.rectTransform.anchoredPosition = new Vector2(12f, 0f);
                            textLeft = 22f;
                        }
                    }
                    var t = GarageTheme.Text(chip, "Label", m_Options[i], SettingsTheme.Label, columns > 0 ? 10f : 10f, SettingsTheme.Text2, 12f,
                        textLeft > 10f ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
                    SettingsUI.Stretch(t.rectTransform, textLeft, 0f, 6f, 0f);
                    t.enableAutoSizing = true;
                    t.fontSizeMin = 8f;
                    t.fontSizeMax = 10f;
                    m_ChipText.Add(t);
                }
                m_Chips.Add(chip);
            }

            var marker = SettingsUI.Shape(transform, "Marker", SettingsTheme.Accent, 0f);
            m_Marker = marker.rectTransform;
            SettingsUI.At(m_Marker, 0f, 0f, m_ChipW, 2f);
            if (m_Locked) marker.color = SettingsTheme.Muted;
            int cur = Mathf.Clamp(getter != null ? getter() : 0, 0, n - 1);
            m_MarkerX = m_Chips[cur].anchoredPosition.x;
            m_MarkerGraphic = marker;
            m_MarkerW = m_ChipW;
            Refresh();
        }

        public override void Refresh()
        {
            int cur = getter != null ? getter() : 0;
            if (cur >= m_Options.Length) cur = m_Options.Length - 1;
            m_Value.text = cur >= 0 ? m_Options[cur] : noneLabel;
            if (!m_Swatches && m_Columns == 0) m_Value.text = cur >= 0 ? "" : noneLabel;
            for (int i = 0; i < m_Chips.Count; i++)
            {
                bool on = i == cur;
                var frame = m_ChipFrames[i];
                frame.color = on ? (m_Locked ? SettingsTheme.Muted : SettingsTheme.Accent) : SettingsTheme.A(SettingsTheme.Metallic, m_Swatches ? 0.25f : 0.3f);
                if (m_ChipText[i] != null) m_ChipText[i].color = on ? SettingsTheme.White : (m_Locked ? SettingsTheme.Muted : SettingsTheme.Text2);
            }
        }

        void Pick(int index, bool sound)
        {
            if (m_Locked || setter == null) return;
            index = Mathf.Clamp(index, 0, m_Options.Length - 1);
            if (getter != null && getter() == index) return;
            setter(index);
            Refresh();
            if (sound) GarageTheme.PlaySound(UiSound.Change);
        }

        public override void OnMove(AxisEventData eventData)
        {
            int cur = getter != null ? getter() : 0;
            int n = m_Options.Length;
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                int d = eventData.moveDir == MoveDirection.Right ? 1 : -1;
                if (cur < 0) { Pick(d > 0 ? 0 : n - 1, true); eventData.Use(); return; }
                if (m_Columns > 0)
                {
                    int col = cur % m_Columns;
                    if ((d < 0 && col == 0) || (d > 0 && (col == m_Columns - 1 || cur + 1 >= n))) { eventData.Use(); return; }
                }
                Pick((cur + d + n) % n, true);
                eventData.Use();
                return;
            }
            if (m_Columns > 0 && cur >= 0)
            {
                // Inside a grid, up/down move between tile rows before leaving the control.
                int d = eventData.moveDir == MoveDirection.Down ? m_Columns : -m_Columns;
                int next = cur + d;
                if (next >= 0 && next < n) { Pick(next, true); eventData.Use(); return; }
            }
            base.OnMove(eventData);
        }

        protected override void Update()
        {
            base.Update();
            int raw = getter != null ? getter() : 0;
            if (m_MarkerGraphic != null) m_MarkerGraphic.canvasRenderer.SetAlpha(raw >= 0 ? 1f : 0f);
            int cur = Mathf.Clamp(raw, 0, m_Options.Length - 1);
            var chip = m_Chips[cur];
            float k = GarageTheme.Approach(20f);
            m_MarkerX = Mathf.Lerp(m_MarkerX, chip.anchoredPosition.x, k);
            float y = -chip.anchoredPosition.y + m_ChipH + 3f;
            m_Marker.anchoredPosition = new Vector2(m_MarkerX, -y);
            m_Marker.sizeDelta = new Vector2(m_ChipW, 2f);
        }
    }

    /// <summary>Two-stop horizontal gradient on an AccertoneShape-sized rect (used by accent swatches).</summary>
    public class PlayGradientHelper : MonoBehaviour
    {
        public void Set(Color a, Color b)
        {
            var g = new GameObject("Gradient", typeof(RectTransform));
            g.layer = 5;
            g.transform.SetParent(transform, false);
            var rt = (RectTransform)g.transform;
            SettingsUI.Stretch(rt);
            var grad = g.AddComponent<Accertone.Play.PlayGradient>();
            grad.raycastTarget = false;
            grad.axis = Accertone.Play.PlayGradient.Axis.Horizontal;
            grad.SetStopCount(2);
            grad.SetStop(0, 0f, a);
            grad.SetStop(1, 1f, b);
        }
    }

    /// <summary>A command button (save, load, reset, preview DRS...). Optional two-step confirm for destructive actions.</summary>
    public class GarageButton : GarageControl, IPointerClickHandler, ISubmitHandler
    {
        public Action onClick;
        public bool confirm;
        public bool primary;
        TextMeshProUGUI m_Text;
        AccertoneShape m_Fill, m_Frame;
        string m_Caption;
        float m_ArmedUntil = -1f;
        float m_Flash;

        public static GarageButton Create(RectTransform parent, string label, float width, Action onClick, bool primary = false, bool confirm = false,
            GarageEffect effect = GarageEffect.Spec, GarageIconKind? icon = null, float height = 40f)
        {
            var rt = SettingsUI.Rect("Button_" + label, parent);
            var b = rt.gameObject.AddComponent<GarageButton>();
            b.onClick = onClick; b.primary = primary; b.confirm = confirm; b.effect = effect;
            b.Build(label, width, height, icon);
            return b;
        }

        void Build(string label, float width, float height, GarageIconKind? icon)
        {
            m_Rect = (RectTransform)transform;
            m_Rect.sizeDelta = new Vector2(width, height);
            transition = Transition.None;
            m_Caption = label;
            m_Fill = SettingsUI.Shape(transform, "Fill", primary ? SettingsTheme.Accent : SettingsTheme.A(SettingsTheme.Carbon, 0.7f), 7f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, false, 1f, true);
            SettingsUI.Stretch(m_Fill.rectTransform);
            targetGraphic = m_Fill;
            m_Frame = SettingsUI.Shape(transform, "Frame", primary ? SettingsTheme.AccentLight : SettingsTheme.A(SettingsTheme.Metallic, 0.4f), 7f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, true, 1f);
            SettingsUI.Stretch(m_Frame.rectTransform);
            float left = 0f;
            if (icon.HasValue)
            {
                var ir = SettingsUI.Rect("Icon", transform);
                SettingsUI.At(ir, 12f, (height - 16f) * 0.5f, 16f, 16f);
                var ic = ir.gameObject.AddComponent<GarageIcon>();
                ic.kind = icon.Value;
                ic.thickness = 1.4f;
                ic.color = primary ? SettingsTheme.White : SettingsTheme.Silver;
                ic.raycastTarget = false;
                left = 20f;
            }
            m_Text = GarageTheme.Text(transform, "Label", label, SettingsTheme.Label, 11f, SettingsTheme.White, 20f, TextAlignmentOptions.Center);
            SettingsUI.Stretch(m_Text.rectTransform, left, 0f, 0f, 0f);
        }

        public void SetLabel(string label)
        {
            m_Caption = label;
            if (m_ArmedUntil < 0f) m_Text.text = label;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Press();
        }

        public void OnSubmit(BaseEventData eventData) { Press(); }

        void Press()
        {
            if (!IsInteractable()) return;
            if (confirm && m_ArmedUntil < 0f)
            {
                m_ArmedUntil = Time.unscaledTime + 3f;
                m_Text.text = "CONFIRM — PRESS AGAIN";
                GarageTheme.PlaySound(UiSound.Error);
                return;
            }
            m_ArmedUntil = -1f;
            m_Text.text = m_Caption;
            m_Flash = 1f;
            GarageTheme.PlaySound(UiSound.Confirm);
            if (onClick != null) onClick();
        }

        protected override void Update()
        {
            m_Focus = Mathf.Lerp(m_Focus, m_FocusTarget, GarageTheme.Approach(18f));
            m_Flash = Mathf.MoveTowards(m_Flash, 0f, Time.unscaledDeltaTime * 2.5f);
            if (m_ArmedUntil > 0f && Time.unscaledTime > m_ArmedUntil) { m_ArmedUntil = -1f; m_Text.text = m_Caption; }
            bool armed = m_ArmedUntil > 0f;
            Color frame = armed ? SettingsTheme.Warning : (primary ? SettingsTheme.AccentLight : Color.Lerp(SettingsTheme.A(SettingsTheme.Metallic, 0.4f), SettingsTheme.Accent, m_Focus));
            m_Frame.color = frame;
            m_Frame.outlineWidth = 1f + m_Focus * 0.8f;
            Color fill = primary ? Color.Lerp(SettingsTheme.Accent, SettingsTheme.AccentLight, m_Focus * 0.35f + m_Flash * 0.4f)
                                 : Color.Lerp(SettingsTheme.A(SettingsTheme.Carbon, 0.7f), SettingsTheme.A(SettingsTheme.AccentDeep, 0.55f), m_Focus * 0.6f + m_Flash * 0.4f);
            m_Fill.color = fill;
            m_Text.canvasRenderer.SetAlpha(IsInteractable() ? 1f : 0.4f);
        }
    }

    /// <summary>Pointer helper for sub-parts of a control (chips).</summary>
    public class GarageClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public Action onClick, onEnter;
        public void OnPointerClick(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left && onClick != null) onClick(); }
        public void OnPointerEnter(PointerEventData eventData) { if (onEnter != null) onEnter(); }
    }

    /// <summary>A read-only figure: label, value and an optional bar (0..1) with the factory value marked.</summary>
    public class GarageReadout : MonoBehaviour
    {
        TextMeshProUGUI m_Value;
        RectTransform m_Fill, m_Base;
        float m_Width, m_Shown, m_Target;
        bool m_Bar;

        public static GarageReadout Create(RectTransform parent, string label, float width, bool bar, GarageEffect effect = GarageEffect.Estimate)
        {
            var rt = SettingsUI.Rect("Readout_" + label, parent);
            rt.sizeDelta = new Vector2(width, bar ? 34f : 24f);
            var r = rt.gameObject.AddComponent<GarageReadout>();
            r.m_Width = width;
            r.m_Bar = bar;
            var l = GarageTheme.Text(rt, "Label", label, SettingsTheme.Label, 10f, SettingsTheme.Muted, 20f);
            SettingsUI.At(l.rectTransform, 0f, 0f, width * 0.6f, 18f);
            float lw = Mathf.Ceil(l.GetPreferredValues(label, 600f, 18f).x);
            GarageTheme.Badge(rt, lw + 8f, 2f, effect);
            r.m_Value = GarageTheme.Text(rt, "Value", "", SettingsTheme.Data, 14f, SettingsTheme.White, 0f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(r.m_Value.rectTransform, 0f, 0f, 220f, 20f);
            if (bar)
            {
                var track = SettingsUI.Shape(rt, "Track", SettingsTheme.A(SettingsTheme.Metallic, 0.2f));
                SettingsUI.At(track.rectTransform, 0f, 26f, width, 3f);
                var fill = SettingsUI.Shape(rt, "Fill", SettingsTheme.Silver);
                r.m_Fill = fill.rectTransform;
                SettingsUI.At(r.m_Fill, 0f, 26f, 0f, 3f);
                var b = SettingsUI.Shape(rt, "Factory", SettingsTheme.A(SettingsTheme.White, 0.6f));
                r.m_Base = b.rectTransform;
                SettingsUI.At(r.m_Base, 0f, 23f, 1f, 9f);
            }
            return r;
        }

        public void Set(string value, float bar01 = -1f, float factory01 = -1f, Color? color = null)
        {
            if (m_Value.text != value) m_Value.text = value;
            m_Value.color = color ?? SettingsTheme.White;
            if (!m_Bar) return;
            m_Target = Mathf.Clamp01(bar01);
            if (factory01 >= 0f) m_Base.anchoredPosition = new Vector2(Mathf.Clamp01(factory01) * m_Width, -23f);
            m_Base.gameObject.SetActive(factory01 >= 0f);
        }

        void Update()
        {
            if (!m_Bar) return;
            m_Shown = Mathf.Lerp(m_Shown, m_Target, GarageTheme.Approach(10f));
            m_Fill.sizeDelta = new Vector2(m_Shown * m_Width, 3f);
        }
    }

    /// <summary>Front / rear split bar (aero balance, brake bias, mechanical balance).</summary>
    public class GarageBalance : MonoBehaviour
    {
        TextMeshProUGUI m_Front, m_Rear;
        RectTransform m_Split, m_Ideal;
        float m_Width, m_Shown = 0.5f, m_Target = 0.5f;

        public static GarageBalance Create(RectTransform parent, string label, float width, GarageEffect effect = GarageEffect.Estimate)
        {
            var rt = SettingsUI.Rect("Balance_" + label, parent);
            rt.sizeDelta = new Vector2(width, 40f);
            var b = rt.gameObject.AddComponent<GarageBalance>();
            b.m_Width = width;
            var l = GarageTheme.Text(rt, "Label", label, SettingsTheme.Label, 10f, SettingsTheme.Muted, 20f);
            SettingsUI.At(l.rectTransform, 0f, 0f, width, 18f);
            float lw = Mathf.Ceil(l.GetPreferredValues(label, 600f, 18f).x);
            GarageTheme.Badge(rt, lw + 8f, 2f, effect);
            b.m_Front = GarageTheme.Text(rt, "Front", "", SettingsTheme.Data, 12f, SettingsTheme.White);
            SettingsUI.At(b.m_Front.rectTransform, 0f, 30f, 120f, 14f);
            b.m_Rear = GarageTheme.Text(rt, "Rear", "", SettingsTheme.Data, 12f, SettingsTheme.White, 0f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(b.m_Rear.rectTransform, 0f, 30f, 120f, 14f);
            var front = SettingsUI.Shape(rt, "FrontBar", SettingsTheme.A(SettingsTheme.Accent, 0.85f));
            SettingsUI.At(front.rectTransform, 0f, 22f, width, 4f);
            var rear = SettingsUI.Shape(rt, "RearBar", SettingsTheme.A(SettingsTheme.Metallic, 0.45f));
            b.m_Split = rear.rectTransform;
            rear.rectTransform.anchorMin = rear.rectTransform.anchorMax = new Vector2(1f, 1f);
            rear.rectTransform.pivot = new Vector2(1f, 1f);
            rear.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            rear.rectTransform.sizeDelta = new Vector2(width * 0.5f, 4f);
            var ideal = SettingsUI.Shape(rt, "Ideal", SettingsTheme.White);
            b.m_Ideal = ideal.rectTransform;
            SettingsUI.At(b.m_Ideal, width * 0.5f, 18f, 1f, 12f);
            return b;
        }

        /// <param name="front01">Front share 0..1.</param>
        /// <param name="ideal01">Marker for the target / factory split (negative hides it).</param>
        public void Set(float front01, string frontText, string rearText, float ideal01 = -1f)
        {
            m_Target = Mathf.Clamp01(front01);
            m_Front.text = frontText;
            m_Rear.text = rearText;
            m_Ideal.gameObject.SetActive(ideal01 >= 0f);
            if (ideal01 >= 0f) m_Ideal.anchoredPosition = new Vector2(Mathf.Clamp01(ideal01) * m_Width, -18f);
        }

        void Update()
        {
            m_Shown = Mathf.Lerp(m_Shown, m_Target, GarageTheme.Approach(10f));
            m_Split.sizeDelta = new Vector2((1f - m_Shown) * m_Width, 4f);
        }
    }

    /// <summary>Lays rows out top to bottom inside a panel's content area.</summary>
    public class GarageStack
    {
        readonly RectTransform m_Root;
        public float y;
        public readonly float width;
        public readonly List<GarageControl> controls = new List<GarageControl>();
        public readonly List<RectTransform> rows = new List<RectTransform>();

        public GarageStack(RectTransform root, float width)
        {
            m_Root = root;
            this.width = width;
        }

        public RectTransform Root { get { return m_Root; } }

        public T Place<T>(T item, float gapAfter = 12f, float x = 0f) where T : Component
        {
            var rt = (RectTransform)item.transform;
            SettingsUI.At(rt, x, y, rt.sizeDelta.x, rt.sizeDelta.y);
            y += rt.sizeDelta.y + gapAfter;
            rows.Add(rt);
            var c = item as GarageControl;
            if (c != null) controls.Add(c);
            return item;
        }

        /// <summary>Two items side by side (front / rear pairs); returns the taller height.</summary>
        public void PlacePair(Component left, Component right, float gapAfter = 12f)
        {
            var a = (RectTransform)left.transform;
            var b = (RectTransform)right.transform;
            float half = (width - 28f) * 0.5f;
            SettingsUI.At(a, 0f, y, a.sizeDelta.x, a.sizeDelta.y);
            SettingsUI.At(b, half + 28f, y, b.sizeDelta.x, b.sizeDelta.y);
            y += Mathf.Max(a.sizeDelta.y, b.sizeDelta.y) + gapAfter;
            rows.Add(a);
            rows.Add(b);
            var ca = left as GarageControl;
            var cb = right as GarageControl;
            if (ca != null) controls.Add(ca);
            if (cb != null) controls.Add(cb);
        }

        public float Half { get { return (width - 28f) * 0.5f; } }

        public void Section(string title, float gapBefore = 10f)
        {
            y += gapBefore;
            var rt = SettingsUI.Rect("Section_" + title, m_Root);
            SettingsUI.At(rt, 0f, y, width, 18f);
            var tick = SettingsUI.Shape(rt, "Tick", SettingsTheme.Accent);
            SettingsUI.At(tick.rectTransform, 0f, 7f, 10f, 2f);
            var t = GarageTheme.Text(rt, "Title", title, SettingsTheme.Label, 10f, SettingsTheme.Silver, 26f);
            SettingsUI.At(t.rectTransform, 18f, 0f, width - 18f, 16f);
            float tw = Mathf.Ceil(t.GetPreferredValues(title, 800f, 16f).x);
            var line = SettingsUI.Shape(rt, "Rule", SettingsTheme.A(SettingsTheme.Hairline, 0.9f));
            SettingsUI.At(line.rectTransform, 30f + tw, 8f, Mathf.Max(0f, width - 30f - tw), 1f);
            rows.Add(rt);
            y += 26f;
        }

        public TextMeshProUGUI Note(string text, GarageIconKind icon = GarageIconKind.Lock, float gapAfter = 8f)
        {
            var rt = SettingsUI.Rect("Note", m_Root);
            var ir = SettingsUI.Rect("Icon", rt);
            SettingsUI.At(ir, 0f, 2f, 12f, 12f);
            var ic = ir.gameObject.AddComponent<GarageIcon>();
            ic.kind = icon;
            ic.thickness = 1.2f;
            ic.color = SettingsTheme.Muted;
            ic.raycastTarget = false;
            var t = SettingsUI.Paragraph(rt, "Text", text, SettingsTheme.Body, 11.5f, SettingsTheme.Muted, 2f);
            SettingsUI.At(t.rectTransform, 20f, 0f, width - 20f, 40f);
            float h = Mathf.Ceil(t.GetPreferredValues(text, width - 20f, 400f).y);
            t.rectTransform.sizeDelta = new Vector2(width - 20f, h);
            SettingsUI.At(rt, 0f, y, width, h);
            y += h + gapAfter;
            rows.Add(rt);
            return t;
        }

        /// <summary>Links the controls into an explicit up/down chain (left/right stay with each control).</summary>
        public void LinkNavigation(Selectable up = null)
        {
            for (int i = 0; i < controls.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? controls[i - 1] : up;
                nav.selectOnDown = i < controls.Count - 1 ? controls[i + 1] : null;
                controls[i].navigation = nav;
            }
        }
    }
}
