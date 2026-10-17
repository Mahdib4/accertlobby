using System;
using System.Collections.Generic;
using Accertone.Branding;
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
    /// Master UI for the Accert one Play game-mode selection system.
    /// Manages the 6 core game modes:
    /// 1. Quick Match
    /// 2. Online Multiplayer
    /// 3. Career
    /// 4. Hot Lap
    /// 5. Free Practice
    /// 6. Play With Friends
    ///
    /// The selection screen shows the modes as one row of identical, art-directed cards
    /// (<see cref="PlayModeCard"/>) over a dark graphite stage lit by slow violet accents. It shares the
    /// Settings screen's 1920×1080 design frame, header and key-prompt footer, keeps the lobby wordmark and
    /// navigation above it, and animates every change of state: screen fade, staggered card entrance,
    /// hover focus with neighbour dimming, confirm, and the hand-over to each mode's sub-panel.
    /// </summary>
    public class PlayModeSelectionUI : MonoBehaviour
    {
        public static PlayModeSelectionUI Instance { get; private set; }

        public Action OnCloseRequested;
        public Action OnOpenCarSetupRequested;

        // Design frame (same 1920×1080 reference and margins as the Settings screen).
        const float SideMargin = 112f;
        const float HeaderTop = 170f;
        const float RowTop = 300f;
        const float RowBottomMargin = 128f;
        const float FooterBottom = 42f;
        const float CardAspect = 0.6f;        // width / height at 16:9; identical for all five cards
        const float MinCardAspect = 0.5f;     // taller screens (4:3, 16:10) grow the cards vertically
        const float MaxCardAspect = 0.78f;    // short, wide screens (21:9, 32:9) widen them instead
        const float CardGap = 22f;
        const float SubPanelTop = 290f;
        const float SubPanelBottomMargin = 112f;
        const float SubPanelWidth = 1608f;
        const float SubPanelHeight = 680f;
        const string DefaultTitle = "SELECT MODE";
        const string DefaultBreadcrumb = "2026 FIA FORMULA ONE SEASON  ·  SELECT RACING DISCIPLINE";

        private CanvasGroup m_CanvasGroup;
        private RectTransform m_Root;
        private RectTransform m_HeaderRt;
        private RectTransform m_CardsContainer;
        private RectTransform m_SubPanelContainer;
        private CanvasGroup m_CardsGroup;
        private CanvasGroup m_SubGroup;
        private CanvasGroup m_HeaderGroup;
        private CanvasGroup m_TitleGroup;
        private RectTransform m_TitleBlock;
        private RectTransform m_FooterRt;
        private CanvasGroup m_FooterGroup;
        private CanvasGroup m_GridPromptsGroup;
        private RectTransform m_Prompts;
        private bool m_PromptsBuilt;

        // Sprites for 5 modes
        [SerializeField] private Sprite m_QuickMatchSprite;
        [SerializeField] private Sprite m_MultiplayerSprite;
        [SerializeField] private Sprite m_CareerSprite;
        [SerializeField] private Sprite m_HotLapSprite;
        [SerializeField] private Sprite m_PracticeSprite;
        [Tooltip("Optional. Falls back to Resources/" + FriendsSpriteResource + " so builds need no scene wiring.")]
        [SerializeField] private Sprite m_FriendsSprite;
        const string FriendsSpriteResource = "PlayModes/play_with_friends";

        private PlayModeType m_ActiveSubMode = PlayModeType.QuickMatch;
        private bool m_IsInSubPanel = false;
        private readonly List<PlayModeCard> m_Cards = new List<PlayModeCard>();

        // Header / footer text
        private TMP_Text m_TitleText;
        private TMP_Text m_BreadcrumbText;
        private TMP_Text m_CounterText;
        private TMP_Text m_CounterName;
        private TMP_Text m_BackLabel;
        private Button m_BackButton;

        // Sub-panel UI references
        private GameObject m_CurrentSubPanelGo;
        // Keyboard / gamepad focus carried across an in-place rebuild, as a sibling-index path from the panel root.
        private readonly List<int> m_SubFocusPath = new List<int>();
        private bool m_RestoreSubFocus;
        private bool m_SubPanelFresh;

        // Stage lighting
        private RectTransform m_Ambient;
        private RawImage m_GlowWide;
        private RawImage m_GlowHigh;
        private RawImage m_Spot;
        private PlayGradient m_FloorLine;
        private AccertoneShape m_FloorAccent;
        private PlaySpring m_SpotX, m_SpotAlpha, m_ParallaxX, m_ParallaxY;
        private Vector2 m_RowCenter;
        private Vector2 m_CardSize;
        private float m_RowWidth;

        // Flow
        private enum Flow { Idle, Confirming, GridOut, SubOut }
        private Flow m_Flow = Flow.Idle;
        private float m_FlowTime;
        private PlayModeType m_PendingMode;
        private int m_ConfirmIndex;
        private bool m_Open;
        private bool m_Closing;
        private PlayTween m_ScreenFade;
        private PlayTween m_HeaderReveal;
        private PlayTween m_FooterReveal;
        private PlayTween m_TitleReveal;
        private PlayTween m_SubReveal;
        private float m_InputReadyTime;
        private Vector2 m_LastRootSize;
        private Vector2 m_SubHome;

        // Focus
        private PlayModeCard m_Active;
        private float m_LastActiveTime = -10f;
        private int m_LastIndex;
        private bool m_SelectingFromPointer;

        public bool IsOpen => gameObject.activeSelf && m_Open;
        public bool IsInSubPanel => m_IsInSubPanel;

        /// <summary>The card currently in focus (hovered or selected with keys / pad), if any.</summary>
        public PlayModeCard ActiveCard => m_Active;

        /// <summary>True while a card has focus, with a short grace so gliding between cards never flickers.</summary>
        public bool FocusActive => m_Active != null || PlayMotion.Now - m_LastActiveTime < 0.16f;

        private bool m_Initialized = false;
        private bool m_Built = false;

        public void InitializeIfNeeded()
        {
            if (m_Initialized) return;
            m_Initialized = true;
            Instance = this;
            m_CanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null) m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            // The screen batches on its own canvas so card motion never re-batches the lobby.
            if (gameObject.GetComponent<Canvas>() == null) gameObject.AddComponent<Canvas>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            LoadSpritesIfNeeded();
            BuildUIHierarchy();
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void LoadSpritesIfNeeded()
        {
#if UNITY_EDITOR
            if (m_QuickMatchSprite == null)
                m_QuickMatchSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_LobbyModule/Play/Images/quick_match.jpg");
            if (m_MultiplayerSprite == null)
                m_MultiplayerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_LobbyModule/Play/Images/online_multiplayer.jpg");
            if (m_CareerSprite == null)
                m_CareerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_LobbyModule/Play/Images/career.jpg");
            if (m_HotLapSprite == null)
                m_HotLapSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_LobbyModule/Play/Images/hot_lap.jpg");
            if (m_PracticeSprite == null)
                m_PracticeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_LobbyModule/Play/Images/free_practice.jpg");
#endif
            if (m_FriendsSprite == null)
                m_FriendsSprite = Resources.Load<Sprite>(FriendsSpriteResource);
        }

        // =========================================================================
        // BUILD
        // =========================================================================
        private void BuildUIHierarchy()
        {
            m_Root = (RectTransform)transform;
            m_Root.anchorMin = Vector2.zero;
            m_Root.anchorMax = Vector2.one;
            m_Root.sizeDelta = Vector2.zero;

            // Opaque graphite stage. (The project renders in linear space, where even a 95% overlay lets the
            // emissive showroom read through; the violet stage lighting carries the atmosphere instead.)
            var bgShape = gameObject.GetComponent<AccertoneShape>();
            if (bgShape == null) bgShape = gameObject.AddComponent<AccertoneShape>();
            bgShape.color = SettingsTheme.Graphite;
            bgShape.alphaLeft = 1f;
            bgShape.alphaRight = 1f;
            bgShape.raycastTarget = true;

            BuildStageLighting();
            BuildHeader();

            // Cards row (fills the screen; cards are positioned in screen-centre space).
            m_CardsContainer = SettingsUI.Stretch(SettingsUI.Rect("CardsRow", transform));
            m_CardsGroup = m_CardsContainer.gameObject.AddComponent<CanvasGroup>();
            BuildModeCards();

            // Sub-panel container (each mode's detail panel).
            m_SubPanelContainer = SettingsUI.Rect("SubPanels", transform);
            m_SubPanelContainer.anchorMin = new Vector2(0.5f, 0.5f);
            m_SubPanelContainer.anchorMax = new Vector2(0.5f, 0.5f);
            m_SubPanelContainer.pivot = new Vector2(0.5f, 0.5f);
            m_SubPanelContainer.sizeDelta = new Vector2(SubPanelWidth, SubPanelHeight);
            m_SubGroup = m_SubPanelContainer.gameObject.AddComponent<CanvasGroup>();
            m_SubPanelContainer.gameObject.SetActive(false);

            BuildFooter();

            m_CanvasGroup.alpha = 0f;
            m_ScreenFade.Set(0f);
            m_HeaderReveal.Set(1f);
            m_FooterReveal.Set(1f);
            m_TitleReveal.Set(1f);
            m_SubReveal.Set(0f);
            m_Built = true;
            LayoutScreen();
        }

        private void BuildStageLighting()
        {
            m_Ambient = SettingsUI.Stretch(SettingsUI.Rect("StageLighting", transform));
            // Lighting animates every frame; its own canvas keeps that from touching anything else.
            m_Ambient.gameObject.AddComponent<Canvas>();

            var vignette = SettingsUI.Rect("Vignette", m_Ambient).gameObject.AddComponent<PlayGradient>();
            SettingsUI.Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;
            vignette.SetStopCount(4);
            vignette.SetStop(0, 0f, SettingsTheme.A(SettingsTheme.Graphite, 0.55f));
            vignette.SetStop(1, 0.22f, SettingsTheme.A(SettingsTheme.Graphite, 0f));
            vignette.SetStop(2, 0.8f, SettingsTheme.A(SettingsTheme.Graphite, 0f));
            vignette.SetStop(3, 1f, SettingsTheme.A(SettingsTheme.Graphite, 0.5f));

            var glowTex = SettingsUI.RadialGlow();
            m_GlowWide = Glow("WideViolet", glowTex, SettingsTheme.A(SettingsTheme.AccentDeep, 0.22f), new Vector2(1760f, 760f));
            m_GlowHigh = Glow("HighViolet", glowTex, SettingsTheme.A(SettingsTheme.Accent, 0.06f), new Vector2(1100f, 640f));
            m_Spot = Glow("FocusSpot", glowTex, SettingsTheme.A(SettingsTheme.Accent, 0f), new Vector2(560f, 230f));

            m_FloorLine = SettingsUI.Rect("FloorLine", m_Ambient).gameObject.AddComponent<PlayGradient>();
            m_FloorLine.raycastTarget = false;
            m_FloorLine.axis = PlayGradient.Axis.Horizontal;
            m_FloorLine.SetStopCount(4);
            m_FloorLine.SetStop(0, 0f, SettingsTheme.A(SettingsTheme.Hairline, 0f));
            m_FloorLine.SetStop(1, 0.16f, SettingsTheme.A(SettingsTheme.Hairline, 0.9f));
            m_FloorLine.SetStop(2, 0.84f, SettingsTheme.A(SettingsTheme.Hairline, 0.9f));
            m_FloorLine.SetStop(3, 1f, SettingsTheme.A(SettingsTheme.Hairline, 0f));

            m_FloorAccent = SettingsUI.Shape(m_Ambient, "FloorAccent", SettingsTheme.Accent, 0f, ChamferCorners.None, 3f);
            m_FloorAccent.canvasRenderer.SetAlpha(0f);
        }

        private RawImage Glow(string name, Texture tex, Color c, Vector2 size)
        {
            var rt = SettingsUI.Rect(name, m_Ambient);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private void BuildHeader()
        {
            m_HeaderRt = SettingsUI.Rect("PlayHeader", transform);
            m_HeaderRt.anchorMin = new Vector2(0f, 1f);
            m_HeaderRt.anchorMax = new Vector2(1f, 1f);
            m_HeaderRt.pivot = new Vector2(0.5f, 1f);
            m_HeaderRt.offsetMin = new Vector2(SideMargin, -HeaderTop - 100f);
            m_HeaderRt.offsetMax = new Vector2(-SideMargin, -HeaderTop);
            m_HeaderGroup = m_HeaderRt.gameObject.AddComponent<CanvasGroup>();

            var tick = SettingsUI.Fill(m_HeaderRt, "Tick", SettingsTheme.Accent);
            SettingsUI.At(tick.rectTransform, 0f, 5f, 18f, 2f);
            var eyebrow = SettingsUI.Text(m_HeaderRt, "Eyebrow", "PLAY", SettingsTheme.Label, 11f, SettingsTheme.Muted, 26f);
            SettingsUI.At(eyebrow.rectTransform, 30f, 0f, 300f, 12f);

            m_TitleBlock = SettingsUI.At(SettingsUI.Rect("TitleBlock", m_HeaderRt), 0f, 18f, 1100f, 82f);
            m_TitleGroup = m_TitleBlock.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < 2; i++)
            {
                var bar = SettingsUI.Shape(m_TitleBlock, "Mark", SettingsTheme.Accent, 0f, ChamferCorners.None, 9f);
                SettingsUI.At(bar.rectTransform, i * 14f, 6f, 15f, 34f);
            }
            var title = SettingsUI.Text(m_TitleBlock, "Title", DefaultTitle, SettingsTheme.Display, 46f, SettingsTheme.White, 7f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(title.rectTransform, 46f, 0f, 1000f, 48f);
            m_TitleText = title;
            var blurb = SettingsUI.Text(m_TitleBlock, "Breadcrumb", DefaultBreadcrumb, SettingsTheme.Label, 11f, SettingsTheme.Text2, 22f);
            SettingsUI.At(blurb.rectTransform, 0f, 58f, 1100f, 16f);
            m_BreadcrumbText = blurb;

            m_BackButton = AccertoneUIFactory.CreateButton(m_HeaderRt, "BackButton", "RETURN TO SHOWROOM", new Vector2(232f, 38f), RequestBack);
            var backRt = (RectTransform)m_BackButton.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(1f, 1f);
            backRt.pivot = new Vector2(1f, 1f);
            backRt.anchoredPosition = new Vector2(0f, -24f);
            m_BackLabel = m_BackButton.GetComponentInChildren<TMP_Text>();
            if (m_BackLabel != null)
            {
                m_BackLabel.font = SettingsTheme.Label;
                m_BackLabel.fontSize = 11f;
                m_BackLabel.characterSpacing = 18f;
            }
        }

        private void BuildFooter()
        {
            m_FooterRt = SettingsUI.Rect("PlayFooter", transform);
            m_FooterRt.anchorMin = new Vector2(0f, 0f);
            m_FooterRt.anchorMax = new Vector2(1f, 0f);
            m_FooterRt.pivot = new Vector2(0.5f, 0f);
            m_FooterRt.offsetMin = new Vector2(SideMargin, FooterBottom);
            m_FooterRt.offsetMax = new Vector2(-SideMargin, FooterBottom + 54f);
            m_FooterGroup = m_FooterRt.gameObject.AddComponent<CanvasGroup>();
            m_Prompts = SettingsUI.At(SettingsUI.Rect("Prompts", m_FooterRt), 0f, 0f, 1100f, 54f);

            m_CounterName = SettingsUI.Text(m_FooterRt, "ModeName", "", SettingsTheme.Label, 11f, SettingsTheme.Text2, 22f, TextAlignmentOptions.MidlineRight, true);
            SettingsUI.AtRight(m_CounterName.rectTransform, 96f, 0f, 420f, 54f);
            m_CounterText = SettingsUI.Text(m_FooterRt, "Counter", "", SettingsTheme.Data, 13f, SettingsTheme.Silver, 4f, TextAlignmentOptions.MidlineRight);
            SettingsUI.AtRight(m_CounterText.rectTransform, 0f, 0f, 90f, 54f);
            UpdateFooterCounter();
        }

        /// <summary>Key prompts are measured, so they are built the first time the screen is active (text measures 0 while inactive).</summary>
        private void BuildPromptsIfNeeded()
        {
            if (m_PromptsBuilt || m_Prompts == null || !gameObject.activeInHierarchy) return;
            m_PromptsBuilt = true;
            float x = Prompt(m_Prompts, 0f, new[] { "ESC" }, "Back", RequestBack);
            var grid = SettingsUI.At(SettingsUI.Rect("GridPrompts", m_Prompts), x + 28f, 0f, 900f, 54f);
            m_GridPromptsGroup = grid.gameObject.AddComponent<CanvasGroup>();
            m_GridPromptsGroup.blocksRaycasts = false;
            m_GridPromptsGroup.alpha = m_IsInSubPanel ? 0f : 1f;
            float gx = Prompt(grid, 0f, new[] { "←", "→" }, "Select", null);
            gx = Prompt(grid, gx + 28f, new[] { "ENTER" }, "Confirm", null);
            Prompt(grid, gx + 28f, new[] { "1–" + Mathf.Min(9, m_Cards.Count) }, "Jump", null);
        }

        /// <summary>Key-cap prompt in the Settings footer style. Returns the x where the next prompt can start.</summary>
        private static float Prompt(RectTransform parent, float x, string[] keys, string label, Action onClick)
        {
            var prompt = SettingsUI.At(SettingsUI.Rect("Prompt_" + label, parent), x, 0f, 10f, 54f);
            float px = 0f;
            foreach (var k in keys)
            {
                var cap = SettingsUI.Keycap(prompt, k);
                cap.anchorMin = cap.anchorMax = new Vector2(0f, 0.5f);
                cap.pivot = new Vector2(0f, 0.5f);
                cap.anchoredPosition = new Vector2(px, 0f);
                px += cap.sizeDelta.x + 3f;
            }
            var t = SettingsUI.Text(prompt, "Label", label, SettingsTheme.Label, 11f, SettingsTheme.Text2, 22f, TextAlignmentOptions.MidlineLeft, true);
            SettingsUI.At(t.rectTransform, px + 6f, 0f, 200f, 54f);
            float w = px + 6f + SettingsUI.PreferredWidth(t);
            prompt.sizeDelta = new Vector2(w, 54f);
            if (onClick != null)
            {
                var hit = SettingsUI.Fill(prompt, "Hit", new Color(0f, 0f, 0f, 0f), true);
                SettingsUI.Stretch(hit.rectTransform);
                var pointer = hit.gameObject.AddComponent<SettingsPointer>();
                pointer.onClick = () => onClick();
            }
            return x + w;
        }

        private void BuildModeCards()
        {
            // Card 1: QUICK MATCH
            var c1 = CreateCard(m_CardsContainer, "Card_QuickMatch", PlayModeType.QuickMatch, m_QuickMatchSprite, new Vector2(0.5f, 0.42f),
                "01", "SOLO // AI GRID", "QUICK MATCH",
                "Play with AI", "Instant Grand Prix against a full 20-car AI field with customizable distance, grid size and pace.");
            m_Cards.Add(c1);

            // Card 2: ONLINE MULTIPLAYER
            var c2 = CreateCard(m_CardsContainer, "Card_Online", PlayModeType.OnlineMultiplayer, m_MultiplayerSprite, new Vector2(0.42f, 0.5f),
                "02", "ONLINE // 20 PLAYERS", "ONLINE MULTIPLAYER",
                "Race against real players", "Join public rooms, host private 20-car sessions with room codes, or compete on dedicated servers.");
            m_Cards.Add(c2);

            // Card 3: CAREER
            var c3 = CreateCard(m_CardsContainer, "Card_Career", PlayModeType.Career, m_CareerSprite, new Vector2(0.43f, 0.5f),
                "03", "CHAMPIONSHIP // 24 ROUNDS", "CAREER",
                "Build your racing career", "Compete through the 2026 FIA Formula One Championship calendar, earn points, and chase the Drivers' World Title.");
            m_Cards.Add(c3);

            // Card 4: HOT LAP
            var c4 = CreateCard(m_CardsContainer, "Card_HotLap", PlayModeType.HotLap, m_HotLapSprite, new Vector2(0.37f, 0.42f),
                "04", "TIME ATTACK // TELEMETRY", "HOT LAP",
                "Set your fastest lap", "Pure single-lap qualifying shootout against the Valcorsa benchmark lap record (1:29.874).");
            m_Cards.Add(c4);

            // Card 5: FREE PRACTICE
            var c5 = CreateCard(m_CardsContainer, "Card_Practice", PlayModeType.FreePractice, m_PracticeSprite, new Vector2(0.45f, 0.5f),
                "05", "TESTING // SETUP & TUNING", "FREE PRACTICE",
                "Practice and test car setups", "Unrestricted track session with live telemetry readouts and direct access to full vehicle setup tuning.");
            m_Cards.Add(c5);

            // Card 6: PLAY WITH FRIENDS
            var c6 = CreateCard(m_CardsContainer, "Card_Friends", PlayModeType.PlayWithFriends, m_FriendsSprite, new Vector2(0.5f, 0.44f),
                "06", "PRIVATE // INVITE ONLY", "PLAY WITH FRIENDS",
                "Race your friends", "Invite friends from your list or share a room code, set your own rules and race them head-to-head.");
            m_Cards.Add(c6);

            // Explicit left / right navigation along the row; up reaches the back button.
            for (int i = 0; i < m_Cards.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnLeft = i > 0 ? m_Cards[i - 1] : null;
                nav.selectOnRight = i < m_Cards.Count - 1 ? m_Cards[i + 1] : null;
                nav.selectOnUp = m_BackButton;
                m_Cards[i].navigation = nav;
                m_Cards[i].Index = i;
            }
        }

        private PlayModeCard CreateCard(Transform parent, string name, PlayModeType type, Sprite sprite, Vector2 focus, string num, string tag, string title, string sub, string desc)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var card = go.AddComponent<PlayModeCard>();
            card.Owner = this;
            card.artFocus = focus;
            card.Setup(type, sprite, num, tag, title, sub, desc, false, OnCardSelected);
            return card;
        }

        private void OnCardSelected(PlayModeType mode)
        {
            OpenSubPanel(mode);
        }

        // =========================================================================
        // LAYOUT
        // =========================================================================
        private void OnRectTransformDimensionsChange()
        {
            if (m_Built) LayoutScreen();
        }

        /// <summary>Fits five identical cards (fixed aspect) into the space between header and footer at any resolution.</summary>
        private void LayoutScreen()
        {
            if (!m_Built || m_Root == null) return;
            Vector2 size = m_Root.rect.size;
            if (size.x <= 1f || size.y <= 1f) return;
            m_LastRootSize = size;

            float availW = size.x - 2f * SideMargin;
            float availH = size.y - RowTop - RowBottomMargin;
            int count = Mathf.Max(1, m_Cards.Count);
            float w = (availW - (count - 1) * CardGap) / count;
            float h = w / CardAspect;
            if (h > availH)
            {
                // Height-bound: keep the full height and widen up to the max aspect.
                h = availH;
                w = Mathf.Min(w, h * MaxCardAspect);
            }
            else if (availH > h * 1.25f)
            {
                // Lots of spare height: let the cards stand taller instead of floating in empty space.
                h = Mathf.Min(availH * 0.8f, w / MinCardAspect);
            }
            w = Mathf.Floor(w);
            h = Mathf.Floor(h);
            m_CardSize = new Vector2(w, h);
            m_RowWidth = count * w + (count - 1) * CardGap;
            m_RowCenter = new Vector2(0f, size.y * 0.5f - RowTop - availH * 0.5f);

            for (int i = 0; i < m_Cards.Count; i++)
            {
                var rt = (RectTransform)m_Cards[i].transform;
                rt.sizeDelta = m_CardSize;
                rt.anchoredPosition = new Vector2(-m_RowWidth * 0.5f + w * 0.5f + i * (w + CardGap), m_RowCenter.y);
            }

            // Sub-panels keep their authored 1608×680 layout and scale down only when the screen is smaller.
            float subAvailH = size.y - SubPanelTop - SubPanelBottomMargin;
            float subScale = Mathf.Min(1f, Mathf.Min(availW / SubPanelWidth, subAvailH / SubPanelHeight));
            m_SubPanelContainer.localScale = new Vector3(subScale, subScale, 1f);
            m_SubHome = new Vector2(0f, size.y * 0.5f - SubPanelTop - subAvailH * 0.5f);
            m_SubPanelContainer.anchoredPosition = m_SubHome;

            // Stage lighting follows the row.
            float floorY = m_RowCenter.y - h * 0.5f - 34f;
            m_GlowWide.rectTransform.sizeDelta = new Vector2(m_RowWidth + 160f, h * 1.35f);
            m_FloorLine.rectTransform.anchorMin = m_FloorLine.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            m_FloorLine.rectTransform.sizeDelta = new Vector2(m_RowWidth + 220f, 1f);
            m_FloorLine.rectTransform.anchoredPosition = new Vector2(0f, floorY);
            var accent = m_FloorAccent.rectTransform;
            accent.anchorMin = accent.anchorMax = new Vector2(0.5f, 0.5f);
            accent.sizeDelta = new Vector2(w * 0.42f, 2f);
            m_Spot.rectTransform.sizeDelta = new Vector2(w * 1.9f, 200f);
            UpdateStageLighting(true);
        }

        // =========================================================================
        // OPEN / CLOSE
        // =========================================================================
        public void Open(PlayModeType initialMode = PlayModeType.QuickMatch, bool openSubPanel = false)
        {
            // Activate before building so text measures with its fonts initialised.
            bool wasVisible = gameObject.activeSelf && m_CanvasGroup != null && m_CanvasGroup.alpha > 0.02f;
            gameObject.SetActive(true);
            InitializeIfNeeded();
            BuildPromptsIfNeeded();
            PlaceInLobbyHierarchy();

            m_Open = true;
            m_Closing = false;
            m_Flow = Flow.Idle;
            m_CanvasGroup.blocksRaycasts = true;
            m_CanvasGroup.interactable = true;
            m_ScreenFade.Start(m_CanvasGroup.alpha, 1f, 0.36f, 0f, PlayEase.OutCubic);
            if (!wasVisible)
            {
                m_HeaderReveal.Start(0f, 1f, 0.5f, 0.02f, PlayEase.OutCubic);
                m_FooterReveal.Start(0f, 1f, 0.5f, 0.2f, PlayEase.OutCubic);
            }
            m_InputReadyTime = PlayMotion.Now + 0.25f;
            LayoutScreen();
            ClearFocus();

            if (openSubPanel)
            {
                OpenSubPanel(initialMode);
            }
            else
            {
                DestroySubPanel();
                ShowGrid(true);
            }
        }

        public void Close()
        {
            ClearFocus();
            m_Open = false;
            m_IsInSubPanel = false;
            m_Flow = Flow.Idle;
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.blocksRaycasts = false;
                m_CanvasGroup.interactable = false;
            }
            if (!gameObject.activeInHierarchy || m_CanvasGroup == null || PlayMotion.Instant)
            {
                FinishClose();
                return;
            }
            m_Closing = true;
            m_ScreenFade.Retarget(0f, 0.26f, 0f, PlayEase.OutCubic);
            for (int i = 0; i < m_Cards.Count; i++) m_Cards[i].Hide(0f, 0.26f);
        }

        private void FinishClose()
        {
            m_Closing = false;
            if (m_CanvasGroup != null) m_CanvasGroup.alpha = 0f;
            m_ScreenFade.Set(0f);
            DestroySubPanel();
            if (m_SubPanelContainer != null) m_SubPanelContainer.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            m_Open = false;
            m_Closing = false;
            m_IsInSubPanel = false;
            m_Flow = Flow.Idle;
            m_Active = null;
            if (m_CanvasGroup != null) m_CanvasGroup.alpha = 0f;
            m_ScreenFade.Set(0f);
        }

        /// <summary>
        /// Draw the Play screen where the Settings screen sits (directly above the lobby backdrop), so the
        /// wordmark and navigation stay on top of it and the race-launch transition overlay covers it.
        /// </summary>
        private void PlaceInLobbyHierarchy()
        {
            var parent = transform.parent;
            if (parent == null) return;
            var anchor = parent.Find("Screen_Settings");
            if (anchor == null) anchor = parent.Find("Backdrop_Shades");
            if (anchor == null || anchor == transform) return;
            int current = transform.GetSiblingIndex();
            int target = anchor.GetSiblingIndex() + 1;
            if (current < target) target -= 1;
            if (current != target) transform.SetSiblingIndex(target);
        }

        public void RequestBack()
        {
            if (m_Flow != Flow.Idle) return;
            if (m_IsInSubPanel)
            {
                BackToModeGrid();
            }
            else
            {
                Close();
                OnCloseRequested?.Invoke();
            }
        }

        public void BackToModeGrid()
        {
            if (m_IsInSubPanel && m_CurrentSubPanelGo != null && gameObject.activeInHierarchy && !PlayMotion.Instant)
            {
                // Sub-panel fades down first, then the cards come back in.
                m_Flow = Flow.SubOut;
                m_FlowTime = PlayMotion.Now;
                m_SubGroup.interactable = false;
                m_SubGroup.blocksRaycasts = false;
                m_SubReveal.Retarget(0f, 0.2f, 0f, PlayEase.OutCubic);
                return;
            }
            DestroySubPanel();
            ShowGrid(true);
        }

        private void ShowGrid(bool animate, float firstDelay = 0.1f)
        {
            m_IsInSubPanel = false;
            m_Flow = Flow.Idle;
            if (m_SubPanelContainer != null) m_SubPanelContainer.gameObject.SetActive(false);
            if (m_CardsContainer != null) m_CardsContainer.gameObject.SetActive(true);
            m_CardsGroup.interactable = true;
            m_CardsGroup.blocksRaycasts = true;
            LayoutScreen();
            for (int i = 0; i < m_Cards.Count; i++)
            {
                m_Cards[i].SnapHidden();
                m_Cards[i].Show(animate ? firstDelay + i * 0.065f : 0f, animate ? 0.85f : 0f);
            }
            SetHeader(DefaultTitle, DefaultBreadcrumb);
            if (m_BackLabel != null) m_BackLabel.text = "RETURN TO SHOWROOM";
            UpdateFooterCounter();
        }

        public void OpenSubPanel(PlayModeType mode)
        {
            InitializeIfNeeded();
            m_ActiveSubMode = mode;
            m_IsInSubPanel = true;
            m_Flow = Flow.Idle;
            ClearFocus();

            for (int i = 0; i < m_Cards.Count; i++) m_Cards[i].SnapHidden();
            if (m_CardsContainer != null) m_CardsContainer.gameObject.SetActive(false);
            if (m_SubPanelContainer != null)
            {
                DestroySubPanel();
                m_SubPanelContainer.gameObject.SetActive(true);
                switch (mode)
                {
                    case PlayModeType.QuickMatch:
                        BuildQuickMatchSubPanel();
                        break;
                    case PlayModeType.OnlineMultiplayer:
                        BuildMultiplayerSubPanel();
                        break;
                    case PlayModeType.Career:
                        BuildCareerSubPanel();
                        break;
                    case PlayModeType.HotLap:
                        BuildHotLapSubPanel();
                        break;
                    case PlayModeType.FreePractice:
                        BuildPracticeSubPanel();
                        break;
                    case PlayModeType.PlayWithFriends:
                        BuildFriendsSubPanel();
                        break;
                }
            }

            m_SubGroup.interactable = true;
            m_SubGroup.blocksRaycasts = true;
            m_SubReveal.Start(0f, 1f, 0.46f, 0.02f, PlayEase.OutCubic);
            string crumb = m_BreadcrumbText != null ? m_BreadcrumbText.text : DefaultBreadcrumb;
            SetHeader(ModeTitle(mode), crumb);
            if (m_BackLabel != null) m_BackLabel.text = "BACK TO GAME MODES";
            UpdateFooterCounter();
        }

        private void SetHeader(string title, string crumb)
        {
            bool changed = m_TitleText != null && m_TitleText.text != title;
            if (m_TitleText != null) m_TitleText.text = title;
            if (m_BreadcrumbText != null) m_BreadcrumbText.text = crumb;
            if (changed || m_TitleReveal.Done) m_TitleReveal.Start(0f, 1f, 0.42f, 0.04f, PlayEase.OutCubic);
        }

        private static string ModeTitle(PlayModeType mode)
        {
            switch (mode)
            {
                case PlayModeType.OnlineMultiplayer: return "ONLINE MULTIPLAYER";
                case PlayModeType.Career: return "CAREER";
                case PlayModeType.HotLap: return "HOT LAP";
                case PlayModeType.FreePractice: return "FREE PRACTICE";
                case PlayModeType.PlayWithFriends: return "PLAY WITH FRIENDS";
                default: return "QUICK MATCH";
            }
        }

        private void DestroySubPanel()
        {
            m_RestoreSubFocus = false;
            if (m_CurrentSubPanelGo != null)
            {
                Destroy(m_CurrentSubPanelGo);
                m_CurrentSubPanelGo = null;
            }
            if (m_SubPanelContainer != null)
            {
                for (int i = m_SubPanelContainer.childCount - 1; i >= 0; i--)
                    Destroy(m_SubPanelContainer.GetChild(i).gameObject);
            }
        }

        /// <summary>Starts a fresh sub-panel, removing the previous one (option changes rebuild the panel in place).</summary>
        private GameObject BeginSubPanel(string name)
        {
            if (m_CurrentSubPanelGo != null)
            {
                RememberSubPanelFocus(m_CurrentSubPanelGo.transform);
                Destroy(m_CurrentSubPanelGo);
            }
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(m_SubPanelContainer, false);
            m_SubPanelFresh = true;
            return go;
        }

        /// <summary>Notes which control had focus so the rebuilt panel (same layout, new objects) can hand it back.</summary>
        private void RememberSubPanelFocus(Transform panel)
        {
            m_SubFocusPath.Clear();
            m_RestoreSubFocus = false;
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null || !selected.transform.IsChildOf(panel)) return;
            for (var t = selected.transform; t != panel; t = t.parent)
                m_SubFocusPath.Insert(0, t.GetSiblingIndex());
            m_RestoreSubFocus = true;
        }

        /// <summary>
        /// Keeps keyboard / gamepad focus inside a sub-panel and visible: rings the panel's buttons once built, gives
        /// focus back after an option change rebuilds the panel, and otherwise picks it up on the first directional /
        /// confirm press, as the card row does.
        /// </summary>
        private void HandleSubPanelFocus()
        {
            if (m_CurrentSubPanelGo == null) return;
            if (m_SubPanelFresh)
            {
                m_SubPanelFresh = false;
                PlayFocusRing.Attach(m_CurrentSubPanelGo);
            }
            var es = EventSystem.current;
            if (es == null) return;
            if (m_RestoreSubFocus)
            {
                m_RestoreSubFocus = false;
                Transform t = m_CurrentSubPanelGo.transform;
                for (int i = 0; i < m_SubFocusPath.Count && t != null; i++)
                    t = m_SubFocusPath[i] < t.childCount ? t.GetChild(m_SubFocusPath[i]) : null;
                if (t != null && t.GetComponent<Selectable>() != null)
                {
                    es.SetSelectedGameObject(t.gameObject);
                    return;
                }
            }
            var current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy) return;
#if ENABLE_INPUT_SYSTEM
            if (!NavigationPressed()) return;
            var first = m_CurrentSubPanelGo.GetComponentInChildren<Selectable>();
            if (first != null) es.SetSelectedGameObject(first.gameObject);
#endif
        }

        // =========================================================================
        // FOCUS (called by the cards)
        // =========================================================================
        internal void OnCardPointerEnter(PlayModeCard card)
        {
            if (!CanFocus()) return;
            SetActiveCard(card);
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != card.gameObject)
            {
                m_SelectingFromPointer = true;
                es.SetSelectedGameObject(card.gameObject);
                m_SelectingFromPointer = false;
            }
            card.SelectedByPointer = true;
        }

        internal void OnCardPointerExit(PlayModeCard card)
        {
            if (m_Flow != Flow.Idle) return; // keep the confirmed card lit while the screen hands over
            if (m_Active != card) return;
            SetActiveCard(null);
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == card.gameObject && card.SelectedByPointer)
                es.SetSelectedGameObject(null);
        }

        internal void OnCardSelected(PlayModeCard card)
        {
            if (!CanFocus()) return;
            card.SelectedByPointer = m_SelectingFromPointer;
            SetActiveCard(card);
        }

        internal void OnCardDeselected(PlayModeCard card)
        {
            if (m_Flow != Flow.Idle) return;
            if (m_Active == card && !card.PointerInside) SetActiveCard(null);
        }

        internal void ConfirmCard(PlayModeCard card)
        {
            if (!m_Open || m_Flow != Flow.Idle || m_IsInSubPanel || !card.IsShown) return;
            SetActiveCard(card);
            m_PendingMode = card.modeType;
            m_ConfirmIndex = card.Index;
            card.PlayConfirm();
            m_CardsGroup.interactable = false;
            m_Flow = Flow.Confirming;
            m_FlowTime = PlayMotion.Now;
            if (PlayMotion.Instant)
            {
                m_Flow = Flow.Idle;
                OpenSubPanel(m_PendingMode);
            }
        }

        private bool CanFocus()
        {
            return m_Open && !m_Closing && m_Flow == Flow.Idle && !m_IsInSubPanel;
        }

        private void SetActiveCard(PlayModeCard card)
        {
            if (m_Active == card) return;
            if (m_Active != null) m_LastActiveTime = PlayMotion.Now;
            m_Active = card;
            if (card != null) m_LastIndex = card.Index;
            UpdateFooterCounter();
        }

        private void ClearFocus()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<PlayModeCard>() != null)
                es.SetSelectedGameObject(null);
            m_Active = null;
            m_LastActiveTime = -10f;
        }

        private void FocusCard(int index)
        {
            if (index < 0 || index >= m_Cards.Count || !CanFocus()) return;
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(m_Cards[index].gameObject);
            else SetActiveCard(m_Cards[index]);
        }

        private void UpdateFooterCounter()
        {
            if (m_CounterText == null) return;
            string total = " / " + m_Cards.Count.ToString("00");
            if (m_IsInSubPanel)
            {
                m_CounterText.text = ((int)m_ActiveSubMode + 1).ToString("00") + total;
                m_CounterName.text = ModeTitle(m_ActiveSubMode);
                m_CounterName.color = SettingsTheme.Silver;
            }
            else if (m_Active != null)
            {
                m_CounterText.text = m_Active.modeNumber + total;
                m_CounterName.text = m_Active.title;
                m_CounterName.color = SettingsTheme.AccentLight;
            }
            else
            {
                m_CounterText.text = "—" + total;
                m_CounterName.text = "Choose a discipline";
                m_CounterName.color = SettingsTheme.Muted;
            }
        }

        // =========================================================================
        // FRAME
        // =========================================================================
        private void Update()
        {
            if (!m_Built) return;
            float now = PlayMotion.Now;

            if (m_Root != null && m_Root.rect.size != m_LastRootSize) LayoutScreen();

            // Screen fade and the Settings-style header / footer reveal.
            m_CanvasGroup.alpha = m_ScreenFade.Value;
            float hr = m_HeaderReveal.Value;
            m_HeaderGroup.alpha = hr;
            m_HeaderRt.anchoredPosition = new Vector2(0f, -HeaderTop - 14f * (1f - hr));
            float fr = m_FooterReveal.Value;
            m_FooterGroup.alpha = fr;
            m_FooterRt.anchoredPosition = new Vector2(0f, FooterBottom - 14f * (1f - fr));
            float tr = m_TitleReveal.Value;
            m_TitleGroup.alpha = tr;
            m_TitleBlock.anchoredPosition = new Vector2(-12f * (1f - tr), -18f);
            float sub = m_SubReveal.Value;
            m_SubGroup.alpha = sub;
            if (m_IsInSubPanel || m_Flow == Flow.SubOut)
                m_SubPanelContainer.anchoredPosition = m_SubHome + new Vector2(0f, -18f * (1f - sub));
            if (m_GridPromptsGroup != null)
                m_GridPromptsGroup.alpha = Mathf.MoveTowards(m_GridPromptsGroup.alpha, m_IsInSubPanel ? 0f : 1f, Time.unscaledDeltaTime * 6f);

            if (m_Closing && m_ScreenFade.Done)
            {
                FinishClose();
                return;
            }

            UpdateFlow(now);
            UpdateStageLighting(false);
            HandleNavigationInput(now);
        }

        private void UpdateFlow(float now)
        {
            switch (m_Flow)
            {
                case Flow.Confirming:
                    if (now - m_FlowTime >= 0.16f * AccertoneMotion.Scale)
                    {
                        // Cards fall away outward from the chosen one.
                        for (int i = 0; i < m_Cards.Count; i++)
                            m_Cards[i].Hide(Mathf.Abs(i - m_ConfirmIndex) * 0.04f, i == m_ConfirmIndex ? 0.34f : 0.28f);
                        m_Flow = Flow.GridOut;
                        m_FlowTime = now;
                    }
                    break;
                case Flow.GridOut:
                    if (now - m_FlowTime >= 0.34f * AccertoneMotion.Scale)
                    {
                        m_Flow = Flow.Idle;
                        m_Active = null;
                        OpenSubPanel(m_PendingMode);
                    }
                    break;
                case Flow.SubOut:
                    if (m_SubReveal.Done)
                    {
                        m_Flow = Flow.Idle;
                        DestroySubPanel();
                        ShowGrid(true, 0.02f);
                    }
                    break;
            }
        }

        private void UpdateStageLighting(bool snap)
        {
            if (m_Ambient == null) return;
            float now = PlayMotion.Now;
            float dt = snap ? 1f : Time.unscaledDeltaTime;

            // Slow parallax against the pointer.
            Vector2 n = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = pointer.position.ReadValue();
                n = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
#endif
            m_ParallaxX.Step(n.x, 0.6f, dt);
            m_ParallaxY.Step(n.y, 0.6f, dt);
            Vector2 parallax = new Vector2(m_ParallaxX.value * -22f, m_ParallaxY.value * -12f);

            float breathe = 0.5f + 0.5f * Mathf.Sin(now * 0.55f);
            m_GlowWide.rectTransform.anchoredPosition = m_RowCenter + new Vector2(Mathf.Sin(now * 0.11f) * 60f, -m_CardSize.y * 0.28f + Mathf.Sin(now * 0.17f) * 10f) + parallax;
            m_GlowWide.color = SettingsTheme.A(SettingsTheme.AccentDeep, 0.17f + 0.05f * breathe);
            m_GlowHigh.rectTransform.anchoredPosition = new Vector2(m_LastRootSize.x * 0.27f + Mathf.Cos(now * 0.09f) * 50f, m_LastRootSize.y * 0.26f + Mathf.Sin(now * 0.13f) * 22f) + parallax * 1.6f;
            m_GlowHigh.color = SettingsTheme.A(SettingsTheme.Accent, 0.045f + 0.025f * (1f - breathe));

            // Focus spot and floor accent glide to the active card.
            bool has = m_Active != null && !m_IsInSubPanel;
            if (has)
            {
                float x = ((RectTransform)m_Active.transform).anchoredPosition.x;
                // Fade in where the focus lands; once lit, glide between cards.
                if (m_SpotAlpha.value < 0.03f) m_SpotX.Snap(x);
                else m_SpotX.Step(x, 0.14f, dt);
            }
            if (snap && m_Active == null) m_SpotX.Snap(m_SpotX.value);
            m_SpotAlpha.Step(has ? 1f : 0f, has ? 0.16f : 0.22f, dt);
            float floorY = m_RowCenter.y - m_CardSize.y * 0.5f - 34f;
            m_Spot.rectTransform.anchoredPosition = new Vector2(m_SpotX.value, floorY + 6f);
            m_Spot.color = SettingsTheme.A(SettingsTheme.Accent, 0.2f * m_SpotAlpha.value);
            m_FloorAccent.rectTransform.anchoredPosition = new Vector2(m_SpotX.value, floorY);
            m_FloorAccent.canvasRenderer.SetAlpha(m_SpotAlpha.value);
        }

        private void HandleNavigationInput(float now)
        {
            if (!m_Open || m_Closing || m_Flow != Flow.Idle || now < m_InputReadyTime) return;
            if (m_IsInSubPanel)
            {
                HandleSubPanelFocus();
                return;
            }
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i < m_Cards.Count && i < 9; i++)
                {
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame || kb[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame)
                    {
                        FocusCard(i);
                        return;
                    }
                }
            }

            // Nothing selected yet (mouse was elsewhere): the first directional / confirm press picks up focus.
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject != null) return;
            if (NavigationPressed()) FocusCard(Mathf.Clamp(m_LastIndex, 0, m_Cards.Count - 1));
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool NavigationPressed()
        {
            var kb = Keyboard.current;
            if (kb != null &&
                (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                 kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                 kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                 kb.spaceKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            return pad != null &&
                   (pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame ||
                    pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
                    pad.leftStick.left.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame ||
                    pad.leftStick.up.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame ||
                    pad.buttonSouth.wasPressedThisFrame);
        }
#endif

        // =========================================================================
        // SUB-PANEL 1: QUICK MATCH
        // =========================================================================
        private void BuildQuickMatchSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // QUICK MATCH  ·  SINGLE PLAYER AI GRAND PRIX";

            m_CurrentSubPanelGo = BeginSubPanel("Panel_QuickMatch");
            var rootRt = (RectTransform)m_CurrentSubPanelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            // Left Box: Track & Car Information (680w x 680h)
            var leftRt = AccertoneUIFactory.CreatePanel(rootRt, "TrackPreview", new Vector2(680f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(0f, 0f);

            var tTitle = AccertoneUIFactory.CreateText(leftRt, "CircuitTitle", "AUTODROMO VALCORSA", BrandTextRole.Headline, Color.white);
            var ttRt = (RectTransform)tTitle.transform;
            ttRt.anchorMin = new Vector2(0f, 1f);
            ttRt.anchorMax = new Vector2(1f, 1f);
            ttRt.pivot = new Vector2(0f, 1f);
            ttRt.anchoredPosition = new Vector2(32f, -32f);
            tTitle.fontSize = 24f;

            var tSub = AccertoneUIFactory.CreateText(leftRt, "CircuitMeta", "INTERNATIONAL GRAND PRIX CIRCUIT  ·  ROUND 07 OF 24", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var tsRt = (RectTransform)tSub.transform;
            tsRt.anchorMin = new Vector2(0f, 1f);
            tsRt.anchorMax = new Vector2(1f, 1f);
            tsRt.pivot = new Vector2(0f, 1f);
            tsRt.anchoredPosition = new Vector2(32f, -66f);
            tSub.fontSize = 11f;

            // Track stats rows
            string[] statLabels = { "CIRCUIT LENGTH", "TURNS", "DRS ZONES", "LAP RECORD", "RECORD HOLDER", "CAR ALLOCATED" };
            string[] statVals = { "5.218 KM", "17 TURNS", "2 ZONES (MAIN & BACK STRAIGHT)", "1:29.874", "M. VERSTAPPEN (RB20)", "AC-26 WORKS RACING #01" };
            for (int i = 0; i < statLabels.Length; i++)
            {
                CreateStatRow(leftRt, statLabels[i], statVals[i], -120f - (i * 44f));
            }

            // Right Box: Race Session Configuration (880w x 680h)
            var rightRt = AccertoneUIFactory.CreatePanel(rootRt, "RaceSettings", new Vector2(880f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(0f, 0f);

            var sTitle = AccertoneUIFactory.CreateText(rightRt, "SettingsTitle", "SESSION CONFIGURATION", BrandTextRole.Headline, Color.white);
            var stRt = (RectTransform)sTitle.transform;
            stRt.anchorMin = new Vector2(0f, 1f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(0f, 1f);
            stRt.anchoredPosition = new Vector2(36f, -32f);
            sTitle.fontSize = 22f;

            var qm = GameModeManager.Instance != null ? GameModeManager.Instance.quickMatch : new QuickMatchConfig();

            // Config Option 1: AI Opponents (3 - 19)
            CreateConfigStepper(rightRt, "AI GRID SIZE", (qm.aiDriverCount + 1) + " CARS (" + qm.aiDriverCount + " AI OPPONENTS)", -90f, () => {
                qm.aiDriverCount = qm.aiDriverCount == 19 ? 9 : (qm.aiDriverCount == 9 ? 3 : 19);
                BuildQuickMatchSubPanel();
            });

            // Config Option 2: AI Difficulty
            string[] diffNames = { "AMATEUR (75% PACE)", "CLUBMAN (85% PACE)", "PRO (95% PACE)", "LEGEND (105% WORLD CLASS)" };
            CreateConfigStepper(rightRt, "AI DIFFICULTY", diffNames[Mathf.Clamp(qm.difficultyIndex, 0, 3)], -160f, () => {
                qm.difficultyIndex = (qm.difficultyIndex + 1) % diffNames.Length;
                BuildQuickMatchSubPanel();
            });

            // Config Option 3: Race Distance
            string[] lapOptions = { "3 LAPS (SPRINT)", "5 LAPS (SHORT)", "15 LAPS (25% DISTANCE)", "30 LAPS (50% DISTANCE)" };
            CreateConfigStepper(rightRt, "RACE DISTANCE", lapOptions[Mathf.Clamp(qm.lapDistanceIndex, 0, 3)], -230f, () => {
                qm.lapDistanceIndex = (qm.lapDistanceIndex + 1) % lapOptions.Length;
                BuildQuickMatchSubPanel();
            });

            // Config Option 4: Weather Conditions
            string[] weatherOptions = { "OPTIMAL DRY (24°C AIR / 38°C TRACK)", "OVERCAST (20°C AIR / 28°C TRACK)", "WET / INTERMEDIATE (17°C AIR)" };
            CreateConfigStepper(rightRt, "CONDITIONS", weatherOptions[Mathf.Clamp(qm.weatherIndex, 0, 2)], -300f, () => {
                qm.weatherIndex = (qm.weatherIndex + 1) % weatherOptions.Length;
                BuildQuickMatchSubPanel();
            });

            // Launch Hero Button
            var launchBtn = AccertoneUIFactory.CreateButton(rightRt, "LaunchButton", "START QUICK MATCH  //  DEPLOY TO GRID", new Vector2(808f, 54f), () => {
                GameModeManager.Instance?.LaunchSession(PlayModeType.QuickMatch);
            }, true);
            var lRt = (RectTransform)launchBtn.transform;
            lRt.anchorMin = new Vector2(0.5f, 0f);
            lRt.anchorMax = new Vector2(0.5f, 0f);
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.anchoredPosition = new Vector2(0f, 36f);
        }

        // =========================================================================
        // SUB-PANEL 2: ONLINE MULTIPLAYER
        // =========================================================================
        private void BuildMultiplayerSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // ONLINE MULTIPLAYER  ·  DEDICATED 20-PLAYER GRID";

            m_CurrentSubPanelGo = BeginSubPanel("Panel_Multiplayer");
            var rootRt = (RectTransform)m_CurrentSubPanelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            var gm = GameModeManager.Instance;

            // Left Box: Public Lobby Browser (760w x 680h)
            var leftRt = AccertoneUIFactory.CreatePanel(rootRt, "LobbyBrowser", new Vector2(760f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(0f, 0f);

            var lTitle = AccertoneUIFactory.CreateText(leftRt, "BrowserTitle", "PUBLIC LOBBIES", BrandTextRole.Headline, Color.white);
            var ltRt = (RectTransform)lTitle.transform;
            ltRt.anchorMin = new Vector2(0f, 1f);
            ltRt.anchorMax = new Vector2(1f, 1f);
            ltRt.pivot = new Vector2(0f, 1f);
            ltRt.anchoredPosition = new Vector2(28f, -28f);
            lTitle.fontSize = 20f;

            var serverInfo = AccertoneUIFactory.CreateText(leftRt, "ServerInfo", "REGION: EU-WEST  ·  PING: 24 MS  ·  NETWORKING READY", BrandTextRole.Caption, new Color(0.25f, 0.84f, 0.6f));
            var siRt = (RectTransform)serverInfo.transform;
            siRt.anchorMin = new Vector2(0f, 1f);
            siRt.anchorMax = new Vector2(1f, 1f);
            siRt.pivot = new Vector2(0f, 1f);
            siRt.anchoredPosition = new Vector2(28f, -56f);
            serverInfo.fontSize = 11f;

            // Lobby rooms list
            if (gm != null && gm.publicRooms != null)
            {
                for (int i = 0; i < gm.publicRooms.Count; i++)
                {
                    var room = gm.publicRooms[i];
                    var itemRt = AccertoneUIFactory.CreatePanel(leftRt, "Room_" + i, new Vector2(704f, 72f), new Color(0.11f, 0.13f, 0.16f, 1f), 8f);
                    itemRt.anchorMin = new Vector2(0.5f, 1f);
                    itemRt.anchorMax = new Vector2(0.5f, 1f);
                    itemRt.pivot = new Vector2(0.5f, 1f);
                    itemRt.anchoredPosition = new Vector2(0f, -90f - (i * 84f));

                    var rName = AccertoneUIFactory.CreateText(itemRt, "Name", room.roomName, BrandTextRole.Title, Color.white);
                    var rnRt = (RectTransform)rName.transform;
                    rnRt.anchorMin = new Vector2(0f, 1f);
                    rnRt.anchorMax = new Vector2(0f, 1f);
                    rnRt.pivot = new Vector2(0f, 1f);
                    rnRt.anchoredPosition = new Vector2(20f, -14f);
                    rnRt.sizeDelta = new Vector2(560f, 22f);
                    rName.textWrappingMode = TextWrappingModes.NoWrap;
                    rName.fontSize = 13f;

                    var rDetails = AccertoneUIFactory.CreateText(itemRt, "Details", room.circuit + "  ·  " + room.currentPlayers + "/" + room.maxPlayers + " DRIVERS  ·  " + room.pingMs + " MS", BrandTextRole.Caption, new Color(0.68f, 0.72f, 0.78f));
                    var rdRt = (RectTransform)rDetails.transform;
                    rdRt.anchorMin = new Vector2(0f, 1f);
                    rdRt.anchorMax = new Vector2(0f, 1f);
                    rdRt.pivot = new Vector2(0f, 1f);
                    rdRt.anchoredPosition = new Vector2(20f, -40f);
                    rdRt.sizeDelta = new Vector2(560f, 20f);
                    rDetails.textWrappingMode = TextWrappingModes.NoWrap;
                    rDetails.fontSize = 11f;

                    var joinBtn = AccertoneUIFactory.CreateButton(itemRt, "JoinBtn", "JOIN", new Vector2(80f, 32f), () => {
                        BuildMultiplayerSubPanel();
                    });
                    var jRt = (RectTransform)joinBtn.transform;
                    jRt.anchorMin = new Vector2(1f, 0.5f);
                    jRt.anchorMax = new Vector2(1f, 0.5f);
                    jRt.pivot = new Vector2(1f, 0.5f);
                    jRt.anchoredPosition = new Vector2(-16f, 0f);
                }
            }

            // Right Box: Active Room Grid Roster (800w x 680h)
            var rightRt = AccertoneUIFactory.CreatePanel(rootRt, "ActiveLobby", new Vector2(800f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(0f, 0f);

            var rTitle = AccertoneUIFactory.CreateText(rightRt, "RoomTitle", "CURRENT ROOM: AC-7921", BrandTextRole.Headline, Color.white);
            var rtRt = (RectTransform)rTitle.transform;
            rtRt.anchorMin = new Vector2(0f, 1f);
            rtRt.anchorMax = new Vector2(1f, 1f);
            rtRt.pivot = new Vector2(0f, 1f);
            rtRt.anchoredPosition = new Vector2(28f, -28f);
            rTitle.fontSize = 20f;

            var rMeta = AccertoneUIFactory.CreateText(rightRt, "RoomMeta", "AUTODROMO VALCORSA GP  ·  15 LAPS  ·  DYNAMIC WEATHER", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var rmRt = (RectTransform)rMeta.transform;
            rmRt.anchorMin = new Vector2(0f, 1f);
            rmRt.anchorMax = new Vector2(1f, 1f);
            rmRt.pivot = new Vector2(0f, 1f);
            rmRt.anchoredPosition = new Vector2(28f, -56f);
            rMeta.fontSize = 11f;

            // Driver slots
            if (gm != null && gm.activeLobbySlots != null)
            {
                for (int i = 0; i < gm.activeLobbySlots.Count; i++)
                {
                    var slot = gm.activeLobbySlots[i];
                    var slotRt = AccertoneUIFactory.CreatePanel(rightRt, "Slot_" + i, new Vector2(744f, 48f), slot.isLocalPlayer ? new Color(0.18f, 0.12f, 0.24f, 1f) : new Color(0.10f, 0.12f, 0.14f, 1f), 6f);
                    slotRt.anchorMin = new Vector2(0.5f, 1f);
                    slotRt.anchorMax = new Vector2(0.5f, 1f);
                    slotRt.pivot = new Vector2(0.5f, 1f);
                    slotRt.anchoredPosition = new Vector2(0f, -90f - (i * 54f));

                    var sName = AccertoneUIFactory.CreateText(slotRt, "SlotName", "P" + (i + 1) + "  ·  " + slot.gamerTag + " (" + slot.teamCar + ")", BrandTextRole.Label, Color.white);
                    var snRt = (RectTransform)sName.transform;
                    snRt.anchorMin = new Vector2(0f, 0.5f);
                    snRt.anchorMax = new Vector2(0f, 0.5f);
                    snRt.pivot = new Vector2(0f, 0.5f);
                    snRt.anchoredPosition = new Vector2(16f, 0f);
                    sName.fontSize = 12f;

                    var sStatus = AccertoneUIFactory.CreateText(slotRt, "Status", slot.isReady ? "READY" : "NOT READY", BrandTextRole.Label, slot.isReady ? new Color(0.25f, 0.84f, 0.6f) : new Color(0.85f, 0.35f, 0.35f), TextAlignmentOptions.Right);
                    var ssRt = (RectTransform)sStatus.transform;
                    ssRt.anchorMin = new Vector2(1f, 0.5f);
                    ssRt.anchorMax = new Vector2(1f, 0.5f);
                    ssRt.pivot = new Vector2(1f, 0.5f);
                    ssRt.anchoredPosition = new Vector2(-16f, 0f);
                    sStatus.fontSize = 12f;
                }
            }

            // Ready Toggle Button
            bool isReady = gm != null && gm.isLocalPlayerReady;
            var readyBtn = AccertoneUIFactory.CreateButton(rightRt, "ReadyBtn", isReady ? "CANCEL READY STATE" : "READY UP FOR RACE", new Vector2(360f, 50f), () => {
                gm?.ToggleLocalPlayerReady();
                BuildMultiplayerSubPanel();
            }, !isReady);
            var rdBtnRt = (RectTransform)readyBtn.transform;
            rdBtnRt.anchorMin = new Vector2(0f, 0f);
            rdBtnRt.anchorMax = new Vector2(0f, 0f);
            rdBtnRt.pivot = new Vector2(0f, 0f);
            rdBtnRt.anchoredPosition = new Vector2(28f, 28f);

            // Launch Race Button
            var startBtn = AccertoneUIFactory.CreateButton(rightRt, "StartRaceBtn", "LAUNCH MULTIPLAYER GRID", new Vector2(360f, 50f), () => {
                gm?.LaunchSession(PlayModeType.OnlineMultiplayer);
            }, isReady);
            var sbRt = (RectTransform)startBtn.transform;
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 0f);
            sbRt.pivot = new Vector2(1f, 0f);
            sbRt.anchoredPosition = new Vector2(-28f, 28f);
        }

        // =========================================================================
        // SUB-PANEL 3: CAREER
        // =========================================================================
        private void BuildCareerSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // CAREER  ·  2026 FIA FORMULA ONE WORLD CHAMPIONSHIP";

            m_CurrentSubPanelGo = BeginSubPanel("Panel_Career");
            var rootRt = (RectTransform)m_CurrentSubPanelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            var gm = GameModeManager.Instance;

            // Left Box: 2026 Season Calendar (960w x 680h)
            var leftRt = AccertoneUIFactory.CreatePanel(rootRt, "CalendarPanel", new Vector2(960f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(0f, 0f);

            var cTitle = AccertoneUIFactory.CreateText(leftRt, "CalendarTitle", "2026 CHAMPIONSHIP CALENDAR (24 ROUNDS)", BrandTextRole.Headline, Color.white);
            var ctRt = (RectTransform)cTitle.transform;
            ctRt.anchorMin = new Vector2(0f, 1f);
            ctRt.anchorMax = new Vector2(1f, 1f);
            ctRt.pivot = new Vector2(0f, 1f);
            ctRt.anchoredPosition = new Vector2(28f, -28f);
            cTitle.fontSize = 20f;

            var cSub = AccertoneUIFactory.CreateText(leftRt, "CalendarSub", "CURRENT EVENT: ROUND 04 // JAPANESE GRAND PRIX (SUZUKA)", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var csRt = (RectTransform)cSub.transform;
            csRt.anchorMin = new Vector2(0f, 1f);
            csRt.anchorMax = new Vector2(1f, 1f);
            csRt.pivot = new Vector2(0f, 1f);
            csRt.anchoredPosition = new Vector2(28f, -56f);
            cSub.fontSize = 11f;

            // Calendar items (Show first 8 rounds for clean display)
            if (gm != null && gm.careerRounds != null)
            {
                int count = Mathf.Min(8, gm.careerRounds.Count);
                for (int i = 0; i < count; i++)
                {
                    var round = gm.careerRounds[i];
                    bool isNext = (i == gm.currentRoundIndex);
                    var itemRt = AccertoneUIFactory.CreatePanel(leftRt, "Round_" + i, new Vector2(904f, 52f), isNext ? new Color(0.22f, 0.14f, 0.32f, 1f) : new Color(0.10f, 0.12f, 0.14f, 1f), 6f);
                    itemRt.anchorMin = new Vector2(0.5f, 1f);
                    itemRt.anchorMax = new Vector2(0.5f, 1f);
                    itemRt.pivot = new Vector2(0.5f, 1f);
                    itemRt.anchoredPosition = new Vector2(0f, -88f - (i * 58f));

                    string tag = round.isCompleted ? "[COMPLETED - P" + round.playerFinishPosition + " · +" + round.pointsAwarded + " PTS]" : (isNext ? "[NEXT ACTIVE EVENT]" : "[UPCOMING]");
                    Color tagCol = round.isCompleted ? new Color(0.25f, 0.84f, 0.6f) : (isNext ? new Color(0.85f, 0.35f, 1f) : new Color(0.55f, 0.58f, 0.64f));

                    var rName = AccertoneUIFactory.CreateText(itemRt, "RName", "ROUND " + round.roundNumber.ToString("00") + "  ·  " + round.grandPrixName.ToUpperInvariant() + " (" + round.country.ToUpperInvariant() + ")", BrandTextRole.Label, Color.white);
                    var rnRt = (RectTransform)rName.transform;
                    rnRt.anchorMin = new Vector2(0f, 0.5f);
                    rnRt.anchorMax = new Vector2(0f, 0.5f);
                    rnRt.pivot = new Vector2(0f, 0.5f);
                    rnRt.anchoredPosition = new Vector2(16f, 0f);
                    rName.fontSize = 12f;

                    var rStatus = AccertoneUIFactory.CreateText(itemRt, "RStatus", tag, BrandTextRole.Label, tagCol, TextAlignmentOptions.Right);
                    var rsRt = (RectTransform)rStatus.transform;
                    rsRt.anchorMin = new Vector2(1f, 0.5f);
                    rsRt.anchorMax = new Vector2(1f, 0.5f);
                    rsRt.pivot = new Vector2(1f, 0.5f);
                    rsRt.anchoredPosition = new Vector2(-16f, 0f);
                    rStatus.fontSize = 11f;
                }
            }

            // Right Box: Driver Profile & Standings Table (600w x 680h)
            var rightRt = AccertoneUIFactory.CreatePanel(rootRt, "DriverHub", new Vector2(600f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(0f, 0f);

            var dTitle = AccertoneUIFactory.CreateText(rightRt, "DriverTitle", "DRIVER PROFILE", BrandTextRole.Headline, Color.white);
            var dtRt = (RectTransform)dTitle.transform;
            dtRt.anchorMin = new Vector2(0f, 1f);
            dtRt.anchorMax = new Vector2(1f, 1f);
            dtRt.pivot = new Vector2(0f, 1f);
            dtRt.anchoredPosition = new Vector2(24f, -28f);
            dTitle.fontSize = 20f;

            CreateStatRow(rightRt, "DRIVER", gm != null ? gm.careerDriverName : "ALEXANDER VANCE", -70f);
            CreateStatRow(rightRt, "TEAM", gm != null ? gm.careerTeam : "ACCERT ONE RACING", -110f);
            CreateStatRow(rightRt, "SUPER LICENCE", gm != null ? gm.careerLicence : "GRADE A — ACTIVE", -150f);
            CreateStatRow(rightRt, "POINTS", (gm != null ? gm.careerPoints : 68) + " PTS (P3 IN STANDINGS)", -190f);

            // Standings preview title
            var stHeader = AccertoneUIFactory.CreateText(rightRt, "StandingsHeader", "CHAMPIONSHIP STANDINGS (TOP 5)", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var sthRt = (RectTransform)stHeader.transform;
            sthRt.anchorMin = new Vector2(0f, 1f);
            sthRt.anchorMax = new Vector2(1f, 1f);
            sthRt.pivot = new Vector2(0f, 1f);
            sthRt.anchoredPosition = new Vector2(24f, -240f);
            stHeader.fontSize = 11f;

            if (gm != null && gm.careerStandings != null)
            {
                int sCount = Mathf.Min(5, gm.careerStandings.Count);
                for (int i = 0; i < sCount; i++)
                {
                    var std = gm.careerStandings[i];
                    CreateStatRow(rightRt, "P" + std.rank + " " + std.driverName, std.points + " PTS", -270f - (i * 38f));
                }
            }

            // Enter race weekend button
            var enterBtn = AccertoneUIFactory.CreateButton(rightRt, "EnterWeekendBtn", "ENTER RACE WEEKEND  //  SUZUKA ROUND 04", new Vector2(552f, 52f), () => {
                gm?.LaunchSession(PlayModeType.Career);
            }, true);
            var ebRt = (RectTransform)enterBtn.transform;
            ebRt.anchorMin = new Vector2(0.5f, 0f);
            ebRt.anchorMax = new Vector2(0.5f, 0f);
            ebRt.pivot = new Vector2(0.5f, 0f);
            ebRt.anchoredPosition = new Vector2(0f, 28f);
        }

        // =========================================================================
        // SUB-PANEL 4: HOT LAP
        // =========================================================================
        private void BuildHotLapSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // HOT LAP  ·  QUALIFYING TIMING SHOOTOUT & DELTAS";

            m_CurrentSubPanelGo = BeginSubPanel("Panel_HotLap");
            var rootRt = (RectTransform)m_CurrentSubPanelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            var gm = GameModeManager.Instance;
            var hl = gm != null ? gm.hotLap : new HotLapData();

            // Left Box: Timing Deltas and Sector Splits (820w x 680h)
            var leftRt = AccertoneUIFactory.CreatePanel(rootRt, "TimingPanel", new Vector2(820f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(0f, 0f);

            var hTitle = AccertoneUIFactory.CreateText(leftRt, "HTitle", "AUTODROMO VALCORSA // HOT LAP", BrandTextRole.Headline, Color.white);
            var htRt = (RectTransform)hTitle.transform;
            htRt.anchorMin = new Vector2(0f, 1f);
            htRt.anchorMax = new Vector2(1f, 1f);
            htRt.pivot = new Vector2(0f, 1f);
            htRt.anchoredPosition = new Vector2(28f, -28f);
            hTitle.fontSize = 22f;

            var hSub = AccertoneUIFactory.CreateText(leftRt, "HSub", "BENCHMARK LAP RECORD: " + hl.benchmarkRecord + " (" + hl.benchmarkHolder + ")", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var hsRt = (RectTransform)hSub.transform;
            hsRt.anchorMin = new Vector2(0f, 1f);
            hsRt.anchorMax = new Vector2(1f, 1f);
            hsRt.pivot = new Vector2(0f, 1f);
            hsRt.anchoredPosition = new Vector2(28f, -60f);
            hSub.fontSize = 11f;

            // Delta comparison box
            var deltaBox = AccertoneUIFactory.CreatePanel(leftRt, "DeltaBox", new Vector2(764f, 120f), new Color(0.12f, 0.14f, 0.18f, 1f), 8f);
            deltaBox.anchorMin = new Vector2(0.5f, 1f);
            deltaBox.anchorMax = new Vector2(0.5f, 1f);
            deltaBox.pivot = new Vector2(0.5f, 1f);
            deltaBox.anchoredPosition = new Vector2(0f, -94f);

            var pbLabel = AccertoneUIFactory.CreateText(deltaBox, "PBLabel", "PERSONAL BEST LAP TIME", BrandTextRole.Caption, new Color(0.68f, 0.72f, 0.78f));
            var pblRt = (RectTransform)pbLabel.transform;
            pblRt.anchorMin = new Vector2(0f, 1f);
            pblRt.anchorMax = new Vector2(1f, 1f);
            pblRt.pivot = new Vector2(0f, 1f);
            pblRt.anchoredPosition = new Vector2(24f, -18f);
            pbLabel.fontSize = 11f;

            var pbVal = AccertoneUIFactory.CreateText(deltaBox, "PBVal", hl.personalBest, BrandTextRole.DataLarge, Color.white);
            var pbvRt = (RectTransform)pbVal.transform;
            pbvRt.anchorMin = new Vector2(0f, 0f);
            pbvRt.anchorMax = new Vector2(0f, 0f);
            pbvRt.pivot = new Vector2(0f, 0f);
            pbvRt.anchoredPosition = new Vector2(24f, 16f);
            pbVal.fontSize = 36f;

            var deltaVal = AccertoneUIFactory.CreateText(deltaBox, "DeltaVal", "+1.546s VS RECORD", BrandTextRole.Data, new Color(0.85f, 0.45f, 0.25f), TextAlignmentOptions.Right);
            var dvRt = (RectTransform)deltaVal.transform;
            dvRt.anchorMin = new Vector2(1f, 0.5f);
            dvRt.anchorMax = new Vector2(1f, 0.5f);
            dvRt.pivot = new Vector2(1f, 0.5f);
            dvRt.anchoredPosition = new Vector2(-24f, 0f);
            deltaVal.fontSize = 20f;

            // Sector Splits
            CreateStatRow(leftRt, "SECTOR 1 SPLIT", hl.s1Time.ToString("F3") + " s  ·  RECORD: " + hl.benchmarkS1.ToString("F3") + " s (+0.390s)", -240f);
            CreateStatRow(leftRt, "SECTOR 2 SPLIT", hl.s2Time.ToString("F3") + " s  ·  RECORD: " + hl.benchmarkS2.ToString("F3") + " s (+0.610s)", -290f);
            CreateStatRow(leftRt, "SECTOR 3 SPLIT", hl.s3Time.ToString("F3") + " s  ·  RECORD: " + hl.benchmarkS3.ToString("F3") + " s (+0.546s)", -340f);
            CreateStatRow(leftRt, "MAX SPEED SPEEDTRAP", "338.4 KM/H (MAIN STRAIGHT DRS)", -390f);
            CreateStatRow(leftRt, "TRACK LIMITS RULE", "STRICT FIA RULESET (LAP INVALIDATION ON 4 WHEELS OUT)", -440f);

            // Right Box: Shootout Launch Settings (740w x 680h)
            var rightRt = AccertoneUIFactory.CreatePanel(rootRt, "SettingsPanel", new Vector2(740f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(0f, 0f);

            var sTitle = AccertoneUIFactory.CreateText(rightRt, "STitle", "SHOOTOUT CONDITIONS", BrandTextRole.Headline, Color.white);
            var stRt = (RectTransform)sTitle.transform;
            stRt.anchorMin = new Vector2(0f, 1f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(0f, 1f);
            stRt.anchoredPosition = new Vector2(28f, -28f);
            sTitle.fontSize = 20f;

            string[] tireOptions = { "SOFT COMPOUND (C5 - MAXIMUM ONE-LAP GRIP)", "MEDIUM COMPOUND (C3)", "HARD COMPOUND (C1)" };
            CreateConfigStepper(rightRt, "TYRE COMPOUND", tireOptions[Mathf.Clamp(hl.tireCompoundIndex, 0, 2)], -80f, () => {
                hl.tireCompoundIndex = (hl.tireCompoundIndex + 1) % tireOptions.Length;
                BuildHotLapSubPanel();
            });

            CreateConfigStepper(rightRt, "GHOST VEHICLE", hl.ghostEnabled ? "ENABLED (PERSONAL BEST GHOST)" : "DISABLED", -150f, () => {
                hl.ghostEnabled = !hl.ghostEnabled;
                BuildHotLapSubPanel();
            });

            CreateStatRow(rightRt, "FUEL LOAD", "10 KG (OPTIMAL QUALIFYING WEIGHT)", -230f);
            CreateStatRow(rightRt, "ERS DEPLOYMENT", "QUALIFYING HOT LAP (100% ELECTRICAL BOOST)", -280f);
            CreateStatRow(rightRt, "TRACK SURFACE", "RUBBERED IN  ·  OPTIMAL 100% GRIP  ·  38°C TRACK", -330f);

            // Deploy Button
            var deployBtn = AccertoneUIFactory.CreateButton(rightRt, "DeployBtn", "DEPLOY FOR HOT LAP  //  OUT-LAP FLYING START", new Vector2(684f, 54f), () => {
                gm?.LaunchSession(PlayModeType.HotLap);
            }, true);
            var dbRt = (RectTransform)deployBtn.transform;
            dbRt.anchorMin = new Vector2(0.5f, 0f);
            dbRt.anchorMax = new Vector2(0.5f, 0f);
            dbRt.pivot = new Vector2(0.5f, 0f);
            dbRt.anchoredPosition = new Vector2(0f, 32f);
        }

        // =========================================================================
        // SUB-PANEL 5: FREE PRACTICE
        // =========================================================================
        private void BuildPracticeSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // FREE PRACTICE  ·  TESTING & SETUP TUNING";

            m_CurrentSubPanelGo = BeginSubPanel("Panel_Practice");
            var rootRt = (RectTransform)m_CurrentSubPanelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            var gm = GameModeManager.Instance;
            var pr = gm != null ? gm.practice : new PracticeSessionData();

            // Left Box: Telemetry & Active Engineering Specs (820w x 680h)
            var leftRt = AccertoneUIFactory.CreatePanel(rootRt, "PracticeTelemetry", new Vector2(820f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(0f, 0f);

            var pTitle = AccertoneUIFactory.CreateText(leftRt, "PTitle", "ACTIVE VEHICLE SETUP // AC-26", BrandTextRole.Headline, Color.white);
            var ptRt = (RectTransform)pTitle.transform;
            ptRt.anchorMin = new Vector2(0f, 1f);
            ptRt.anchorMax = new Vector2(1f, 1f);
            ptRt.pivot = new Vector2(0f, 1f);
            ptRt.anchoredPosition = new Vector2(28f, -28f);
            pTitle.fontSize = 22f;

            var pSub = AccertoneUIFactory.CreateText(leftRt, "PSub", "PRESET: " + pr.activePreset.ToUpperInvariant() + "  ·  REAL-TIME TELEMETRY LINKED", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var psRt = (RectTransform)pSub.transform;
            psRt.anchorMin = new Vector2(0f, 1f);
            psRt.anchorMax = new Vector2(1f, 1f);
            psRt.pivot = new Vector2(0f, 1f);
            psRt.anchoredPosition = new Vector2(28f, -60f);
            pSub.fontSize = 11f;

            // Setup telemetry parameters
            CreateStatRow(leftRt, "AERODYNAMIC BALANCE", pr.aeroBalance.ToString("F1") + "% FRONT (FW: " + pr.frontWingAngle + "° / RW: " + pr.rearWingAngle + "°)", -100f);
            CreateStatRow(leftRt, "RIDE HEIGHT & RAKE", "F: " + pr.rideHeightFront + "mm / R: " + pr.rideHeightRear + "mm (RAKE: +6mm)", -150f);
            CreateStatRow(leftRt, "DIFFERENTIAL LOCK", "ON-THROTTLE: " + pr.diffOnThrottle + "%  ·  OFF-THROTTLE: " + pr.diffOffThrottle + "%", -200f);
            CreateStatRow(leftRt, "BRAKE BIAS", pr.brakeBiasFront.ToString("F1") + "% FRONT  ·  100% PRESSURE", -250f);
            CreateStatRow(leftRt, "TYRE PRESSURES", "F: " + pr.tyrePressureFront + " PSI  ·  R: " + pr.tyrePressureRear + " PSI", -300f);
            CreateStatRow(leftRt, "ENGINE MAP & FUEL", "MAP 1 (RACE)  ·  110 KG CAPACITY (TESTING LOAD: 25 KG)", -350f);

            // Direct button to open the full Car Setup engineering system!
            var openSetupBtn = AccertoneUIFactory.CreateButton(leftRt, "OpenSetupBtn", "OPEN DETAILED CAR SETUP // 35+ PARAMETERS", new Vector2(764f, 54f), () => {
                OnOpenCarSetupRequested?.Invoke();
            }, false);
            var osRt = (RectTransform)openSetupBtn.transform;
            osRt.anchorMin = new Vector2(0.5f, 0f);
            osRt.anchorMax = new Vector2(0.5f, 0f);
            osRt.pivot = new Vector2(0.5f, 0f);
            osRt.anchoredPosition = new Vector2(0f, 32f);

            // Right Box: Practice Run Launch (740w x 680h)
            var rightRt = AccertoneUIFactory.CreatePanel(rootRt, "PracticeLaunchPanel", new Vector2(740f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(0f, 0f);

            var rTitle = AccertoneUIFactory.CreateText(rightRt, "RTitle", "PRACTICE SESSION SETTINGS", BrandTextRole.Headline, Color.white);
            var rtRt = (RectTransform)rTitle.transform;
            rtRt.anchorMin = new Vector2(0f, 1f);
            rtRt.anchorMax = new Vector2(1f, 1f);
            rtRt.pivot = new Vector2(0f, 1f);
            rtRt.anchoredPosition = new Vector2(28f, -28f);
            rTitle.fontSize = 20f;

            string[] presets = { "Balanced Setup", "Maximum Downforce (Street/Monaco)", "Low Drag Speed (Monza)", "Wet Weather Setup" };
            CreateConfigStepper(rightRt, "ACTIVE SETUP PRESET", pr.activePreset, -80f, () => {
                int next = (Array.IndexOf(presets, pr.activePreset) + 1) % presets.Length;
                pr.activePreset = presets[next];
                BuildPracticeSubPanel();
            });

            CreateStatRow(rightRt, "CIRCUIT", "AUTODROMO VALCORSA (PITLANE EXIT)", -160f);
            CreateStatRow(rightRt, "SESSION DURATION", "UNLIMITED OPEN RUN (EXIT VIA ESC MENU)", -210f);
            CreateStatRow(rightRt, "TRACK CONDITION", "OPTIMAL DRY  ·  24°C AIR / 38°C TRACK", -260f);
            CreateStatRow(rightRt, "TELEMETRY LOGGING", "LIVE REAL-TIME G-FORCE & WHEEL SLIP", -310f);

            // Start Practice Run Button
            var startPracBtn = AccertoneUIFactory.CreateButton(rightRt, "StartPracBtn", "START PRACTICE RUN  //  EXIT PIT LANE", new Vector2(684f, 54f), () => {
                gm?.LaunchSession(PlayModeType.FreePractice);
            }, true);
            var spRt = (RectTransform)startPracBtn.transform;
            spRt.anchorMin = new Vector2(0.5f, 0f);
            spRt.anchorMax = new Vector2(0.5f, 0f);
            spRt.pivot = new Vector2(0.5f, 0f);
            spRt.anchoredPosition = new Vector2(0f, 32f);
        }

        // =========================================================================
        // SUB-PANEL 6: PLAY WITH FRIENDS
        // =========================================================================
        private void BuildFriendsSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // PLAY WITH FRIENDS  ·  PRIVATE INVITE-ONLY SESSION";

            m_CurrentSubPanelGo = BeginSubPanel("Panel_Friends");
            var rootRt = (RectTransform)m_CurrentSubPanelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            var gm = GameModeManager.Instance;
            var fs = gm != null ? gm.friendSession : new FriendSessionConfig();
            var friends = gm != null ? gm.friends : new List<FriendEntry>();
            int online = 0;
            foreach (var f in friends) if (f.isOnline) online++;
            int inLobby = gm != null ? gm.FriendsInLobby : 0;
            int invited = gm != null ? gm.FriendsInvited : 0;

            // Left Box: Friends & room code (760w x 680h)
            var leftRt = AccertoneUIFactory.CreatePanel(rootRt, "FriendsList", new Vector2(760f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            leftRt.anchorMin = new Vector2(0f, 0.5f);
            leftRt.anchorMax = new Vector2(0f, 0.5f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = Vector2.zero;

            var lTitle = AccertoneUIFactory.CreateText(leftRt, "FriendsTitle", "FRIENDS", BrandTextRole.Headline, Color.white);
            var ltRt = (RectTransform)lTitle.transform;
            ltRt.anchorMin = new Vector2(0f, 1f);
            ltRt.anchorMax = new Vector2(1f, 1f);
            ltRt.pivot = new Vector2(0f, 1f);
            ltRt.anchoredPosition = new Vector2(28f, -28f);
            lTitle.fontSize = 20f;

            var lMeta = AccertoneUIFactory.CreateText(leftRt, "FriendsMeta", online + " ONLINE  ·  " + inLobby + " IN LOBBY  ·  " + invited + " INVITED", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var lmRt = (RectTransform)lMeta.transform;
            lmRt.anchorMin = new Vector2(0f, 1f);
            lmRt.anchorMax = new Vector2(1f, 1f);
            lmRt.pivot = new Vector2(0f, 1f);
            lmRt.anchoredPosition = new Vector2(28f, -56f);
            lMeta.fontSize = 11f;

            // Room code: friends can also join by typing it in.
            var codeRt = AccertoneUIFactory.CreatePanel(leftRt, "RoomCode", new Vector2(704f, 72f), new Color(0.12f, 0.14f, 0.18f, 1f), 8f);
            codeRt.anchorMin = new Vector2(0.5f, 1f);
            codeRt.anchorMax = new Vector2(0.5f, 1f);
            codeRt.pivot = new Vector2(0.5f, 1f);
            codeRt.anchoredPosition = new Vector2(0f, -88f);

            var codeLabel = AccertoneUIFactory.CreateText(codeRt, "CodeLabel", "PRIVATE ROOM CODE", BrandTextRole.Caption, new Color(0.62f, 0.66f, 0.72f));
            var clRt = (RectTransform)codeLabel.transform;
            clRt.anchorMin = new Vector2(0f, 1f);
            clRt.anchorMax = new Vector2(0f, 1f);
            clRt.pivot = new Vector2(0f, 1f);
            clRt.anchoredPosition = new Vector2(20f, -12f);
            clRt.sizeDelta = new Vector2(300f, 16f);
            codeLabel.fontSize = 11f;

            var codeValue = AccertoneUIFactory.CreateText(codeRt, "CodeValue", fs.roomCode, BrandTextRole.DataLarge, Color.white);
            var cvRt = (RectTransform)codeValue.transform;
            cvRt.anchorMin = new Vector2(0f, 0f);
            cvRt.anchorMax = new Vector2(0f, 0f);
            cvRt.pivot = new Vector2(0f, 0f);
            cvRt.anchoredPosition = new Vector2(20f, 8f);
            cvRt.sizeDelta = new Vector2(300f, 34f);
            codeValue.fontSize = 26f;
            codeValue.textWrappingMode = TextWrappingModes.NoWrap;

            Button copyBtn = null;
            copyBtn = AccertoneUIFactory.CreateButton(codeRt, "CopyCodeBtn", "COPY CODE", new Vector2(150f, 36f), () => {
                GUIUtility.systemCopyBuffer = fs.roomCode;
                var label = copyBtn != null ? copyBtn.GetComponentInChildren<TMP_Text>() : null;
                if (label != null) label.text = "COPIED";
            });
            var cbRt = (RectTransform)copyBtn.transform;
            cbRt.anchorMin = new Vector2(1f, 0.5f);
            cbRt.anchorMax = new Vector2(1f, 0.5f);
            cbRt.pivot = new Vector2(1f, 0.5f);
            cbRt.anchoredPosition = new Vector2(-16f, 0f);

            // Friend rows
            for (int i = 0; i < friends.Count && i < 7; i++)
            {
                int index = i;
                var friend = friends[i];
                var rowRt = AccertoneUIFactory.CreatePanel(leftRt, "Friend_" + i, new Vector2(704f, 56f),
                    friend.hasJoined ? new Color(0.18f, 0.12f, 0.24f, 1f) : new Color(0.10f, 0.12f, 0.14f, 1f), 6f);
                rowRt.anchorMin = new Vector2(0.5f, 1f);
                rowRt.anchorMax = new Vector2(0.5f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -176f - i * 62f);

                var dot = SettingsUI.Diamond(rowRt, "Presence",
                    friend.hasJoined ? SettingsTheme.Accent : (friend.isOnline ? SettingsTheme.Positive : SettingsTheme.Muted), 7f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                dot.rectTransform.anchoredPosition = new Vector2(22f, 0f);

                var nameText = AccertoneUIFactory.CreateText(rowRt, "Name", friend.gamerTag, BrandTextRole.Title, friend.isOnline ? Color.white : new Color(0.62f, 0.66f, 0.72f));
                var nRt = (RectTransform)nameText.transform;
                nRt.anchorMin = new Vector2(0f, 1f);
                nRt.anchorMax = new Vector2(0f, 1f);
                nRt.pivot = new Vector2(0f, 1f);
                nRt.anchoredPosition = new Vector2(40f, -9f);
                nRt.sizeDelta = new Vector2(420f, 20f);
                nameText.textWrappingMode = TextWrappingModes.NoWrap;
                nameText.fontSize = 13f;

                var status = AccertoneUIFactory.CreateText(rowRt, "Status", friend.status + (friend.isOnline ? "  ·  " + friend.ping + " MS" : ""), BrandTextRole.Caption, new Color(0.62f, 0.66f, 0.72f));
                var sRt = (RectTransform)status.transform;
                sRt.anchorMin = new Vector2(0f, 0f);
                sRt.anchorMax = new Vector2(0f, 0f);
                sRt.pivot = new Vector2(0f, 0f);
                sRt.anchoredPosition = new Vector2(40f, 8f);
                sRt.sizeDelta = new Vector2(460f, 18f);
                status.textWrappingMode = TextWrappingModes.NoWrap;
                status.fontSize = 10f;

                if (friend.hasJoined || !friend.isOnline)
                {
                    var state = AccertoneUIFactory.CreateText(rowRt, "State", friend.hasJoined ? "IN LOBBY" : "OFFLINE", BrandTextRole.Label,
                        friend.hasJoined ? new Color(0.85f, 0.55f, 1f) : new Color(0.42f, 0.44f, 0.49f), TextAlignmentOptions.Right);
                    var stRt = (RectTransform)state.transform;
                    stRt.anchorMin = stRt.anchorMax = new Vector2(1f, 0.5f);
                    stRt.pivot = new Vector2(1f, 0.5f);
                    stRt.anchoredPosition = new Vector2(-20f, 0f);
                    stRt.sizeDelta = new Vector2(150f, 20f);
                    state.fontSize = 11f;
                }
                else
                {
                    var inviteBtn = AccertoneUIFactory.CreateButton(rowRt, "InviteBtn", friend.isInvited ? "INVITE SENT" : "INVITE", new Vector2(130f, 34f), () => {
                        if (gm != null) gm.ToggleFriendInvite(index);
                        BuildFriendsSubPanel();
                    }, !friend.isInvited);
                    var ibRt = (RectTransform)inviteBtn.transform;
                    ibRt.anchorMin = ibRt.anchorMax = new Vector2(1f, 0.5f);
                    ibRt.pivot = new Vector2(1f, 0.5f);
                    ibRt.anchoredPosition = new Vector2(-12f, 0f);
                }
            }

            var hint = AccertoneUIFactory.CreateText(leftRt, "JoinHint", "FRIENDS JOIN AS SOON AS THEY ACCEPT  ·  ANYONE WITH THE ROOM CODE CAN JOIN", BrandTextRole.Caption, SettingsTheme.Muted);
            var hRt = (RectTransform)hint.transform;
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0f, 0f);
            hRt.anchoredPosition = new Vector2(28f, 26f);
            hRt.sizeDelta = new Vector2(-56f, 16f);
            hint.fontSize = 10f;
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            // Right Box: Private session rules (800w x 680h)
            var rightRt = AccertoneUIFactory.CreatePanel(rootRt, "PrivateSession", new Vector2(800f, 680f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 12f);
            rightRt.anchorMin = new Vector2(1f, 0.5f);
            rightRt.anchorMax = new Vector2(1f, 0.5f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = Vector2.zero;

            var rTitle = AccertoneUIFactory.CreateText(rightRt, "SessionTitle", "PRIVATE SESSION", BrandTextRole.Headline, Color.white);
            var rtRt = (RectTransform)rTitle.transform;
            rtRt.anchorMin = new Vector2(0f, 1f);
            rtRt.anchorMax = new Vector2(1f, 1f);
            rtRt.pivot = new Vector2(0f, 1f);
            rtRt.anchoredPosition = new Vector2(28f, -28f);
            rTitle.fontSize = 20f;

            var rMeta = AccertoneUIFactory.CreateText(rightRt, "SessionMeta", "HOST: YOU  ·  " + (inLobby + 1) + " / 20 DRIVERS  ·  YOUR RULES", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var rmRt = (RectTransform)rMeta.transform;
            rmRt.anchorMin = new Vector2(0f, 1f);
            rmRt.anchorMax = new Vector2(1f, 1f);
            rmRt.pivot = new Vector2(0f, 1f);
            rmRt.anchoredPosition = new Vector2(28f, -56f);
            rMeta.fontSize = 11f;

            string[] lapOptions = { "3 LAPS (SPRINT)", "5 LAPS (SHORT)", "10 LAPS (MEDIUM)", "15 LAPS (LONG)" };
            string[] weatherOptions = { "DRY", "OVERCAST", "WET" };
            string[] assistOptions = { "ALLOWED", "PRO (NO ASSISTS)" };

            CreateConfigStepper(rightRt, "CIRCUIT", FriendSessionConfig.Circuits[Mathf.Clamp(fs.circuitIndex, 0, FriendSessionConfig.Circuits.Length - 1)].ToUpperInvariant(), -90f, () => {
                fs.circuitIndex = (fs.circuitIndex + 1) % FriendSessionConfig.Circuits.Length;
                BuildFriendsSubPanel();
            });
            CreateConfigStepper(rightRt, "RACE DISTANCE", lapOptions[Mathf.Clamp(fs.lapIndex, 0, lapOptions.Length - 1)], -160f, () => {
                fs.lapIndex = (fs.lapIndex + 1) % lapOptions.Length;
                BuildFriendsSubPanel();
            });
            CreateConfigStepper(rightRt, "CONDITIONS", weatherOptions[Mathf.Clamp(fs.weatherIndex, 0, weatherOptions.Length - 1)], -230f, () => {
                fs.weatherIndex = (fs.weatherIndex + 1) % weatherOptions.Length;
                BuildFriendsSubPanel();
            });
            CreateConfigStepper(rightRt, "COLLISIONS", fs.collisions ? "ON" : "OFF (GHOSTED CARS)", -300f, () => {
                fs.collisions = !fs.collisions;
                BuildFriendsSubPanel();
            });
            CreateConfigStepper(rightRt, "DRIVING ASSISTS", assistOptions[Mathf.Clamp(fs.assistsIndex, 0, assistOptions.Length - 1)], -370f, () => {
                fs.assistsIndex = (fs.assistsIndex + 1) % assistOptions.Length;
                BuildFriendsSubPanel();
            });

            // Lobby: drivers already in, then pending invites, then open slots.
            var lobbyTitle = AccertoneUIFactory.CreateText(rightRt, "LobbyTitle",
                "LOBBY  ·  " + (inLobby + 1) + " / 20 DRIVERS" + (invited > 0 ? "  ·  " + invited + (invited == 1 ? " INVITE" : " INVITES") + " PENDING" : ""),
                BrandTextRole.Caption, new Color(0.62f, 0.66f, 0.72f));
            var lbRt = (RectTransform)lobbyTitle.transform;
            lbRt.anchorMin = new Vector2(0f, 1f);
            lbRt.anchorMax = new Vector2(1f, 1f);
            lbRt.pivot = new Vector2(0f, 1f);
            lbRt.anchoredPosition = new Vector2(28f, -448f);
            lbRt.sizeDelta = new Vector2(-56f, 16f);
            lobbyTitle.fontSize = 11f;

            int slot = 0;
            CreateLobbySlot(rightRt, slot++, "YOU", "HOST", true);
            foreach (var f in friends)
                if (f.hasJoined && slot < LobbySlots) CreateLobbySlot(rightRt, slot++, f.gamerTag, "READY", true);
            foreach (var f in friends)
                if (f.isInvited && !f.hasJoined && slot < LobbySlots) CreateLobbySlot(rightRt, slot++, f.gamerTag, "INVITED", false);
            while (slot < LobbySlots) CreateLobbySlot(rightRt, slot++, null, "OPEN SLOT", false);

            var startBtn = AccertoneUIFactory.CreateButton(rightRt, "StartPrivateBtn", "START PRIVATE RACE  //  " + (inLobby + 1) + " DRIVERS READY", new Vector2(744f, 54f), () => {
                gm?.LaunchSession(PlayModeType.PlayWithFriends);
            }, true);
            var sbRt = (RectTransform)startBtn.transform;
            sbRt.anchorMin = new Vector2(0.5f, 0f);
            sbRt.anchorMax = new Vector2(0.5f, 0f);
            sbRt.pivot = new Vector2(0.5f, 0f);
            sbRt.anchoredPosition = new Vector2(0f, 32f);
        }

        private const int LobbySlots = 8;

        /// <summary>One chip of the private-session lobby: four per row under the rules, name over state.</summary>
        private void CreateLobbySlot(RectTransform parent, int index, string driver, string state, bool inLobby)
        {
            const float w = 178.5f, h = 42f, gap = 10f;
            bool open = driver == null;
            var chip = AccertoneUIFactory.CreatePanel(parent, "LobbySlot_" + index, new Vector2(w, h),
                inLobby ? new Color(0.18f, 0.12f, 0.24f, 1f) : (open ? new Color(0.09f, 0.10f, 0.12f, 1f) : new Color(0.11f, 0.13f, 0.16f, 1f)), 6f);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = new Vector2(28f + (index % 4) * (w + gap), -472f - (index / 4) * (h + 8f));

            if (open)
            {
                var label = AccertoneUIFactory.CreateText(chip, "State", state, BrandTextRole.Caption, new Color(0.42f, 0.44f, 0.49f), TextAlignmentOptions.Center);
                var lRt = (RectTransform)label.transform;
                lRt.anchorMin = Vector2.zero;
                lRt.anchorMax = Vector2.one;
                lRt.sizeDelta = Vector2.zero;
                label.fontSize = 9f;
                return;
            }

            var dot = SettingsUI.Diamond(chip, "Presence", inLobby ? SettingsTheme.Accent : SettingsTheme.Muted, 6f);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dot.rectTransform.anchoredPosition = new Vector2(16f, 0f);

            var driverText = AccertoneUIFactory.CreateText(chip, "Name", driver, BrandTextRole.Title, inLobby ? Color.white : new Color(0.75f, 0.78f, 0.83f));
            var nRt = (RectTransform)driverText.transform;
            nRt.anchorMin = nRt.anchorMax = new Vector2(0f, 1f);
            nRt.pivot = new Vector2(0f, 1f);
            nRt.anchoredPosition = new Vector2(30f, -6f);
            nRt.sizeDelta = new Vector2(w - 40f, 18f);
            driverText.fontSize = 12f;
            driverText.textWrappingMode = TextWrappingModes.NoWrap;
            driverText.overflowMode = TextOverflowModes.Ellipsis;

            var st = AccertoneUIFactory.CreateText(chip, "State", state, BrandTextRole.Caption, inLobby ? new Color(0.85f, 0.55f, 1f) : new Color(0.62f, 0.66f, 0.72f));
            var sRt = (RectTransform)st.transform;
            sRt.anchorMin = sRt.anchorMax = new Vector2(0f, 0f);
            sRt.pivot = new Vector2(0f, 0f);
            sRt.anchoredPosition = new Vector2(30f, 5f);
            sRt.sizeDelta = new Vector2(w - 40f, 14f);
            st.fontSize = 9f;
        }

        // =========================================================================
        // REUSABLE UI HELPERS
        // =========================================================================
        private void CreateStatRow(Transform parent, string label, string value, float yPos)
        {
            var rowGo = new GameObject("StatRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rt = (RectTransform)rowGo.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(rt.parent.GetComponent<RectTransform>().sizeDelta.x - 56f, 32f);

            var lText = AccertoneUIFactory.CreateText(rowGo.transform, "Label", label.ToUpperInvariant(), BrandTextRole.Caption, new Color(0.62f, 0.66f, 0.72f));
            var lRt = (RectTransform)lText.transform;
            lRt.anchorMin = new Vector2(0f, 0.5f);
            lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = new Vector2(280f, 32f);
            lText.textWrappingMode = TextWrappingModes.NoWrap;
            lText.fontSize = 11f;

            var vText = AccertoneUIFactory.CreateText(rowGo.transform, "Val", value, BrandTextRole.Data, Color.white, TextAlignmentOptions.Right);
            var vRt = (RectTransform)vText.transform;
            vRt.anchorMin = new Vector2(1f, 0.5f);
            vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.pivot = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = Vector2.zero;
            vRt.sizeDelta = new Vector2(460f, 32f);
            vText.textWrappingMode = TextWrappingModes.NoWrap;
            vText.fontSize = 12f;
        }

        private void CreateConfigStepper(Transform parent, string title, string currentValue, float yPos, Action onNext)
        {
            var boxRt = AccertoneUIFactory.CreatePanel(parent, "ConfigBox", new Vector2(parent.GetComponent<RectTransform>().sizeDelta.x - 56f, 56f), new Color(0.11f, 0.13f, 0.16f, 1f), 6f);
            boxRt.anchorMin = new Vector2(0.5f, 1f);
            boxRt.anchorMax = new Vector2(0.5f, 1f);
            boxRt.pivot = new Vector2(0.5f, 1f);
            boxRt.anchoredPosition = new Vector2(0f, yPos);

            var lText = AccertoneUIFactory.CreateText(boxRt, "Title", title.ToUpperInvariant(), BrandTextRole.Caption, new Color(0.62f, 0.66f, 0.72f));
            var lRt = (RectTransform)lText.transform;
            lRt.anchorMin = new Vector2(0f, 0.5f);
            lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = new Vector2(16f, 0f);
            lText.fontSize = 11f;

            var vText = AccertoneUIFactory.CreateText(boxRt, "Val", currentValue, BrandTextRole.Label, Color.white, TextAlignmentOptions.Right);
            var vRt = (RectTransform)vText.transform;
            vRt.anchorMin = new Vector2(1f, 0.5f);
            vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.pivot = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = new Vector2(-120f, 0f);
            // Everything between the title and the button, on one line; long values shrink a little before they clip.
            float titleWidth = lText.GetPreferredValues(lText.text).x;
            vRt.sizeDelta = new Vector2(Mathf.Max(120f, boxRt.sizeDelta.x - 120f - 16f - titleWidth - 24f), 24f);
            vText.fontSize = 12f;
            vText.enableAutoSizing = true;
            vText.fontSizeMin = 10f;
            vText.fontSizeMax = 12f;
            vText.textWrappingMode = TextWrappingModes.NoWrap;
            vText.overflowMode = TextOverflowModes.Ellipsis;

            var stepBtn = AccertoneUIFactory.CreateButton(boxRt, "ChangeBtn", "CHANGE >", new Vector2(90f, 32f), () => onNext?.Invoke());
            var bRt = (RectTransform)stepBtn.transform;
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-12f, 0f);
        }
    }
}
