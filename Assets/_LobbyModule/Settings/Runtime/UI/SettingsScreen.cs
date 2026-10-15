using System;
using System.Collections.Generic;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>
    /// The lobby settings screen: an overlay over the live showroom with a category rail, the settings list
    /// (generated from the <see cref="SettingsCatalog"/>), a detail panel with live previews, a footer with
    /// prompts and Apply / Discard / Reset all, plus dialogs, toasts and dropdown lists on an overlay layer.
    ///
    /// Opened and closed by <see cref="LobbyNavigationCoordinator"/> (SETTINGS tab / Esc). Everything is built at
    /// runtime from the catalog; layout follows the approved 1920×1080 design.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        [Tooltip("Layer for dialogs, toasts and dropdown lists. Should render above the lobby UI (created if empty).")]
        public RectTransform overlay;
        [Tooltip("Category opened first.")]
        public int startCategory;

        public event Action CloseRequested;

        AccertoneSettings S { get { return AccertoneSettings.Instance; } }

        // structure
        bool m_Built;
        CanvasGroup m_Group;
        RectTransform m_Frame, m_Rail, m_Content, m_Head, m_Viewport, m_List, m_Prompts, m_ScrollBar, m_ScrollThumb;
        CanvasGroup m_ListGroup;
        TextMeshProUGUI m_Title, m_Blurb, m_Status, m_HeadNote;
        Image m_StatusDot, m_RailIndicator, m_HeadNoteDot;
        AccertoneShape m_StatusBorder;
        SettingsDetailPanel m_Detail;
        SettingsButton m_Apply, m_Discard, m_ResetAll;
        SettingsDialog m_Dialog;
        SettingsToast m_Toast;
        SettingsDropdownList m_DropdownList;
        SettingsMenuInput m_Input;
        readonly List<RailItem> m_RailItems = new List<RailItem>();
        readonly List<RevealPart> m_Reveal = new List<RevealPart>();

        // state
        readonly List<SettingsRow> m_Rows = new List<SettingsRow>();
        readonly Dictionary<SettingsRow, string> m_RowSection = new Dictionary<SettingsRow, string>();
        int m_Cat, m_Page, m_Focus;
        float m_Scroll, m_ScrollTarget, m_RailY;
        bool m_RefreshPending, m_Open;
        [System.NonSerialized] SettingDefinition m_RebindDef;
        int m_RebindSlot = -1;
        TextMeshProUGUI m_CountNumber;
        readonly List<Graphic> m_CountSegs = new List<Graphic>();
        Action m_AfterClose;

        class RailItem
        {
            public RectTransform rect;
            public Image bg, dot;
            public TextMeshProUGUI label;
            public UIPolyline chevron;
            public bool hovered;
        }

        struct RevealPart
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector2 home;
            public float delay;
        }

        public bool IsOpen { get { return m_Open; } }
        public int CurrentPage { get { return m_Page; } }
        public bool UsingGamepad { get { return m_Input != null && m_Input.UsingGamepad; } }
        SettingCategory Category { get { return S.Catalog.categories[m_Cat]; } }

        // ================================================================== open / close
        public void Open()
        {
            // Activate before building so text measures with its fonts initialised.
            gameObject.SetActive(true);
            if (overlay != null) overlay.gameObject.SetActive(true);
            Build();
            if (m_Open) return;
            m_Open = true;
            S.BeginEditing();
            S.SettingChanged += OnSettingChanged;
            S.ConfirmTick += OnConfirmTick;
            S.ConfirmEnded += OnConfirmEnded;
            m_Input.enabled = true;
            m_Input.Resume(0.15f);
            m_Cat = Mathf.Clamp(m_Cat, 0, S.Catalog.categories.Count - 1);
            BuildPage(false);
            m_RailY = m_Cat * 58f;
            PlayReveal();
            PlayUi(UiSound.Category);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>Asks to leave. With unsaved changes the player chooses Apply, Discard or Keep editing.</summary>
        public void RequestClose(Action leave = null, Action stay = null)
        {
            if (!m_Open) { if (leave != null) leave(); return; }
            if (m_Dialog.IsOpen || m_RebindDef != null) { if (stay != null) stay(); return; }
            m_DropdownList.Close();
            Action go = () =>
            {
                if (leave != null) leave();
                else if (CloseRequested != null) CloseRequested();
                else Hide();
            };
            if (!S.HasChanges && !S.IsConfirming) { PlayUi(UiSound.Back); go(); return; }
            int n = S.ChangeCount;
            m_Dialog.Show("Settings", "You have " + n + " unsaved change" + (n == 1 ? "" : "s"),
                "Apply them before you go back to the lobby, or discard them.", false, ChangeListHeight(), BuildChangeList,
                new[]
                {
                    new SettingsDialog.Choice("Apply & exit", SettingsButton.Style.Primary, () => { m_Dialog.Close(); if (ApplyNow()) { m_AfterClose = go; return; } go(); }),
                    new SettingsDialog.Choice("Discard & exit", SettingsButton.Style.Secondary, () => { m_Dialog.Close(); S.Discard(); go(); }),
                    new SettingsDialog.Choice("Keep editing", SettingsButton.Style.Ghost, () => { m_Dialog.Close(); if (stay != null) stay(); })
                },
                () => { m_Dialog.Close(); if (stay != null) stay(); });
            PlayUi(UiSound.Category);
        }

        /// <summary>Closes immediately (unsaved changes are discarded).</summary>
        public void Hide()
        {
            if (!m_Open) { gameObject.SetActive(false); return; }
            m_Open = false;
            S.Input.CancelRebind();
            m_DropdownList.Close();
            m_Dialog.Close();
            S.SettingChanged -= OnSettingChanged;
            S.ConfirmTick -= OnConfirmTick;
            S.ConfirmEnded -= OnConfirmEnded;
            S.Input.SetPreview(false);
            S.EndEditing();
            m_Input.enabled = false;
            SettingsTween.Run(this, 0.22f, e => m_Group.alpha = 1f - e, () => { if (!m_Open) gameObject.SetActive(false); });
        }

        void OnDisable()
        {
            if (m_Open && S != null)
            {
                m_Open = false;
                S.SettingChanged -= OnSettingChanged;
                S.ConfirmTick -= OnConfirmTick;
                S.ConfirmEnded -= OnConfirmEnded;
                S.Input.SetPreview(false);
                S.EndEditing();
            }
        }

        // ================================================================== build
        void Build()
        {
            if (m_Built) return;
            m_Built = true;
            var rt = (RectTransform)transform;
            SettingsUI.Stretch(rt);
            m_Group = gameObject.GetComponent<CanvasGroup>();
            if (m_Group == null) m_Group = gameObject.AddComponent<CanvasGroup>();

            // Scrim over the showroom: darker on the left where the menu is.
            var scrim = SettingsUI.Shape(rt, "Scrim", SettingsTheme.Graphite, 0f, ChamferCorners.None, 0f, false, 1f, true);
            SettingsUI.Stretch(scrim.rectTransform);
            scrim.alphaLeft = 0.95f; scrim.alphaRight = 0.84f;

            m_Frame = SettingsUI.Rect("Frame", rt);
            m_Frame.anchorMin = m_Frame.anchorMax = new Vector2(0.5f, 0.5f);
            m_Frame.pivot = new Vector2(0.5f, 0.5f);
            m_Frame.sizeDelta = new Vector2(1920f, 1080f);

            BuildHeader();
            BuildRail();
            BuildContentPanel();
            BuildDetailPanel();
            BuildFooter();
            BuildOverlay();

            m_Input = gameObject.AddComponent<SettingsMenuInput>();
            m_Input.Command += OnCommand;
            m_Input.DeviceChanged += () => { RenderPrompts(); RefreshAll(); };
            m_Input.enabled = false;
        }

        RectTransform Part(string name, float x, float y, float w, float h, float delay)
        {
            var r = SettingsUI.At(SettingsUI.Rect(name, m_Frame), x, y, w, h);
            var g = r.gameObject.AddComponent<CanvasGroup>();
            m_Reveal.Add(new RevealPart { rect = r, group = g, home = r.anchoredPosition, delay = delay });
            return r;
        }

        void BuildHeader()
        {
            var head = Part("Header", 112f, 170f, 1696f, 100f, 0.02f);
            var tick = SettingsUI.Fill(head, "Tick", SettingsTheme.Accent);
            SettingsUI.At(tick.rectTransform, 0f, 5f, 18f, 2f);
            var eyebrow = SettingsUI.Text(head, "Eyebrow", "SETTINGS", SettingsTheme.Label, 11f, SettingsTheme.Muted, 26f);
            SettingsUI.At(eyebrow.rectTransform, 30f, 0f, 300f, 12f);
            for (int i = 0; i < 2; i++)
            {
                var bar = SettingsUI.Shape(head, "Mark", SettingsTheme.Accent, 0f, ChamferCorners.None, 9f);
                SettingsUI.At(bar.rectTransform, i * 14f, 24f, 15f, 34f);
            }
            m_Title = SettingsUI.Text(head, "Title", "", SettingsTheme.Display, 46f, SettingsTheme.White, 7f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(m_Title.rectTransform, 46f, 18f, 900f, 48f);
            m_Blurb = SettingsUI.Text(head, "Blurb", "", SettingsTheme.Body, 15f, SettingsTheme.Text2);
            SettingsUI.At(m_Blurb.rectTransform, 0f, 72f, 1000f, 20f);

            var status = SettingsUI.AtRight(SettingsUI.Rect("Status", head), 0f, 18f, 260f, 30f);
            m_StatusBorder = SettingsUI.Shape(status, "Border", SettingsTheme.Hairline, 0f, ChamferCorners.None, 0f, true, 1f);
            var sbg = SettingsUI.Fill(status, "Bg", SettingsTheme.A(SettingsTheme.Carbon, 0.8f));
            sbg.transform.SetAsFirstSibling();
            SettingsUI.Stretch(sbg.rectTransform);
            SettingsUI.Stretch(m_StatusBorder.rectTransform);
            m_StatusDot = SettingsUI.Fill(status, "Dot", SettingsTheme.Positive);
            m_StatusDot.rectTransform.anchorMin = m_StatusDot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            m_StatusDot.rectTransform.sizeDelta = new Vector2(7f, 7f);
            m_StatusDot.rectTransform.anchoredPosition = new Vector2(18f, 0f);
            m_Status = SettingsUI.Text(status, "Text", "", SettingsTheme.Label, 11f, SettingsTheme.Text2, 22f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.Stretch(m_Status.rectTransform, 30f, 0f, 10f, 0f);
            var profile = SettingsUI.Text(head, "Profile", "PROFILE  ·  <color=#C4C9D1>DRIVER 01</color>", SettingsTheme.Label, 11f, SettingsTheme.Muted, 26f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(profile.rectTransform, 0f, 60f, 400f, 14f);
        }

        void BuildRail()
        {
            m_Rail = Part("Rail", 112f, 280f, 288f, 460f, 0.06f);
            var cats = S.Catalog.categories;
            for (int i = 0; i < cats.Count; i++)
            {
                int index = i;
                var item = new RailItem();
                item.rect = SettingsUI.At(SettingsUI.Rect("Category_" + cats[i].id, m_Rail), 0f, i * 58f, 288f, 54f);
                item.bg = SettingsUI.Fill(item.rect, "Bg", new Color(0, 0, 0, 0), true);
                SettingsUI.Stretch(item.bg.rectTransform);
                var key = SettingsUI.Keycap(item.rect, (i + 1).ToString());
                key.anchorMin = key.anchorMax = new Vector2(0f, 0.5f); key.pivot = new Vector2(0f, 0.5f); key.anchoredPosition = new Vector2(22f, 0f);
                item.label = SettingsUI.Text(item.rect, "Label", cats[i].title, SettingsTheme.Label, 13f, SettingsTheme.Text2, 18f, TextAlignmentOptions.MidlineLeft, true);
                SettingsUI.At(item.label.rectTransform, 62f, 0f, 170f, 54f);
                item.dot = SettingsUI.Diamond(item.rect, "Changes", SettingsTheme.Accent);
                item.dot.rectTransform.anchorMin = item.dot.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                item.dot.rectTransform.anchoredPosition = new Vector2(-48f, 0f);
                item.chevron = SettingsUI.Chevron(item.rect, "Chevron", 1, SettingsTheme.Accent, 14f, 2f);
                item.chevron.rectTransform.anchorMin = item.chevron.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                item.chevron.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
                var p = item.bg.gameObject.AddComponent<SettingsPointer>();
                p.onEnter = () => { item.hovered = true; PaintRail(); };
                p.onExit = () => { item.hovered = false; PaintRail(); };
                p.onClick = () => SetCategory(index);
                m_RailItems.Add(item);
            }
            m_RailIndicator = SettingsUI.Fill(m_Rail, "Indicator", SettingsTheme.Accent);
            SettingsUI.At(m_RailIndicator.rectTransform, 0f, 0f, 3f, 54f);
            var foot = SettingsUI.Text(m_Rail, "Hint", "PRESS 1–7 TO JUMP", SettingsTheme.Label, 10.5f, SettingsTheme.Muted, 22f);
            SettingsUI.At(foot.rectTransform, 22f, cats.Count * 58f + 18f, 260f, 14f);
        }

        void BuildContentPanel()
        {
            m_Content = Part("Content", 432f, 280f, 960f, 680f, 0.1f);
            PanelChrome(m_Content, 0.94f);
            m_Head = SettingsUI.At(SettingsUI.Rect("Head", m_Content), 36f, 26f, 888f, 0f);
            m_Viewport = SettingsUI.Rect("Viewport", m_Content);
            m_Viewport.gameObject.AddComponent<RectMask2D>();
            var wheel = SettingsUI.Fill(m_Viewport, "WheelCatcher", new Color(0, 0, 0, 0), true);
            SettingsUI.Stretch(wheel.rectTransform);
            m_Viewport.gameObject.AddComponent<SettingsScrollArea>().onScroll = d => ScrollBy(-d * 60f);
            m_List = SettingsUI.At(SettingsUI.Rect("List", m_Viewport), 20f, 0f, 920f, 100f);
            m_ListGroup = m_List.gameObject.AddComponent<CanvasGroup>();
            var v = m_List.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            v.padding = new RectOffset(0, 0, 4, 40);
            m_List.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_ScrollBar = SettingsUI.Fill(m_Content, "ScrollTrack", new Color(0, 0, 0, 0)).rectTransform;
            m_ScrollThumb = SettingsUI.Fill(m_ScrollBar, "Thumb", SettingsTheme.Hairline).rectTransform;
        }

        void BuildDetailPanel()
        {
            var panel = Part("Detail", 1424f, 280f, 384f, 680f, 0.14f);
            PanelChrome(panel, 0.88f);
            m_Detail = panel.gameObject.AddComponent<SettingsDetailPanel>();
            m_Detail.Build(this, panel);
        }

        static void PanelChrome(RectTransform r, float alpha)
        {
            var bg = SettingsUI.Shape(r, "Bg", SettingsTheme.A(SettingsTheme.Carbon, alpha), 22f, ChamferCorners.TopRight, 0f, false, 1f, true);
            SettingsUI.Stretch(bg.rectTransform);
            var border = SettingsUI.Shape(r, "Border", SettingsTheme.Hairline, 22f, ChamferCorners.TopRight, 0f, true, 1f);
            SettingsUI.Stretch(border.rectTransform);
            var tick = SettingsUI.Fill(r, "AccentTick", SettingsTheme.Accent);
            SettingsUI.At(tick.rectTransform, 0f, 0f, 56f, 2f);
        }

        void BuildFooter()
        {
            var foot = Part("Footer", 112f, 984f, 1696f, 54f, 0.18f);
            m_Prompts = SettingsUI.At(SettingsUI.Rect("Prompts", foot), 0f, 0f, 1100f, 54f);
            m_Apply = SettingsButton.Create(foot, "Apply", SettingsButton.Style.Primary, "SPACE", ApplyFromButton);
            m_Discard = SettingsButton.Create(foot, "Discard", SettingsButton.Style.Secondary, null, Discard);
            m_ResetAll = SettingsButton.Create(foot, "Reset all", SettingsButton.Style.Ghost, null, AskResetAll);
            LayoutFooterButtons();
        }

        void LayoutFooterButtons()
        {
            float x = 1696f;
            foreach (var b in new[] { m_Apply, m_Discard, m_ResetAll })
            {
                x -= b.Rect.sizeDelta.x;
                b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0f, 1f);
                b.Rect.pivot = new Vector2(0f, 1f);
                b.Rect.anchoredPosition = new Vector2(x, -3f);
                x -= 14f;
            }
        }

        void BuildOverlay()
        {
            if (overlay == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                overlay = SettingsUI.Rect("Screen_Settings_Overlay", canvas != null ? canvas.transform : transform.parent);
                overlay.SetAsLastSibling();
            }
            SettingsUI.Stretch(overlay);
            var frame = SettingsUI.Rect("Frame", overlay);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1920f, 1080f);
            m_DropdownList = overlay.gameObject.AddComponent<SettingsDropdownList>();
            m_DropdownList.Build(overlay);
            m_Dialog = overlay.gameObject.AddComponent<SettingsDialog>();
            m_Dialog.Build(overlay);
            m_Toast = overlay.gameObject.AddComponent<SettingsToast>();
            m_Toast.Build(frame);
        }

        void PlayReveal()
        {
            m_Group.alpha = 1f;
            foreach (var part in m_Reveal) { part.group.alpha = 0f; part.rect.anchoredPosition = part.home + new Vector2(0f, -14f); }
            var scrim = transform.Find("Scrim").GetComponent<Graphic>();
            SettingsTween.Run(this, 0.38f, e => scrim.canvasRenderer.SetAlpha(e));
            foreach (var part in m_Reveal)
            {
                var p = part;
                StartCoroutine(Delayed(p.delay * AccertoneMotion.Scale, () =>
                    SettingsTween.Run(this, 0.42f, e => { p.group.alpha = e; p.rect.anchoredPosition = p.home + new Vector2(0f, -14f * (1f - e)); })));
            }
        }

        static System.Collections.IEnumerator Delayed(float seconds, Action a)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            a();
        }

        // ================================================================== pages
        void SetCategory(int index)
        {
            int n = S.Catalog.categories.Count;
            index = (index % n + n) % n;
            if (index == m_Cat && m_Open) return;
            m_DropdownList.Close();
            m_Cat = index;
            m_Page = 0;
            PlayUi(UiSound.Category);
            BuildPage(true);
        }

        public void SetPage(int page)
        {
            if (page == m_Page || page < 0 || page >= Category.pages.Count) return;
            m_Page = page;
            PlayUi(UiSound.Category);
            BuildPage(true, keepFocus: true);
        }

        void BuildPage(bool animate, bool keepFocus = false)
        {
            if (!animate || AccertoneMotion.Scale <= 0.01f) { RebuildPage(keepFocus); return; }
            SettingsTween.Run(this, 0.09f, e => m_ListGroup.alpha = 1f - e, () =>
            {
                RebuildPage(keepFocus);
                SettingsTween.Run(this, 0.22f, e =>
                {
                    m_ListGroup.alpha = e;
                    m_List.anchoredPosition = new Vector2(20f + 10f * (1f - e), m_List.anchoredPosition.y);
                });
            });
        }

        void RebuildPage(bool keepFocus)
        {
            var cat = Category;
            m_Title.text = cat.title;
            m_Blurb.text = cat.blurb;
            S.Input.SetPreview(cat.id == "controls");

            for (int i = m_Head.childCount - 1; i >= 0; i--) Destroy(m_Head.GetChild(i).gameObject);
            for (int i = m_List.childCount - 1; i >= 0; i--) Destroy(m_List.GetChild(i).gameObject);
            m_Rows.Clear();
            m_RowSection.Clear();
            m_HeadNote = null;

            float headH = 0f;
            if (cat.HasHeader)
            {
                var row = NewRow(m_Head, cat.header, 920f, 560f, true);
                row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(0f, 1f);
                row.Rect.pivot = new Vector2(0f, 1f);
                row.Rect.anchoredPosition = new Vector2(-16f, 0f);
                m_RowSection[row] = null;
                headH = SettingsRow.BaseHeight;
                if (cat.id == "controls")
                {
                    m_HeadNoteDot = SettingsUI.Fill(m_Head, "NoteDot", SettingsTheme.Positive);
                    SettingsUI.At(m_HeadNoteDot.rectTransform, 0f, headH + 13f, 7f, 7f);
                    m_HeadNote = SettingsUI.Text(m_Head, "Note", "", SettingsTheme.Body, 13f, SettingsTheme.Text2);
                    SettingsUI.At(m_HeadNote.rectTransform, 17f, headH + 6f, 860f, 20f);
                    headH += 30f;
                }
            }
            float top = cat.HasHeader ? 26f + headH + 6f : 8f;
            SettingsUI.At(m_Viewport, 0f, top, 960f, 680f - top - 2f);

            var page = cat.pages[Mathf.Clamp(m_Page, 0, cat.pages.Count - 1)];
            bool first = true;
            foreach (var section in page.sections)
            {
                var spacer = SettingsUI.Rect("Spacer", m_List);
                spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = first ? 14f : 26f;
                first = false;
                SectionHeader(section);
                foreach (var def in section.settings)
                {
                    var row = NewRow(m_List, def, 920f, 432f, false);
                    m_RowSection[row] = section.title;
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_List);
            if (!keepFocus) m_Focus = 0;
            m_Focus = Mathf.Clamp(m_Focus, 0, Mathf.Max(0, m_Rows.Count - 1));
            m_Scroll = m_ScrollTarget = 0f;
            ApplyScroll(true);
            FocusIndex(m_Focus, false);
            RefreshAll();
        }

        SettingsRow NewRow(RectTransform parent, SettingDefinition def, float width, float controlWidth, bool header)
        {
            var rt = SettingsUI.Rect("Row_" + def.key, parent);
            var row = rt.gameObject.AddComponent<SettingsRow>();
            row.Build(this, def, width, controlWidth, header);
            m_Rows.Add(row);
            return row;
        }

        void SectionHeader(SettingSection section)
        {
            var rt = SettingsUI.Rect("Section_" + section.title, m_List);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = section.bindingColumns ? 52f : 28f;
            var tick = SettingsUI.Fill(rt, "Tick", SettingsTheme.Accent);
            SettingsUI.At(tick.rectTransform, 16f, 9f, 10f, 2f);
            var t = SettingsUI.Text(rt, "Title", section.title, SettingsTheme.Label, 12f, SettingsTheme.Text2, 18f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(t.rectTransform, 40f, 0f, 400f, 20f);
            float lw = SettingsUI.PreferredWidth(t);
            var rule = SettingsUI.Fill(rt, "Rule", SettingsTheme.Hairline);
            SettingsUI.At(rule.rectTransform, 40f + lw + 14f, 10f, 920f - 16f - (40f + lw + 14f), 1f);
            if (section.bindingColumns)
            {
                bool pad = m_Page == 1;
                Column(rt, "ACTION", 16f);
                Column(rt, pad ? "BUTTON" : "PRIMARY", 920f - 16f - 332f);
                Column(rt, pad ? "ALTERNATE" : "SECONDARY", 920f - 16f - 160f);
            }
        }

        static void Column(RectTransform parent, string text, float x)
        {
            var t = SettingsUI.Text(parent, "Column", text, SettingsTheme.Label, 10.5f, SettingsTheme.Muted, 22f);
            SettingsUI.At(t.rectTransform, x, 32f, 200f, 14f);
        }

        // ================================================================== focus & scroll
        public void FocusRow(SettingsRow row)
        {
            int i = m_Rows.IndexOf(row);
            if (i >= 0 && i != m_Focus) FocusIndex(i, true);
        }

        void FocusIndex(int i, bool sound)
        {
            if (m_Rows.Count == 0) { m_Detail.Show(null, Category, null); return; }
            i = Mathf.Clamp(i, 0, m_Rows.Count - 1);
            if (i != m_Focus && sound) PlayUi(UiSound.Move);
            m_Focus = i;
            for (int r = 0; r < m_Rows.Count; r++) m_Rows[r].SetFocused(r == i);
            EnsureVisible(m_Rows[i]);
            ShowDetail();
            RenderPrompts();
        }

        public void RefreshFocusedRow()
        {
            if (m_Focus < m_Rows.Count) m_Rows[m_Focus].Refresh();
            ShowDetail();
            RenderPrompts();
        }

        SettingsRow Focused { get { return m_Focus < m_Rows.Count ? m_Rows[m_Focus] : null; } }

        void ShowDetail()
        {
            var row = Focused;
            string section;
            m_RowSection.TryGetValue(row, out section);
            m_Detail.Show(row != null ? row.Definition : null, Category, section);
        }

        void EnsureVisible(SettingsRow row)
        {
            if (row == null || row.transform.parent != m_List) return;
            float top = -row.Rect.anchoredPosition.y - row.Rect.rect.height * (1f - row.Rect.pivot.y);
            float bottom = top + row.Rect.rect.height;
            float view = m_Viewport.rect.height;
            if (top - 40f < m_ScrollTarget) m_ScrollTarget = Mathf.Max(0f, top - 40f);
            else if (bottom + 30f > m_ScrollTarget + view) m_ScrollTarget = bottom + 30f - view;
            ClampScroll();
        }

        void ScrollBy(float delta)
        {
            m_ScrollTarget += delta;
            ClampScroll();
        }

        void ClampScroll()
        {
            float max = Mathf.Max(0f, m_List.rect.height - m_Viewport.rect.height);
            m_ScrollTarget = Mathf.Clamp(m_ScrollTarget, 0f, max);
        }

        void ApplyScroll(bool snap)
        {
            m_Scroll = snap ? m_ScrollTarget : Mathf.Lerp(m_Scroll, m_ScrollTarget, SettingsTween.Approach(16f));
            m_List.anchoredPosition = new Vector2(m_List.anchoredPosition.x, m_Scroll);
            float content = m_List.rect.height, view = m_Viewport.rect.height;
            bool show = content > view + 1f;
            m_ScrollBar.gameObject.SetActive(show);
            if (!show) return;
            SettingsUI.AtRight(m_ScrollBar, 8f, -m_Viewport.anchoredPosition.y + 8f, 3f, view - 16f);
            float track = view - 16f;
            float h = Mathf.Max(30f, track * view / content);
            float y = (track - h) * (m_Scroll / Mathf.Max(1f, content - view));
            SettingsUI.At(m_ScrollThumb, 0f, y, 3f, h);
        }

        void Update()
        {
            if (!m_Built) return;
            ApplyScroll(false);
            float target = m_Cat * 58f;
            m_RailY = Mathf.Lerp(m_RailY, target, SettingsTween.Approach(14f));
            m_RailIndicator.rectTransform.anchoredPosition = new Vector2(0f, -m_RailY);
        }

        void LateUpdate()
        {
            if (!m_RefreshPending) return;
            m_RefreshPending = false;
            RefreshAll();
        }

        // ================================================================== refresh
        void OnSettingChanged(string key) { m_RefreshPending = true; }

        void RefreshAll()
        {
            foreach (var r in m_Rows) r.Refresh();
            PaintRail();
            int n = S.ChangeCount;
            m_Status.text = n > 0 ? n + " UNSAVED CHANGE" + (n == 1 ? "" : "S") : "ALL CHANGES SAVED";
            m_Status.color = n > 0 ? SettingsTheme.White : SettingsTheme.Text2;
            m_StatusDot.color = n > 0 ? SettingsTheme.Accent : SettingsTheme.Positive;
            m_StatusDot.rectTransform.localRotation = n > 0 ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
            m_StatusBorder.color = n > 0 ? SettingsTheme.A(SettingsTheme.Accent, 0.55f) : SettingsTheme.Hairline;
            m_Apply.Interactable = n > 0;
            m_Discard.Interactable = n > 0;
            if (m_HeadNote != null)
            {
                bool pad = m_Page == 1;
                int unbound = S.Input.UnboundCount(pad ? InputSettingsSystem.GamepadGroup : InputSettingsSystem.KeyboardGroup);
                var gamepad = UnityEngine.InputSystem.Gamepad.current;
                if (unbound > 0) { m_HeadNote.text = unbound + " action" + (unbound > 1 ? "s are" : " is") + " unbound."; m_HeadNote.color = SettingsTheme.Warning; m_HeadNoteDot.color = SettingsTheme.Warning; }
                else if (pad) { m_HeadNote.text = gamepad != null ? gamepad.displayName + " connected." : "No controller detected. Connect one to test bindings and vibration."; m_HeadNote.color = SettingsTheme.Text2; m_HeadNoteDot.color = gamepad != null ? SettingsTheme.Positive : SettingsTheme.Muted; }
                else { m_HeadNote.text = "Choose a binding and press " + Glyph("confirm") + ", then the key you want. " + Glyph("clear") + " clears it."; m_HeadNote.color = SettingsTheme.Text2; m_HeadNoteDot.color = SettingsTheme.Positive; }
            }
            ShowDetail();
            RenderPrompts();
        }

        void PaintRail()
        {
            var cats = S.Catalog.categories;
            for (int i = 0; i < m_RailItems.Count; i++)
            {
                var item = m_RailItems[i];
                bool sel = i == m_Cat;
                item.bg.color = sel ? SettingsTheme.A(SettingsTheme.Panel, 0.94f) : item.hovered ? SettingsTheme.A(SettingsTheme.Panel, 0.5f) : new Color(0, 0, 0, 0);
                item.label.color = sel || item.hovered ? SettingsTheme.White : SettingsTheme.Text2;
                item.chevron.enabled = sel;
                item.dot.enabled = S.CategoryHasChanges(cats[i]);
            }
        }

        // ================================================================== prompts / glyphs
        public string Glyph(string name)
        {
            bool pad = UsingGamepad;
            switch (name)
            {
                case "back": return pad ? "B" : "ESC";
                case "prevCat": return pad ? "LB" : "Q";
                case "nextCat": return pad ? "RB" : "E";
                case "select": return pad ? "↕" : "↑↓";
                case "adjust": return pad ? "↔" : "←→";
                case "reset": return pad ? "X" : "R";
                case "apply": return pad ? "Y" : "SPACE";
                case "confirm": return pad ? "A" : "ENTER";
                case "clear": return pad ? "X" : "BKSP";
            }
            return name;
        }

        void RenderPrompts()
        {
            if (m_Prompts == null) return;
            for (int i = m_Prompts.childCount - 1; i >= 0; i--) Destroy(m_Prompts.GetChild(i).gameObject);
            var items = new List<KeyValuePair<string[], string>>();
            var cmds = new List<string>();
            var def = Focused != null ? Focused.Definition : null;
            if (m_RebindDef != null)
            {
                items.Add(Pair(new[] { m_RebindDef.bindingGroup == InputSettingsSystem.GamepadGroup ? "MENU" : "ESC" }, "Cancel")); cmds.Add(null);
                if (m_RebindDef.bindingGroup != InputSettingsSystem.GamepadGroup) { items.Add(Pair(new[] { "BKSP" }, "Clear")); cmds.Add(null); }
            }
            else
            {
                items.Add(Pair(new[] { Glyph("back") }, "Back")); cmds.Add("back");
                items.Add(Pair(new[] { Glyph("prevCat"), Glyph("nextCat") }, "Category")); cmds.Add("nextCat");
                items.Add(Pair(new[] { Glyph("select") }, "Select")); cmds.Add(null);
                items.Add(Pair(new[] { Glyph("adjust") }, "Change")); cmds.Add(null);
                if (def != null && def.kind == SettingKind.Binding && !def.locked) { items.Add(Pair(new[] { Glyph("confirm") }, "Rebind")); cmds.Add("confirm"); }
                else if (def != null && def.kind == SettingKind.Slider && !string.IsNullOrEmpty(def.audioChannel)) { items.Add(Pair(new[] { Glyph("confirm") }, "Test sound")); cmds.Add("confirm"); }
                else if (def != null && def.kind == SettingKind.Dropdown) { items.Add(Pair(new[] { Glyph("confirm") }, "Open")); cmds.Add("confirm"); }
                items.Add(Pair(new[] { Glyph("reset") }, "Reset category")); cmds.Add("reset");
            }
            float x = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var prompt = SettingsUI.At(SettingsUI.Rect("Prompt", m_Prompts), x, 0f, 10f, 54f);
                float px = 0f;
                foreach (var k in items[i].Key)
                {
                    var cap = SettingsUI.Keycap(prompt, k, UsingGamepad);
                    cap.anchorMin = cap.anchorMax = new Vector2(0f, 0.5f); cap.pivot = new Vector2(0f, 0.5f); cap.anchoredPosition = new Vector2(px, 0f);
                    px += cap.sizeDelta.x + 3f;
                }
                var t = SettingsUI.Text(prompt, "Label", items[i].Value, SettingsTheme.Label, 11f, SettingsTheme.Text2, 22f, TextAlignmentOptions.MidlineLeft, true);
                SettingsUI.At(t.rectTransform, px + 6f, 0f, 200f, 54f);
                float w = px + 6f + SettingsUI.PreferredWidth(t);
                prompt.sizeDelta = new Vector2(w, 54f);
                string cmd = cmds[i];
                if (cmd != null)
                {
                    var hit = prompt.gameObject.AddComponent<Image>();
                    hit.color = new Color(0, 0, 0, 0);
                    var p = prompt.gameObject.AddComponent<SettingsPointer>();
                    var label = t;
                    p.onEnter = () => label.color = SettingsTheme.White;
                    p.onExit = () => label.color = SettingsTheme.Text2;
                    p.onClick = () => OnCommand(cmd);
                }
                x += w + 26f;
            }
            m_Apply.SetKey(Glyph("apply"), UsingGamepad);
            LayoutFooterButtons();
        }

        static KeyValuePair<string[], string> Pair(string[] k, string v) { return new KeyValuePair<string[], string>(k, v); }

        // ================================================================== commands
        void OnCommand(string cmd)
        {
            if (!m_Open || m_RebindDef != null) return;
            if (m_Dialog.IsOpen)
            {
                if (cmd == "left" || cmd == "up") { m_Dialog.Navigate(-1); PlayUi(UiSound.Move); }
                else if (cmd == "right" || cmd == "down") { m_Dialog.Navigate(1); PlayUi(UiSound.Move); }
                else if (cmd == "confirm") m_Dialog.Confirm();
                else if (cmd == "back") m_Dialog.Cancel();
                return;
            }
            if (m_DropdownList.IsOpen)
            {
                if (cmd == "up") { m_DropdownList.Move(-1); PlayUi(UiSound.Move); }
                else if (cmd == "down") { m_DropdownList.Move(1); PlayUi(UiSound.Move); }
                else if (cmd == "confirm") m_DropdownList.Confirm();
                else if (cmd == "back") m_DropdownList.Close();
                return;
            }
            var row = Focused;
            bool usable = row != null && !row.IsDisabled;
            switch (cmd)
            {
                case "up": FocusIndex(m_Focus - 1, true); break;
                case "down": FocusIndex(m_Focus + 1, true); break;
                case "left": if (usable) row.Control.Step(-1, m_Input.BigStep); else if (row != null) PlayUi(UiSound.Error); break;
                case "right": if (usable) row.Control.Step(1, m_Input.BigStep); else if (row != null) PlayUi(UiSound.Error); break;
                case "confirm": if (usable) row.Control.Submit(); break;
                case "clear": if (usable) row.Control.Clear(); break;
                case "back": RequestClose(); break;
                case "prevCat": SetCategory(m_Cat - 1); break;
                case "nextCat": SetCategory(m_Cat + 1); break;
                case "apply": ApplyFromButton(); break;
                case "reset": AskResetCategory(); break;
                default:
                    if (cmd.StartsWith("cat:")) SetCategory(int.Parse(cmd.Substring(4)));
                    break;
            }
        }

        public void ChangeValue(string key, double value, bool sound = true)
        {
            if (S.Store.Set(key, value) && sound) PlayUi(UiSound.Change);
        }

        public void PlayUi(UiSound s) { if (S != null && S.Audio.Player != null) S.Audio.Player.Play(s); }
        public void PlayTestSound(string channel) { if (S.Audio.Player != null) S.Audio.Player.PlayTest(channel); }

        public void RunAction(SettingDefinition def)
        {
            if (def.key == "controls.pad.testVibration")
            {
                float k = (float)S.Store.Get(SettingKeys.VibrationIntensity) / 100f;
                if (S.Input.TestVibration(k)) Toast("Vibration test", "Played at " + Mathf.RoundToInt(k * 100f) + "% intensity.", false, true);
                else Toast("No controller detected", "Connect a controller to feel the vibration test.", true, true);
            }
        }

        // ================================================================== dropdowns
        public void ToggleDropdown(DropdownControl control)
        {
            if (m_DropdownList.IsOpen) { bool same = m_DropdownList.Control == control; m_DropdownList.Close(); if (same) return; }
            var def = control.Definition;
            var opts = S.GetOptions(def);
            control.IsOpen = true;
            control.Refresh();
            m_DropdownList.Open(control, control.FieldRect, opts, S.OptionIndex(def, S.Store.Get(def.key)),
                i => ChangeValue(def.key, opts[i].value),
                () => { control.IsOpen = false; control.Refresh(); });
            PlayUi(UiSound.Move);
        }

        // ================================================================== rebinding
        public bool IsRebinding(SettingDefinition def, int slot)
        {
            return m_RebindDef != null && m_RebindDef == def && (slot < 0 || slot == m_RebindSlot);
        }

        public void StartRebind(BindingControl control, int slot)
        {
            var def = control.Definition;
            if (m_RebindDef != null || m_Dialog.IsOpen || def.locked) return;
            m_RebindDef = def;
            m_RebindSlot = slot;
            m_Input.Suspend();
            PlayUi(UiSound.Change);
            RefreshFocusedRow();
            S.Input.StartRebind(def, slot, result =>
            {
                m_RebindDef = null;
                m_RebindSlot = -1;
                m_Input.Resume(0.25f);
                if (result.conflict) ShowConflict(def, slot, result);
                else if (!result.cancelled) PlayUi(UiSound.Confirm);
                m_RefreshPending = true;
                RefreshFocusedRow();
            });
        }

        void ShowConflict(SettingDefinition def, int slot, RebindResult r)
        {
            string key = InputGlyphs.Label(r.path);
            string mine = S.Input.SlotLabel(def, slot);
            var other = r.other; int otherSlot = r.otherSlot;
            m_Dialog.Show("Controls  ·  " + (def.bindingGroup == InputSettingsSystem.GamepadGroup ? "Controller" : "Keyboard"),
                key + " is already in use",
                "<b><color=#F3F5F8>" + key + "</color></b> is bound to <b><color=#F3F5F8>" + other.label + "</color></b>. Choose what happens to it." +
                (string.IsNullOrEmpty(mine) ? "" : " Swap gives " + other.label + " your current " + mine + "."),
                true, 0f, null,
                new[]
                {
                    new SettingsDialog.Choice("Swap", SettingsButton.Style.Primary, () => { S.Input.ResolveConflict(def, slot, r.path, other, otherSlot, true); m_Dialog.Close(); PlayUi(UiSound.Confirm); }),
                    new SettingsDialog.Choice("Replace", SettingsButton.Style.Secondary, () =>
                    {
                        S.Input.ResolveConflict(def, slot, r.path, other, otherSlot, false);
                        m_Dialog.Close();
                        if (S.Input.IsUnbound(other)) Toast(other.label + " is unbound", "Bind a key to it before racing.", true);
                    }),
                    new SettingsDialog.Choice("Cancel", SettingsButton.Style.Ghost, () => m_Dialog.Close())
                },
                () => m_Dialog.Close());
            PlayUi(UiSound.Error);
        }

        // ================================================================== apply / discard / reset
        void ApplyFromButton()
        {
            if (!S.HasChanges || m_Dialog.IsOpen) return;
            ApplyNow();
        }

        /// <summary>Applies; returns true if the display confirmation started.</summary>
        bool ApplyNow()
        {
            int n = S.ChangeCount;
            var previous = DisplayDescription(true);
            bool confirm = S.Apply();
            PlayUi(UiSound.Confirm);
            if (confirm) { ShowCountdown(previous); return true; }
            Toast("Settings applied", n + " change" + (n == 1 ? "" : "s") + " saved to your profile.", false);
            return false;
        }

        string DisplayDescription(bool applied)
        {
            var st = S.Store;
            Func<string, double> v = k => applied ? st.GetApplied(k) : st.Get(k);
            var cat = S.Catalog;
            string s = S.FormatValue(cat.Find(SettingKeys.Resolution), v(SettingKeys.Resolution)) + " · " + S.FormatValue(cat.Find(SettingKeys.WindowMode), v(SettingKeys.WindowMode));
            if (v(SettingKeys.WindowMode) < 0.5) s += " · " + S.FormatValue(cat.Find(SettingKeys.RefreshRate), v(SettingKeys.RefreshRate));
            return s;
        }

        void ShowCountdown(string previous)
        {
            m_CountSegs.Clear();
            string now = DisplayDescription(false);
            m_Dialog.Show("Display", "Keep these display settings?",
                "The screen is now <b><color=#F3F5F8>" + now + "</color></b>. If the picture looks wrong or you can't read this, do nothing and it switches back.",
                false, 70f, content =>
                {
                    m_CountNumber = SettingsUI.Text(content, "Seconds", AccertoneSettings.ConfirmSeconds.ToString(), SettingsTheme.Light, 64f, SettingsTheme.White);
                    SettingsUI.At(m_CountNumber.rectTransform, 0f, 0f, 90f, 70f);
                    var cap = SettingsUI.Text(content, "Caption", "Reverting to " + previous + " in", SettingsTheme.Label, 10.5f, SettingsTheme.Muted, 22f, TextAlignmentOptions.MidlineLeft, true);
                    SettingsUI.At(cap.rectTransform, 110f, 16f, 500f, 14f);
                    float segW = (612f - 110f - 14f * 4f) / 15f;
                    for (int i = 0; i < AccertoneSettings.ConfirmSeconds; i++)
                    {
                        var seg = SettingsUI.Shape(content, "Seg", SettingsTheme.Accent, 0f, ChamferCorners.None, 4f);
                        SettingsUI.At(seg.rectTransform, 110f + i * (segW + 4f), 40f, segW, 10f);
                        m_CountSegs.Add(seg);
                    }
                },
                new[]
                {
                    new SettingsDialog.Choice("Keep changes", SettingsButton.Style.Primary, () => S.KeepDisplayChanges()),
                    new SettingsDialog.Choice("Revert", SettingsButton.Style.Secondary, () => S.RevertDisplayChanges())
                },
                () => S.RevertDisplayChanges());
        }

        void OnConfirmTick(int seconds)
        {
            if (m_CountNumber != null) m_CountNumber.text = seconds.ToString();
            for (int i = 0; i < m_CountSegs.Count; i++) m_CountSegs[i].color = i < seconds ? SettingsTheme.Accent : SettingsTheme.Hairline;
        }

        void OnConfirmEnded(bool kept)
        {
            m_Dialog.Close();
            m_CountNumber = null;
            if (kept) Toast("Settings applied", "Display set to " + DisplayDescription(true) + ".", false);
            else Toast("Display settings reverted", "Back to " + DisplayDescription(true) + ".", true);
            m_RefreshPending = true;
            if (m_AfterClose != null) { var a = m_AfterClose; m_AfterClose = null; a(); }
        }

        void Discard()
        {
            if (!S.HasChanges) return;
            S.Discard();
            PlayUi(UiSound.Back);
            Toast("Changes discarded", "Settings are back to what you last applied.", true);
        }

        void AskResetCategory()
        {
            var cat = Category;
            int n = S.CountNonDefault(cat);
            if (n == 0)
            {
                m_Dialog.Show(cat.title, "Reset " + cat.title + " to defaults?", "Everything in " + cat.title + " is already at its default.", true, 0f, null,
                    new[] { new SettingsDialog.Choice("OK", SettingsButton.Style.Secondary, () => m_Dialog.Close()) }, () => m_Dialog.Close());
                return;
            }
            string extra = cat.id == "graphics" ? "the High preset" : "their defaults";
            string binds = cat.id == "controls" ? ", including every key and button binding" : "";
            m_Dialog.Show(cat.title, "Reset " + cat.title + " to defaults?",
                n + " setting" + (n == 1 ? "" : "s") + " will go back to " + extra + binds + ". You can still discard before applying.", true, 0f, null,
                new[]
                {
                    new SettingsDialog.Choice("Reset", SettingsButton.Style.Primary, () => { S.ResetCategory(cat); m_Dialog.Close(); Toast(cat.title + " reset", "Press Apply to keep the defaults.", false); }),
                    new SettingsDialog.Choice("Cancel", SettingsButton.Style.Secondary, () => m_Dialog.Close())
                },
                () => m_Dialog.Close());
            PlayUi(UiSound.Category);
        }

        void AskResetAll()
        {
            if (m_Dialog.IsOpen) return;
            int n = S.CountNonDefault(null);
            m_Dialog.Show("All settings", "Reset every setting?",
                "All seven categories go back to their defaults, including key bindings. " + n + " setting" + (n == 1 ? " differs" : "s differ") + " from the defaults right now.", true, 0f, null,
                new[]
                {
                    new SettingsDialog.Choice("Reset all", SettingsButton.Style.Primary, () => { S.ResetAll(); m_Dialog.Close(); Toast("All settings reset", "Press Apply to keep the defaults.", false); }),
                    new SettingsDialog.Choice("Cancel", SettingsButton.Style.Secondary, () => m_Dialog.Close())
                },
                () => m_Dialog.Close());
            PlayUi(UiSound.Category);
        }

        float ChangeListHeight() { return Mathf.Min(7, S.Store.DirtyKeys().Count + S.Input.DirtyBindingCount()) * 42f; }

        void BuildChangeList(RectTransform content)
        {
            var lines = new List<KeyValuePair<string, string>>();
            foreach (var k in S.Store.DirtyKeys())
            {
                bool mute = k.EndsWith(SettingKeys.MuteSuffix);
                var def = S.Catalog.Find(AccertoneSettings.BaseKey(k));
                if (def == null) continue;
                var cat = S.Catalog.CategoryOf(k);
                string from = mute ? (S.Store.GetApplied(k) >= 0.5 ? "Muted" : "On") : S.FormatValue(def, S.Store.GetApplied(k));
                string to = mute ? (S.Store.Get(k) >= 0.5 ? "Muted" : "On") : S.FormatValue(def, S.Store.Get(k));
                lines.Add(new KeyValuePair<string, string>(cat.title + " · " + def.label + (mute ? " mute" : ""), "<color=#6B717C>" + from + "</color>   " + to));
            }
            foreach (var def in S.Catalog.AllDefinitions())
                if (def.kind == SettingKind.Binding && S.Input.IsBindingDirty(def))
                    lines.Add(new KeyValuePair<string, string>("Controls · " + def.label, S.Input.BindingSummary(def)));
            int shown = Mathf.Min(lines.Count, 7);
            for (int i = 0; i < shown; i++)
            {
                bool more = i == 6 && lines.Count > 7;
                var rule = SettingsUI.Fill(content, "Rule", SettingsTheme.Hairline);
                SettingsUI.At(rule.rectTransform, 0f, i * 42f, 612f, 1f);
                var a = SettingsUI.Text(content, "Setting", more ? "And " + (lines.Count - 6) + " more" : lines[i].Key, SettingsTheme.Body, 14f, SettingsTheme.Text2);
                SettingsUI.At(a.rectTransform, 0f, i * 42f, 380f, 42f);
                if (more) continue;
                var b = SettingsUI.Text(content, "Value", lines[i].Value, SettingsTheme.Data, 13f, SettingsTheme.White, 0f, TextAlignmentOptions.MidlineRight, true);
                SettingsUI.At(b.rectTransform, 360f, i * 42f, 252f, 42f);
            }
        }

        // ================================================================== toast
        void Toast(string title, string text, bool warn, bool always = false)
        {
            if (!warn && !always && !S.Lobby.NotificationsOn) return;
            m_Toast.Show(title, text, warn);
        }
    }

    /// <summary>Forwards mouse-wheel scrolling.</summary>
    public class SettingsScrollArea : MonoBehaviour, IScrollHandler
    {
        public Action<float> onScroll;
        public void OnScroll(PointerEventData e) { if (onScroll != null) onScroll(e.scrollDelta.y); }
    }
}
