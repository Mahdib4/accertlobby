using System;
using Accertone.Branding;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// Interactive presentation card for a game mode in the Play selection screen.
    /// Features reference cover art, chamfered framing, subtle darkening scrim,
    /// discipline chip, and violet hover state transitions.
    /// </summary>
    public class PlayModeCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public PlayModeType modeType;
        public Sprite coverSprite;
        public string modeNumber = "01";
        public string disciplineTag = "SOLO // AI GRID";
        public string title = "QUICK MATCH";
        public string subtitle = "Play with AI";
        public string description = "Instant Grand Prix against a full 20-car AI field.";
        public bool isFeatured = false;

        public Action<PlayModeType> OnCardClicked;

        private Image m_CoverImage;
        private Image m_ScrimOverlay;
        private AccertoneShape m_BorderShape;
        private RectTransform m_ContentRoot;
        private TMP_Text m_TitleText;
        private TMP_Text m_DisciplineText;
        private TMP_Text m_SubtitleText;
        private TMP_Text m_DescriptionText;

        private bool m_IsHovered = false;
        private float m_HoverProgress = 0f;

        public void Setup(PlayModeType type, Sprite sprite, string number, string tag, string titleText, string subText, string desc, bool featured, Action<PlayModeType> clickCallback)
        {
            modeType = type;
            coverSprite = sprite;
            modeNumber = number;
            disciplineTag = tag;
            title = titleText;
            subtitle = subText;
            description = desc;
            isFeatured = featured;
            OnCardClicked = clickCallback;

            BuildCardUI();
        }

        private void BuildCardUI()
        {
            var cardRt = (RectTransform)transform;

            // Background Mask / Base
            var mask = gameObject.GetComponent<Mask>();
            if (mask == null) mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var baseShape = gameObject.GetComponent<AccertoneShape>();
            if (baseShape == null) baseShape = gameObject.AddComponent<AccertoneShape>();
            baseShape.color = new Color(0.06f, 0.07f, 0.09f, 1f);
            baseShape.chamfer = 12f;
            baseShape.corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft;
            baseShape.raycastTarget = true;

            // 1. Cover Art Image
            var imgGo = new GameObject("CoverImage", typeof(RectTransform));
            imgGo.transform.SetParent(transform, false);
            var imgRt = (RectTransform)imgGo.transform;
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = Vector2.zero;

            m_CoverImage = imgGo.AddComponent<Image>();
            m_CoverImage.sprite = coverSprite;
            m_CoverImage.preserveAspect = false; // Filled nicely
            m_CoverImage.raycastTarget = false;
            m_CoverImage.color = new Color(0.85f, 0.88f, 0.92f, 1.0f);

            // 2. Darkening Scrim Gradient (ensures text readability while keeping motorsport artwork vivid)
            var scrimGo = new GameObject("Scrim", typeof(RectTransform));
            scrimGo.transform.SetParent(transform, false);
            var scrimRt = (RectTransform)scrimGo.transform;
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.sizeDelta = Vector2.zero;

            m_ScrimOverlay = scrimGo.AddComponent<Image>();
            m_ScrimOverlay.color = new Color(0.04f, 0.05f, 0.07f, 0.68f);
            m_ScrimOverlay.raycastTarget = false;

            // 3. Chamfered Outer Border with violet reactive highlight
            var borderGo = new GameObject("BorderOutline", typeof(RectTransform));
            borderGo.transform.SetParent(transform, false);
            var borderRt = (RectTransform)borderGo.transform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = Vector2.zero;

            m_BorderShape = borderGo.AddComponent<AccertoneShape>();
            m_BorderShape.color = new Color(0.18f, 0.20f, 0.24f, 1f);
            m_BorderShape.chamfer = 12f;
            m_BorderShape.corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft;
            m_BorderShape.outline = true;
            m_BorderShape.outlineWidth = isFeatured ? 2f : 1.5f;
            m_BorderShape.raycastTarget = false;

            // 4. Content Area
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(transform, false);
            m_ContentRoot = (RectTransform)contentGo.transform;
            m_ContentRoot.anchorMin = Vector2.zero;
            m_ContentRoot.anchorMax = Vector2.one;
            m_ContentRoot.sizeDelta = new Vector2(-28f, -28f);

            // Tag Chip (e.g. "01 // SOLO AI GRID")
            m_DisciplineText = AccertoneUIFactory.CreateText(m_ContentRoot, "Discipline", modeNumber + "  ·  " + disciplineTag.ToUpperInvariant(), BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var discRt = (RectTransform)m_DisciplineText.transform;
            discRt.anchorMin = new Vector2(0f, 1f);
            discRt.anchorMax = new Vector2(1f, 1f);
            discRt.pivot = new Vector2(0f, 1f);
            discRt.anchoredPosition = new Vector2(0f, 0f);
            discRt.sizeDelta = new Vector2(0f, 20f);
            m_DisciplineText.fontSize = 11f;
            m_DisciplineText.characterSpacing = 8f;

            // Title
            m_TitleText = AccertoneUIFactory.CreateText(m_ContentRoot, "Title", title.ToUpperInvariant(), BrandTextRole.Headline, Color.white);
            var titleRt = (RectTransform)m_TitleText.transform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 0f);
            titleRt.pivot = new Vector2(0f, 0f);
            titleRt.anchoredPosition = new Vector2(0f, 62f);
            titleRt.sizeDelta = new Vector2(0f, 32f);
            m_TitleText.fontSize = isFeatured ? 24f : 19f;
            m_TitleText.textWrappingMode = TextWrappingModes.NoWrap;

            // Subtitle
            m_SubtitleText = AccertoneUIFactory.CreateText(m_ContentRoot, "Subtitle", subtitle.ToUpperInvariant(), BrandTextRole.Label, new Color(0.75f, 0.26f, 1f));
            var subRt = (RectTransform)m_SubtitleText.transform;
            subRt.anchorMin = new Vector2(0f, 0f);
            subRt.anchorMax = new Vector2(1f, 0f);
            subRt.pivot = new Vector2(0f, 0f);
            subRt.anchoredPosition = new Vector2(0f, 40f);
            subRt.sizeDelta = new Vector2(0f, 20f);
            m_SubtitleText.fontSize = 12f;
            m_SubtitleText.textWrappingMode = TextWrappingModes.NoWrap;

            // Description
            m_DescriptionText = AccertoneUIFactory.CreateText(m_ContentRoot, "Description", description, BrandTextRole.Body, new Color(0.68f, 0.72f, 0.78f));
            var descRt = (RectTransform)m_DescriptionText.transform;
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 0f);
            descRt.pivot = new Vector2(0f, 0f);
            descRt.anchoredPosition = new Vector2(0f, 0f);
            descRt.sizeDelta = new Vector2(0f, 36f);
            m_DescriptionText.fontSize = 11f;
            m_DescriptionText.textWrappingMode = TextWrappingModes.Normal;
        }

        private void Update()
        {
            float target = m_IsHovered ? 1f : 0f;
            m_HoverProgress = Mathf.MoveTowards(m_HoverProgress, target, Time.unscaledDeltaTime * 6f);
            float h = AccertoneEase.OutCubic(m_HoverProgress);

            if (m_BorderShape != null)
            {
                Color normalBorder = isFeatured ? new Color(0.35f, 0.2f, 0.5f, 1f) : new Color(0.18f, 0.20f, 0.24f, 1f);
                Color accentBorder = new Color(0.75f, 0.26f, 1f, 1f); // #C042FF
                m_BorderShape.color = Color.Lerp(normalBorder, accentBorder, h);
                m_BorderShape.outlineWidth = Mathf.Lerp(isFeatured ? 2f : 1.5f, 2.5f, h);
            }

            if (m_ScrimOverlay != null)
            {
                m_ScrimOverlay.color = Color.Lerp(new Color(0.04f, 0.05f, 0.07f, 0.68f), new Color(0.04f, 0.05f, 0.07f, 0.45f), h);
            }

            if (m_CoverImage != null)
            {
                m_CoverImage.color = Color.Lerp(new Color(0.85f, 0.88f, 0.92f, 1f), Color.white, h);
            }

            if (m_TitleText != null)
            {
                m_TitleText.color = Color.Lerp(new Color(0.95f, 0.96f, 0.98f), Color.white, h);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_IsHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnCardClicked?.Invoke(modeType);
        }
    }
}
