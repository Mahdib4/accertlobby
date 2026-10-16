using System;
using Accertone.Settings;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.Play
{
    /// <summary>
    /// One game-mode card in the Play selection row.
    ///
    /// Layers (back to front): contact shadow, violet glow, then the chamfered card body which masks the
    /// cover-cropped artwork, a grading scrim, a violet hover tint, a light sweep and the confirm flash.
    /// Type floats in front of the art. Every layer sits at its own depth and is run through a real
    /// perspective projection, so the pointer-driven tilt reads as 3D on the orthographic overlay canvas.
    ///
    /// All motion is spring- or tween-driven from unscaled time, interruptible without snapping,
    /// staggered per layer and allocation-free per frame.
    /// </summary>
    public class PlayModeCard : Selectable, IPointerClickHandler, ISubmitHandler, IPointerMoveHandler
    {
        public PlayModeType modeType;
        public Sprite coverSprite;
        public string modeNumber = "01";
        public string disciplineTag = "SOLO // AI GRID";
        public string title = "QUICK MATCH";
        public string subtitle = "Play with AI";
        public string description = "Instant Grand Prix against a full 20-car AI field.";
        [Tooltip("Kept for compatibility. Every card now shares one size and aspect ratio.")]
        public bool isFeatured;
        [Tooltip("Point of interest in the artwork (0..1, origin bottom-left) the crop is framed on.")]
        public Vector2 artFocus = new Vector2(0.5f, 0.5f);

        public Action<PlayModeType> OnCardClicked;

        /// <summary>The selection screen that coordinates focus, dimming and confirmation.</summary>
        public PlayModeSelectionUI Owner { get; set; }
        public int Index { get; set; }
        public bool PointerInside { get { return m_PointerInside; } }
        public bool SelectedByPointer { get; set; }

        // ------------------------------------------------------------------ look
        static readonly Color Graphite = SettingsTheme.Graphite;
        static readonly Color IdleArt = new Color(0.66f, 0.67f, 0.74f, 1f);   // cool, unified grade at rest
        const float Focal = 1500f;          // perspective focal length (px)
        const float MaxYaw = 7f;            // degrees
        const float MaxPitch = 5f;

        // Layer depths (px, positive = toward the viewer).
        const float DepthShadow = -36f;
        const float DepthGlow = -6f;
        const float DepthArt = -26f;
        const float DepthSurface = 0f;
        const float DepthRail = 6f;
        const float DepthTag = 8f;
        const float DepthDesc = 12f;
        const float DepthSubtitle = 15f;
        const float DepthTitle = 20f;
        const float DepthNumber = 14f;
        const float DepthMark = 10f;

        // ------------------------------------------------------------------ parts
        RectTransform m_Rect;
        RectTransform m_Body;
        CanvasGroup m_Group;
        PlaySoftRect m_Shadow;
        PlaySoftRect m_Glow;
        AccertoneShape m_BodyShape;
        PlayCardArt m_Art;
        PlayGradient m_Shade;
        PlayGradient m_Tint;
        PlayGradient m_Sheen;
        PlayGradient m_TopEdge;
        AccertoneShape m_Flash;
        AccertoneShape m_Border;
        AccertoneShape m_RailTrack;
        AccertoneShape m_RailFill;
        AccertoneShape m_MarkA;
        AccertoneShape m_MarkB;
        TextMeshProUGUI m_Number;
        TextMeshProUGUI m_Title;
        TextMeshProUGUI m_Subtitle;
        TextMeshProUGUI m_Description;
        TextMeshProUGUI m_Tag;
        TextMeshProUGUI m_CtaLabel;
        UIPolyline m_Chevron;
        RectTransform m_Cta;
        CanvasGroup m_CtaGroup;

        Graphic[] m_Projected;

        struct FloatLayer
        {
            public RectTransform rect;
            public Vector2 home;
            public Vector2 offset;
            public float depth;
        }

        const int FloatNumber = 0, FloatTitle = 1, FloatSubtitle = 2, FloatDesc = 3, FloatTag = 4, FloatCta = 5, FloatCount = 6;
        readonly FloatLayer[] m_Floats = new FloatLayer[FloatCount];

        // ------------------------------------------------------------------ state
        bool m_Built;
        bool m_PointerInside;
        bool m_Pressed;
        Camera m_EventCamera;
        Vector2 m_PointerScreen;
        Vector2 m_PolledScreen = new Vector2(float.NaN, float.NaN);
        Vector2 m_CardSize;
        float m_Phase;

        bool m_ActiveState;
        float m_StateTime;
        float m_TitleTarget, m_SubTarget, m_DescTarget, m_CtaTarget, m_RailTarget;

        PlaySpring m_Hover, m_GlowSpring, m_Zoom, m_Dim, m_Press, m_Yaw, m_Pitch;
        PlaySpring m_TitleCh, m_SubCh, m_DescCh, m_CtaCh, m_RailCh;
        PlayTween m_Presence;
        PlayTween m_SheenTween;
        PlayTween m_FlashTween;
        bool m_SheenRunning;
        bool m_FlashRunning;

        // Projection parameters (card space, origin at the card centre).
        bool m_HasProjection;
        float m_PScale = 1f, m_SinYaw, m_CosYaw = 1f, m_SinPitch, m_CosPitch = 1f;
        Vector2 m_POffset;
        float m_LastScale = -1f, m_LastYaw = 999f, m_LastPitch = 999f;
        Vector2 m_LastOffset = new Vector2(999f, 999f);

        public bool HasProjection { get { return m_HasProjection; } }
        public RectTransform ProjectionSpace { get { return m_Rect; } }
        public bool IsShown { get { return m_Presence.to > 0.5f; } }

        // ================================================================== setup
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

            transition = Transition.None;
            m_Phase = (int)type * 1.618f;
            BuildCardUI();
        }

        void BuildCardUI()
        {
            if (m_Built) return;
            m_Built = true;

            m_Rect = (RectTransform)transform;
            m_Rect.pivot = new Vector2(0.5f, 0.5f);

            // Each card batches on its own canvas so its animation never rebuilds the rest of the screen.
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            m_Group = gameObject.GetComponent<CanvasGroup>();
            if (m_Group == null) m_Group = gameObject.AddComponent<CanvasGroup>();

            // Behind the card.
            m_Shadow = Layer<PlaySoftRect>(m_Rect, "ContactShadow");
            m_Shadow.color = new Color(0f, 0f, 0f, 0.5f);
            m_Shadow.feather = 46f;
            m_Glow = Layer<PlaySoftRect>(m_Rect, "Glow");
            m_Glow.color = new Color(SettingsTheme.Accent.r, SettingsTheme.Accent.g, SettingsTheme.Accent.b, 0f);
            m_Glow.feather = 38f;

            // Card body: chamfered silhouette that also masks everything inside it.
            m_BodyShape = Layer<AccertoneShape>(m_Rect, "Body");
            m_Body = m_BodyShape.rectTransform;
            SettingsUI.Stretch(m_Body);
            m_BodyShape.color = Graphite;
            m_BodyShape.chamfer = 18f;
            m_BodyShape.corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft;
            m_BodyShape.raycastTarget = true;
            var mask = m_Body.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            m_Art = Layer<PlayCardArt>(m_Body, "Art");
            m_Art.sprite = coverSprite;
            m_Art.focus = artFocus;
            m_Art.color = IdleArt;
            m_Art.zoom = 1.04f;

            m_Shade = Layer<PlayGradient>(m_Body, "Shade");
            SettingsUI.Stretch(m_Shade.rectTransform);
            m_Shade.SetStopCount(5);
            m_Shade.SetStop(0, 0.00f, A(Graphite, 0.97f));
            m_Shade.SetStop(1, 0.30f, A(Graphite, 0.86f));
            m_Shade.SetStop(2, 0.56f, A(Graphite, 0.42f));
            m_Shade.SetStop(3, 0.80f, A(Graphite, 0.10f));
            m_Shade.SetStop(4, 1.00f, A(Graphite, 0.42f));

            m_Tint = Layer<PlayGradient>(m_Body, "HoverTint");
            SettingsUI.Stretch(m_Tint.rectTransform);
            m_Tint.SetStopCount(3);
            m_Tint.SetStop(0, 0f, A(SettingsTheme.AccentDeep, 0f));
            m_Tint.SetStop(1, 0.38f, A(SettingsTheme.Accent, 0f));
            m_Tint.SetStop(2, 0.66f, A(SettingsTheme.Accent, 0f));

            m_Sheen = Layer<PlayGradient>(m_Body, "LightSweep");
            m_Sheen.axis = PlayGradient.Axis.Horizontal;
            m_Sheen.SetStopCount(3);
            m_Sheen.SetStop(0, 0f, new Color(1f, 1f, 1f, 0f));
            m_Sheen.SetStop(1, 0.5f, new Color(1f, 1f, 1f, 0.12f));
            m_Sheen.SetStop(2, 1f, new Color(1f, 1f, 1f, 0f));
            m_Sheen.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            m_Sheen.canvasRenderer.SetAlpha(0f);

            m_Flash = Layer<AccertoneShape>(m_Body, "ConfirmFlash");
            SettingsUI.Stretch(m_Flash.rectTransform);
            m_Flash.color = SettingsTheme.AccentLight;
            m_Flash.canvasRenderer.SetAlpha(0f);

            m_TopEdge = Layer<PlayGradient>(m_Body, "TopEdge");
            m_TopEdge.axis = PlayGradient.Axis.Horizontal;
            m_TopEdge.SetStopCount(3);
            m_TopEdge.SetStop(0, 0f, A(SettingsTheme.Silver, 0f));
            m_TopEdge.SetStop(1, 0.45f, A(SettingsTheme.Silver, 0.22f));
            m_TopEdge.SetStop(2, 1f, A(SettingsTheme.Silver, 0f));

            // Top-right brand slashes.
            m_MarkA = Layer<AccertoneShape>(m_Body, "MarkA");
            m_MarkB = Layer<AccertoneShape>(m_Body, "MarkB");
            m_MarkA.color = m_MarkB.color = SettingsTheme.Accent;

            // Bottom rail: hairline track with a violet fill that runs out on hover.
            m_RailTrack = Layer<AccertoneShape>(m_Body, "RailTrack");
            m_RailTrack.color = A(SettingsTheme.Hairline, 0.95f);
            m_RailFill = Layer<AccertoneShape>(m_Body, "RailFill");
            m_RailFill.color = SettingsTheme.Accent;

            // Typography.
            m_Number = Label(m_Body, "Number", modeNumber, SettingsTheme.Display, 40f, 0f, TextAlignmentOptions.TopLeft);
            m_Title = Label(m_Body, "Title", title.ToUpperInvariant(), SettingsTheme.Display, 28f, 1f, TextAlignmentOptions.BottomLeft);
            m_Title.textWrappingMode = TextWrappingModes.Normal;
            m_Title.lineSpacing = -14f;
            m_Subtitle = Label(m_Body, "Subtitle", subtitle.ToUpperInvariant(), SettingsTheme.Label, 10.5f, 20f, TextAlignmentOptions.MidlineLeft);
            m_Description = Label(m_Body, "Description", description, SettingsTheme.Body, 12.5f, 0f, TextAlignmentOptions.TopLeft);
            m_Description.textWrappingMode = TextWrappingModes.Normal;
            m_Description.lineSpacing = 6f;
            m_Tag = Label(m_Body, "Discipline", disciplineTag.ToUpperInvariant(), SettingsTheme.Label, 9f, 12f, TextAlignmentOptions.MidlineLeft);
            m_Tag.overflowMode = TextOverflowModes.Ellipsis;

            m_Cta = SettingsUI.Rect("Cta", m_Body);
            m_CtaGroup = m_Cta.gameObject.AddComponent<CanvasGroup>();
            m_CtaGroup.alpha = 0f;
            m_CtaLabel = Label(m_Cta, "Label", "SELECT", SettingsTheme.Label, 9.5f, 26f, TextAlignmentOptions.MidlineRight);
            m_Chevron = SettingsUI.Chevron(m_Cta, "Chevron", 1, SettingsTheme.AccentLight, 10f, 1.6f);

            m_Border = Layer<AccertoneShape>(m_Body, "Border");
            SettingsUI.Stretch(m_Border.rectTransform);
            m_Border.chamfer = 18f;
            m_Border.corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft;
            m_Border.outline = true;
            m_Border.outlineWidth = 1f;
            m_Border.color = SettingsTheme.Hairline;

            // Text is tinted through its CanvasRenderer so colour animation never regenerates glyph meshes.
            m_Number.color = Color.white;
            m_Title.color = Color.white;
            m_Subtitle.color = Color.white;
            m_Description.color = Color.white;
            m_Tag.color = Color.white;
            m_CtaLabel.color = SettingsTheme.AccentLight;

            m_Projected = new Graphic[]
            {
                m_Shadow, m_Glow, m_BodyShape, m_Art, m_Shade, m_Tint, m_Sheen, m_Flash, m_TopEdge,
                m_MarkA, m_MarkB, m_RailTrack, m_RailFill, m_Border
            };
            Depth(m_Shadow, DepthShadow);
            Depth(m_Glow, DepthGlow);
            Depth(m_BodyShape, DepthSurface);
            Depth(m_Art, DepthArt);
            Depth(m_Shade, DepthSurface);
            Depth(m_Tint, DepthSurface);
            Depth(m_Sheen, 4f);
            Depth(m_Flash, DepthSurface);
            Depth(m_TopEdge, DepthSurface);
            Depth(m_MarkA, DepthMark);
            Depth(m_MarkB, DepthMark);
            Depth(m_RailTrack, DepthRail);
            Depth(m_RailFill, DepthRail);
            Depth(m_Border, DepthSurface);

            m_Floats[FloatNumber] = new FloatLayer { rect = m_Number.rectTransform, depth = DepthNumber };
            m_Floats[FloatTitle] = new FloatLayer { rect = m_Title.rectTransform, depth = DepthTitle };
            m_Floats[FloatSubtitle] = new FloatLayer { rect = m_Subtitle.rectTransform, depth = DepthSubtitle };
            m_Floats[FloatDesc] = new FloatLayer { rect = m_Description.rectTransform, depth = DepthDesc };
            m_Floats[FloatTag] = new FloatLayer { rect = m_Tag.rectTransform, depth = DepthTag };
            m_Floats[FloatCta] = new FloatLayer { rect = m_Cta, depth = DepthTag };

            m_HasProjection = true;
            m_Presence.Set(1f);
            m_Zoom.Snap(0f);
            ApplyTextTint(0f, 0f, 0f, 0f, 0f, 0f);
            Layout();
        }

        T Layer<T>(Transform parent, string name) where T : Graphic
        {
            var rt = SettingsUI.Rect(name, parent);
            var g = rt.gameObject.AddComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        void Depth(Graphic g, float depth)
        {
            var d = g.gameObject.AddComponent<PlayCardDepth>();
            d.card = this;
            d.depth = depth;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, float spacing, TextAlignmentOptions align)
        {
            var t = SettingsUI.Text(parent, name, text, font, size, Color.white, spacing, align);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return t;
        }

        static Color A(Color c, float a) { c.a = a; return c; }

        // ================================================================== layout
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (m_Built) Layout();
        }

        /// <summary>Positions every layer for the current card size. Type scales with the card.</summary>
        public void Layout()
        {
            if (!m_Built || m_Rect == null || m_Projected == null) return; // m_Projected is assigned once every layer exists
            Vector2 size = m_Rect.rect.size;
            if (size.x <= 1f || size.y <= 1f) return;
            m_CardSize = size;
            float w = size.x, h = size.y;
            float u = Mathf.Clamp(Mathf.Min(w / 320f, h / 536f), 0.7f, 1.25f);
            float pad = Mathf.Round(24f * u);
            float innerW = w - 2f * pad;
            float left = -w * 0.5f + pad;
            float bottom = -h * 0.5f + pad;
            float top = h * 0.5f - pad;

            // Shadow and glow extend past the card; their solid core hides behind it.
            Around(m_Shadow.rectTransform, 46f - 16f, new Vector2(0f, -20f));
            Around(m_Glow.rectTransform, 38f - 10f, Vector2.zero);

            // Art bleeds past the mask so the deeper layer never shows an edge while tilting.
            SettingsUI.Stretch(m_Art.rectTransform, -0.045f * w, -0.045f * h, -0.045f * w, -0.045f * h);

            Place(m_Sheen.rectTransform, new Vector2(-w, 0f), new Vector2(w * 0.42f, h * 1.8f));
            Place(m_TopEdge.rectTransform, new Vector2(-9f, h * 0.5f - 0.75f), new Vector2(w - 18f, 1.5f));

            float markH = 15f * u, markW = 5f * u;
            Place(m_MarkA.rectTransform, new Vector2(w * 0.5f - pad - 14f * u, top - markH * 0.5f + 2f), new Vector2(markW, markH));
            Place(m_MarkB.rectTransform, new Vector2(w * 0.5f - pad - 5f * u, top - markH * 0.5f + 2f), new Vector2(markW, markH));
            m_MarkA.skew = m_MarkB.skew = 4f * u;

            float railY = bottom + 26f * u;
            Place(m_RailTrack.rectTransform, new Vector2(0f, railY), new Vector2(innerW, 1f));
            var fill = m_RailFill.rectTransform;
            fill.anchorMin = fill.anchorMax = new Vector2(0.5f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(left, railY);
            fill.sizeDelta = new Vector2(innerW * m_RailCh.value, 2f);

            // Type.
            m_Number.fontSize = 40f * u;
            m_Title.fontSize = 28f * u;
            m_Subtitle.fontSize = 10.5f * u;
            m_Description.fontSize = 12.5f * u;
            m_Tag.fontSize = 9f * u;
            m_CtaLabel.fontSize = 9.5f * u;

            SetFloat(FloatNumber, new Vector2(0f, top - 22f * u), new Vector2(innerW, 46f * u));
            SetFloat(FloatTag, new Vector2(-40f * u, bottom + 7f * u), new Vector2(innerW - 80f * u, 14f * u));
            SetFloat(FloatCta, new Vector2(w * 0.5f - pad - 50f * u, bottom + 7f * u), new Vector2(100f * u, 14f * u));
            SetFloat(FloatDesc, new Vector2(0f, bottom + 70f * u), new Vector2(innerW, 58f * u));
            SetFloat(FloatSubtitle, new Vector2(0f, bottom + 113f * u), new Vector2(innerW, 14f * u));
            float titleH = 72f * u;
            SetFloat(FloatTitle, new Vector2(0f, bottom + 128f * u + titleH * 0.5f), new Vector2(innerW, titleH));

            var label = m_CtaLabel.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(1f, 0.5f);
            label.pivot = new Vector2(1f, 0.5f);
            label.anchoredPosition = new Vector2(-16f * u, 0f);
            label.sizeDelta = new Vector2(90f * u, 14f * u);
            var chev = m_Chevron.rectTransform;
            chev.anchorMin = chev.anchorMax = new Vector2(1f, 0.5f);
            chev.pivot = new Vector2(1f, 0.5f);
            chev.anchoredPosition = Vector2.zero;
            chev.sizeDelta = new Vector2(10f * u, 10f * u);

            m_LastScale = -1f; // force a full re-projection
            ApplyProjection(true);
        }

        void SetFloat(int index, Vector2 home, Vector2 size)
        {
            m_Floats[index].home = home;
            m_Floats[index].rect.sizeDelta = size;
        }

        static void Place(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
        }

        static void Around(RectTransform rt, float grow, Vector2 offset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-grow, -grow) + offset;
            rt.offsetMax = new Vector2(grow, grow) + offset;
        }

        // ================================================================== projection
        /// <summary>Projects a card-space point lying <paramref name="depth"/> px off the card plane.</summary>
        public Vector2 Project(Vector2 p, float depth)
        {
            float x = p.x * m_PScale, y = p.y * m_PScale, z = depth * m_PScale;
            float x1 = x * m_CosYaw + z * m_SinYaw;
            float z1 = -x * m_SinYaw + z * m_CosYaw;
            float y2 = y * m_CosPitch - z1 * m_SinPitch;
            float z2 = y * m_SinPitch + z1 * m_CosPitch;
            float k = Focal / Mathf.Max(1f, Focal - z2);
            return new Vector2(x1 * k + m_POffset.x, y2 * k + m_POffset.y);
        }

        void ApplyProjection(bool force)
        {
            if (!force &&
                Mathf.Abs(m_PScale - m_LastScale) < 1e-5f &&
                Mathf.Abs(m_Yaw.value - m_LastYaw) < 1e-4f &&
                Mathf.Abs(m_Pitch.value + PresencePitch() - m_LastPitch) < 1e-4f &&
                (m_POffset - m_LastOffset).sqrMagnitude < 1e-6f)
            {
                UpdateFloats();
                return;
            }

            m_LastScale = m_PScale;
            m_LastYaw = m_Yaw.value;
            m_LastPitch = m_Pitch.value + PresencePitch();
            m_LastOffset = m_POffset;

            for (int i = 0; i < m_Projected.Length; i++)
                if (m_Projected[i] != null) m_Projected[i].SetVerticesDirty();
            UpdateFloats();
        }

        float PresencePitch()
        {
            return -(1f - m_Presence.Value) * 14f;
        }

        /// <summary>Type layers follow the projection rigidly: projected centre plus the local scale of the projection there.</summary>
        void UpdateFloats()
        {
            for (int i = 0; i < FloatCount; i++)
            {
                var f = m_Floats[i];
                if (f.rect == null) continue;
                Vector2 c = f.home + f.offset;
                Vector2 p = Project(c, f.depth);
                float sx = (Project(c + new Vector2(6f, 0f), f.depth) - Project(c - new Vector2(6f, 0f), f.depth)).magnitude / 12f;
                float sy = (Project(c + new Vector2(0f, 6f), f.depth) - Project(c - new Vector2(0f, 6f), f.depth)).magnitude / 12f;
                if ((f.rect.anchoredPosition - p).sqrMagnitude > 1e-6f) f.rect.anchoredPosition = p;
                Vector3 s = new Vector3(sx, sy, 1f);
                if ((f.rect.localScale - s).sqrMagnitude > 1e-8f) f.rect.localScale = s;
            }
        }

        // ================================================================== presence (entrance / exit)
        /// <summary>Plays the entrance: rises from below, stands up from a backward tilt and fades in.</summary>
        public void Show(float delay, float duration = 0.8f)
        {
            if (!m_Built) return;
            if (m_Presence.to < 0.5f && m_Presence.Value < 0.01f)
            {
                m_Presence.Start(0f, 1f, duration, delay, PlayEase.OutQuint);
                m_Zoom.Snap(0f);
            }
            else
            {
                m_Presence.Retarget(1f, duration, delay, PlayEase.OutQuint);
            }
        }

        public void Hide(float delay, float duration = 0.3f)
        {
            if (!m_Built) return;
            m_Presence.Retarget(0f, duration, delay, PlayEase.InOutCubic);
        }

        public void SnapHidden()
        {
            if (!m_Built) return;
            m_Presence.Set(0f);
            m_Group.alpha = 0f;
        }

        /// <summary>Confirmation feedback: violet flash and a firm press.</summary>
        public void PlayConfirm()
        {
            m_FlashTween.Start(0f, 1f, 0.55f, 0f, PlayEase.Linear);
            m_FlashRunning = true;
            m_Press.velocity = 60f;
        }

        // ================================================================== frame
        void LateUpdate()
        {
            if (!m_Built || m_Projected == null) return;
            float dt = Time.unscaledDeltaTime;
            float now = PlayMotion.Now;

            bool active = Owner != null ? Owner.ActiveCard == this : m_PointerInside;
            bool dimmed = Owner != null && Owner.FocusActive && !active;

            // Staggered channel targets: in = title → subtitle → description → call to action,
            // out = the reverse, so the layers settle back in order.
            if (active != m_ActiveState)
            {
                m_ActiveState = active;
                m_StateTime = now;
                if (active) StartSheen();
            }
            float e = now - m_StateTime;
            m_RailTarget = Switch(m_RailTarget, active, e, 0.02f, 0f);
            m_TitleTarget = Switch(m_TitleTarget, active, e, 0.03f, 0.07f);
            m_SubTarget = Switch(m_SubTarget, active, e, 0.07f, 0.05f);
            m_DescTarget = Switch(m_DescTarget, active, e, 0.11f, 0.02f);
            m_CtaTarget = Switch(m_CtaTarget, active, e, 0.16f, 0f);

            float on = active ? 1f : 0f;
            m_Hover.Step(on, active ? 0.11f : 0.15f, dt);
            m_GlowSpring.Step(on, active ? 0.16f : 0.14f, dt);
            m_Zoom.Step(on, active ? 0.42f : 0.32f, dt);
            m_Dim.Step(dimmed ? 1f : 0f, 0.2f, dt);
            m_Press.Step(m_Pressed && m_PointerInside ? 1f : 0f, 0.05f, dt);
            m_RailCh.Step(m_RailTarget, 0.16f, dt);
            m_TitleCh.Step(m_TitleTarget, 0.11f, dt);
            m_SubCh.Step(m_SubTarget, 0.11f, dt);
            m_DescCh.Step(m_DescTarget, 0.12f, dt);
            m_CtaCh.Step(m_CtaTarget, 0.1f, dt);

            // Pointer-driven tilt: the side nearest the cursor lifts toward the viewer.
            float yawT = 0f, pitchT = 0f;
            if (active)
            {
                Vector2 n;
                if (m_PointerInside && TryPointerLocal(out n))
                {
                    yawT = -n.x * MaxYaw;
                    pitchT = n.y * MaxPitch;
                }
                else
                {
                    // Keyboard / pad focus: a slow, barely-there sway keeps the card alive.
                    yawT = Mathf.Sin(now * 0.9f + m_Phase) * 1.6f;
                    pitchT = Mathf.Cos(now * 0.7f + m_Phase) * 1.1f;
                }
            }
            m_Yaw.Step(yawT, 0.13f, dt);
            m_Pitch.Step(pitchT, 0.13f, dt);

            float pres = m_Presence.Value;
            float hover = m_Hover.value;
            float dim = m_Dim.value;
            float press = m_Press.value;

            m_Group.alpha = Mathf.Clamp01(pres * 1.15f);
            bool interactive = pres > 0.6f && m_Presence.to > 0.5f;
            if (m_Group.blocksRaycasts != interactive) m_Group.blocksRaycasts = interactive;

            m_PScale = (0.9f + 0.1f * pres) * (1f + 0.055f * hover - 0.022f * press - 0.018f * dim);
            m_POffset = new Vector2(0f, -(1f - pres) * 64f + 9f * hover);
            float yaw = m_Yaw.value * Mathf.Deg2Rad;
            float pitch = (m_Pitch.value + PresencePitch()) * Mathf.Deg2Rad;
            m_SinYaw = Mathf.Sin(yaw); m_CosYaw = Mathf.Cos(yaw);
            m_SinPitch = Mathf.Sin(pitch); m_CosPitch = Mathf.Cos(pitch);

            // Artwork: slow cinematic push-in, brightening out of the cool resting grade, gentle drift.
            float zoomV = m_Zoom.value;
            m_Art.zoom = 1.04f + 0.075f * zoomV + 0.16f * (1f - pres);
            Color art = Color.Lerp(IdleArt, Color.white, hover);
            float dimK = 1f - 0.34f * dim;
            m_Art.color = new Color(art.r * dimK, art.g * dimK, art.b * dimK, 1f);
            float drift = 1f - 0.6f * hover;
            m_Art.pan = new Vector2(Mathf.Sin(now * 0.13f + m_Phase) * 0.016f * drift,
                                    Mathf.Cos(now * 0.11f + m_Phase * 1.3f) * 0.012f * drift);

            // Grade: the scrim lifts off the top of the image while the base stays dark for the type.
            m_Shade.SetStopColor(2, A(Graphite, Mathf.Lerp(0.42f, 0.26f, hover) + 0.2f * dim));
            m_Shade.SetStopColor(3, A(Graphite, Mathf.Lerp(0.10f, 0.0f, hover) + 0.25f * dim));
            m_Shade.SetStopColor(4, A(Graphite, Mathf.Lerp(0.42f, 0.2f, hover) + 0.2f * dim));
            float g = m_GlowSpring.value;
            m_Tint.SetStopColor(0, A(SettingsTheme.AccentDeep, 0.34f * g));
            m_Tint.SetStopColor(1, A(SettingsTheme.Accent, 0.05f * g));

            // Violet light around the active card, breathing very slightly.
            float breathe = 0.93f + 0.07f * Mathf.Sin(now * 2.4f + m_Phase);
            m_Glow.color = A(SettingsTheme.Accent, 0.34f * g * breathe);
            m_Shadow.color = new Color(0f, 0f, 0f, 0.42f + 0.38f * hover);
            m_Border.color = Color.Lerp(SettingsTheme.Hairline, SettingsTheme.Accent, g);
            m_Border.outlineWidth = 1f + 0.6f * g;
            m_TopEdge.SetStopColor(1, A(SettingsTheme.Silver, 0.16f + 0.3f * hover));
            m_MarkA.color = m_MarkB.color = A(SettingsTheme.Accent, 0.35f + 0.65f * g);

            var fill = m_RailFill.rectTransform;
            float railW = Mathf.Max(0f, m_RailTrack.rectTransform.sizeDelta.x * m_RailCh.value);
            if (Mathf.Abs(fill.sizeDelta.x - railW) > 0.05f) fill.sizeDelta = new Vector2(railW, 2f);

            // Type motion: each line rises a few pixels on its own beat.
            m_Floats[FloatTitle].offset = new Vector2(0f, 7f * m_TitleCh.value);
            m_Floats[FloatSubtitle].offset = new Vector2(0f, 6f * m_SubCh.value);
            m_Floats[FloatDesc].offset = new Vector2(0f, 4f * m_DescCh.value);
            m_Floats[FloatNumber].offset = new Vector2(3f * m_TitleCh.value, 0f);
            m_Floats[FloatCta].offset = new Vector2(-9f * (1f - m_CtaCh.value), 0f);
            m_CtaGroup.alpha = m_CtaCh.value;
            ApplyTextTint(m_TitleCh.value, m_SubCh.value, m_DescCh.value, hover, dim, g);

            UpdateSheen(now);
            UpdateFlash();
            ApplyProjection(false);
        }

        static float Switch(float current, bool on, float elapsed, float inDelay, float outDelay)
        {
            float desired = on ? 1f : 0f;
            if (current == desired) return current;
            return elapsed >= (on ? inDelay : outDelay) * AccertoneMotion.Scale ? desired : current;
        }

        void ApplyTextTint(float title, float sub, float desc, float hover, float dim, float glow)
        {
            if (m_Title == null) return;
            float dimK = 1f - 0.35f * dim;
            m_Title.canvasRenderer.SetColor(Fade(Color.Lerp(SettingsTheme.Silver, SettingsTheme.White, title), dimK));
            m_Subtitle.canvasRenderer.SetColor(Fade(Color.Lerp(SettingsTheme.Metallic, SettingsTheme.AccentLight, sub), dimK));
            m_Description.canvasRenderer.SetColor(Fade(A(SettingsTheme.Text2, Mathf.Lerp(0.62f, 1f, desc)), dimK));
            m_Tag.canvasRenderer.SetColor(Fade(Color.Lerp(SettingsTheme.Muted, SettingsTheme.Text2, hover), dimK));
            m_Number.canvasRenderer.SetColor(Color.Lerp(A(SettingsTheme.White, 0.2f * dimK), A(SettingsTheme.AccentLight, 0.95f), glow));
        }

        static Color Fade(Color c, float k)
        {
            c.a *= k;
            return c;
        }

        void StartSheen()
        {
            if (PlayMotion.Instant) return;
            m_SheenTween.Start(0f, 1f, 1.05f, 0.04f, PlayEase.InOutCubic);
            m_SheenRunning = true;
        }

        void UpdateSheen(float now)
        {
            if (!m_SheenRunning) return;
            float t = m_SheenTween.Value;
            float w = m_CardSize.x;
            m_Sheen.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.95f, w * 0.95f, t), 0f);
            // The sweep belongs to the focused card: it fades out with the hover if focus leaves mid-sweep.
            m_Sheen.canvasRenderer.SetAlpha(Mathf.Sin(t * Mathf.PI) * Mathf.Clamp01(m_Hover.value * 1.25f));
            if (m_SheenTween.Done)
            {
                m_SheenRunning = false;
                m_Sheen.canvasRenderer.SetAlpha(0f);
            }
        }

        void UpdateFlash()
        {
            if (!m_FlashRunning) return;
            float t = m_FlashTween.Value;
            // Quick attack, long soft release.
            float a = t < 0.12f ? t / 0.12f : 1f - AccertoneEase.OutCubic((t - 0.12f) / 0.88f);
            m_Flash.canvasRenderer.SetAlpha(0.3f * a);
            if (m_FlashTween.Done)
            {
                m_FlashRunning = false;
                m_Flash.canvasRenderer.SetAlpha(0f);
            }
        }

        bool TryPointerLocal(out Vector2 normalized)
        {
            normalized = Vector2.zero;
            // Latest of: UI pointer events (any input module) or the device itself when it moves.
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null)
            {
                Vector2 polled = pointer.position.ReadValue();
                if (!(polled == m_PolledScreen))
                {
                    if (!float.IsNaN(m_PolledScreen.x)) m_PointerScreen = polled;
                    m_PolledScreen = polled;
                }
            }
#endif
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Rect, m_PointerScreen, m_EventCamera, out local)) return false;
            Vector2 half = m_CardSize * 0.5f;
            if (half.x <= 0f || half.y <= 0f) return false;
            normalized = new Vector2(Mathf.Clamp(local.x / half.x, -1f, 1f), Mathf.Clamp(local.y / half.y, -1f, 1f));
            return true;
        }

        // ================================================================== input
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            m_PointerInside = true;
            m_EventCamera = eventData.enterEventCamera;
            m_PointerScreen = eventData.position;
            if (Owner != null) Owner.OnCardPointerEnter(this);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            m_PointerInside = false;
            m_Pressed = false;
            if (Owner != null) Owner.OnCardPointerExit(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            m_PointerScreen = eventData.position;
            // Moving the mouse over a card takes focus back from the keyboard.
            if (Owner != null && Owner.ActiveCard != this && m_PointerInside) Owner.OnCardPointerEnter(this);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left) m_Pressed = true;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            m_Pressed = false;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (Owner != null) Owner.OnCardSelected(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            if (Owner != null) Owner.OnCardDeselected(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            Confirm();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsInteractable()) return;
            Confirm();
        }

        void Confirm()
        {
            if (Owner != null) Owner.ConfirmCard(this);
            else if (OnCardClicked != null) OnCardClicked(modeType);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_PointerInside = false;
            m_Pressed = false;
            SelectedByPointer = false;
        }
    }
}
