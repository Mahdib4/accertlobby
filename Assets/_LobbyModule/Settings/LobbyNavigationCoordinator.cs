using System.Collections.Generic;
using Accertone.CarSetup;
using Accertone.Lobby;
using Accertone.Play;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.Settings
{
    /// <summary>
    /// Routes the lobby tabs between the showroom (HOME), the car setup garage (GARAGE),
    /// the play game modes screen (PLAY / CAREER / ONLINE), and the settings overlay (SETTINGS).
    /// </summary>
    public class LobbyNavigationCoordinator : MonoBehaviour
    {
        [Header("Lobby Wiring")]
        public LobbyNavBar navBar;
        public LobbyController lobbyController;

        [Header("Showroom panels hidden while Overlay is open")]
        public List<GameObject> showroomPanels = new List<GameObject>();

        [Header("Screens")]
        public SettingsScreen settingsView;
        public CarSetupUI carSetupView;
        public PlayModeSelectionUI playModeView;

        /// <summary>True while Settings, Garage, or Play Modes cover the showroom.</summary>
        public static bool OverlayOpen { get; private set; }

        const string SettingsId = "settings";
        int m_CurrentTab;
        int m_IgnoreEscapeFrame = -1;
        bool m_Reverting;

        void Awake()
        {
            LocateLobbyReferences();
            EnsureScreensCreated();
            EnsureGameModeManagerCreated();
            UpdateRaceActionText();
        }

        void Start()
        {
            if (navBar != null) navBar.SelectionChanged += OnTabSelectionChanged;
            if (settingsView != null) settingsView.CloseRequested += ReturnToHome;
            ShowHomeView();
        }

        void OnDestroy()
        {
            if (navBar != null) navBar.SelectionChanged -= OnTabSelectionChanged;
            if (settingsView != null) settingsView.CloseRequested -= ReturnToHome;
            OverlayOpen = false;
        }

        void LocateLobbyReferences()
        {
            if (navBar == null) navBar = FindAnyObjectByType<LobbyNavBar>();
            if (lobbyController == null) lobbyController = FindAnyObjectByType<LobbyController>();
            var canvas = GameObject.Find("UI_Lobby");
            if (canvas == null) return;
            // The wordmark, navigation, top rule and season tag stay visible over the settings overlay.
            foreach (var name in new[] { "SessionPanel", "ChassisInfo", "RaceAction", "PlayerCard", "InputHints" })
            {
                var t = canvas.transform.Find(name);
                if (t != null && !showroomPanels.Contains(t.gameObject)) showroomPanels.Add(t.gameObject);
            }
        }

        void EnsureGameModeManagerCreated()
        {
            if (GameModeManager.Instance == null && FindAnyObjectByType<GameModeManager>() == null)
            {
                var gmGo = new GameObject("GameModeManager");
                gmGo.AddComponent<GameModeManager>();
            }
        }

        public void UpdateRaceActionText()
        {
            var raceAction = GameObject.Find("RaceAction");
            if (raceAction != null)
            {
                var tmpros = raceAction.GetComponentsInChildren<TMPro.TMP_Text>(true);
                foreach (var t in tmpros)
                {
                    if (t.gameObject.name == "Label" && t.text == "RACE")
                        t.text = "PLAY";
                    else if (t.gameObject.name == "Subtitle" && (t.text.Contains("DEPLOY") || t.text.Contains("SELECT")))
                        t.text = "SELECT MODE";
                }
            }
        }

        /// <summary>Finds (or creates) the Play, Settings and Garage screens under the lobby canvas.</summary>
        public void EnsureScreensCreated()
        {
            var canvas = GameObject.Find("UI_Lobby");
            if (canvas == null) return;

            // 1. Settings Screen
            if (settingsView == null) settingsView = canvas.GetComponentInChildren<SettingsScreen>(true);
            if (settingsView == null)
            {
                var go = new GameObject("Screen_Settings", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                settingsView = go.AddComponent<SettingsScreen>();
            }
            if (settingsView != null) settingsView.gameObject.SetActive(false);

            // 2. Car Setup Screen
            if (carSetupView == null) carSetupView = canvas.GetComponentInChildren<CarSetupUI>(true);
            if (carSetupView == null)
            {
                var setupGo = new GameObject("Screen_CarSetup", typeof(RectTransform));
                setupGo.transform.SetParent(canvas.transform, false);
                carSetupView = setupGo.AddComponent<CarSetupUI>();
            }
            carSetupView.OnCloseRequested = ReturnToHome;
            carSetupView.gameObject.SetActive(false);

            // 3. Play Modes Screen
            if (playModeView == null) playModeView = canvas.GetComponentInChildren<PlayModeSelectionUI>(true);
            if (playModeView == null)
            {
                var playGo = new GameObject("Screen_PlayModes", typeof(RectTransform));
                playGo.transform.SetParent(canvas.transform, false);
                playModeView = playGo.AddComponent<PlayModeSelectionUI>();
            }
            playModeView.OnCloseRequested = ReturnToHome;
            playModeView.OnOpenCarSetupRequested = ShowCarSetupView;
            playModeView.gameObject.SetActive(false);
        }

        int SettingsTabIndex()
        {
            if (navBar == null) return 4;
            for (int i = 0; i < navBar.tabs.Length; i++) if (navBar.tabs[i].id.ToLowerInvariant() == SettingsId) return i;
            return 4;
        }

        void OnTabSelectionChanged(int index, string id)
        {
            if (m_Reverting) return;
            string target = id.ToLowerInvariant();
            int previous = m_CurrentTab;

            // Leaving Settings with unsaved changes: let the player decide first.
            if (settingsView != null && settingsView.IsOpen && target != SettingsId)
            {
                settingsView.RequestClose(
                    () => { m_CurrentTab = index; Route(target); },
                    () => { m_Reverting = true; if (navBar != null) navBar.Select(previous); m_Reverting = false; });
                return;
            }
            m_CurrentTab = index;
            Route(target);
        }

        void Route(string id)
        {
            if (id == "garage") ShowCarSetupView();
            else if (id == SettingsId) ShowSettingsView();
            else if (id == "career") ShowCareerView();
            else if (id == "online") ShowOnlineView();
            else ShowHomeView(); // HOME tab
        }

        void SetShowroomPanels(bool visible)
        {
            foreach (var panel in showroomPanels) if (panel != null) panel.SetActive(visible);
        }

        public void ShowHomeView()
        {
            if (settingsView != null && settingsView.IsOpen) { settingsView.Hide(); m_IgnoreEscapeFrame = Time.frameCount; }
            if (carSetupView != null) carSetupView.gameObject.SetActive(false);
            if (playModeView != null && playModeView.IsOpen) { playModeView.Close(); m_IgnoreEscapeFrame = Time.frameCount; }
            SetShowroomPanels(true);
            OverlayOpen = false;
        }

        public void ShowSettingsView()
        {
            if (carSetupView != null) carSetupView.gameObject.SetActive(false);
            if (playModeView != null) playModeView.Close();
            SetShowroomPanels(false);
            OverlayOpen = true;
            if (settingsView != null) settingsView.Open();
        }

        public void ShowCarSetupView()
        {
            if (settingsView != null && settingsView.IsOpen) settingsView.Hide();
            if (playModeView != null) playModeView.Close();
            SetShowroomPanels(false);
            OverlayOpen = true;
            if (carSetupView != null) carSetupView.gameObject.SetActive(true);
        }

        public void ShowPlayModeView(PlayModeType mode = PlayModeType.QuickMatch, bool openSubPanel = false)
        {
            if (settingsView != null && settingsView.IsOpen) settingsView.Hide();
            if (carSetupView != null) carSetupView.gameObject.SetActive(false);
            SetShowroomPanels(false);
            OverlayOpen = true;
            if (playModeView != null)
            {
                playModeView.Open(mode, openSubPanel);
            }
        }

        public void ShowCareerView()
        {
            ShowPlayModeView(PlayModeType.Career, true);
        }

        public void ShowOnlineView()
        {
            ShowPlayModeView(PlayModeType.OnlineMultiplayer, true);
        }

        public void ReturnToHome()
        {
            m_IgnoreEscapeFrame = Time.frameCount;
            if (navBar != null && navBar.selectedIndex != 0) navBar.Select(0);
            else { m_CurrentTab = 0; ShowHomeView(); }
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool escape = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (!escape || Time.frameCount <= m_IgnoreEscapeFrame + 1) return;

            if (settingsView != null && settingsView.IsOpen) return; // settings screen handles its own Esc

            if (playModeView != null && playModeView.IsOpen)
            {
                playModeView.RequestBack();
                return;
            }

            if (carSetupView != null && carSetupView.gameObject.activeSelf)
            {
                ReturnToHome();
                return;
            }

            if (m_CurrentTab == 0)
            {
                if (navBar != null) navBar.Select(SettingsTabIndex());
                else ShowSettingsView();
            }
#endif
        }
    }
}
