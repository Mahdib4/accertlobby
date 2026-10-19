using System.Collections.Generic;
using Accertone.CarSetup;
using Accertone.Garage;
using Accertone.Lobby;
using Accertone.Play;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.Settings
{
    /// <summary>
    /// Routes the lobby tabs between the showroom (HOME), the garage (GARAGE), the play game modes screen
    /// (PLAY / CAREER / ONLINE), and the settings overlay (SETTINGS). The engineering sheet (CarSetupUI) is
    /// reached from the garage's Car Setup page.
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
        public GarageScreen garageView;

        /// <summary>True while Settings, Garage, or Play Modes cover the showroom.</summary>
        public static bool OverlayOpen { get; private set; }

        const string SettingsId = "settings";
        const string GarageId = "garage";
        const int GarageSetupPage = 6;
        int m_CurrentTab;
        int m_PendingGaragePage = -1;
        int m_IgnoreEscapeFrame = -1;
        bool m_Reverting;

        void Awake()
        {
            LocateLobbyReferences();
            EnsureScreensCreated();
            EnsureGameModeManagerCreated();
            UpdateRaceActionText();
            // The car shows its saved livery and setup in the lobby too, not only inside the garage.
            if (Application.isPlaying) GarageScreen.Boot();
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
            playModeView.OnOpenCarSetupRequested = OpenGarageSetup;
            playModeView.gameObject.SetActive(false);

            // 4. Garage
            if (garageView == null) garageView = canvas.GetComponentInChildren<GarageScreen>(true);
            if (garageView == null)
            {
                var garageGo = new GameObject("Screen_Garage", typeof(RectTransform));
                garageGo.layer = 5;
                garageGo.transform.SetParent(canvas.transform, false);
                garageView = garageGo.AddComponent<GarageScreen>();
            }
            garageView.OnCloseRequested = ReturnToHome;
            garageView.OnOpenEngineeringSheet = ShowCarSetupView;
            garageView.gameObject.SetActive(false);
        }

        int GarageTabIndex()
        {
            if (navBar == null) return 1;
            for (int i = 0; i < navBar.tabs.Length; i++) if (navBar.tabs[i].id.ToLowerInvariant() == GarageId) return i;
            return 1;
        }

        /// <summary>Opens the garage on its Car Setup page (used by Free Practice's "detailed car setup").</summary>
        public void OpenGarageSetup()
        {
            m_PendingGaragePage = GarageSetupPage;
            int tab = GarageTabIndex();
            if (navBar != null && navBar.selectedIndex != tab) navBar.Select(tab);
            else { m_CurrentTab = tab; ShowGarageView(); }
        }

        /// <summary>Closes the garage if it is up. Its 3D restore (camera, turntable, driver) animates on its own.</summary>
        void CloseGarage(bool toHome)
        {
            if (garageView == null || !garageView.IsActive) return;
            garageView.Close(() =>
            {
                // Back on HOME (and nothing else opened meanwhile): bring the HUD back now that the camera has landed.
                bool home = m_CurrentTab == 0 && (settingsView == null || !settingsView.IsOpen) && (playModeView == null || !playModeView.IsOpen) &&
                            (carSetupView == null || !carSetupView.gameObject.activeSelf) && (garageView == null || !garageView.IsActive);
                if (toHome && home)
                {
                    garageView.RestoreHud();
                    OverlayOpen = false;
                }
            });
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
            if (id == GarageId) ShowGarageView();
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
            if (garageView != null && garageView.IsActive)
            {
                // The HUD returns (and lobby input resumes) when the garage camera has landed.
                m_IgnoreEscapeFrame = Time.frameCount;
                OverlayOpen = true;
                CloseGarage(true);
                return;
            }
            SetShowroomPanels(true);
            OverlayOpen = false;
        }

        public void ShowGarageView()
        {
            if (settingsView != null && settingsView.IsOpen) settingsView.Hide();
            if (playModeView != null && playModeView.IsOpen) playModeView.Close();
            if (carSetupView != null) carSetupView.gameObject.SetActive(false);
            OverlayOpen = true;
            int page = m_PendingGaragePage;
            m_PendingGaragePage = -1;
            // The garage fades the showroom panels out itself, in step with its camera move.
            if (garageView != null) garageView.Open(page);
            else SetShowroomPanels(false);
        }

        public void ShowSettingsView()
        {
            CloseGarage(false);
            if (carSetupView != null) carSetupView.gameObject.SetActive(false);
            if (playModeView != null) playModeView.Close();
            SetShowroomPanels(false);
            OverlayOpen = true;
            if (settingsView != null) settingsView.Open();
        }

        public void ShowCarSetupView()
        {
            CloseGarage(false);
            if (settingsView != null && settingsView.IsOpen) settingsView.Hide();
            if (playModeView != null) playModeView.Close();
            SetShowroomPanels(false);
            OverlayOpen = true;
            if (carSetupView != null) carSetupView.gameObject.SetActive(true);
        }

        public void ShowPlayModeView(PlayModeType mode = PlayModeType.QuickMatch, bool openSubPanel = false)
        {
            CloseGarage(false);
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

            if (garageView != null && garageView.IsActive)
            {
                if (garageView.IsOpen) garageView.RequestBack();
                return;
            }

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
