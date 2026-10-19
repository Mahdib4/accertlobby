using System;
using Accertone.Settings;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Garage
{
    /// <summary>
    /// One of the eight Garage systems around the car. Four sit on each side; text faces the car.
    /// States are springs, so hover, focus, selection and the collapse into the edge rail (while a panel is open)
    /// blend smoothly in any order:
    /// <list type="bullet">
    /// <item>hover / focus: the item glides toward the car, its icon frame lights up, the accent line grows toward the car;</item>
    /// <item>active: an accent bar on the outer edge, solid icon frame;</item>
    /// <item>rail: text folds away and the icon docks at the screen edge so the categories stay one click away.</item>
    /// </list>
    /// </summary>
    public class GarageNavItem : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public const float Width = 400f, Height = 64f, FrameSize = 56f, RailFrame = 46f, LineStart = 352f;

        public int index;
        public bool rightSide;
        public Action<GarageNavItem> activated, focusedItem;

        RectTransform m_Rect, m_Content, m_Frame, m_Line, m_Tip, m_ActiveBar;
        AccertoneShape m_FrameFill, m_FrameBorder, m_LineShape, m_ActiveShape;
        Image m_TipImage;
        GarageIcon m_Icon;
        TextMeshProUGUI m_Index, m_Title, m_Status;
        CanvasGroup m_Group, m_TextGroup;

        float m_Hover, m_HoverT, m_Active, m_ActiveT, m_Rail, m_RailT, m_Show, m_ShowT, m_ShowDelayUntil;
        float m_HoverV, m_ActiveV, m_RailV, m_ShowV;
        Vector2 m_FullPos, m_RailPos;

        public bool Hovered { get { return m_HoverT > 0.5f; } }
        public string Title { get { return m_Title != null ? m_Title.text : ""; } }

        public static GarageNavItem Create(RectTransform parent, int index, bool rightSide, string title, GarageIconKind icon)
        {
            var rt = SettingsUI.Rect("Item_" + index, parent);
            var item = rt.gameObject.AddComponent<GarageNavItem>();
            item.index = index;
            item.rightSide = rightSide;
            item.Build(title, icon);
            return item;
        }

        void Build(string title, GarageIconKind iconKind)
        {
            m_Rect = (RectTransform)transform;
            m_Rect.anchorMin = m_Rect.anchorMax = new Vector2(rightSide ? 1f : 0f, 1f);
            m_Rect.pivot = new Vector2(rightSide ? 1f : 0f, 1f);
            m_Rect.sizeDelta = new Vector2(Width, Height);
            transition = Transition.None;
            m_Group = gameObject.AddComponent<CanvasGroup>();

            var hit = SettingsUI.Fill(transform, "Hit", new Color(0f, 0f, 0f, 0f), true);
            SettingsUI.Stretch(hit.rectTransform, -8f, -10f, -8f, -10f);
            targetGraphic = hit;

            m_Content = SettingsUI.Rect("Content", transform);
            SettingsUI.Stretch(m_Content);

            // Accent bar on the outer edge (active).
            m_ActiveShape = SettingsUI.Shape(m_Content, "ActiveBar", SettingsTheme.Accent);
            m_ActiveBar = m_ActiveShape.rectTransform;
            Place(m_ActiveBar, -16f, 4f, 3f, FrameSize);

            // Icon frame.
            m_Frame = SettingsUI.Rect("Frame", m_Content);
            Place(m_Frame, 0f, 4f, FrameSize, FrameSize);
            m_FrameFill = SettingsUI.Shape(m_Frame, "Fill", SettingsTheme.A(SettingsTheme.Carbon, 0.55f), 9f, rightSide ? ChamferCorners.TopLeft | ChamferCorners.BottomRight : ChamferCorners.TopRight | ChamferCorners.BottomLeft);
            SettingsUI.Stretch(m_FrameFill.rectTransform);
            m_FrameBorder = SettingsUI.Shape(m_Frame, "Border", SettingsTheme.A(SettingsTheme.Metallic, 0.35f), 9f, rightSide ? ChamferCorners.TopLeft | ChamferCorners.BottomRight : ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, true, 1f);
            SettingsUI.Stretch(m_FrameBorder.rectTransform);
            var iconRt = SettingsUI.Rect("Icon", m_Frame);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(28f, 28f);
            m_Icon = iconRt.gameObject.AddComponent<GarageIcon>();
            m_Icon.kind = iconKind;
            m_Icon.thickness = 1.6f;
            m_Icon.color = SettingsTheme.Silver;
            m_Icon.raycastTarget = false;

            // Text block (faces the car).
            var textRt = SettingsUI.Rect("Text", m_Content);
            SettingsUI.Stretch(textRt);
            m_TextGroup = textRt.gameObject.AddComponent<CanvasGroup>();
            var align = rightSide ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            m_Index = GarageTheme.Text(textRt, "Index", (index + 1).ToString("00"), SettingsTheme.Data, 10f, SettingsTheme.Muted, 0f, align);
            Place(m_Index.rectTransform, 76f, 2f, 200f, 14f);
            m_Title = GarageTheme.Text(textRt, "Title", title, SettingsTheme.Title, 18f, SettingsTheme.White, 4f, align);
            m_Title.fontStyle = FontStyles.UpperCase;
            Place(m_Title.rectTransform, 76f, 16f, 270f, 26f);
            m_Status = GarageTheme.Text(textRt, "Status", "", SettingsTheme.Label, 10f, SettingsTheme.Muted, 18f, align);
            Place(m_Status.rectTransform, 76f, 44f, 268f, 16f);
            m_Status.overflowMode = TextOverflowModes.Ellipsis;

            // Accent line toward the car, with a diamond tip.
            m_LineShape = SettingsUI.Shape(m_Content, "Line", SettingsTheme.Accent);
            m_Line = m_LineShape.rectTransform;
            m_LineShape.alphaLeft = rightSide ? 0f : 1f;
            m_LineShape.alphaRight = rightSide ? 1f : 0f;
            m_TipImage = SettingsUI.Diamond(m_Content, "Tip", SettingsTheme.AccentLight, 6f);
            m_Tip = m_TipImage.rectTransform;
            m_Tip.anchorMin = m_Tip.anchorMax = new Vector2(rightSide ? 1f : 0f, 1f);

            m_ShowT = 0f;
            m_Show = 0f;
            ApplyVisuals();
        }

        /// <summary>Places a child by its top-left corner, mirrored for right-side items.</summary>
        void Place(RectTransform rt, float x, float y, float w, float h)
        {
            if (rightSide) SettingsUI.AtRight(rt, x, y, w, h);
            else SettingsUI.At(rt, x, y, w, h);
        }

        public void SetPositions(Vector2 full, Vector2 rail)
        {
            m_FullPos = full;
            m_RailPos = rail;
        }

        public void SetStatus(string status)
        {
            if (m_Status.text != status) m_Status.text = status;
        }

        public void SetActiveState(bool on) { m_ActiveT = on ? 1f : 0f; }
        public void SetRail(bool on) { m_RailT = on ? 1f : 0f; }
        public bool InRail { get { return m_RailT > 0.5f; } }

        public void Show(float delay)
        {
            m_ShowT = 1f;
            m_ShowDelayUntil = Time.unscaledTime + AccertoneMotion.Duration(delay);
        }

        public void Hide(float delay)
        {
            m_ShowT = 0f;
            m_ShowDelayUntil = Time.unscaledTime + AccertoneMotion.Duration(delay);
        }

        public void Snap(bool shown)
        {
            m_Show = m_ShowT = shown ? 1f : 0f;
            m_Rail = m_RailT;
            m_Hover = m_HoverT;
            m_Active = m_ActiveT;
            m_HoverV = m_ActiveV = m_RailV = m_ShowV = 0f;
            ApplyVisuals();
        }

        float LineLength()
        {
            return (26f + 96f * m_Hover + 34f * m_Active) * (1f - m_Rail) * m_Show;
        }

        // ---------------------------------------------------------------------------------------------- input
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
            m_HoverT = 1f;
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject) m_HoverT = 0f;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            m_HoverT = 1f;
            GarageTheme.PlaySound(UiSound.Move);
            if (focusedItem != null) focusedItem(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            m_HoverT = 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && activated != null) activated(this);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (activated != null) activated(this);
        }

        // ---------------------------------------------------------------------------------------------- frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = Mathf.Max(0.01f, AccertoneMotion.Scale);
            m_Hover = Mathf.SmoothDamp(m_Hover, m_HoverT, ref m_HoverV, 0.11f * k, Mathf.Infinity, dt);
            m_Active = Mathf.SmoothDamp(m_Active, m_ActiveT, ref m_ActiveV, 0.16f * k, Mathf.Infinity, dt);
            m_Rail = Mathf.SmoothDamp(m_Rail, m_RailT, ref m_RailV, 0.24f * k, Mathf.Infinity, dt);
            if (Time.unscaledTime >= m_ShowDelayUntil)
                m_Show = Mathf.SmoothDamp(m_Show, m_ShowT, ref m_ShowV, (m_ShowT > 0.5f ? 0.22f : 0.12f) * k, Mathf.Infinity, dt);
            if (AccertoneMotion.Scale <= 0.01f) { m_Hover = m_HoverT; m_Active = m_ActiveT; m_Rail = m_RailT; if (Time.unscaledTime >= m_ShowDelayUntil) m_Show = m_ShowT; }
            ApplyVisuals();
        }

        void ApplyVisuals()
        {
            if (m_Rect == null) return;
            float dir = rightSide ? -1f : 1f;
            // Position: full column <-> edge rail, sliding in from the outer edge on show.
            Vector2 pos = Vector2.Lerp(m_FullPos, m_RailPos, Ease(m_Rail));
            pos.x += dir * -46f * (1f - m_Show);
            m_Rect.anchoredPosition = new Vector2(rightSide ? -pos.x : pos.x, -pos.y);
            m_Group.alpha = Mathf.Clamp01(m_Show * 1.15f);
            m_Group.blocksRaycasts = m_Show > 0.6f;
            interactable = m_Show > 0.6f;

            // Hover glide toward the car (not in the rail).
            float glide = 14f * m_Hover * (1f - m_Rail);
            m_Content.anchoredPosition = new Vector2(dir * glide, 0f);

            // Icon frame: lights up on hover, solid on active; smaller in the rail.
            float frame = Mathf.Lerp(FrameSize, RailFrame, Ease(m_Rail));
            Place(m_Frame, 0f, 4f + (FrameSize - frame) * 0.5f, frame, frame);
            float lit = Mathf.Max(m_Hover, m_Active);
            m_FrameFill.color = Color.Lerp(SettingsTheme.A(SettingsTheme.Carbon, 0.55f), SettingsTheme.A(SettingsTheme.AccentDeep, 0.85f), Mathf.Max(m_Active, m_Hover * 0.45f));
            m_FrameBorder.color = Color.Lerp(SettingsTheme.A(SettingsTheme.Metallic, 0.35f), SettingsTheme.Accent, lit);
            m_FrameBorder.outlineWidth = 1f + lit * 0.6f;
            m_Icon.color = Color.Lerp(SettingsTheme.Silver, m_Active > 0.5f ? SettingsTheme.White : SettingsTheme.AccentLight, lit);
            m_Icon.rectTransform.localScale = Vector3.one * (1f + 0.08f * m_Hover);

            // Text.
            m_TextGroup.alpha = (1f - Mathf.Clamp01(m_Rail * 1.6f));
            m_Title.canvasRenderer.SetColor(Color.Lerp(new Color(0.86f, 0.88f, 0.9f, 1f), m_Active > 0.5f ? new Color(0.84f, 0.55f, 1f, 1f) : Color.white, Mathf.Max(lit, 0.0f)));
            m_Status.canvasRenderer.SetColor(Color.Lerp(Color.white, new Color(1.6f, 1.6f, 1.6f, 1f), m_Hover));
            m_Index.canvasRenderer.SetColor(Color.Lerp(Color.white, new Color(2.2f, 1.2f, 2.6f, 1f), lit));

            // Accent line from the column's inner edge toward the car.
            float len = LineLength();
            float lineY = 4f + FrameSize * 0.5f - 0.5f;
            Place(m_Line, LineStart, lineY, len, 1f);
            m_LineShape.canvasRenderer.SetAlpha(0.35f + 0.65f * Mathf.Max(m_Hover, m_Active));
            m_Tip.anchoredPosition = new Vector2(dir * (LineStart + len), -lineY - 0.5f);
            m_TipImage.canvasRenderer.SetAlpha(Mathf.Clamp01(len / 30f) * (0.4f + 0.6f * lit));

            // Active bar on the outer edge.
            Place(m_ActiveBar, -16f, 4f, 3f, frame);
            m_ActiveShape.canvasRenderer.SetAlpha(m_Active);
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Canvas-space point (relative to the column parent's top corner) where the accent line ends.</summary>
        public Vector3 LineEndWorld()
        {
            return m_Tip.position;
        }

        public Vector3 FrameCentreWorld()
        {
            return m_Frame.TransformPoint(m_Frame.rect.center);
        }
    }
}
