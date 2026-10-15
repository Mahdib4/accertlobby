using System;
using System.Collections.Generic;
using Accertone.Branding;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// Master UI for the Accert one Play game-mode selection system.
    /// Manages the 5 core game modes:
    /// 1. Quick Match
    /// 2. Online Multiplayer
    /// 3. Career
    /// 4. Hot Lap
    /// 5. Free Practice
    /// </summary>
    public class PlayModeSelectionUI : MonoBehaviour
    {
        public static PlayModeSelectionUI Instance { get; private set; }

        public Action OnCloseRequested;
        public Action OnOpenCarSetupRequested;

        private CanvasGroup m_CanvasGroup;
        private RectTransform m_HeaderRt;
        private RectTransform m_CardsContainer;
        private RectTransform m_SubPanelContainer;

        // Sprites for 5 modes
        [SerializeField] private Sprite m_QuickMatchSprite;
        [SerializeField] private Sprite m_MultiplayerSprite;
        [SerializeField] private Sprite m_CareerSprite;
        [SerializeField] private Sprite m_HotLapSprite;
        [SerializeField] private Sprite m_PracticeSprite;

        private PlayModeType m_ActiveSubMode = PlayModeType.QuickMatch;
        private bool m_IsInSubPanel = false;
        private readonly List<PlayModeCard> m_Cards = new List<PlayModeCard>();

        // Sub-panel UI references
        private GameObject m_CurrentSubPanelGo;
        private TMP_Text m_BreadcrumbText;

        public bool IsOpen => gameObject.activeSelf && m_CanvasGroup != null && m_CanvasGroup.alpha > 0.05f;
        public bool IsInSubPanel => m_IsInSubPanel;

        private bool m_Initialized = false;

        public void InitializeIfNeeded()
        {
            if (m_Initialized) return;
            m_Initialized = true;
            Instance = this;
            m_CanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null) m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();

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
        }

        private void BuildUIHierarchy()
        {
            var rootRt = (RectTransform)transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            var bgShape = gameObject.GetComponent<AccertoneShape>();
            if (bgShape == null) bgShape = gameObject.AddComponent<AccertoneShape>();
            bgShape.color = new Color(0.04f, 0.045f, 0.055f, 0.98f);
            bgShape.raycastTarget = true;

            // 1. Top Header Bar
            m_HeaderRt = AccertoneUIFactory.CreatePanel(transform, "PlayHeader", new Vector2(1608f, 72f), new Color(0.07f, 0.08f, 0.10f, 0.95f), 10f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
            m_HeaderRt.anchorMin = new Vector2(0.5f, 1f);
            m_HeaderRt.anchorMax = new Vector2(0.5f, 1f);
            m_HeaderRt.pivot = new Vector2(0.5f, 1f);
            m_HeaderRt.anchoredPosition = new Vector2(0f, -28f);

            var titleText = AccertoneUIFactory.CreateText(m_HeaderRt, "Title", "PLAY // GAME MODES", BrandTextRole.Headline, Color.white);
            var titleRt = (RectTransform)titleText.transform;
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(24f, 10f);
            titleRt.sizeDelta = new Vector2(600f, 30f);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.fontSize = 20f;

            m_BreadcrumbText = AccertoneUIFactory.CreateText(m_HeaderRt, "Breadcrumb", "2026 FIA FORMULA ONE SEASON  ·  SELECT RACING DISCIPLINE", BrandTextRole.Caption, new Color(0.75f, 0.26f, 1f));
            var bRt = (RectTransform)m_BreadcrumbText.transform;
            bRt.anchorMin = new Vector2(0f, 0.5f);
            bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(24f, -16f);
            bRt.sizeDelta = new Vector2(800f, 20f);
            m_BreadcrumbText.textWrappingMode = TextWrappingModes.NoWrap;
            m_BreadcrumbText.fontSize = 11f;
            m_BreadcrumbText.characterSpacing = 8f;

            var backBtn = AccertoneUIFactory.CreateButton(m_HeaderRt, "BackButton", "[ESC] RETURN TO SHOWROOM", new Vector2(280f, 38f), RequestBack);
            var backRt = (RectTransform)backBtn.transform;
            backRt.anchorMin = new Vector2(1f, 0.5f);
            backRt.anchorMax = new Vector2(1f, 0.5f);
            backRt.pivot = new Vector2(1f, 0.5f);
            backRt.anchoredPosition = new Vector2(-24f, 0f);
            var btnTxt = backBtn.GetComponentInChildren<TMP_Text>();
            if (btnTxt != null) btnTxt.fontSize = 11f;

            // 2. Cards Grid Container
            var cardsGo = new GameObject("CardsGrid", typeof(RectTransform));
            cardsGo.transform.SetParent(transform, false);
            m_CardsContainer = (RectTransform)cardsGo.transform;
            m_CardsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            m_CardsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            m_CardsContainer.pivot = new Vector2(0.5f, 0.5f);
            m_CardsContainer.anchoredPosition = new Vector2(0f, -30f);
            m_CardsContainer.sizeDelta = new Vector2(1608f, 680f);

            BuildModeCards();

            // 3. Sub-Panel Container
            var subGo = new GameObject("SubPanels", typeof(RectTransform));
            subGo.transform.SetParent(transform, false);
            m_SubPanelContainer = (RectTransform)subGo.transform;
            m_SubPanelContainer.anchorMin = new Vector2(0.5f, 0.5f);
            m_SubPanelContainer.anchorMax = new Vector2(0.5f, 0.5f);
            m_SubPanelContainer.pivot = new Vector2(0.5f, 0.5f);
            m_SubPanelContainer.anchoredPosition = new Vector2(0f, -30f);
            m_SubPanelContainer.sizeDelta = new Vector2(1608f, 680f);
            m_SubPanelContainer.gameObject.SetActive(false);
        }

        private void BuildModeCards()
        {
            // Card 1: QUICK MATCH (Hero Prominent Card on Left: 520w x 680h)
            var c1 = CreateCard(m_CardsContainer, "Card_QuickMatch", new Vector2(-544f, 0f), new Vector2(520f, 680f),
                PlayModeType.QuickMatch, m_QuickMatchSprite, "01", "SOLO // AI GRID", "QUICK MATCH",
                "Play with AI", "Instant Grand Prix against a full 20-car AI field with customizable distance, grid size and pace.", true);
            m_Cards.Add(c1);

            // Card 2: ONLINE MULTIPLAYER (Right Column 1 Top: 520w x 328h)
            var c2 = CreateCard(m_CardsContainer, "Card_Online", new Vector2(0f, 176f), new Vector2(520f, 328f),
                PlayModeType.OnlineMultiplayer, m_MultiplayerSprite, "02", "ONLINE // 20 PLAYERS", "ONLINE MULTIPLAYER",
                "Race against real players", "Join public rooms, host private 20-car sessions with room codes, or compete on dedicated servers.", false);
            m_Cards.Add(c2);

            // Card 3: CAREER (Right Column 1 Bottom: 520w x 328h)
            var c3 = CreateCard(m_CardsContainer, "Card_Career", new Vector2(0f, -176f), new Vector2(520f, 328f),
                PlayModeType.Career, m_CareerSprite, "03", "CHAMPIONSHIP // 24 ROUNDS", "CAREER",
                "Build your racing career", "Compete through the 2026 FIA Formula One Championship calendar, earn points, and chase the Drivers' World Title.", false);
            m_Cards.Add(c3);

            // Card 4: HOT LAP (Right Column 2 Top: 520w x 328h)
            var c4 = CreateCard(m_CardsContainer, "Card_HotLap", new Vector2(544f, 176f), new Vector2(520f, 328f),
                PlayModeType.HotLap, m_HotLapSprite, "04", "TIME ATTACK // TELEMETRY", "HOT LAP",
                "Set your fastest lap", "Pure single-lap qualifying shootout against the Valcorsa benchmark lap record (1:29.874).", false);
            m_Cards.Add(c4);

            // Card 5: FREE PRACTICE (Right Column 2 Bottom: 520w x 328h)
            var c5 = CreateCard(m_CardsContainer, "Card_Practice", new Vector2(544f, -176f), new Vector2(520f, 328f),
                PlayModeType.FreePractice, m_PracticeSprite, "05", "TESTING // SETUP & TUNING", "FREE PRACTICE",
                "Practice and test car setups", "Unrestricted track session with live telemetry readouts and direct access to full vehicle setup tuning.", false);
            m_Cards.Add(c5);
        }

        private PlayModeCard CreateCard(Transform parent, string name, Vector2 pos, Vector2 size, PlayModeType type, Sprite sprite, string num, string tag, string title, string sub, string desc, bool featured)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var card = go.AddComponent<PlayModeCard>();
            card.Setup(type, sprite, num, tag, title, sub, desc, featured, OnCardSelected);
            return card;
        }

        private void OnCardSelected(PlayModeType mode)
        {
            OpenSubPanel(mode);
        }

        public void Open(PlayModeType initialMode = PlayModeType.QuickMatch, bool openSubPanel = false)
        {
            InitializeIfNeeded();
            gameObject.SetActive(true);
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 1f;
                m_CanvasGroup.blocksRaycasts = true;
                m_CanvasGroup.interactable = true;
            }

            if (openSubPanel)
            {
                OpenSubPanel(initialMode);
            }
            else
            {
                BackToModeGrid();
            }
        }

        public void Close()
        {
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.blocksRaycasts = false;
                m_CanvasGroup.interactable = false;
            }
            gameObject.SetActive(false);
            m_IsInSubPanel = false;
        }

        public void RequestBack()
        {
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
            m_IsInSubPanel = false;
            if (m_CurrentSubPanelGo != null)
            {
                Destroy(m_CurrentSubPanelGo);
                m_CurrentSubPanelGo = null;
            }
            if (m_SubPanelContainer != null) m_SubPanelContainer.gameObject.SetActive(false);
            if (m_CardsContainer != null) m_CardsContainer.gameObject.SetActive(true);
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "2026 FIA FORMULA ONE SEASON  ·  SELECT RACING DISCIPLINE";
        }

        public void OpenSubPanel(PlayModeType mode)
        {
            m_ActiveSubMode = mode;
            m_IsInSubPanel = true;

            if (m_CardsContainer != null) m_CardsContainer.gameObject.SetActive(false);
            if (m_SubPanelContainer != null)
            {
                m_SubPanelContainer.gameObject.SetActive(true);
                if (m_CurrentSubPanelGo != null) Destroy(m_CurrentSubPanelGo);

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
                }
            }
        }

        // =========================================================================
        // SUB-PANEL 1: QUICK MATCH
        // =========================================================================
        private void BuildQuickMatchSubPanel()
        {
            if (m_BreadcrumbText != null)
                m_BreadcrumbText.text = "PLAY // QUICK MATCH  ·  SINGLE PLAYER AI GRAND PRIX";

            m_CurrentSubPanelGo = new GameObject("Panel_QuickMatch", typeof(RectTransform));
            m_CurrentSubPanelGo.transform.SetParent(m_SubPanelContainer, false);
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
            CreateConfigStepper(rightRt, "AI GRID SIZE", (qm.aiDriverCount + 1) + " CARS (19 AI OPPONENTS)", -90f, () => {
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

            m_CurrentSubPanelGo = new GameObject("Panel_Multiplayer", typeof(RectTransform));
            m_CurrentSubPanelGo.transform.SetParent(m_SubPanelContainer, false);
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

            m_CurrentSubPanelGo = new GameObject("Panel_Career", typeof(RectTransform));
            m_CurrentSubPanelGo.transform.SetParent(m_SubPanelContainer, false);
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

            m_CurrentSubPanelGo = new GameObject("Panel_HotLap", typeof(RectTransform));
            m_CurrentSubPanelGo.transform.SetParent(m_SubPanelContainer, false);
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

            m_CurrentSubPanelGo = new GameObject("Panel_Practice", typeof(RectTransform));
            m_CurrentSubPanelGo.transform.SetParent(m_SubPanelContainer, false);
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
            vText.fontSize = 12f;

            var stepBtn = AccertoneUIFactory.CreateButton(boxRt, "ChangeBtn", "CHANGE >", new Vector2(90f, 32f), () => onNext?.Invoke());
            var bRt = (RectTransform)stepBtn.transform;
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-12f, 0f);
        }
    }
}
