using Accertone.CarSetup;
using UnityEngine;

namespace Accertone.Garage
{
    /// <summary>
    /// Engineering estimates the Garage shows next to the existing <see cref="CarSetupEngineeringEngine"/> figures,
    /// for the systems that engine does not cover (brakes and tyres). Simple, documented heuristics: they respond to
    /// the settings so the trade-offs are readable, but they are estimates, not a vehicle simulation.
    /// </summary>
    public static class GarageEstimates
    {
        public struct Braking
        {
            public float stoppingDistance200; // metres, 200 -> 0 km/h
            public float peakDecelG;
            public float discTempMin, discTempMax; // °C
            public float idealBias;               // % front
            public string tendency;
            public Color tendencyColor;
            public float frontHeat, rearHeat;     // 0..1 share used for the heat preview
        }

        public struct Tyres
        {
            public float gripIndex;     // 0..100 relative dry grip
            public float stintLaps;     // laps before the tyre drops off
            public float tempMin, tempMax;
            public string bestFor;
        }

        public static Braking Brakes(F1CarSetupData s, SetupTelemetryAnalysis a)
        {
            var b = new Braking();
            // Downforce lets the car brake harder at speed; pressure trims peak force.
            float downforce = a != null ? a.downforceIndex : 40f;
            b.peakDecelG = 4.1f + downforce * 0.014f + (s.brakePressure - 90) * 0.03f;
            float avgG = b.peakDecelG * 0.78f;
            float v = 200f / 3.6f;
            b.stoppingDistance200 = v * v / (2f * avgG * 9.81f);
            // Brake ducts: more cooling (higher step) lowers the operating window.
            float peak = 1080f - (s.brakeDucts - 1) * 70f + (s.brakePressure - 90) * 5f;
            b.discTempMin = peak - 220f;
            b.discTempMax = peak;
            // Ideal bias follows the aero balance: more front aero load supports more front bias.
            float aeroBal = a != null ? a.aeroBalancePercent : 42f;
            b.idealBias = Mathf.Clamp(56f + (aeroBal - 42f) * 0.5f, 52f, 60f);
            float d = s.brakeBias - b.idealBias;
            if (d > 2.5f) { b.tendency = "FRONT LOCK-UP RISK"; b.tendencyColor = new Color(0.95f, 0.75f, 0.2f); }
            else if (d < -2.5f) { b.tendency = "REAR INSTABILITY"; b.tendencyColor = new Color(0.95f, 0.3f, 0.3f); }
            else { b.tendency = "STABLE UNDER BRAKING"; b.tendencyColor = new Color(0.25f, 0.85f, 0.6f); }
            b.frontHeat = s.brakeBias / 100f;
            b.rearHeat = 1f - b.frontHeat;
            return b;
        }

        public static Tyres Tyre(F1CarSetupData s, SetupTelemetryAnalysis a)
        {
            var t = new Tyres();
            float grip, laps;
            switch (s.tireCompound)
            {
                case TireCompound.C3_Soft: grip = 100f; laps = 18f; t.tempMin = 90f; t.tempMax = 110f; t.bestFor = "QUALIFYING · SHORT STINTS"; break;
                case TireCompound.C1_Hard: grip = 90f; laps = 40f; t.tempMin = 110f; t.tempMax = 135f; t.bestFor = "LONG RUNS · HIGH ENERGY TRACKS"; break;
                case TireCompound.Intermediate: grip = 78f; laps = 30f; t.tempMin = 60f; t.tempMax = 85f; t.bestFor = "DAMP TRACK · LIGHT RAIN"; break;
                case TireCompound.Wet: grip = 70f; laps = 35f; t.tempMin = 45f; t.tempMax = 65f; t.bestFor = "HEAVY RAIN · STANDING WATER"; break;
                default: grip = 95f; laps = 28f; t.tempMin = 100f; t.tempMax = 120f; t.bestFor = "RACE STINTS · ALL-ROUND"; break;
            }
            // Pressures away from nominal cost grip; the engine's wear index (camber, pressures) shortens the stint.
            float off = Mathf.Abs(s.frontLeftPressure - 23f) + Mathf.Abs(s.frontRightPressure - 23f) +
                        Mathf.Abs(s.rearLeftPressure - 21.2f) + Mathf.Abs(s.rearRightPressure - 21.2f);
            t.gripIndex = Mathf.Clamp(grip - off * 1.6f, 40f, 100f);
            float wear = a != null ? Mathf.Max(0.6f, a.tireWearRateIndex) : 1f;
            t.stintLaps = Mathf.Round(laps / wear);
            return t;
        }
    }
}
