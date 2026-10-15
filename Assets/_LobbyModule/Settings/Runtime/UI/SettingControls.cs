using System.Collections.Generic;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>Base of every setting control: built from a <see cref="SettingDefinition"/>, refreshed from the store.</summary>
    public abstract class SettingControl : MonoBehaviour
    {
        protected SettingsScreen m_Screen;
        protected SettingsRow m_Row;
        [System.NonSerialized] protected SettingDefinition m_Def;
        protected RectTransform m_Rect;
        protected bool m_Focused;

        public SettingDefinition Definition { get { return m_Def; } }
        protected AccertoneSettings S { get { return AccertoneSettings.Instance; } }
        protected double Value { get { return S.Store.Get(m_Def.key); } }

        public void Bind(SettingsScreen screen, SettingsRow row, SettingDefinition def, float width)
        {
            m_Screen = screen; m_Row = row; m_Def = def;
            m_Rect = (RectTransform)transform;
            Build(width);
            Refresh();
        }

        protected abstract void Build(float width);
        public abstract void Refresh();
        public virtual void Step(int direction, bool big) { }
        public virtual void Submit() { }
        public virtual void Clear() { }
        public virtual void SetFocused(bool focused) { m_Focused = focused; Refresh(); }

        protected void FocusRow() { if (m_Row != null) m_Screen.FocusRow(m_Row); }

        /// <summary>A bordered field background whose border turns violet when the row is focused.</summary>
        protected AccertoneShape[] Field(Transform parent, float x, float w, float h, out RectTransform rt)
        {
            rt = SettingsUI.At(SettingsUI.Rect("Field", parent), x, 0f, w, h);
            var fill = SettingsUI.Shape(rt, "Fill", SettingsTheme.Field, 0f, ChamferCorners.None, 0f, false, 1f, true);
            SettingsUI.Stretch(fill.rectTransform);
            var glow = SettingsUI.Shape(rt, "Glow", SettingsTheme.A(SettingsTheme.Accent, 0.22f), 0f, ChamferCorners.None, 0f, true, 3f);
            SettingsUI.Stretch(glow.rectTransform, -3f, -3f, -3f, -3f);
            var border = SettingsUI.Shape(rt, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
            SettingsUI.Stretch(border.rectTransform);
            return new[] { fill, border, glow };
        }

        protected void PaintField(AccertoneShape[] f, bool open = false)
        {
            if (f == null) return;
            f[1].color = m_Focused || open ? SettingsTheme.A(SettingsTheme.Accent, 0.8f) : SettingsTheme.Hairline;
            f[2].enabled = m_Focused;
        }
    }

    // ===================================================================== Selector
    public class SelectorControl : SettingControl
    {
        AccertoneShape[] m_Field;
        TextMeshProUGUI m_Value;
        readonly List<Image> m_Pips = new List<Image>();
        Graphic m_PrevIcon, m_NextIcon;
        Image m_PrevHover, m_NextHover;
        RectTransform m_PipRow;
        int m_PipCount = -1;

        protected override void Build(float width)
        {
            RectTransform f;
            m_Field = Field(transform, 0f, width, 42f, out f);
            m_PrevHover = Arrow(f, 0f, -1, out m_PrevIcon, () => Step(-1, false));
            m_NextHover = Arrow(f, width - 42f, 1, out m_NextIcon, () => Step(1, false));
            m_Value = SettingsUI.Text(f, "Value", "", SettingsTheme.Label, 14f, SettingsTheme.White, 8f, TextAlignmentOptions.Center, true);
            SettingsUI.At(m_Value.rectTransform, 42f, 6f, width - 84f, 22f);
            m_PipRow = SettingsUI.At(SettingsUI.Rect("Pips", f), 42f, 30f, width - 84f, 2f);
        }

        Image Arrow(RectTransform parent, float x, int dir, out Graphic icon, System.Action act)
        {
            var hit = SettingsUI.Fill(parent, dir < 0 ? "Prev" : "Next", new Color(0, 0, 0, 0), true);
            SettingsUI.At(hit.rectTransform, x, 0f, 42f, 42f);
            var hover = SettingsUI.Fill(hit.transform, "Hover", SettingsTheme.A(SettingsTheme.Hairline, 0.6f));
            SettingsUI.Stretch(hover.rectTransform);
            hover.enabled = false;
            var chev = SettingsUI.Chevron(hit.transform, "Chevron", dir, SettingsTheme.Metallic);
            chev.rectTransform.anchoredPosition = Vector2.zero;
            icon = chev;
            var p = hit.gameObject.AddComponent<SettingsPointer>();
            p.onEnter = () => { hover.enabled = true; chev.color = SettingsTheme.White; };
            p.onExit = () => { hover.enabled = false; Refresh(); };
            p.onDown = e => FocusRow();
            p.onClick = () => act();
            return hover;
        }

        IList<SettingOption> Options { get { return S.GetOptions(m_Def); } }

        public override void Refresh()
        {
            var opts = Options;
            int idx = S.OptionIndex(m_Def, Value);
            m_Value.text = opts.Count > 0 ? opts[idx].label : "";
            if (m_PipCount != opts.Count) RebuildPips(opts.Count);
            for (int i = 0; i < m_Pips.Count; i++) m_Pips[i].color = (m_Def.ordinal ? i <= idx : i == idx) ? SettingsTheme.Accent : SettingsTheme.Hairline;
            m_PrevIcon.color = idx > 0 ? SettingsTheme.Metallic : SettingsTheme.A(SettingsTheme.Metallic, 0.3f);
            m_NextIcon.color = idx < opts.Count - 1 ? SettingsTheme.Metallic : SettingsTheme.A(SettingsTheme.Metallic, 0.3f);
            PaintField(m_Field);
        }

        void RebuildPips(int n)
        {
            foreach (var p in m_Pips) Destroy(p.gameObject);
            m_Pips.Clear();
            m_PipCount = n;
            if (n > 12) return;
            float total = n * 14f + (n - 1) * 3f;
            float x0 = (m_PipRow.rect.width - total) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var pip = SettingsUI.Fill(m_PipRow, "Pip", SettingsTheme.Hairline);
                SettingsUI.At(pip.rectTransform, x0 + i * 17f, 0f, 14f, 2f);
                m_Pips.Add(pip);
            }
        }

        public override void Step(int direction, bool big)
        {
            var opts = Options;
            if (opts.Count == 0) return;
            int idx = Mathf.Clamp(S.OptionIndex(m_Def, Value) + direction, 0, opts.Count - 1);
            m_Screen.ChangeValue(m_Def.key, opts[idx].value);
        }

        public override void Submit()
        {
            var opts = Options;
            if (opts.Count == 0) return;
            int idx = (S.OptionIndex(m_Def, Value) + 1) % opts.Count;
            m_Screen.ChangeValue(m_Def.key, opts[idx].value);
        }
    }

    // ===================================================================== Slider
    public class SliderControl : SettingControl
    {
        RectTransform m_Track;
        RectTransform m_FillRt, m_Thumb;
        Image m_Fill, m_ThumbImg, m_ThumbGlow;
        TextMeshProUGUI m_Chip;
        AccertoneShape[] m_ChipField;
        Graphic[] m_MuteIcon;
        Image m_MuteSlash;
        bool m_Dragging;

        protected override void Build(float width)
        {
            float x = 0f;
            if (!string.IsNullOrEmpty(m_Def.muteKey))
            {
                var mute = SettingsUI.Fill(transform, "Mute", new Color(0, 0, 0, 0), true);
                SettingsUI.At(mute.rectTransform, 0f, 5f, 32f, 32f);
                m_MuteIcon = SpeakerIcon(mute.rectTransform);
                var p = mute.gameObject.AddComponent<SettingsPointer>();
                p.onDown = e => FocusRow();
                p.onClick = () => m_Screen.ChangeValue(m_Def.muteKey, S.Store.Get(m_Def.muteKey) >= 0.5 ? 0 : 1);
                p.onEnter = () => { foreach (var g in m_MuteIcon) g.color = SettingsTheme.White; };
                p.onExit = Refresh;
                x = 48f;
            }
            float chipW = 78f;
            float trackW = width - x - chipW - 16f;
            var hit = SettingsUI.Fill(transform, "Track", new Color(0, 0, 0, 0), true);
            m_Track = SettingsUI.At(hit.rectTransform, x, 6f, trackW, 30f);
            var rail = SettingsUI.Fill(m_Track, "Rail", SettingsTheme.Hairline);
            SettingsUI.At(rail.rectTransform, 0f, 13f, trackW, 4f);
            foreach (var k in new[] { 0.25f, 0.5f, 0.75f })
            {
                var tick = SettingsUI.Fill(m_Track, "Tick", SettingsTheme.A(SettingsTheme.Metallic, 0.28f));
                SettingsUI.At(tick.rectTransform, trackW * k, 9f, 1f, 12f);
            }
            m_Fill = SettingsUI.Fill(m_Track, "Fill", SettingsTheme.Accent);
            m_FillRt = SettingsUI.At(m_Fill.rectTransform, 0f, 13f, 0f, 4f);
            m_ThumbGlow = SettingsUI.Fill(m_Track, "ThumbGlow", SettingsTheme.A(SettingsTheme.Accent, 0.35f));
            m_ThumbImg = SettingsUI.Fill(m_Track, "Thumb", SettingsTheme.White);
            m_Thumb = SettingsUI.At(m_ThumbImg.rectTransform, 0f, 4f, 6f, 22f);
            m_Thumb.pivot = new Vector2(0.5f, 1f);
            SettingsUI.At(m_ThumbGlow.rectTransform, 0f, 1f, 12f, 28f).pivot = new Vector2(0.5f, 1f);

            RectTransform chip;
            m_ChipField = Field(transform, width - chipW, chipW, 34f, out chip);
            chip.anchoredPosition = new Vector2(width - chipW, -4f);
            m_Chip = SettingsUI.Text(chip, "Value", "", SettingsTheme.Data, 15f, SettingsTheme.White, 0f, TextAlignmentOptions.MidlineRight);
            SettingsUI.Stretch(m_Chip.rectTransform, 6f, 0f, 10f, 0f);

            var ptr = hit.gameObject.AddComponent<SettingsPointer>();
            ptr.onDown = e => { FocusRow(); m_Dragging = true; SetFromPointer(e); };
            ptr.onDrag = e => { if (m_Dragging) SetFromPointer(e); };
            ptr.onUp = e => { if (!m_Dragging) return; m_Dragging = false; m_Screen.PlayUi(UiSound.Change); Refresh(); };
        }

        Graphic[] SpeakerIcon(RectTransform parent)
        {
            var body = SettingsUI.Fill(parent, "Body", SettingsTheme.Metallic);
            SettingsUI.At(body.rectTransform, 7f, 13f, 5f, 7f);
            var cone = SettingsUI.Shape(parent, "Cone", SettingsTheme.Metallic, 0f, ChamferCorners.None, 0f);
            SettingsUI.At(cone.rectTransform, 11f, 9f, 6f, 15f);
            cone.corners = ChamferCorners.TopLeft | ChamferCorners.BottomLeft;
            cone.chamfer = 4f;
            var wave = SettingsUI.Rect("Wave", parent);
            SettingsUI.At(wave, 18f, 8f, 9f, 17f);
            var line = wave.gameObject.AddComponent<UIPolyline>();
            line.thickness = 1.6f; line.color = SettingsTheme.Metallic; line.raycastTarget = false;
            SettingsUI.SetSharp(line, new[] { new Vector2(0.1f, 0.15f), new Vector2(0.7f, 0.5f), new Vector2(0.1f, 0.85f) });
            m_MuteSlash = SettingsUI.Fill(parent, "Slash", SettingsTheme.Warning);
            var srt = SettingsUI.At(m_MuteSlash.rectTransform, 15f, 6f, 2f, 22f);
            srt.localRotation = Quaternion.Euler(0f, 0f, -45f);
            return new Graphic[] { body, cone, line };
        }

        void SetFromPointer(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Track, e.position, e.pressEventCamera, out local)) return;
            float k = Mathf.Clamp01((local.x - m_Track.rect.xMin) / m_Track.rect.width);
            double v = m_Def.min + k * (m_Def.max - m_Def.min);
            m_Screen.ChangeValue(m_Def.key, v, false);
        }

        public override void Refresh()
        {
            double v = Value;
            float k = (float)((v - m_Def.min) / (m_Def.max - m_Def.min));
            float w = m_Track.rect.width;
            m_FillRt.sizeDelta = new Vector2(w * k, 4f);
            m_Thumb.anchoredPosition = new Vector2(w * k, -4f);
            ((RectTransform)m_ThumbGlow.transform).anchoredPosition = new Vector2(w * k, -1f);
            m_ThumbGlow.enabled = m_Focused || m_Dragging;
            string num = v.ToString("F" + m_Def.decimals, System.Globalization.CultureInfo.InvariantCulture);
            m_Chip.text = string.IsNullOrEmpty(m_Def.unit) ? num : num + "<size=11><color=#6B717C> " + m_Def.unit + "</color></size>";
            PaintField(m_ChipField);
            if (m_MuteIcon != null)
            {
                bool muted = S.Store.Get(m_Def.muteKey) >= 0.5;
                foreach (var g in m_MuteIcon) g.color = muted ? SettingsTheme.Warning : SettingsTheme.Metallic;
                m_MuteSlash.enabled = muted;
                m_Fill.color = muted ? SettingsTheme.A(SettingsTheme.Metallic, 0.5f) : SettingsTheme.Accent;
            }
        }

        public override void Step(int direction, bool big)
        {
            m_Screen.ChangeValue(m_Def.key, Value + direction * m_Def.step * (big ? 5 : 1));
        }

        public override void Submit()
        {
            if (!string.IsNullOrEmpty(m_Def.audioChannel)) m_Screen.PlayTestSound(m_Def.audioChannel);
        }

        public override void Clear()
        {
            if (!string.IsNullOrEmpty(m_Def.muteKey)) m_Screen.ChangeValue(m_Def.muteKey, S.Store.Get(m_Def.muteKey) >= 0.5 ? 0 : 1);
        }
    }

    // ===================================================================== Toggle
    public class ToggleControl : SettingControl
    {
        TextMeshProUGUI m_Text;
        Image m_Track, m_Knob;
        AccertoneShape m_Glow;
        float m_KnobX = -1f;

        protected override void Build(float width)
        {
            var hit = SettingsUI.Fill(transform, "Hit", new Color(0, 0, 0, 0), true);
            SettingsUI.At(hit.rectTransform, width - 110f, 0f, 110f, 42f);
            m_Text = SettingsUI.Text(hit.transform, "State", "OFF", SettingsTheme.Label, 13f, SettingsTheme.Muted, 12f, TextAlignmentOptions.MidlineRight);
            SettingsUI.At(m_Text.rectTransform, 0f, 0f, 44f, 42f);
            m_Glow = SettingsUI.Shape(hit.transform, "Glow", SettingsTheme.A(SettingsTheme.Accent, 0.3f), 0f, ChamferCorners.None, 0f, true, 2f);
            SettingsUI.At(m_Glow.rectTransform, 56f, 6f, 56f, 30f);
            m_Track = SettingsUI.Fill(hit.transform, "Track", SettingsTheme.Hairline);
            SettingsUI.At(m_Track.rectTransform, 58f, 8f, 52f, 26f);
            m_Knob = SettingsUI.Fill(m_Track.transform, "Knob", SettingsTheme.Metallic);
            SettingsUI.At(m_Knob.rectTransform, 3f, 3f, 20f, 20f);
            var p = hit.gameObject.AddComponent<SettingsPointer>();
            p.onDown = e => FocusRow();
            p.onClick = Submit;
        }

        public override void Refresh()
        {
            bool on = Value >= 0.5;
            m_Text.text = on ? "ON" : "OFF";
            m_Text.color = on ? SettingsTheme.White : SettingsTheme.Muted;
            m_Track.color = on ? SettingsTheme.Accent : SettingsTheme.Hairline;
            m_Knob.color = on ? SettingsTheme.White : SettingsTheme.Metallic;
            m_Glow.enabled = m_Focused;
            float target = on ? 29f : 3f;
            if (m_KnobX < 0f || !isActiveAndEnabled) { m_KnobX = target; Place(); return; }
            float from = m_KnobX;
            SettingsTween.Run(this, 0.18f, e => { m_KnobX = Mathf.Lerp(from, target, e); Place(); });
        }

        void Place() { m_Knob.rectTransform.anchoredPosition = new Vector2(m_KnobX, -3f); }

        public override void Step(int direction, bool big) { m_Screen.ChangeValue(m_Def.key, direction > 0 ? 1 : 0); }
        public override void Submit() { m_Screen.ChangeValue(m_Def.key, Value >= 0.5 ? 0 : 1); }
    }

    // ===================================================================== Segmented (and device tabs)
    public class SegmentedControl : SettingControl
    {
        AccertoneShape[] m_Field;
        readonly List<Image> m_Cells = new List<Image>();
        readonly List<Image> m_Lines = new List<Image>();
        readonly List<TextMeshProUGUI> m_Labels = new List<TextMeshProUGUI>();
        readonly List<bool> m_Hover = new List<bool>();

        bool IsTabs { get { return m_Def.kind == SettingKind.DeviceTabs; } }

        protected override void Build(float width)
        {
            RectTransform f;
            m_Field = Field(transform, 0f, width, 42f, out f);
            var opts = S.GetOptions(m_Def);
            int n = Mathf.Max(1, opts.Count);
            float cw = width / n;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                var cell = SettingsUI.Fill(f, "Segment", new Color(0, 0, 0, 0), true);
                SettingsUI.At(cell.rectTransform, i * cw, 0f, cw, 42f);
                if (i > 0)
                {
                    var div = SettingsUI.Fill(f, "Divider", SettingsTheme.Hairline);
                    SettingsUI.At(div.rectTransform, i * cw, 0f, 1f, 42f);
                }
                var line = SettingsUI.Fill(cell.transform, "Selected", SettingsTheme.Accent);
                SettingsUI.At(line.rectTransform, 0f, 40f, cw, 2f);
                var t = SettingsUI.Text(cell.transform, "Label", opts[i].label, SettingsTheme.Label, 12.5f, SettingsTheme.Muted, 12f, TextAlignmentOptions.Center, true);
                SettingsUI.Stretch(t.rectTransform);
                var p = cell.gameObject.AddComponent<SettingsPointer>();
                p.onDown = e => FocusRow();
                p.onEnter = () => { m_Hover[index] = true; Refresh(); };
                p.onExit = () => { m_Hover[index] = false; Refresh(); };
                p.onClick = () => Select(index);
                m_Cells.Add(cell); m_Lines.Add(line); m_Labels.Add(t); m_Hover.Add(false);
            }
        }

        int Current { get { return IsTabs ? m_Screen.CurrentPage : S.OptionIndex(m_Def, Value); } }

        void Select(int i)
        {
            if (IsTabs) m_Screen.SetPage(i);
            else m_Screen.ChangeValue(m_Def.key, S.GetOptions(m_Def)[i].value);
        }

        public override void Refresh()
        {
            int cur = Current;
            for (int i = 0; i < m_Cells.Count; i++)
            {
                bool sel = i == cur;
                m_Cells[i].color = sel ? SettingsTheme.A(SettingsTheme.Hex(0x262A33), 0.95f) : m_Hover[i] ? SettingsTheme.A(SettingsTheme.Hairline, 0.35f) : new Color(0, 0, 0, 0);
                m_Lines[i].enabled = sel;
                m_Labels[i].color = sel ? SettingsTheme.White : m_Hover[i] ? SettingsTheme.Text2 : SettingsTheme.Muted;
            }
            PaintField(m_Field);
        }

        public override void Step(int direction, bool big)
        {
            int n = m_Cells.Count;
            int i = Mathf.Clamp(Current + direction, 0, n - 1);
            if (i != Current) Select(i);
        }
    }

    // ===================================================================== Dropdown
    public class DropdownControl : SettingControl
    {
        AccertoneShape[] m_Field;
        TextMeshProUGUI m_Value;
        UIPolyline m_Chevron;
        public RectTransform FieldRect { get; private set; }
        public bool IsOpen { get; set; }

        protected override void Build(float width)
        {
            RectTransform f;
            m_Field = Field(transform, 0f, width, 42f, out f);
            FieldRect = f;
            var hit = f.GetChild(0).GetComponent<Graphic>();
            m_Value = SettingsUI.Text(f, "Value", "", SettingsTheme.Label, 14f, SettingsTheme.White, 8f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(m_Value.rectTransform, 16f, 0f, width - 50f, 42f);
            m_Chevron = SettingsUI.Chevron(f, "Chevron", 2, SettingsTheme.Metallic);
            SettingsUI.At(m_Chevron.rectTransform, width - 30f, 14f, 14f, 14f);
            var p = hit.gameObject.AddComponent<SettingsPointer>();
            p.onDown = e => FocusRow();
            p.onEnter = () => { if (!m_Focused && !IsOpen) m_Field[1].color = SettingsTheme.Metallic; };
            p.onExit = Refresh;
            p.onClick = Submit;
        }

        public override void Refresh()
        {
            var opts = S.GetOptions(m_Def);
            m_Value.text = opts.Count > 0 ? opts[S.OptionIndex(m_Def, Value)].label : "";
            PaintField(m_Field, IsOpen);
            m_Chevron.color = IsOpen ? SettingsTheme.Accent : SettingsTheme.Metallic;
            m_Chevron.rectTransform.localRotation = IsOpen ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
        }

        public override void Step(int direction, bool big)
        {
            var opts = S.GetOptions(m_Def);
            if (opts.Count == 0) return;
            int idx = Mathf.Clamp(S.OptionIndex(m_Def, Value) + direction, 0, opts.Count - 1);
            m_Screen.ChangeValue(m_Def.key, opts[idx].value);
        }

        public override void Submit() { m_Screen.ToggleDropdown(this); }
    }

    // ===================================================================== Key / button binding
    public class BindingControl : SettingControl
    {
        const float ChipW = 160f, ChipH = 38f, Gap = 12f;
        readonly List<AccertoneShape> m_Borders = new List<AccertoneShape>();
        readonly List<AccertoneShape> m_Glows = new List<AccertoneShape>();
        readonly List<TextMeshProUGUI> m_Labels = new List<TextMeshProUGUI>();
        public int Slot { get; private set; }

        protected override void Build(float width)
        {
            for (int s = 0; s < 2; s++)
            {
                int slot = s;
                float x = width - (2 - s) * ChipW - (1 - s) * Gap;
                var chip = SettingsUI.Rect("Chip" + s, transform);
                SettingsUI.At(chip, x, 2f, ChipW, ChipH);
                var fill = SettingsUI.Shape(chip, "Fill", SettingsTheme.Field, 0f, ChamferCorners.None, 0f, false, 1f, true);
                SettingsUI.Stretch(fill.rectTransform);
                var glow = SettingsUI.Shape(chip, "Glow", SettingsTheme.A(SettingsTheme.Accent, 0.25f), 0f, ChamferCorners.None, 0f, true, 3f);
                SettingsUI.Stretch(glow.rectTransform, -3f, -3f, -3f, -3f);
                var border = SettingsUI.Shape(chip, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
                SettingsUI.Stretch(border.rectTransform);
                var t = SettingsUI.Text(chip, "Label", "", SettingsTheme.Label, 14f, SettingsTheme.White, 4f, TextAlignmentOptions.Center, true);
                SettingsUI.Stretch(t.rectTransform);
                var p = fill.gameObject.AddComponent<SettingsPointer>();
                p.onDown = e => { FocusRow(); Slot = slot; m_Screen.RefreshFocusedRow(); };
                p.onEnter = () => { if (!IsListening(slot) && !m_Def.locked) border.color = SettingsTheme.Metallic; };
                p.onExit = Refresh;
                p.onClick = () => { Slot = slot; if (!m_Def.locked) m_Screen.StartRebind(this, slot); };
                m_Borders.Add(border); m_Glows.Add(glow); m_Labels.Add(t);
            }
        }

        bool IsListening(int slot) { return m_Screen.IsRebinding(m_Def, slot); }

        public override void Refresh()
        {
            var input = S.Input;
            bool unbound = !m_Def.locked && input.IsUnbound(m_Def);
            int slots = input.SlotCount(m_Def);
            for (int s = 0; s < m_Labels.Count; s++)
            {
                var t = m_Labels[s]; var b = m_Borders[s];
                bool exists = s < slots;
                string label = exists ? input.SlotLabel(m_Def, s) : "";
                bool listening = IsListening(s);
                m_Glows[s].enabled = listening || (m_Focused && Slot == s && !m_Def.locked);
                t.font = SettingsTheme.Label; t.fontSize = 14f; t.characterSpacing = 4f;
                if (m_Def.locked)
                {
                    t.text = s == 0 ? label + "  <size=10><color=#6B717C>LOCKED</color></size>" : "—";
                    t.color = SettingsTheme.Muted; b.color = SettingsTheme.Hairline;
                }
                else if (listening)
                {
                    t.text = m_Def.bindingGroup == InputSettingsSystem.GamepadGroup ? "PRESS A BUTTON" : "PRESS A KEY";
                    t.fontSize = 11f; t.characterSpacing = 20f;
                    t.color = SettingsTheme.AccentLight; b.color = SettingsTheme.Accent;
                }
                else if (unbound)
                {
                    t.text = s == 0 ? "UNBOUND" : "—";
                    t.color = SettingsTheme.Warning; b.color = SettingsTheme.A(SettingsTheme.Warning, 0.7f);
                }
                else if (string.IsNullOrEmpty(label))
                {
                    t.text = "—"; t.color = SettingsTheme.Muted;
                    b.color = m_Focused && Slot == s ? SettingsTheme.A(SettingsTheme.Accent, 0.8f) : SettingsTheme.Hairline;
                }
                else
                {
                    t.text = label; t.color = SettingsTheme.White;
                    b.color = m_Focused && Slot == s ? SettingsTheme.A(SettingsTheme.Accent, 0.8f) : SettingsTheme.Hairline;
                }
            }
        }

        public override void SetFocused(bool focused) { if (!focused) Slot = 0; base.SetFocused(focused); }

        public override void Step(int direction, bool big)
        {
            int target = direction > 0 ? 1 : 0;
            if (target == Slot) return;
            Slot = target;
            m_Screen.PlayUi(UiSound.Move);
            m_Screen.RefreshFocusedRow();
        }

        public override void Submit() { if (!m_Def.locked) m_Screen.StartRebind(this, Slot); }

        public override void Clear()
        {
            if (m_Def.locked) return;
            S.Input.SetSlot(m_Def, Slot, "");
            m_Screen.PlayUi(UiSound.Change);
        }
    }

    // ===================================================================== Action button
    public class ActionControl : SettingControl
    {
        AccertoneShape[] m_Field;

        protected override void Build(float width)
        {
            RectTransform f;
            float w = 150f;
            m_Field = Field(transform, width - w, w, 40f, out f);
            var t = SettingsUI.Text(f, "Label", m_Def.options != null && m_Def.options.Length > 0 ? m_Def.options[0] : "Run", SettingsTheme.Label, 12f, SettingsTheme.White, 18f, TextAlignmentOptions.Center, true);
            SettingsUI.Stretch(t.rectTransform);
            var p = f.GetChild(0).gameObject.AddComponent<SettingsPointer>();
            p.onDown = e => FocusRow();
            p.onEnter = () => m_Field[1].color = SettingsTheme.Accent;
            p.onExit = Refresh;
            p.onClick = Submit;
        }

        public override void Refresh() { PaintField(m_Field); }
        public override void Submit() { m_Screen.RunAction(m_Def); }
    }
}
