using System;
using System.Collections.Generic;
using UnityEngine;

namespace Accertone.Play
{
    public enum PlayModeType
    {
        QuickMatch,
        OnlineMultiplayer,
        Career,
        HotLap,
        FreePractice,
        PlayWithFriends
    }

    [Serializable]
    public class QuickMatchConfig
    {
        public string circuitName = "Autodromo Valcorsa";
        public string trackStats = "5.218 KM · 17 Turns · Lap Record: 1:29.874";
        public string carName = "AC-26 Works Racing #01";
        public int aiDriverCount = 19; // 19 AI + 1 Player = 20 drivers
        public int difficultyIndex = 2; // 0=Amateur (75%), 1=Clubman (85%), 2=Pro (95%), 3=Legend (105%)
        public int lapDistanceIndex = 1; // 0=3 Laps, 1=5 Laps, 2=15 Laps (25%), 3=30 Laps (50%)
        public int weatherIndex = 0; // 0=Dry Optimal, 1=Overcast, 2=Wet
    }

    [Serializable]
    public class MultiplayerRoom
    {
        public string roomId;
        public string roomName;
        public string circuit;
        public int currentPlayers;
        public int maxPlayers = 20;
        public int pingMs;
        public bool isPrivate;
    }

    [Serializable]
    public class MultiplayerDriverSlot
    {
        public int slotNumber;
        public string gamerTag;
        public string teamCar;
        public int ping;
        public bool isReady;
        public bool isLocalPlayer;
        public bool isHost;
    }

    [Serializable]
    public class CareerRound
    {
        public int roundNumber;
        public string grandPrixName;
        public string circuitName;
        public string country;
        public int laps;
        public bool isCompleted;
        public int playerFinishPosition;
        public int pointsAwarded;
    }

    [Serializable]
    public class CareerDriverStanding
    {
        public int rank;
        public string driverName;
        public string team;
        public int points;
    }

    [Serializable]
    public class HotLapData
    {
        public string circuitName = "Autodromo Valcorsa (GP Layout)";
        public string benchmarkRecord = "1:29.874";
        public string benchmarkHolder = "M. Verstappen · Red Bull Racing";
        public string personalBest = "1:31.420";
        public float personalBestSeconds = 91.420f;
        public float s1Time = 27.810f;
        public float s2Time = 34.220f;
        public float s3Time = 29.390f;
        public float benchmarkS1 = 27.420f;
        public float benchmarkS2 = 33.610f;
        public float benchmarkS3 = 28.844f;
        public int tireCompoundIndex = 0; // 0=Soft (C5), 1=Medium (C3), 2=Hard (C1)
        public bool ghostEnabled = true;
    }

    [Serializable]
    public class FriendEntry
    {
        public string gamerTag;
        public string status;          // shown under the name, e.g. "ONLINE · IN MENUS"
        public int ping;
        public bool isOnline;
        public bool isInvited;
        public bool hasJoined;         // already in your private lobby
    }

    [Serializable]
    public class FriendSessionConfig
    {
        public static readonly string[] Circuits =
        {
            "Autodromo Valcorsa", "Autodromo Nazionale Monza", "Circuit de Spa-Francorchamps", "Silverstone Circuit", "Suzuka International Racing Course"
        };

        public string roomCode = "AC-F24K";
        public int circuitIndex = 0;
        public int lapIndex = 1;        // 0=3 Laps, 1=5 Laps, 2=10 Laps, 3=15 Laps
        public int weatherIndex = 0;    // 0=Dry, 1=Overcast, 2=Wet
        public bool collisions = true;
        public int assistsIndex = 0;    // 0=Allowed, 1=Pro (no assists)
    }

    [Serializable]
    public class PracticeSessionData
    {
        public string circuitName = "Autodromo Valcorsa";
        public string activePreset = "Balanced Setup";
        public float frontWingAngle = 28f;
        public float rearWingAngle = 32f;
        public float aeroBalance = 43.5f;
        public float rideHeightFront = 28f;
        public float rideHeightRear = 34f;
        public float diffOnThrottle = 65f;
        public float diffOffThrottle = 55f;
        public float brakeBiasFront = 56f;
        public float tyrePressureFront = 24.5f;
        public float tyrePressureRear = 22.0f;
    }
}
