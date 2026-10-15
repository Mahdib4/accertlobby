using System;
using UnityEngine;

namespace Accertone.CarSetup
{
    public enum TireCompound
    {
        C1_Hard = 0,
        C2_Medium = 1,
        C3_Soft = 2,
        Intermediate = 3,
        Wet = 4
    }

    public enum EngineFuelMix
    {
        Lean = 0,
        Standard = 1,
        Rich = 2
    }

    public enum ErsMode
    {
        None = 0,
        Low = 1,
        Balanced = 2,
        Hotlap = 3,
        Overtake = 4
    }

    public enum AssistLevel
    {
        Off = 0,
        Medium = 1,
        Full = 2
    }

    /// <summary>
    /// Master serializable data structure for F1 vehicle tuning and setup.
    /// Complete with validation, clamping, and cloning.
    /// </summary>
    [Serializable]
    public class F1CarSetupData
    {
        public int version = 1;
        public string setupName = "Baseline Setup";
        public string trackName = "Valcorsa";
        public string carModel = "AC-26";
        public string dateModified = "";

        // =================================================================== 1. AERODYNAMICS
        [Header("Aerodynamics")]
        [Range(1, 50)] public int frontWingAngle = 28;
        [Range(1, 50)] public int rearWingAngle = 26;
        [Range(1, 10)] public int drsEfficiency = 6;

        // =================================================================== 2. TRANSMISSION & DIFFERENTIAL
        [Header("Transmission & Differential")]
        [Range(50, 100)] public int diffOnThrottle = 65; // %
        [Range(50, 100)] public int diffOffThrottle = 55; // %
        [Range(0, 100)] public int diffPreload = 40; // Nm
        [Range(2.50f, 4.50f)] public float finalDriveRatio = 3.40f;
        public float[] gearRatios = new float[8] { 3.12f, 2.45f, 1.98f, 1.62f, 1.36f, 1.15f, 0.98f, 0.85f };

        // =================================================================== 3. SUSPENSION & GEOMETRY
        [Header("Suspension Geometry")]
        [Range(-3.50f, -2.50f)] public float frontCamber = -3.00f; // degrees
        [Range(-2.00f, -1.00f)] public float rearCamber = -1.60f; // degrees
        [Range(0.00f, 0.50f)] public float frontToe = 0.10f; // degrees out
        [Range(0.00f, 0.50f)] public float rearToe = 0.20f; // degrees in

        [Header("Suspension Springs & Dampers")]
        [Range(1, 41)] public int frontSuspension = 22;
        [Range(1, 41)] public int rearSuspension = 18;
        [Range(1, 21)] public int frontAntiRollBar = 12;
        [Range(1, 21)] public int rearAntiRollBar = 8;
        [Range(30, 80)] public int frontRideHeight = 36; // mm
        [Range(30, 80)] public int rearRideHeight = 54; // mm

        // =================================================================== 4. BRAKING
        [Header("Brakes")]
        [Range(50, 70)] public int brakeBias = 56; // % Front
        [Range(80, 100)] public int brakePressure = 96; // %
        [Range(1, 5)] public int brakeDucts = 3;

        // =================================================================== 5. TIRES & PRESSURES
        [Header("Tires")]
        public TireCompound tireCompound = TireCompound.C2_Medium;
        [Range(21.5f, 25.5f)] public float frontLeftPressure = 23.0f; // psi
        [Range(21.5f, 25.5f)] public float frontRightPressure = 23.0f;
        [Range(20.0f, 23.5f)] public float rearLeftPressure = 21.2f;
        [Range(20.0f, 23.5f)] public float rearRightPressure = 21.2f;

        // =================================================================== 6. ENGINE & ELECTRONICS
        [Header("Engine & ERS")]
        public EngineFuelMix engineMix = EngineFuelMix.Standard;
        public ErsMode ersMode = ErsMode.Balanced;
        [Range(1, 3)] public int ersRecovery = 2;
        [Range(1, 10)] public int engineBraking = 5;
        public AssistLevel tractionControl = AssistLevel.Medium;
        public bool antiLockBrakes = false;

        // =================================================================== 7. FORCE FEEDBACK
        [Header("Force Feedback")]
        [Range(0, 100)] public int ffbStrength = 80; // %
        [Range(0, 100)] public int ffbDamper = 25; // %
        [Range(0, 100)] public int ffbCurbDetail = 70; // %
        [Range(0, 100)] public int understeerEnhance = 50; // %

        public void ValidateAndClamp()
        {
            frontWingAngle = Mathf.Clamp(frontWingAngle, 1, 50);
            rearWingAngle = Mathf.Clamp(rearWingAngle, 1, 50);
            drsEfficiency = Mathf.Clamp(drsEfficiency, 1, 10);

            diffOnThrottle = Mathf.Clamp(diffOnThrottle, 50, 100);
            diffOffThrottle = Mathf.Clamp(diffOffThrottle, 50, 100);
            diffPreload = Mathf.Clamp(diffPreload, 0, 100);
            finalDriveRatio = Mathf.Clamp(finalDriveRatio, 2.50f, 4.50f);

            if (gearRatios == null || gearRatios.Length != 8)
            {
                gearRatios = new float[8] { 3.12f, 2.45f, 1.98f, 1.62f, 1.36f, 1.15f, 0.98f, 0.85f };
            }

            frontCamber = Mathf.Clamp(frontCamber, -3.50f, -2.50f);
            rearCamber = Mathf.Clamp(rearCamber, -2.00f, -1.00f);
            frontToe = Mathf.Clamp(frontToe, 0.00f, 0.50f);
            rearToe = Mathf.Clamp(rearToe, 0.00f, 0.50f);

            frontSuspension = Mathf.Clamp(frontSuspension, 1, 41);
            rearSuspension = Mathf.Clamp(rearSuspension, 1, 41);
            frontAntiRollBar = Mathf.Clamp(frontAntiRollBar, 1, 21);
            rearAntiRollBar = Mathf.Clamp(rearAntiRollBar, 1, 21);
            frontRideHeight = Mathf.Clamp(frontRideHeight, 30, 80);
            rearRideHeight = Mathf.Clamp(rearRideHeight, 30, 80);

            brakeBias = Mathf.Clamp(brakeBias, 50, 70);
            brakePressure = Mathf.Clamp(brakePressure, 80, 100);
            brakeDucts = Mathf.Clamp(brakeDucts, 1, 5);

            frontLeftPressure = Mathf.Clamp(frontLeftPressure, 21.5f, 25.5f);
            frontRightPressure = Mathf.Clamp(frontRightPressure, 21.5f, 25.5f);
            rearLeftPressure = Mathf.Clamp(rearLeftPressure, 20.0f, 23.5f);
            rearRightPressure = Mathf.Clamp(rearRightPressure, 20.0f, 23.5f);

            ersRecovery = Mathf.Clamp(ersRecovery, 1, 3);
            engineBraking = Mathf.Clamp(engineBraking, 1, 10);

            ffbStrength = Mathf.Clamp(ffbStrength, 0, 100);
            ffbDamper = Mathf.Clamp(ffbDamper, 0, 100);
            ffbCurbDetail = Mathf.Clamp(ffbCurbDetail, 0, 100);
            understeerEnhance = Mathf.Clamp(understeerEnhance, 0, 100);
        }

        public F1CarSetupData Clone()
        {
            string json = JsonUtility.ToJson(this);
            return JsonUtility.FromJson<F1CarSetupData>(json);
        }
    }
}
