using System;
using System.Collections.Generic;
using Accertone.Branding;
using Accertone.CarSetup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Accertone.Play
{
    /// <summary>
    /// Master coordinator for Accert one Play game modes:
    /// Quick Match, Online Multiplayer, Career, Hot Lap, and Free Practice.
    /// Manages data persistence, active session configuration, and race deployment.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [Header("Configurations")]
        public QuickMatchConfig quickMatch = new QuickMatchConfig();
        public List<MultiplayerRoom> publicRooms = new List<MultiplayerRoom>();
        public List<MultiplayerDriverSlot> activeLobbySlots = new List<MultiplayerDriverSlot>();
        public string activeRoomCode = "AC-7921";
        public bool isLocalPlayerReady = false;

        [Header("Career")]
        public string careerDriverName = "Alexander Vance";
        public string careerTeam = "Accert One Racing Works";
        public string careerLicence = "GRADE A — FIA F1 SUPER LICENCE";
        public int careerPoints = 68;
        public int currentRoundIndex = 3; // Round 04 (0-indexed: 3) Japanese GP next
        public List<CareerRound> careerRounds = new List<CareerRound>();
        public List<CareerDriverStanding> careerStandings = new List<CareerDriverStanding>();

        [Header("Hot Lap")]
        public HotLapData hotLap = new HotLapData();

        [Header("Practice Session")]
        public PracticeSessionData practice = new PracticeSessionData();

        public event Action OnStateChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaultData();
            LoadProgress();
        }

        public void InitializeDefaultData()
        {
            // Initialize Multiplayer Sample Lobbies
            publicRooms = new List<MultiplayerRoom>
            {
                new MultiplayerRoom { roomId = "AC-7921", roomName = "EU-OFFICIAL #01 [RANKED S]", circuit = "Autodromo Valcorsa", currentPlayers = 16, maxPlayers = 20, pingMs = 22, isPrivate = false },
                new MultiplayerRoom { roomId = "AC-4410", roomName = "MONZA HIGH-SPEED SHOOTOUT", circuit = "Autodromo Nazionale Monza", currentPlayers = 18, maxPlayers = 20, pingMs = 28, isPrivate = false },
                new MultiplayerRoom { roomId = "AC-3305", roomName = "SPA 50% DISTANCE LEAGUE", circuit = "Circuit de Spa-Francorchamps", currentPlayers = 12, maxPlayers = 20, pingMs = 34, isPrivate = false },
                new MultiplayerRoom { roomId = "AC-1688", roomName = "SILVERSTONE WET SPECIAL", circuit = "Silverstone Circuit", currentPlayers = 8, maxPlayers = 20, pingMs = 26, isPrivate = false }
            };

            // Active room slots
            activeLobbySlots = new List<MultiplayerDriverSlot>
            {
                new MultiplayerDriverSlot { slotNumber = 1, gamerTag = "A. Vance (You)", teamCar = "AC-26 Works #01", ping = 24, isReady = false, isLocalPlayer = true, isHost = true },
                new MultiplayerDriverSlot { slotNumber = 2, gamerTag = "M. Schumacher_91", teamCar = "AC-26 Velocity #04", ping = 31, isReady = true, isLocalPlayer = false, isHost = false },
                new MultiplayerDriverSlot { slotNumber = 3, gamerTag = "ApexPredator_UK", teamCar = "AC-26 Horizon #11", ping = 28, isReady = true, isLocalPlayer = false, isHost = false },
                new MultiplayerDriverSlot { slotNumber = 4, gamerTag = "ScuderiaFan99", teamCar = "AC-26 Works #02", ping = 42, isReady = false, isLocalPlayer = false, isHost = false },
                new MultiplayerDriverSlot { slotNumber = 5, gamerTag = "NordicSpeedster", teamCar = "AC-26 Velocity #07", ping = 35, isReady = true, isLocalPlayer = false, isHost = false },
                new MultiplayerDriverSlot { slotNumber = 6, gamerTag = "TokyoDriftKing", teamCar = "AC-26 Horizon #14", ping = 88, isReady = true, isLocalPlayer = false, isHost = false }
            };

            // 2026 FIA Formula One Season Calendar (24 Rounds)
            careerRounds = new List<CareerRound>
            {
                new CareerRound { roundNumber = 1, grandPrixName = "Bahrain Grand Prix", circuitName = "Bahrain International Circuit", country = "Bahrain", laps = 57, isCompleted = true, playerFinishPosition = 2, pointsAwarded = 18 },
                new CareerRound { roundNumber = 2, grandPrixName = "Saudi Arabian Grand Prix", circuitName = "Jeddah Corniche Circuit", country = "Saudi Arabia", laps = 50, isCompleted = true, playerFinishPosition = 1, pointsAwarded = 25 },
                new CareerRound { roundNumber = 3, grandPrixName = "Australian Grand Prix", circuitName = "Albert Park Circuit", country = "Australia", laps = 58, isCompleted = true, playerFinishPosition = 3, pointsAwarded = 15 },
                new CareerRound { roundNumber = 4, grandPrixName = "Japanese Grand Prix", circuitName = "Suzuka International Racing Course", country = "Japan", laps = 53, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 5, grandPrixName = "Chinese Grand Prix", circuitName = "Shanghai International Circuit", country = "China", laps = 56, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 6, grandPrixName = "Miami Grand Prix", circuitName = "Miami International Autodrome", country = "USA", laps = 57, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 7, grandPrixName = "Emilia Romagna Grand Prix", circuitName = "Autodromo Enzo e Dino Ferrari", country = "Italy", laps = 63, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 8, grandPrixName = "Monaco Grand Prix", circuitName = "Circuit de Monaco", country = "Monaco", laps = 78, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 9, grandPrixName = "Canadian Grand Prix", circuitName = "Circuit Gilles Villeneuve", country = "Canada", laps = 70, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 10, grandPrixName = "Spanish Grand Prix", circuitName = "Circuit de Barcelona-Catalunya", country = "Spain", laps = 66, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 11, grandPrixName = "Austrian Grand Prix", circuitName = "Red Bull Ring", country = "Austria", laps = 71, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 12, grandPrixName = "British Grand Prix", circuitName = "Silverstone Circuit", country = "Great Britain", laps = 52, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 13, grandPrixName = "Hungarian Grand Prix", circuitName = "Hungaroring", country = "Hungary", laps = 70, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 14, grandPrixName = "Belgian Grand Prix", circuitName = "Circuit de Spa-Francorchamps", country = "Belgium", laps = 44, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 15, grandPrixName = "Dutch Grand Prix", circuitName = "Circuit Zandvoort", country = "Netherlands", laps = 72, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 16, grandPrixName = "Italian Grand Prix", circuitName = "Autodromo Nazionale Monza", country = "Italy", laps = 53, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 17, grandPrixName = "Azerbaijan Grand Prix", circuitName = "Baku City Circuit", country = "Azerbaijan", laps = 51, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 18, grandPrixName = "Singapore Grand Prix", circuitName = "Marina Bay Street Circuit", country = "Singapore", laps = 62, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 19, grandPrixName = "United States Grand Prix", circuitName = "Circuit of the Americas", country = "USA", laps = 56, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 20, grandPrixName = "Mexico City Grand Prix", circuitName = "Autódromo Hermanos Rodríguez", country = "Mexico", laps = 71, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 21, grandPrixName = "São Paulo Grand Prix", circuitName = "Autódromo José Carlos Pace", country = "Brazil", laps = 71, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 22, grandPrixName = "Las Vegas Grand Prix", circuitName = "Las Vegas Strip Circuit", country = "USA", laps = 50, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 23, grandPrixName = "Qatar Grand Prix", circuitName = "Lusail International Circuit", country = "Qatar", laps = 57, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 },
                new CareerRound { roundNumber = 24, grandPrixName = "Abu Dhabi Grand Prix", circuitName = "Yas Marina Circuit", country = "UAE", laps = 58, isCompleted = false, playerFinishPosition = 0, pointsAwarded = 0 }
            };

            // Career Standings Table
            careerStandings = new List<CareerDriverStanding>
            {
                new CareerDriverStanding { rank = 1, driverName = "M. Verstappen", team = "Red Bull Racing", points = 77 },
                new CareerDriverStanding { rank = 2, driverName = "C. Leclerc", team = "Scuderia Ferrari", points = 71 },
                new CareerDriverStanding { rank = 3, driverName = "A. Vance (You)", team = "Accert One Works", points = 68 },
                new CareerDriverStanding { rank = 4, driverName = "L. Norris", team = "McLaren Formula 1", points = 52 },
                new CareerDriverStanding { rank = 5, driverName = "C. Sainz", team = "Scuderia Ferrari", points = 44 },
                new CareerDriverStanding { rank = 6, driverName = "G. Russell", team = "Mercedes-AMG Petronas", points = 39 },
                new CareerDriverStanding { rank = 7, driverName = "L. Hamilton", team = "Scuderia Ferrari", points = 34 },
                new CareerDriverStanding { rank = 8, driverName = "O. Piastri", team = "McLaren Formula 1", points = 28 },
                new CareerDriverStanding { rank = 9, driverName = "F. Alonso", team = "Aston Martin F1", points = 16 },
                new CareerDriverStanding { rank = 10, driverName = "S. Perez", team = "Red Bull Racing", points = 12 }
            };
        }

        public void ToggleLocalPlayerReady()
        {
            isLocalPlayerReady = !isLocalPlayerReady;
            if (activeLobbySlots.Count > 0)
            {
                activeLobbySlots[0].isReady = isLocalPlayerReady;
            }
            NotifyChanged();
        }

        public void SaveProgress()
        {
            PlayerPrefs.SetInt("Accertone_Career_Points", careerPoints);
            PlayerPrefs.SetInt("Accertone_Career_Round", currentRoundIndex);
            PlayerPrefs.SetFloat("Accertone_HotLap_PB", hotLap.personalBestSeconds);
            PlayerPrefs.Save();
        }

        public void LoadProgress()
        {
            if (PlayerPrefs.HasKey("Accertone_Career_Points"))
                careerPoints = PlayerPrefs.GetInt("Accertone_Career_Points");
            if (PlayerPrefs.HasKey("Accertone_Career_Round"))
                currentRoundIndex = PlayerPrefs.GetInt("Accertone_Career_Round");
            if (PlayerPrefs.HasKey("Accertone_HotLap_PB"))
            {
                hotLap.personalBestSeconds = PlayerPrefs.GetFloat("Accertone_HotLap_PB");
                int m = (int)(hotLap.personalBestSeconds / 60f);
                float s = hotLap.personalBestSeconds % 60f;
                hotLap.personalBest = string.Format("{0}:{1:00.000}", m, s);
            }
        }

        public void LaunchSession(PlayModeType mode, string raceSceneName = "")
        {
            string status = "";
            switch (mode)
            {
                case PlayModeType.QuickMatch:
                    status = "DEPLOYING QUICK MATCH // " + quickMatch.circuitName.ToUpperInvariant();
                    break;
                case PlayModeType.OnlineMultiplayer:
                    status = "SYNCHRONIZING MULTIPLAYER GRID // 20 DRIVERS";
                    break;
                case PlayModeType.Career:
                    var current = (currentRoundIndex >= 0 && currentRoundIndex < careerRounds.Count) ? careerRounds[currentRoundIndex] : null;
                    status = current != null ? "STARTING CAREER // " + current.grandPrixName.ToUpperInvariant() : "STARTING CAREER ROUND";
                    break;
                case PlayModeType.HotLap:
                    status = "LAUNCHING HOT LAP SHOOTOUT // " + hotLap.circuitName.ToUpperInvariant();
                    break;
                case PlayModeType.FreePractice:
                    status = "ENTERING FREE PRACTICE RUN // TELEMETRY LINKED";
                    break;
            }

            var transition = FindAnyObjectByType<AccertoneTransitionOverlay>();
            bool hasScene = !string.IsNullOrEmpty(raceSceneName) && Application.CanStreamedLevelBeLoaded(raceSceneName);

            if (transition != null)
            {
                transition.Play(status, 2.0f, () =>
                {
                    if (hasScene)
                    {
                        SceneManager.LoadScene(raceSceneName);
                    }
                }, !hasScene);
            }
            else
            {
                if (hasScene)
                {
                    SceneManager.LoadScene(raceSceneName);
                }
            }
        }

        public void NotifyChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
