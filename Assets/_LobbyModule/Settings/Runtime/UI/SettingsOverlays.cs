using System;
using System.Collections;
using System.Collections.Generic;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>Parallelogram action button (the cut of the lobby RACE button): Primary, Secondary or Ghost.</summary>
    public class SettingsButton : MonoBehaviour
    {
        public enum Style { Primary, Secondary, Ghost }

        const float Skew = 12f;
        Style m_Style;
        AccertoneShape m_Fill, m_Border, m_Glow;
        TextMeshProUGUI m_Label;
        RectTransform m_Key;
        bool m_Hovered, m_Pressed, m_Focused, m_Interactable = true, m_Pad;
        Action m_OnClick;
        string m_KeyLabel;
        CanvasGroup m_Group;

        public RectTransform Rect { get { return (RectTransform)transform; } }
        public bool Interactable { get { return m_Interactable; } set { m_Interactable = value; Paint(); } }
        public bool KeyboardFocused { get { return m_Focused; } set { m_Focused = value; Paint(); } }

        public static SettingsButton Create(Transform parent, string label, Style style, string key, Action onClick)
        {
            var rt = SettingsUI.Rect("Button_" + label, parent);
            var b = rt.gameObject.AddComponent<SettingsButton>();
            b.Build(label, style, key, onClick);
            return b;
        }

        void Build(string label, Style style, string key, Action onClick)
        {
            m_Style = style;
            m_OnClick = onClick;
            m_Glow = SettingsUI.Shape(transform, "Glow", SettingsTheme.A(SettingsTheme.Accent, 0.35f), 0f, ChamferCorners.None, Skew, true, 3f);
            m_Fill = SettingsUI.Shape(transform, "Fill", SettingsTheme.Accent, 0f, ChamferCorners.None, Skew, false, 1f, true);
            m_Border = SettingsUI.Shape(transform, "Border", SettingsTheme.Metallic, 0f, ChamferCorners.None, Skew, true, 1f);
            m_Label = SettingsUI.Text(transform, "Label", label, SettingsTheme.Label, 13f, SettingsTheme.White, 20f, TextAlignmentOptions.MidlineLeft, true);
            SetKey(key, false);
            var p = m_Fill.gameObject.AddComponent<SettingsPointer>();
            p.onEnter = () => { m_Hovered = true; Paint(); };
            p.onExit = () => { m_Hovered = false; m_Pressed = false; Paint(); };
            p.onDown = e => { m_Pressed = true; Paint(); };
            p.onUp = e => { m_Pressed = false; Paint(); };
            p.onClick = () => { if (m_Interactable && m_OnClick != null) m_OnClick(); };
        }

        public void SetLabel(string label) { m_Label.text = label; Layout(); }

        public void SetKey(string key, bool pad)
        {
            if (m_Key != null && key == m_KeyLabel && pad == m_Pad) return;
            m_KeyLabel = key; m_Pad = pad;
            if (m_Key != null) Destroy(m_Key.gameObject);
            m_Key = null;
            if (!string.IsNullOrEmpty(key))
            {
                m_Key = SettingsUI.Keycap(transform, key, pad);
                if (m_Style == Style.Primary)
                {
                    m_Key.Find("Fill").GetComponent<Graphic>().color = new Color(0f, 0f, 0f, 0.22f);
                    m_Key.Find("Border").GetComponent<Graphic>().color = new Color(1f, 1f, 1f, 0.45f);
                }
            }
            Layout();
        }

        void Layout()
        {
            float lw = SettingsUI.PreferredWidth(m_Label);
            float kw = m_Key != null ? m_Key.sizeDelta.x + 12f : 0f;
            float w = 30f + lw + kw + 30f;
            Rect.sizeDelta = new Vector2(w, 48f);
            foreach (var g in new Graphic[] { m_Fill, m_Border }) SettingsUI.Stretch(g.rectTransform);
            SettingsUI.Stretch(m_Glow.rectTransform, -4f, -4f, -4f, -4f);
            m_Label.rectTransform.anchorMin = m_Label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            m_Label.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_Label.rectTransform.sizeDelta = new Vector2(lw + 4f, 48f);
            m_Label.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            if (m_Key != null)
            {
                m_Key.anchorMin = m_Key.anchorMax = new Vector2(0f, 0.5f);
                m_Key.pivot = new Vector2(0f, 0.5f);
                m_Key.anchoredPosition = new Vector2(30f + lw + 12f, 0f);
            }
            Paint();
        }

        void Paint()
        {
            if (m_Fill == null) return;
            bool hot = m_Interactable && (m_Hovered || m_Focused);
            switch (m_Style)
            {
                case Style.Primary:
                    m_Fill.color = !m_Interactable ? SettingsTheme.Accent : m_Pressed ? SettingsTheme.AccentDeep : hot ? SettingsTheme.Hex(0xCB63FF) : SettingsTheme.Accent;
                    m_Border.color = m_Focused && m_Interactable ? SettingsTheme.White : new Color(0, 0, 0, 0);
                    m_Label.color = SettingsTheme.White;
                    break;
                case Style.Secondary:
                    m_Fill.color = m_Pressed ? SettingsTheme.Hairline : hot ? SettingsTheme.Hex(0x1E2129) : SettingsTheme.Hex(0x15171C);
                    m_Border.color = hot ? SettingsTheme.Accent : SettingsTheme.A(SettingsTheme.Metallic, 0.7f);
                    m_Label.color = SettingsTheme.White;
                    break;
                default:
                    m_Fill.color = m_Pressed ? SettingsTheme.A(SettingsTheme.Hairline, 0.7f) : hot ? SettingsTheme.A(SettingsTheme.Panel, 0.75f) : new Color(0, 0, 0, 0);
                    m_Border.color = m_Focused && m_Interactable ? SettingsTheme.Accent : new Color(0, 0, 0, 0);
                    m_Label.color = hot ? SettingsTheme.White : SettingsTheme.Text2;
                    break;
            }
            m_Glow.enabled = m_Focused && m_Interactable;
            if (m_Group == null) m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.alpha = m_Interactable ? 1f : 0.32f;
        }

        public void Click() { if (m_Interactable && m_OnClick != null) m_OnClick(); }
    }

    /// <summary>Modal dialog: eyebrow, title, body, optional content and up to three buttons.</summary>
    public class SettingsDialog : MonoBehaviour
    {
        public struct Choice
        {
            public string label;
            public SettingsButton.Style style;
            public Action action;
            public Choice(string label, SettingsButton.Style style, Action action) { this.label = label; this.style = style; this.action = action; }
        }

        CanvasGroup m_Group;
        RectTransform m_Panel, m_Content, m_Actions;
        Image m_TopBar;
        TextMeshProUGUI m_Eyebrow, m_Title, m_Body;
        readonly List<SettingsButton> m_Buttons = new List<SettingsButton>();
        Choice[] m_Choices;
        Action m_OnCancel;
        int m_Selected;
        Coroutine m_Anim;

        public bool IsOpen { get; private set; }
        public RectTransform Content { get { return m_Content; } }

        public void Build(RectTransform overlay)
        {
            var root = SettingsUI.Stretch(SettingsUI.Rect("Dialog", overlay));
            m_Group = root.gameObject.AddComponent<CanvasGroup>();
            var scrim = SettingsUI.Fill(root, "Scrim", new Color(0.024f, 0.027f, 0.035f, 0.78f), true);
            SettingsUI.Stretch(scrim.rectTransform);
            m_Panel = SettingsUI.Rect("Panel", root);
            m_Panel.anchorMin = m_Panel.anchorMax = new Vector2(0.5f, 0.5f);
            m_Panel.pivot = new Vector2(0.5f, 0.5f);
            m_Panel.sizeDelta = new Vector2(700f, 300f);
            var bg = SettingsUI.Shape(m_Panel, "Bg", SettingsTheme.Hex(0x121419), 26f, ChamferCorners.TopRight);
            SettingsUI.Stretch(bg.rectTransform);
            var border = SettingsUI.Shape(m_Panel, "Border", SettingsTheme.Hairline, 26f, ChamferCorners.TopRight, 0f, true, 1f);
            SettingsUI.Stretch(border.rectTransform);
            m_TopBar = SettingsUI.Fill(m_Panel, "Accent", SettingsTheme.Accent);
            SettingsUI.At(m_TopBar.rectTransform, 0f, 0f, 120f, 3f);
            m_Eyebrow = SettingsUI.Text(m_Panel, "Eyebrow", "", SettingsTheme.Label, 11f, SettingsTheme.Muted, 26f, TextAlignmentOptions.TopLeft, true);
            SettingsUI.At(m_Eyebrow.rectTransform, 44f, 40f, 600f, 14f);
            m_Title = SettingsUI.Paragraph(m_Panel, "Title", "", SettingsTheme.Headline, 30f, SettingsTheme.White, 0f);
            m_Title.fontStyle = FontStyles.UpperCase; m_Title.characterSpacing = 2f;
            m_Body = SettingsUI.Paragraph(m_Panel, "Body", "", SettingsTheme.Body, 15f, SettingsTheme.Text2, 10f);
            m_Content = SettingsUI.Rect("Content", m_Panel);
            m_Actions = SettingsUI.Rect("Actions", m_Panel);
            root.gameObject.SetActive(false);
        }

        public void Show(string eyebrow, string title, string body, bool warn, float contentHeight, Action<RectTransform> buildContent, Choice[] choices, Action onCancel)
        {
            // Active first, so text (and button widths) measure with their fonts initialised.
            m_Group.gameObject.SetActive(true);
            for (int i = m_Content.childCount - 1; i >= 0; i--) { var old = m_Content.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
            foreach (var b in m_Buttons) Destroy(b.gameObject);
            m_Buttons.Clear();

            m_Eyebrow.text = eyebrow;
            m_Title.text = title;
            m_Body.text = body;
            m_TopBar.color = warn ? SettingsTheme.Warning : SettingsTheme.Accent;
            m_Choices = choices;
            m_OnCancel = onCancel;
            m_Selected = 0;

            float w = 700f - 88f;
            float y = 66f;
            float th = m_Title.GetPreferredValues(title, w, 400f).y;
            SettingsUI.At(m_Title.rectTransform, 44f, y, w, th + 4f);
            y += th + 14f;
            float bh = string.IsNullOrEmpty(body) ? 0f : m_Body.GetPreferredValues(body, w, 600f).y;
            SettingsUI.At(m_Body.rectTransform, 44f, y, w, bh + 4f);
            y += bh + (contentHeight > 0f ? 24f : 0f);
            SettingsUI.At(m_Content, 44f, y, w, contentHeight);
            if (buildContent != null) buildContent(m_Content);
            y += contentHeight + 32f;

            float x = w;
            for (int i = choices.Length - 1; i >= 0; i--)
            {
                var c = choices[i];
                int index = i;
                var b = SettingsButton.Create(m_Actions, c.label, c.style, null, () => Choose(index));
                x -= b.Rect.sizeDelta.x;
                b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0f, 1f);
                b.Rect.pivot = new Vector2(0f, 1f);
                b.Rect.anchoredPosition = new Vector2(x, 0f);
                x -= 14f;
                m_Buttons.Insert(0, b);
            }
            SettingsUI.At(m_Actions, 44f, y, w, 48f);
            m_Panel.sizeDelta = new Vector2(700f, y + 48f + 36f);
            Paint();

            IsOpen = true;
            m_Group.gameObject.SetActive(true);
            m_Group.transform.SetAsLastSibling();
            if (m_Anim != null) StopCoroutine(m_Anim);
            m_Group.alpha = 0f;
            m_Anim = SettingsTween.Run(this, 0.24f, e => { m_Group.alpha = e; m_Panel.anchoredPosition = new Vector2(0f, -14f * (1f - e)); });
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (m_Anim != null) StopCoroutine(m_Anim);
            m_Anim = SettingsTween.Run(this, 0.16f, e => m_Group.alpha = 1f - e, () => m_Group.gameObject.SetActive(false));
        }

        void Choose(int i)
        {
            if (!IsOpen || m_Choices == null || i < 0 || i >= m_Choices.Length) return;
            var a = m_Choices[i].action;
            if (a != null) a();
        }

        void Paint() { for (int i = 0; i < m_Buttons.Count; i++) m_Buttons[i].KeyboardFocused = i == m_Selected; }

        public void Navigate(int dir)
        {
            if (m_Buttons.Count == 0) return;
            m_Selected = (m_Selected + dir + m_Buttons.Count) % m_Buttons.Count;
            Paint();
        }

        public void Confirm() { Choose(m_Selected); }
        public void Cancel() { if (m_OnCancel != null) m_OnCancel(); }
    }

    /// <summary>Short confirmation message above the footer.</summary>
    public class SettingsToast : MonoBehaviour
    {
        CanvasGroup m_Group;
        RectTransform m_Rect;
        Image m_Bar;
        UIPolyline m_Icon;
        TextMeshProUGUI m_Title, m_Text;
        Coroutine m_Routine;

        public void Build(RectTransform overlay)
        {
            m_Rect = SettingsUI.Rect("Toast", overlay);
            m_Rect.anchorMin = m_Rect.anchorMax = new Vector2(0.5f, 0f);
            m_Rect.pivot = new Vector2(0.5f, 0f);
            m_Rect.sizeDelta = new Vector2(420f, 62f);
            m_Rect.anchoredPosition = new Vector2(0f, 120f);
            m_Group = m_Rect.gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;
            var bg = SettingsUI.Fill(m_Rect, "Bg", SettingsTheme.Hex(0x14161B));
            SettingsUI.Stretch(bg.rectTransform);
            var border = SettingsUI.Shape(m_Rect, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
            SettingsUI.Stretch(border.rectTransform);
            m_Bar = SettingsUI.Fill(m_Rect, "Bar", SettingsTheme.Positive);
            m_Bar.rectTransform.anchorMin = Vector2.zero; m_Bar.rectTransform.anchorMax = new Vector2(0f, 1f); m_Bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_Bar.rectTransform.sizeDelta = new Vector2(3f, 0f); m_Bar.rectTransform.anchoredPosition = Vector2.zero;
            m_Icon = SettingsUI.Chevron(m_Rect, "Icon", 3, SettingsTheme.Positive, 18f, 2.2f);
            m_Icon.rectTransform.anchorMin = m_Icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            m_Icon.rectTransform.anchoredPosition = new Vector2(28f, 0f);
            m_Title = SettingsUI.Text(m_Rect, "Title", "", SettingsTheme.Label, 12f, SettingsTheme.White, 20f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(m_Title.rectTransform, 50f, 12f, 360f, 16f);
            m_Text = SettingsUI.Text(m_Rect, "Text", "", SettingsTheme.Body, 13f, SettingsTheme.Text2);
            SettingsUI.At(m_Text.rectTransform, 50f, 32f, 360f, 18f);
            m_Rect.gameObject.SetActive(false);
        }

        public void Show(string title, string text, bool warn)
        {
            m_Title.text = title;
            m_Text.text = text;
            Color c = warn ? SettingsTheme.Warning : SettingsTheme.Positive;
            m_Bar.color = c;
            m_Icon.color = c;
            float w = Mathf.Max(SettingsUI.PreferredWidth(m_Title), SettingsUI.PreferredWidth(m_Text)) + 50f + 26f;
            m_Rect.sizeDelta = new Vector2(Mathf.Max(320f, w), 62f);
            m_Rect.gameObject.SetActive(true);
            m_Rect.SetAsLastSibling();
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            bool done = false;
            SettingsTween.Run(this, 0.3f, e => { m_Group.alpha = e; m_Rect.anchoredPosition = new Vector2(0f, 110f + 10f * e); }, () => done = true);
            while (!done) yield return null;
            yield return new WaitForSecondsRealtime(2.6f);
            done = false;
            SettingsTween.Run(this, 0.25f, e => m_Group.alpha = 1f - e, () => done = true);
            while (!done) yield return null;
            m_Rect.gameObject.SetActive(false);
            m_Routine = null;
        }
    }

    /// <summary>The option list of a dropdown, shown on the overlay layer under its field.</summary>
    public class SettingsDropdownList : MonoBehaviour
    {
        const float RowH = 40f;
        const int MaxVisible = 8;

        RectTransform m_Root, m_Panel, m_Content;
        readonly List<Image> m_RowBg = new List<Image>();
        readonly List<TextMeshProUGUI> m_RowText = new List<TextMeshProUGUI>();
        readonly List<UIPolyline> m_RowCheck = new List<UIPolyline>();
        DropdownControl m_Control;
        int m_Highlight, m_Selected, m_Scroll;
        Action<int> m_OnPick;
        Action m_OnClose;
        CanvasGroup m_Group;

        public bool IsOpen { get { return m_Control != null; } }
        public DropdownControl Control { get { return m_Control; } }

        public void Build(RectTransform overlay)
        {
            m_Root = SettingsUI.Stretch(SettingsUI.Rect("DropdownList", overlay));
            var blocker = SettingsUI.Fill(m_Root, "Blocker", new Color(0, 0, 0, 0), true);
            SettingsUI.Stretch(blocker.rectTransform);
            blocker.gameObject.AddComponent<SettingsPointer>().onDown = e => Close();
            m_Panel = SettingsUI.Rect("Panel", m_Root);
            m_Group = m_Panel.gameObject.AddComponent<CanvasGroup>();
            var bg = SettingsUI.Fill(m_Panel, "Bg", SettingsTheme.Hex(0x14161B), true);
            SettingsUI.Stretch(bg.rectTransform);
            var border = SettingsUI.Shape(m_Panel, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
            SettingsUI.Stretch(border.rectTransform);
            var viewport = SettingsUI.Stretch(SettingsUI.Rect("Viewport", m_Panel), 0f, 6f, 0f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            m_Content = SettingsUI.At(SettingsUI.Rect("Content", viewport), 0f, 0f, 100f, 100f);
            m_Root.gameObject.SetActive(false);
        }

        public void Open(DropdownControl control, RectTransform field, IList<SettingOption> options, int selected, Action<int> onPick, Action onClose)
        {
            Close();
            m_Control = control; m_OnPick = onPick; m_OnClose = onClose;
            m_Selected = m_Highlight = selected;
            for (int i = m_Content.childCount - 1; i >= 0; i--) Destroy(m_Content.GetChild(i).gameObject);
            m_RowBg.Clear(); m_RowText.Clear(); m_RowCheck.Clear();

            // Position under the field, in the overlay's space.
            var corners = new Vector3[4];
            field.GetWorldCorners(corners);
            Vector2 bl = m_Root.InverseTransformPoint(corners[0]);
            Vector2 tr = m_Root.InverseTransformPoint(corners[2]);
            float w = tr.x - bl.x;
            int visible = Mathf.Min(MaxVisible, options.Count);
            float h = visible * RowH + 12f;
            m_Panel.anchorMin = m_Panel.anchorMax = new Vector2(0.5f, 0.5f);
            m_Panel.pivot = new Vector2(0f, 1f);
            Vector2 rootMin = m_Root.rect.min;
            float top = bl.y - 6f;
            if (top - h < rootMin.y + 20f) top = tr.y + 6f + h; // open upward when there's no room below
            m_Panel.anchoredPosition = new Vector2(bl.x, top);
            m_Panel.sizeDelta = new Vector2(w, h);
            m_Content.sizeDelta = new Vector2(w, options.Count * RowH);

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var row = SettingsUI.Fill(m_Content, "Option", new Color(0, 0, 0, 0), true);
                SettingsUI.At(row.rectTransform, 0f, i * RowH, w, RowH);
                var t = SettingsUI.Text(row.transform, "Label", options[i].label, SettingsTheme.Label, 13f, SettingsTheme.Text2, 8f, TextAlignmentOptions.MidlineLeft, true);
                SettingsUI.At(t.rectTransform, 16f, 0f, w - 50f, RowH);
                var check = SettingsUI.Chevron(row.transform, "Check", 3, SettingsTheme.AccentLight, 14f, 2f);
                SettingsUI.At(check.rectTransform, w - 32f, 13f, 14f, 14f);
                var p = row.gameObject.AddComponent<SettingsPointer>();
                p.onEnter = () => { m_Highlight = index; Paint(); };
                p.onClick = () => Pick(index);
                m_RowBg.Add(row); m_RowText.Add(t); m_RowCheck.Add(check);
            }
            m_Scroll = Mathf.Clamp(selected - visible / 2, 0, Mathf.Max(0, options.Count - visible));
            Paint();
            m_Root.gameObject.SetActive(true);
            m_Root.SetAsLastSibling();
            m_Group.alpha = 0f;
            SettingsTween.Run(this, 0.16f, e => { m_Group.alpha = e; });
        }

        void Paint()
        {
            for (int i = 0; i < m_RowBg.Count; i++)
            {
                bool hl = i == m_Highlight, sel = i == m_Selected;
                m_RowBg[i].color = hl ? SettingsTheme.A(SettingsTheme.Hex(0x262A33), 0.95f) : new Color(0, 0, 0, 0);
                m_RowText[i].color = sel ? SettingsTheme.AccentLight : hl ? SettingsTheme.White : SettingsTheme.Text2;
                m_RowCheck[i].enabled = sel;
            }
            int visible = Mathf.Min(MaxVisible, m_RowBg.Count);
            if (m_Highlight < m_Scroll) m_Scroll = m_Highlight;
            if (m_Highlight >= m_Scroll + visible) m_Scroll = m_Highlight - visible + 1;
            m_Content.anchoredPosition = new Vector2(0f, m_Scroll * RowH);
        }

        public void Move(int dir)
        {
            m_Highlight = Mathf.Clamp(m_Highlight + dir, 0, m_RowBg.Count - 1);
            Paint();
        }

        public void Confirm() { Pick(m_Highlight); }

        void Pick(int i)
        {
            var pick = m_OnPick;
            Close();
            if (pick != null) pick(i);
        }

        public void Close()
        {
            if (m_Control == null) return;
            m_Control = null;
            m_Root.gameObject.SetActive(false);
            var c = m_OnClose;
            m_OnClose = null;
            if (c != null) c();
        }
    }
}
