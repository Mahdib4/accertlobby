using System;
using UnityEngine;

namespace Accertone.CarSetup
{
    [Serializable]
    public class SetupTelemetryAnalysis
    {
        public float aeroBalancePercent;     // e.g. 42.5% front
        public float topSpeedKmh;            // e.g. 338 km/h
        public float topSpeedMph;            // e.g. 210 mph
        public float downforceIndex;         // Relative index 0-100
        public float dragIndex;              // Relative index 0-100
        public float mechanicalBalance;      // Front roll stiffness %
        public float rakeAngleMm;            // Rear height minus front height
        public float tireWearRateIndex;      // Index of tire degradation (1.0 = baseline)
        public string handlingSummary;       // "BALANCED - NEUTRAL"
        public string engineerFeedback;      // Dynamic advice text
        public Color handlingColor;
    }

    /// <summary>
    /// Vehicle physics evaluation engine. Calculates real aerodynamic balance,
    /// top speed, mechanical roll distribution, rake ground effects, and telemetry trade-offs.
    /// </summary>
    public static class CarSetupEngineeringEngine
    {
        public static SetupTelemetryAnalysis Analyze(F1CarSetupData setup)
        {
            if (setup == null) setup = new F1CarSetupData();
            var analysis = new SetupTelemetryAnalysis();

            // 1. Rake calculation (rear minus front ride height)
            float rake = setup.rearRideHeight - setup.frontRideHeight;
            analysis.rakeAngleMm = rake;

            // 2. Aerodynamic calculations
            // Formula 1 front wing vs rear wing balance:
            // Typical front downforce fraction is ~38-44% depending on wing angles and diffuser rake
            float totalWing = Mathf.Max(2f, setup.frontWingAngle + setup.rearWingAngle);
            float wingRatio = (float)setup.frontWingAngle / totalWing;

            // Rake enhances diffuser suction up to optimal ~20-25mm rake, then stalls if too steep
            float diffuserRakeBonus = Mathf.Clamp(rake / 20f, 0.8f, 1.25f);
            float baseAeroBal = (wingRatio * 0.70f + 0.15f) * 100f;
            analysis.aeroBalancePercent = Mathf.Clamp(baseAeroBal + (diffuserRakeBonus - 1f) * 4f, 35.0f, 52.0f);

            // Drag Index (0 to 100)
            float aeroDrag = (setup.frontWingAngle * 0.45f + setup.rearWingAngle * 0.55f);
            analysis.dragIndex = Mathf.Clamp(aeroDrag, 5f, 95f);

            // Downforce Index (0 to 100)
            analysis.downforceIndex = Mathf.Clamp((setup.frontWingAngle * 0.5f + setup.rearWingAngle * 0.5f) * diffuserRakeBonus, 5f, 98f);

            // Estimated Top Speed (km/h & mph)
            // Baseline 355 km/h at Monza trim (low drag), down to ~315 km/h at Monaco trim
            float topSpeedBase = 360f - (analysis.dragIndex * 0.48f);
            // Gearing modifier: final drive ratio affects terminal speed
            float gearModifier = (3.40f - setup.finalDriveRatio) * 12f;
            analysis.topSpeedKmh = Mathf.Round(Mathf.Clamp(topSpeedBase + gearModifier, 310f, 362f));
            analysis.topSpeedMph = Mathf.Round(analysis.topSpeedKmh * 0.621371f);

            // 3. Mechanical Balance & Roll Stiffness
            // Anti-roll bar distribution: Front vs Rear ARB
            float totalArb = setup.frontAntiRollBar + setup.rearAntiRollBar;
            float arbDistribution = (float)setup.frontAntiRollBar / Mathf.Max(1f, totalArb);

            // Suspension spring distribution
            float totalSpring = setup.frontSuspension + setup.rearSuspension;
            float springDistribution = (float)setup.frontSuspension / Mathf.Max(1f, totalSpring);

            analysis.mechanicalBalance = Mathf.Round((arbDistribution * 0.6f + springDistribution * 0.4f) * 100f);

            // 4. Tire Degradation & Thermal estimate
            float camberAggression = (Mathf.Abs(setup.frontCamber) - 2.5f) * 1.5f + (Mathf.Abs(setup.rearCamber) - 1.0f) * 1.2f;
            float pressureOffset = Mathf.Abs(setup.frontLeftPressure - 23.0f) + Mathf.Abs(setup.rearLeftPressure - 21.2f);
            analysis.tireWearRateIndex = 1.0f + (camberAggression * 0.12f) + (pressureOffset * 0.05f);

            // 5. Handling Tendency & Engineering Advice
            float understeerBias = (analysis.mechanicalBalance - 50f) * 0.6f - (analysis.aeroBalancePercent - 42f) * 1.2f;

            if (understeerBias > 4.5f)
            {
                analysis.handlingSummary = "UNDERSTEER BIASED";
                analysis.handlingColor = new Color(0.95f, 0.75f, 0.2f);
                analysis.engineerFeedback = "Stiff front roll bar and low front wing balance will stabilize corner entry but induce mid-corner push and slow turn-in rotation. Consider adding 2 clicks of front wing or softening front ARB.";
            }
            else if (understeerBias < -4.5f)
            {
                analysis.handlingSummary = "OVERSTEER / ROTATION";
                analysis.handlingColor = new Color(0.95f, 0.3f, 0.3f);
                analysis.engineerFeedback = "High front aerodynamic grip relative to rear downforce provides sharp turn-in, but may provoke sudden snap-oversteer on high-speed sweeps and corner exits. Recommend raising rear wing angle or softening rear springs.";
            }
            else
            {
                analysis.handlingSummary = "BALANCED / OPTIMAL";
                analysis.handlingColor = new Color(0.25f, 0.85f, 0.6f);
                analysis.engineerFeedback = "Aero balance and mechanical roll stiffness are well-harmonized. Chassis provides confident high-speed platform with responsive direction change without compromising rear traction.";
            }

            return analysis;
        }
    }
}
