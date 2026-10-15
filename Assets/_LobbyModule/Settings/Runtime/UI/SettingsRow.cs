using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>One setting line: label, optional ON TRACK / ONLINE tag, disabled reason, unsaved marker and its control.</summary>
    public class SettingsRow : MonoBehaviour
    {
        public const float BaseHeight = 56f;
        public const float NoteHeight = 74f;

        SettingsScreen m_Screen;
        RectTransform m_Rect;
        LayoutElement m_Layout;
        Image m_Bg, m_Bar, m_Dirty;
        TextMeshProUGUI m_Label, m_Note;
        RectTransform m_Badge;
        RectTransform m_ControlRoot;
        CanvasGroup m_ControlGroup;
        bool m_Focused, m_Hovered;
        float m_ControlWidth;

        public SettingDefinition Definition { get; private set; }
        public SettingControl Control { get; private set; }
        public bool IsDisabled { get; private set; }
        public bool IsHeader { get; private set; }
        public RectTransform Rect { get { return m_Rect; } }

        public void Build(SettingsScreen screen, SettingDefinition def, float width, float controlWidth, bool header)
        {
            m_Screen = screen;
            Definition = def;
            IsHeader = header;
            m_ControlWidth = controlWidth;
            m_Rect = (RectTransform)transform;
            m_Rect.sizeDelta = new Vector2(width, BaseHeight);
            m_Layout = gameObject.AddComponent<LayoutElement>();
            m_Layout.preferredHeight = BaseHeight;
            m_Layout.preferredWidth = width;

            m_Bg = SettingsUI.Fill(transform, "Background", new Color(0, 0, 0, 0), true);
            SettingsUI.Stretch(m_Bg.rectTransform);
            m_Bar = SettingsUI.Fill(transform, "FocusBar", SettingsTheme.Accent);
            m_Bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            m_Bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            m_Bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_Bar.rectTransform.sizeDelta = new Vector2(2f, 0f);
            m_Bar.rectTransform.anchoredPosition = Vector2.zero;
            m_Bar.enabled = false;

            m_Label = SettingsUI.Text(transform, "Label", def.label, SettingsTheme.Body, 16f, SettingsTheme.White);
            m_Label.rectTransform.anchorMin = m_Label.rectTransform.anchorMax = new Vector2(0f, 1f);
            m_Label.rectTransform.pivot = new Vector2(0f, 0.5f);

            float x = 16f + SettingsUI.PreferredWidth(m_Label);
            if (def.applies == SettingApplies.OnTrack || def.applies == SettingApplies.Online)
            {
                if (def.kind != SettingKind.DeviceTabs && !header || header)
                {
                    bool online = def.applies == SettingApplies.Online;
                    m_Badge = SettingsUI.Rect("Badge", transform);
                    Color c = online ? SettingsTheme.Silver : SettingsTheme.AccentLight;
                    var border = SettingsUI.Shape(m_Badge, "Border", SettingsTheme.A(online ? SettingsTheme.Silver : SettingsTheme.Accent, 0.55f), 0f, Accertone.UI.ChamferCorners.None, 0f, true, 1f);
                    SettingsUI.Stretch(border.rectTransform);
                    var dot = SettingsUI.Fill(m_Badge, "Dot", c);
                    SettingsUI.At(dot.rectTransform, 7f, 6.5f, 5f, 5f);
                    var t = SettingsUI.Text(m_Badge, "Label", online ? "ONLINE" : "ON TRACK", SettingsTheme.Label, 10f, c, 20f);
                    SettingsUI.At(t.rectTransform, 17f, 0f, 100f, 18f);
                    float bw = 17f + SettingsUI.PreferredWidth(t) + 7f;
                    m_Badge.anchorMin = m_Badge.anchorMax = new Vector2(0f, 1f);
                    m_Badge.pivot = new Vector2(0f, 0.5f);
                    m_Badge.sizeDelta = new Vector2(bw, 18f);
                    m_Badge.anchoredPosition = new Vector2(x + 12f, 0f);
                    x += 12f + bw;
                }
            }
            m_Dirty = SettingsUI.Diamond(transform, "Changed", SettingsTheme.Accent);
            m_Dirty.rectTransform.anchorMin = m_Dirty.rectTransform.anchorMax = new Vector2(0f, 1f);
            m_Dirty.rectTransform.anchoredPosition = new Vector2(x + 12f, 0f);

            m_Note = SettingsUI.Text(transform, "Note", "", SettingsTheme.Body, 12.5f, SettingsTheme.Muted);
            m_Note.rectTransform.anchorMin = m_Note.rectTransform.anchorMax = new Vector2(0f, 1f);
            m_Note.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_Note.rectTransform.sizeDelta = new Vector2(width - controlWidth - 56f, 18f);

            m_ControlRoot = SettingsUI.Rect("Control", transform);
            m_ControlRoot.anchorMin = m_ControlRoot.anchorMax = new Vector2(1f, 0.5f);
            m_ControlRoot.pivot = new Vector2(1f, 0.5f);
            m_ControlRoot.sizeDelta = new Vector2(controlWidth, 42f);
            m_ControlRoot.anchoredPosition = new Vector2(-16f, 0f);
            m_ControlGroup = m_ControlRoot.gameObject.AddComponent<CanvasGroup>();
            var content = SettingsUI.At(SettingsUI.Rect("Content", m_ControlRoot), 0f, 0f, controlWidth, 42f);
            Control = AddControl(content.gameObject, def.kind);
            Control.Bind(screen, this, def, controlWidth);

            var ptr = m_Bg.gameObject.AddComponent<SettingsPointer>();
            ptr.onEnter = () => { m_Hovered = true; Paint(); };
            ptr.onExit = () => { m_Hovered = false; Paint(); };
            ptr.onDown = e => m_Screen.FocusRow(this);
            ptr.onDoubleClick = () => { if (Control != null && !IsDisabled) Control.Submit(); };
            Refresh();
        }

        static SettingControl AddControl(GameObject go, SettingKind kind)
        {
            switch (kind)
            {
                case SettingKind.Selector: return go.AddComponent<SelectorControl>();
                case SettingKind.Slider: return go.AddComponent<SliderControl>();
                case SettingKind.Toggle: return go.AddComponent<ToggleControl>();
                case SettingKind.Dropdown: return go.AddComponent<DropdownControl>();
                case SettingKind.Binding: return go.AddComponent<BindingControl>();
                case SettingKind.Action: return go.AddComponent<ActionControl>();
                default: return go.AddComponent<SegmentedControl>();
            }
        }

        public void Refresh()
        {
            var s = AccertoneSettings.Instance;
            string reason = s.DisabledReason(Definition);
            IsDisabled = reason != null;
            bool warn = false;
            if (reason == null && Definition.kind == SettingKind.Binding && !Definition.locked && s.Input.IsUnbound(Definition))
            {
                reason = "Not bound. This action can't be used until you bind it.";
                warn = true;
            }
            bool note = !string.IsNullOrEmpty(reason) && !IsHeader;
            float h = note ? NoteHeight : BaseHeight;
            if (!Mathf.Approximately(m_Layout.preferredHeight, h))
            {
                m_Layout.preferredHeight = h;
                m_Rect.sizeDelta = new Vector2(m_Rect.sizeDelta.x, h);
            }
            float labelY = note ? -24f : -h * 0.5f;
            m_Label.rectTransform.anchoredPosition = new Vector2(16f, labelY);
            m_Label.color = IsDisabled ? SettingsTheme.Text2 : SettingsTheme.White;
            if (m_Badge != null) m_Badge.anchoredPosition = new Vector2(m_Badge.anchoredPosition.x, labelY);
            m_Dirty.rectTransform.anchoredPosition = new Vector2(m_Dirty.rectTransform.anchoredPosition.x, labelY);
            m_Dirty.enabled = s.IsDirty(Definition);
            m_Note.text = note ? reason : "";
            m_Note.color = warn ? SettingsTheme.Warning : SettingsTheme.Muted;
            m_Note.rectTransform.anchoredPosition = new Vector2(16f, -48f);
            m_ControlGroup.alpha = IsDisabled ? 0.36f : 1f;
            m_ControlGroup.blocksRaycasts = !IsDisabled;
            Control.Refresh();
            Paint();
        }

        public void SetFocused(bool focused)
        {
            if (m_Focused == focused) return;
            m_Focused = focused;
            Control.SetFocused(focused);
            Paint();
        }

        void Paint()
        {
            m_Bar.enabled = m_Focused;
            m_Bg.color = m_Focused ? SettingsTheme.A(SettingsTheme.Hex(0x1E2128), 0.96f) : m_Hovered ? SettingsTheme.A(SettingsTheme.Panel, 0.5f) : new Color(0, 0, 0, 0);
        }
    }
}
