using System.Collections.Generic;
using Accertone.Play;
using Accertone.Settings;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Garage
{
    /// <summary>
    /// The frame around a configuration panel: a graphite gradient that feathers out toward the car, the header,
    /// a scrolling content area (rows reveal in a quick stagger) and the footer actions. Slides in from its side and
    /// out again, then removes itself.
    /// </summary>
    public class GaragePanelShell : MonoBehaviour, IScrollHandler
    {
        const float FooterHeight = 58f;

        GaragePanel m_Panel;
        GarageScreen m_Screen;
        int m_Index;
        bool m_Right;
        float m_Width, m_Height, m_Pad;

        RectTransform m_Rect, m_Viewport, m_Content, m_Thumb;
        CanvasGroup m_Group;
        GarageButton m_Back, m_Reset;
        readonly List<CanvasGroup> m_Rows = new List<CanvasGroup>();
        float m_Show, m_ShowT = 1f, m_ShowVel, m_OpenTime;
        float m_Scroll, m_ScrollT, m_ContentHeight, m_ViewHeight;
        bool m_Hiding;

        public static GaragePanelShell Create(RectTransform host, GaragePanel panel, int index, bool right, float width, float top, float bottomMargin,
            float edge, float pad, GarageScreen screen)
        {
            var rt = SettingsUI.Rect("Panel_" + (index + 1).ToString("00"), host);
            var shell = rt.gameObject.AddComponent<GaragePanelShell>();
            shell.m_Panel = panel;
            shell.m_Screen = screen;
            shell.m_Index = index;
            shell.m_Right = right;
            shell.m_Width = width;
            shell.m_Pad = pad;
            float height = 1080f - top - bottomMargin;
            shell.m_Height = height;
            if (right) SettingsUI.AtRight(rt, edge, top, width, height);
            else SettingsUI.At(rt, edge, top, width, height);
            shell.Build();
            return shell;
        }

        void Build()
        {
            m_Rect = (RectTransform)transform;
            m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.alpha = 0f;
            m_OpenTime = Time.unscaledTime;

            // Background: dense at the screen edge, feathering out toward the car.
            var bgRt = SettingsUI.Rect("Background", m_Rect);
            SettingsUI.Stretch(bgRt, m_Right ? -40f : -PanelBleed, -8f, m_Right ? -PanelBleed : -40f, -8f);
            var bg = bgRt.gameObject.AddComponent<PlayGradient>();
            bg.raycastTarget = true;
            bg.axis = PlayGradient.Axis.Horizontal;
            bg.SetStopCount(4);
            Color g = SettingsTheme.Graphite;
            // The feather lives in the 40 px outside the panel edge; behind the rows the panel is near-opaque so bright
            // livery passing behind it never competes with the text.
            float edge = 40f / (40f + m_Width + PanelBleed);
            float settle = edge + 48f / (40f + m_Width + PanelBleed);
            if (m_Right)
            {
                bg.SetStop(0, 0f, SettingsTheme.A(g, 0f));
                bg.SetStop(1, edge, SettingsTheme.A(g, 0.78f));
                bg.SetStop(2, settle, SettingsTheme.A(g, 0.93f));
                bg.SetStop(3, 1f, SettingsTheme.A(g, 0.95f));
            }
            else
            {
                bg.SetStop(0, 0f, SettingsTheme.A(g, 0.95f));
                bg.SetStop(1, 1f - settle, SettingsTheme.A(g, 0.93f));
                bg.SetStop(2, 1f - edge, SettingsTheme.A(g, 0.78f));
                bg.SetStop(3, 1f, SettingsTheme.A(g, 0f));
            }

            // Inner edge rule toward the car, with an accent at the top.
            var rule = SettingsUI.Shape(m_Rect, "EdgeRule", SettingsTheme.A(SettingsTheme.Hairline, 0.9f));
            var rrt = rule.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(m_Right ? 0f : 1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -4f);
            rrt.sizeDelta = new Vector2(1f, m_Height - 8f);
            var accent = SettingsUI.Shape(m_Rect, "EdgeAccent", SettingsTheme.Accent);
            var art = accent.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(m_Right ? 0f : 1f, 1f);
            art.pivot = new Vector2(0.5f, 1f);
            art.anchoredPosition = new Vector2(0f, -4f);
            art.sizeDelta = new Vector2(2f, 46f);

            // Header.
            var index = GarageTheme.Text(m_Rect, "Index", (m_Index + 1).ToString("00") + " / 08", SettingsTheme.Data, 11f, SettingsTheme.Muted);
            SettingsUI.At(index.rectTransform, m_Pad, 10f, 200f, 16f);
            var iconRt = SettingsUI.Rect("Icon", m_Rect);
            SettingsUI.At(iconRt, m_Pad, 36f, 26f, 26f);
            var icon = iconRt.gameObject.AddComponent<GarageIcon>();
            icon.kind = m_Panel.Icon;
            icon.thickness = 1.6f;
            icon.color = SettingsTheme.AccentLight;
            icon.raycastTarget = false;
            var title = GarageTheme.Text(m_Rect, "Title", m_Panel.Title, SettingsTheme.Headline, 30f, SettingsTheme.White, 1f);
            title.fontStyle = FontStyles.UpperCase;
            SettingsUI.At(title.rectTransform, m_Pad + 40f, 30f, m_Width - m_Pad * 2f - 40f, 38f);
            var desc = SettingsUI.Paragraph(m_Rect, "Description", m_Panel.Description, SettingsTheme.Body, 13f, SettingsTheme.Text2, 2f);
            float inner = m_Width - m_Pad * 2f;
            float dh = Mathf.Ceil(desc.GetPreferredValues(m_Panel.Description, inner, 200f).y);
            SettingsUI.At(desc.rectTransform, m_Pad, 76f, inner, dh);
            var effect = GarageTheme.Text(m_Rect, "Effect", m_Panel.Effect, SettingsTheme.Label, 9.5f, SettingsTheme.AccentLight, 18f);
            SettingsUI.At(effect.rectTransform, m_Pad, 84f + dh, inner, 14f);
            float headerBottom = 84f + dh + 30f;

            // Content area.
            m_Viewport = SettingsUI.Rect("Viewport", m_Rect);
            SettingsUI.At(m_Viewport, m_Pad, headerBottom, inner + 24f, m_Height - headerBottom - FooterHeight - 6f);
            m_Viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-14f, 0f, 0f, 0f);
            m_ViewHeight = m_Viewport.sizeDelta.y;
            BuildContent();

            // Scroll thumb.
            var thumb = SettingsUI.Shape(m_Rect, "ScrollThumb", SettingsTheme.A(SettingsTheme.Metallic, 0.5f));
            m_Thumb = thumb.rectTransform;
            SettingsUI.At(m_Thumb, m_Right ? 12f : m_Width - 14f, headerBottom, 2f, 40f);

            // Footer.
            var footRule = SettingsUI.Shape(m_Rect, "FooterRule", SettingsTheme.A(SettingsTheme.Hairline, 0.9f));
            SettingsUI.At(footRule.rectTransform, m_Pad, m_Height - FooterHeight, inner, 1f);
            m_Back = GarageButton.Create(m_Rect, "BACK  ·  ESC", 170f, () => m_Screen.ClosePanel(), false, false, GarageEffect.Spec, null, 38f);
            SettingsUI.At((RectTransform)m_Back.transform, m_Pad, m_Height - FooterHeight + 12f, 170f, 38f);
            if (m_Panel.HasReset)
            {
                m_Reset = GarageButton.Create(m_Rect, "RESET THIS SYSTEM", 220f, () => { m_Panel.ResetCategory(); m_Panel.Refresh(); }, false, true, GarageEffect.Live, GarageIconKind.Reset, 38f);
                SettingsUI.At((RectTransform)m_Reset.transform, m_Width - m_Pad - 220f, m_Height - FooterHeight + 12f, 220f, 38f);
            }
            LinkFooter();
        }

        // The background runs out to the screen edge, behind the icon rail.
        const float PanelBleed = 96f;

        void BuildContent()
        {
            m_Content = SettingsUI.Rect("Content", m_Viewport);
            m_Content.anchorMin = m_Content.anchorMax = new Vector2(0f, 1f);
            m_Content.pivot = new Vector2(0f, 1f);
            m_Content.anchoredPosition = Vector2.zero;
            m_Content.sizeDelta = new Vector2(m_Width - m_Pad * 2f, 10f);
            m_Panel.stack = new GarageStack(m_Content, m_Width - m_Pad * 2f);
            m_Panel.Build();
            m_ContentHeight = m_Panel.stack.y;
            m_Content.sizeDelta = new Vector2(m_Content.sizeDelta.x, m_ContentHeight);
            m_Rows.Clear();
            foreach (var row in m_Panel.stack.rows)
            {
                if (row == null) continue;
                var cg = row.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                m_Rows.Add(cg);
            }
            foreach (var c in m_Panel.stack.controls)
            {
                if (c == null) continue;
                c.focused = OnControlFocused;
            }
        }

        void LinkFooter()
        {
            var controls = m_Panel.stack.controls;
            Selectable last = controls.Count > 0 ? controls[controls.Count - 1] : null;
            m_Panel.stack.LinkNavigation();
            if (last != null)
            {
                var nav = last.navigation;
                nav.selectOnDown = m_Back;
                last.navigation = nav;
            }
            var back = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnRight = m_Reset };
            m_Back.navigation = back;
            m_Back.focused = OnControlFocused;
            if (m_Reset != null)
            {
                m_Reset.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnLeft = m_Back };
                m_Reset.focused = OnControlFocused;
            }
        }

        void OnControlFocused(GarageControl c)
        {
            m_Screen.OnControlFocused(c);
            EnsureVisible(c);
        }

        public void FocusFirst()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var controls = m_Panel.stack.controls;
            Selectable first = controls.Count > 0 ? (Selectable)controls[0] : m_Back;
            es.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        public void Hide()
        {
            m_Hiding = true;
            m_ShowT = 0f;
            m_Group.blocksRaycasts = false;
            m_Group.interactable = false;
        }

        /// <summary>Rebuilds the content (after a list changed), keeping the focused row and scroll position.</summary>
        public void Rebuild()
        {
            var es = EventSystem.current;
            int focus = -1;
            var controls = m_Panel.stack.controls;
            if (es != null && es.currentSelectedGameObject != null)
                for (int i = 0; i < controls.Count; i++) if (controls[i] != null && controls[i].gameObject == es.currentSelectedGameObject) focus = i;
            Destroy(m_Content.gameObject);
            m_Content = null;
            BuildContent();
            foreach (var row in m_Rows) row.alpha = 1f;
            LinkFooter();
            m_Panel.Refresh();
            var now = m_Panel.stack.controls;
            if (es != null && now.Count > 0)
            {
                var target = now[Mathf.Clamp(focus < 0 ? 0 : focus, 0, now.Count - 1)];
                es.SetSelectedGameObject(target.gameObject);
            }
            m_ScrollT = Mathf.Clamp(m_ScrollT, 0f, Mathf.Max(0f, m_ContentHeight - m_ViewHeight));
        }

        void EnsureVisible(GarageControl c)
        {
            if (c == null || m_Content == null || !c.transform.IsChildOf(m_Content)) return;
            var rt = (RectTransform)c.transform;
            float top = -rt.anchoredPosition.y;
            float bottom = top + rt.sizeDelta.y;
            float margin = 40f;
            if (top - margin < m_ScrollT) m_ScrollT = Mathf.Max(0f, top - margin);
            else if (bottom + margin > m_ScrollT + m_ViewHeight) m_ScrollT = Mathf.Min(Mathf.Max(0f, m_ContentHeight - m_ViewHeight), bottom + margin - m_ViewHeight);
        }

        public void OnScroll(PointerEventData eventData)
        {
            float max = Mathf.Max(0f, m_ContentHeight - m_ViewHeight);
            m_ScrollT = Mathf.Clamp(m_ScrollT - Mathf.Sign(eventData.scrollDelta.y) * 90f, 0f, max);
        }

        /// <summary>Point on the panel's inner edge level with a control (root space, centre pivot).</summary>
        public Vector2 EdgePoint(GarageControl control, RectTransform root)
        {
            float y;
            if (control != null && m_Content != null && control.transform.IsChildOf(m_Content))
            {
                var rt = (RectTransform)control.transform;
                Vector3 world = rt.TransformPoint(new Vector3(rt.rect.center.x, rt.rect.center.y, 0f));
                y = root.InverseTransformPoint(world).y;
                Vector3 vpTop = m_Viewport.TransformPoint(new Vector3(0f, m_Viewport.rect.yMax, 0f));
                Vector3 vpBottom = m_Viewport.TransformPoint(new Vector3(0f, m_Viewport.rect.yMin, 0f));
                y = Mathf.Clamp(y, root.InverseTransformPoint(vpBottom).y, root.InverseTransformPoint(vpTop).y);
            }
            else
            {
                y = root.InverseTransformPoint(m_Rect.TransformPoint(new Vector3(0f, m_Rect.rect.yMax - 50f, 0f))).y;
            }
            float edgeX = m_Right ? m_Rect.rect.xMin : m_Rect.rect.xMax;
            float x = root.InverseTransformPoint(m_Rect.TransformPoint(new Vector3(edgeX, 0f, 0f))).x;
            return new Vector2(x, y);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = Mathf.Max(0.01f, AccertoneMotion.Scale);
            m_Show = Mathf.SmoothDamp(m_Show, m_ShowT, ref m_ShowVel, (m_Hiding ? 0.1f : 0.2f) * k, Mathf.Infinity, dt);
            if (AccertoneMotion.Scale <= 0.01f) m_Show = m_ShowT;
            m_Group.alpha = Mathf.Clamp01(m_Show * 1.2f);
            float dir = m_Right ? 1f : -1f;
            var pos = m_Rect.anchoredPosition;
            m_Rect.anchoredPosition = new Vector2(BaseX() + dir * 44f * (1f - m_Show), pos.y);
            if (m_Hiding && m_Show < 0.02f) { Destroy(gameObject); return; }

            // Rows arrive in a quick stagger.
            float t = (Time.unscaledTime - m_OpenTime) / k;
            for (int i = 0; i < m_Rows.Count; i++)
            {
                var row = m_Rows[i];
                if (row == null) continue;
                if (row.alpha >= 1f) continue;
                row.alpha = Mathf.Clamp01((t - 0.08f - i * 0.022f) / 0.22f);
                if (AccertoneMotion.Scale <= 0.01f) row.alpha = 1f;
            }

            // Scroll.
            float max = Mathf.Max(0f, m_ContentHeight - m_ViewHeight);
            m_ScrollT = Mathf.Clamp(m_ScrollT, 0f, max);
            m_Scroll = Mathf.Lerp(m_Scroll, m_ScrollT, GarageTheme.Approach(14f));
            if (m_Content != null) m_Content.anchoredPosition = new Vector2(0f, m_Scroll);
            bool scrolls = max > 1f;
            m_Thumb.gameObject.SetActive(scrolls);
            if (scrolls)
            {
                float h = Mathf.Max(30f, m_ViewHeight * m_ViewHeight / m_ContentHeight);
                float y = (m_ViewHeight - h) * (m_Scroll / max);
                var viewTop = -m_Viewport.anchoredPosition.y;
                m_Thumb.anchoredPosition = new Vector2(m_Thumb.anchoredPosition.x, -(viewTop + y));
                m_Thumb.sizeDelta = new Vector2(2f, h);
            }
        }

        float m_BaseX = float.NaN;

        float BaseX()
        {
            if (float.IsNaN(m_BaseX)) m_BaseX = m_Rect.anchoredPosition.x;
            return m_BaseX;
        }
    }
}
