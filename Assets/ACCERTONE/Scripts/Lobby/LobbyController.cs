using Accertone.Branding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.Lobby
{
    /// <summary>Lobby flow: navigation, keyboard shortcuts and the PLAY game mode transition.</summary>
    public class LobbyController : MonoBehaviour
    {
        public LobbyNavBar navBar;
        public Button raceButton;
        public AccertoneTransitionOverlay transition;

        [Tooltip("Scene to load when RACE is pressed. Leave empty until the race scene exists; the transition then returns to the lobby.")]
        public string raceSceneName = "";
        public string deployStatus = "DEPLOYING TO GRID";
        public float deployHold = 1.8f;

        // Tab routing (HOME / GARAGE / CAREER / ONLINE / SETTINGS) lives in LobbyNavigationCoordinator.
        void Start()
        {
            if (raceButton != null)
            {
                raceButton.onClick.RemoveAllListeners();
                raceButton.onClick.AddListener(OnPlayClicked);
            }
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null || (transition != null && transition.IsBusy)) return;

            // While an overlay is open (Settings, CarSetup, or Play Modes), ignore lobby shortcuts
            if (Accertone.Settings.LobbyNavigationCoordinator.OverlayOpen) return;

            var settings = Accertone.Settings.AccertoneSettings.Instance;
            if (settings != null && settings.IsEditing) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                OnPlayClicked();

            if (navBar != null)
            {
                if (kb.qKey.wasPressedThisFrame) navBar.Step(-1);
                if (kb.eKey.wasPressedThisFrame) navBar.Step(1);
            }
#endif
        }

        public void OnPlayClicked()
        {
            var coordinator = FindAnyObjectByType<Accertone.Settings.LobbyNavigationCoordinator>();
            if (coordinator != null)
            {
                coordinator.ShowPlayModeView();
            }
            else
            {
                Race();
            }
        }

        public void Race()
        {
            if (transition != null && transition.IsBusy) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            bool hasScene = !string.IsNullOrEmpty(raceSceneName) && Application.CanStreamedLevelBeLoaded(raceSceneName);
            if (transition == null)
            {
                if (hasScene) SceneManager.LoadScene(raceSceneName);
                return;
            }
            transition.Play(deployStatus, deployHold, () =>
            {
                if (hasScene) SceneManager.LoadScene(raceSceneName);
            }, !hasScene);
        }
    }
}
