using System;
using System.Collections.Generic;
using Accertone.Branding;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.CarSetup
{
    /// <summary>
    /// Master UI controller for the Accert one Formula 1 Car Setup screen.
    /// Real-time parameter tuning, telemetry calculations, preset management,
    /// baseline comparison, and physics vehicle integration.
    /// </summary>
    public class CarSetupUI : MonoBehaviour
    {
        public enum SetupTab
        {
            Aerodynamics,
            Transmission,
            Suspension,
            Braking,
            Tires,
            Electronics,
            ForceFeedback
        }

        [SerializeField] private SetupTab m_ActiveTab = SetupTab.Aerodynamics;
        private F1CarSetupData m_WorkingSetup;
        private F1CarSetupData m_BaselineSetup;
        private bool m_CompareMode = false;

        private RectTransform m_TabsBar;
        private RectTransform m_ContentArea;
        private RectTransform m_TelemetryPanel;
        private TMP_Text m_TooltipText;

        // Telemetry readout UI
        private TMP_Text m_AeroBalanceText;
        private TMP_Text m_TopSpeedText;
        private TMP_Text m_DownforceText;
        private TMP_Text m_RakeText;
        private TMP_Text m_HandlingBadge;
        private TMP_Text m_EngineerFeedbackText;

        private readonly List<Button> m_TabButtons = new List<Button>();
        private readonly List<GameObject> m_ActiveRows = new List<GameObject>();

        public Action OnCloseRequested;

        private void Awake()
        {
            InitializeData();
            BuildUIHierarchy();
        }

        private void OnEnable()
        {
            InitializeData();
            ShowTab(m_ActiveTab);
            UpdateTelemetryDisplay();
        }

        private void InitializeData()
        {
            if (CarSetupPresetManager.Instance != null)
            {
                m_WorkingSetup = CarSetupPresetManager.Instance.ActiveSetup != null ?
                                 CarSetupPresetManager.Instance.ActiveSetup.Clone() :
                                 CarSetupPresetManager.Instance.CreateBalancedPreset();
                m_BaselineSetup = CarSetupPresetManager.Instance.BaselineSetup != null ?
                                  CarSetupPresetManager.Instance.BaselineSetup.Clone() :
                                  CarSetupPresetManager.Instance.CreateBalancedPreset();
            }
            else
            {
                m_WorkingSetup = new F1CarSetupData();
                m_BaselineSetup = new F1CarSetupData();
            }
        }

        private void BuildUIHierarchy()
        {
            var overlayShape = gameObject.GetComponent<AccertoneShape>();
            if (overlayShape == null) overlayShape = gameObject.AddComponent<AccertoneShape>();
            overlayShape.color = new Color(0.04f, 0.05f, 0.06f, 1.0f);
            overlayShape.raycastTarget = true;

            var rootRt = (RectTransform)transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            // Top Header & Sub-Tabs
            var headerRt = AccertoneUIFactory.CreatePanel(transform, "Header", new Vector2(1760f, 64f), new Color(0.08f, 0.09f, 0.11f, 0.95f), 0f, ChamferCorners.None);
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -36f);

            var title = AccertoneUIFactory.CreateText(headerRt, "Title", "GARAGE // CAR SETUP", BrandTextRole.Headline, Color.white);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.fontSize = 24f;
            var tRt = (RectTransform)title.transform;
            tRt.anchorMin = new Vector2(0f, 0.5f);
            tRt.anchorMax = new Vector2(0f, 0.5f);
            tRt.pivot = new Vector2(0f, 0.5f);
            tRt.anchoredPosition = new Vector2(24f, 0f);
            tRt.sizeDelta = new Vector2(400f, 40f);

            var modelTag = AccertoneUIFactory.CreateText(headerRt, "ModelTag", "AC-26 WORKS PROTOTYPE", BrandTextRole.Caption, AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.accent : Color.magenta);
            modelTag.textWrappingMode = TextWrappingModes.NoWrap;
            var mRt = (RectTransform)modelTag.transform;
            mRt.anchorMin = new Vector2(0f, 0.5f);
            mRt.anchorMax = new Vector2(0f, 0.5f);
            mRt.pivot = new Vector2(0f, 0.5f);
            mRt.anchoredPosition = new Vector2(400f, 0f);
            mRt.sizeDelta = new Vector2(300f, 30f);

            // Tab Bar
            m_TabsBar = AccertoneUIFactory.CreatePanel(transform, "TabsBar", new Vector2(1760f, 48f), new Color(0.06f, 0.07f, 0.09f, 0.85f), 0f, ChamferCorners.None);
            m_TabsBar.anchorMin = new Vector2(0.5f, 1f);
            m_TabsBar.anchorMax = new Vector2(0.5f, 1f);
            m_TabsBar.pivot = new Vector2(0.5f, 1f);
            m_TabsBar.anchoredPosition = new Vector2(0f, -106f);

            BuildTabs();

            // Left / Center Main Parameter Tuning Area
            m_ContentArea = AccertoneUIFactory.CreatePanel(transform, "ContentPanel", new Vector2(1180f, 720f), new Color(0.07f, 0.08f, 0.10f, 0.95f), 12f);
            m_ContentArea.anchorMin = new Vector2(0f, 0.5f);
            m_ContentArea.anchorMax = new Vector2(0f, 0.5f);
            m_ContentArea.pivot = new Vector2(0f, 0.5f);
            m_ContentArea.anchoredPosition = new Vector2(80f, -44f);

            // Right Column: Live Telemetry, Aero Balance & Presets
            m_TelemetryPanel = AccertoneUIFactory.CreatePanel(transform, "TelemetryPanel", new Vector2(540f, 720f), new Color(0.07f, 0.08f, 0.10f, 0.95f), 12f);
            m_TelemetryPanel.anchorMin = new Vector2(1f, 0.5f);
            m_TelemetryPanel.anchorMax = new Vector2(1f, 0.5f);
            m_TelemetryPanel.pivot = new Vector2(1f, 0.5f);
            m_TelemetryPanel.anchoredPosition = new Vector2(-80f, -44f);

            BuildTelemetrySection();

            // Bottom Action Bar
            var bottomBar = AccertoneUIFactory.CreatePanel(transform, "BottomBar", new Vector2(1760f, 60f), new Color(0.06f, 0.07f, 0.09f, 0.95f), 0f, ChamferCorners.None);
            bottomBar.anchorMin = new Vector2(0.5f, 0f);
            bottomBar.anchorMax = new Vector2(0.5f, 0f);
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.anchoredPosition = new Vector2(0f, 26f);

            var btnBack = AccertoneUIFactory.CreateButton(bottomBar, "BtnBack", "RETURN TO LOBBY [ESC]", new Vector2(230f, 40f), () => OnCloseRequested?.Invoke());
            var bRt = (RectTransform)btnBack.transform;
            bRt.anchorMin = new Vector2(0f, 0.5f);
            bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(20f, 0f);

            var btnCompare = AccertoneUIFactory.CreateButton(bottomBar, "BtnCompare", "COMPARE WITH BASELINE", new Vector2(240f, 40f), () =>
            {
                m_CompareMode = !m_CompareMode;
                ShowTab(m_ActiveTab);
            });
            var cRt = (RectTransform)btnCompare.transform;
            cRt.anchorMin = new Vector2(0f, 0.5f);
            cRt.anchorMax = new Vector2(0f, 0.5f);
            cRt.pivot = new Vector2(0f, 0.5f);
            cRt.anchoredPosition = new Vector2(270f, 0f);

            var btnReset = AccertoneUIFactory.CreateButton(bottomBar, "BtnReset", "RESET TAB TO BASELINE", new Vector2(230f, 40f), () =>
            {
                ResetCurrentTabToBaseline();
                ShowTab(m_ActiveTab);
                UpdateTelemetryDisplay();
            });
            var rRt = (RectTransform)btnReset.transform;
            rRt.anchorMin = new Vector2(1f, 0.5f);
            rRt.anchorMax = new Vector2(1f, 0.5f);
            rRt.pivot = new Vector2(1f, 0.5f);
            rRt.anchoredPosition = new Vector2(-250f, 0f);

            var btnApply = AccertoneUIFactory.CreateButton(bottomBar, "BtnApply", "APPLY SETUP TO CAR", new Vector2(220f, 40f), () =>
            {
                ApplySetupToCar();
            }, true);
            var aRt = (RectTransform)btnApply.transform;
            aRt.anchorMin = new Vector2(1f, 0.5f);
            aRt.anchorMax = new Vector2(1f, 0.5f);
            aRt.pivot = new Vector2(1f, 0.5f);
            aRt.anchoredPosition = new Vector2(-20f, 0f);
        }

        private void BuildTabs()
        {
            string[] tabs = { "AERODYNAMICS", "TRANSMISSION", "SUSPENSION", "BRAKING", "TIRES", "ELECTRONICS", "FORCE FEEDBACK" };
            SetupTab[] tabEnums = {
                SetupTab.Aerodynamics,
                SetupTab.Transmission,
                SetupTab.Suspension,
                SetupTab.Braking,
                SetupTab.Tires,
                SetupTab.Electronics,
                SetupTab.ForceFeedback
            };

            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabEnums[i];
                var btn = AccertoneUIFactory.CreateButton(m_TabsBar, "Tab_" + tabs[i], tabs[i], new Vector2(236f, 38f), () =>
                {
                    ShowTab(tab);
                });

                var bRt = (RectTransform)btn.transform;
                bRt.anchorMin = new Vector2(0f, 0.5f);
                bRt.anchorMax = new Vector2(0f, 0.5f);
                bRt.pivot = new Vector2(0f, 0.5f);
                bRt.anchoredPosition = new Vector2(20f + (i * 248f), 0f);

                m_TabButtons.Add(btn);
            }
        }

        public void ShowTab(SetupTab tab)
        {
            m_ActiveTab = tab;

            foreach (var row in m_ActiveRows)
            {
                Destroy(row);
            }
            m_ActiveRows.Clear();

            for (int i = 0; i < m_TabButtons.Count; i++)
            {
                bool active = (int)m_ActiveTab == i;
                var shape = m_TabButtons[i].GetComponent<AccertoneShape>();
                if (shape != null)
                {
                    shape.color = active ? (AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.accent : Color.magenta)
                                         : (AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.panel : new Color(0.12f, 0.14f, 0.18f));
                }
            }

            switch (tab)
            {
                case SetupTab.Aerodynamics:
                    BuildAeroRows();
                    break;
                case SetupTab.Transmission:
                    BuildTransmissionRows();
                    break;
                case SetupTab.Suspension:
                    BuildSuspensionRows();
                    break;
                case SetupTab.Braking:
                    BuildBrakingRows();
                    break;
                case SetupTab.Tires:
                    BuildTiresRows();
                    break;
                case SetupTab.Electronics:
                    BuildElectronicsRows();
                    break;
                case SetupTab.ForceFeedback:
                    BuildForceFeedbackRows();
                    break;
            }
        }

        private void AddRow(GameObject rowGo, int index)
        {
            var rRt = (RectTransform)rowGo.transform;
            rRt.anchorMin = new Vector2(0.5f, 1f);
            rRt.anchorMax = new Vector2(0.5f, 1f);
            rRt.pivot = new Vector2(0.5f, 1f);
            rRt.anchoredPosition = new Vector2(0f, -28f - (index * 54f));
            rRt.sizeDelta = new Vector2(1100f, 44f);
            m_ActiveRows.Add(rowGo);
        }

        // =================================================================== TAB ROWS
        private void BuildAeroRows()
        {
            string deltaFront = m_CompareMode ? $" [Δ {m_WorkingSetup.frontWingAngle - m_BaselineSetup.frontWingAngle:+0;-0;0}]" : "";
            var r1 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FrontWing", "FRONT WING ANGLE" + deltaFront, "DEG", 1f, 50f, m_WorkingSetup.frontWingAngle, val =>
            {
                m_WorkingSetup.frontWingAngle = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r1, 0);

            string deltaRear = m_CompareMode ? $" [Δ {m_WorkingSetup.rearWingAngle - m_BaselineSetup.rearWingAngle:+0;-0;0}]" : "";
            var r2 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "RearWing", "REAR WING ANGLE" + deltaRear, "DEG", 1f, 50f, m_WorkingSetup.rearWingAngle, val =>
            {
                m_WorkingSetup.rearWingAngle = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "DRS", "DRS SLOT EFFICIENCY", "", 1f, 10f, m_WorkingSetup.drsEfficiency, val =>
            {
                m_WorkingSetup.drsEfficiency = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r3, 2);
        }

        private void BuildTransmissionRows()
        {
            var r1 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "DiffOn", "DIFFERENTIAL (ON-THROTTLE)", "%", 50f, 100f, m_WorkingSetup.diffOnThrottle, val =>
            {
                m_WorkingSetup.diffOnThrottle = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r1, 0);

            var r2 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "DiffOff", "DIFFERENTIAL (OFF-THROTTLE)", "%", 50f, 100f, m_WorkingSetup.diffOffThrottle, val =>
            {
                m_WorkingSetup.diffOffThrottle = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "Preload", "DIFFERENTIAL PRELOAD", "NM", 0f, 100f, m_WorkingSetup.diffPreload, val =>
            {
                m_WorkingSetup.diffPreload = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r3, 2);

            var r4 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FinalDrive", "FINAL DRIVE RATIO", "", 2.50f, 4.50f, m_WorkingSetup.finalDriveRatio, val =>
            {
                m_WorkingSetup.finalDriveRatio = val;
                UpdateTelemetryDisplay();
            });
            AddRow(r4, 3);
        }

        private void BuildSuspensionRows()
        {
            var r1 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FrontCamber", "FRONT CAMBER", "DEG", -3.50f, -2.50f, m_WorkingSetup.frontCamber, val =>
            {
                m_WorkingSetup.frontCamber = val;
                UpdateTelemetryDisplay();
            });
            AddRow(r1, 0);

            var r2 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "RearCamber", "REAR CAMBER", "DEG", -2.00f, -1.00f, m_WorkingSetup.rearCamber, val =>
            {
                m_WorkingSetup.rearCamber = val;
                UpdateTelemetryDisplay();
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FrontSpring", "FRONT SUSPENSION STIFFNESS", "", 1f, 41f, m_WorkingSetup.frontSuspension, val =>
            {
                m_WorkingSetup.frontSuspension = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r3, 2);

            var r4 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "RearSpring", "REAR SUSPENSION STIFFNESS", "", 1f, 41f, m_WorkingSetup.rearSuspension, val =>
            {
                m_WorkingSetup.rearSuspension = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r4, 3);

            var r5 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FrontARB", "FRONT ANTI-ROLL BAR", "", 1f, 21f, m_WorkingSetup.frontAntiRollBar, val =>
            {
                m_WorkingSetup.frontAntiRollBar = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r5, 4);

            var r6 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "RearARB", "REAR ANTI-ROLL BAR", "", 1f, 21f, m_WorkingSetup.rearAntiRollBar, val =>
            {
                m_WorkingSetup.rearAntiRollBar = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r6, 5);

            var r7 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FrontHeight", "FRONT RIDE HEIGHT", "MM", 30f, 80f, m_WorkingSetup.frontRideHeight, val =>
            {
                m_WorkingSetup.frontRideHeight = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r7, 6);

            var r8 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "RearHeight", "REAR RIDE HEIGHT", "MM", 30f, 80f, m_WorkingSetup.rearRideHeight, val =>
            {
                m_WorkingSetup.rearRideHeight = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r8, 7);
        }

        private void BuildBrakingRows()
        {
            var r1 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "BrakeBias", "FRONT BRAKE BIAS", "%", 50f, 70f, m_WorkingSetup.brakeBias, val =>
            {
                m_WorkingSetup.brakeBias = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r1, 0);

            var r2 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "BrakePress", "BRAKE PRESSURE", "%", 80f, 100f, m_WorkingSetup.brakePressure, val =>
            {
                m_WorkingSetup.brakePressure = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "BrakeDucts", "BRAKE DUCT COOLING SIZE", "", 1f, 5f, m_WorkingSetup.brakeDucts, val =>
            {
                m_WorkingSetup.brakeDucts = Mathf.RoundToInt(val);
                UpdateTelemetryDisplay();
            });
            AddRow(r3, 2);
        }

        private void BuildTiresRows()
        {
            string[] compounds = { "C1 HARD (WHITE)", "C2 MEDIUM (YELLOW)", "C3 SOFT (RED)", "INTERMEDIATE (GREEN)", "WET (BLUE)" };
            var r1 = AccertoneUIFactory.CreateStepperRow(m_ContentArea, "Compound", "TIRE COMPOUND", compounds, (int)m_WorkingSetup.tireCompound, idx =>
            {
                m_WorkingSetup.tireCompound = (TireCompound)idx;
                UpdateTelemetryDisplay();
            });
            AddRow(r1, 0);

            var r2 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FrontPress", "FRONT TIRE PRESSURE", "PSI", 21.5f, 25.5f, m_WorkingSetup.frontLeftPressure, val =>
            {
                m_WorkingSetup.frontLeftPressure = val;
                m_WorkingSetup.frontRightPressure = val;
                UpdateTelemetryDisplay();
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "RearPress", "REAR TIRE PRESSURE", "PSI", 20.0f, 23.5f, m_WorkingSetup.rearLeftPressure, val =>
            {
                m_WorkingSetup.rearLeftPressure = val;
                m_WorkingSetup.rearRightPressure = val;
                UpdateTelemetryDisplay();
            });
            AddRow(r3, 2);
        }

        private void BuildElectronicsRows()
        {
            string[] fuelMix = { "LEAN", "STANDARD", "RICH" };
            var r1 = AccertoneUIFactory.CreateStepperRow(m_ContentArea, "FuelMix", "ENGINE FUEL MIX", fuelMix, (int)m_WorkingSetup.engineMix, idx =>
            {
                m_WorkingSetup.engineMix = (EngineFuelMix)idx;
            });
            AddRow(r1, 0);

            string[] ersModes = { "NONE", "LOW / HARVEST", "BALANCED", "HOTLAP", "OVERTAKE" };
            var r2 = AccertoneUIFactory.CreateStepperRow(m_ContentArea, "ERS", "ERS DEPLOYMENT MODE", ersModes, (int)m_WorkingSetup.ersMode, idx =>
            {
                m_WorkingSetup.ersMode = (ErsMode)idx;
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "EngineBrake", "ENGINE BRAKING", "", 1f, 10f, m_WorkingSetup.engineBraking, val =>
            {
                m_WorkingSetup.engineBraking = Mathf.RoundToInt(val);
            });
            AddRow(r3, 2);

            string[] tcOptions = { "OFF", "MEDIUM", "FULL" };
            var r4 = AccertoneUIFactory.CreateStepperRow(m_ContentArea, "TC", "TRACTION CONTROL ASSIST", tcOptions, (int)m_WorkingSetup.tractionControl, idx =>
            {
                m_WorkingSetup.tractionControl = (AssistLevel)idx;
            });
            AddRow(r4, 3);
        }

        private void BuildForceFeedbackRows()
        {
            var r1 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FFBStrength", "FFB MASTER STRENGTH", "%", 0f, 100f, m_WorkingSetup.ffbStrength, val =>
            {
                m_WorkingSetup.ffbStrength = Mathf.RoundToInt(val);
            });
            AddRow(r1, 0);

            var r2 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FFBDamper", "ON-CENTER WHEEL DAMPER", "%", 0f, 100f, m_WorkingSetup.ffbDamper, val =>
            {
                m_WorkingSetup.ffbDamper = Mathf.RoundToInt(val);
            });
            AddRow(r2, 1);

            var r3 = AccertoneUIFactory.CreateSliderRow(m_ContentArea, "FFBCurb", "ROAD SURFACE & CURB DETAIL", "%", 0f, 100f, m_WorkingSetup.ffbCurbDetail, val =>
            {
                m_WorkingSetup.ffbCurbDetail = Mathf.RoundToInt(val);
            });
            AddRow(r3, 2);
        }

        // =================================================================== TELEMETRY & PRESETS PANEL
        private void BuildTelemetrySection()
        {
            var pHeader = AccertoneUIFactory.CreateText(m_TelemetryPanel, "Header", "RACE ENGINEER // TELEMETRY", BrandTextRole.Caption, Color.white);
            var phRt = (RectTransform)pHeader.transform;
            phRt.anchorMin = new Vector2(0f, 1f);
            phRt.anchorMax = new Vector2(0f, 1f);
            phRt.pivot = new Vector2(0f, 1f);
            phRt.anchoredPosition = new Vector2(24f, -24f);

            // Preset Quick Selectors
            string[] presetNames = { "BALANCED (VALCORSA)", "HIGH DOWNFORCE (MONACO)", "LOW DOWNFORCE (MONZA)", "WET WEATHER TRIM" };
            for (int i = 0; i < presetNames.Length; i++)
            {
                int pIdx = i;
                var pBtn = AccertoneUIFactory.CreateButton(m_TelemetryPanel, "Preset_" + i, presetNames[i], new Vector2(490f, 36f), () =>
                {
                    LoadPresetByIndex(pIdx);
                });
                var pbRt = (RectTransform)pBtn.transform;
                pbRt.anchorMin = new Vector2(0.5f, 1f);
                pbRt.anchorMax = new Vector2(0.5f, 1f);
                pbRt.pivot = new Vector2(0.5f, 1f);
                pbRt.anchoredPosition = new Vector2(0f, -60f - (i * 42f));
            }

            // Divider
            var div = AccertoneUIFactory.CreatePanel(m_TelemetryPanel, "Divider", new Vector2(490f, 1f), AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.hairline : Color.gray, 0f, ChamferCorners.None);
            div.anchorMin = new Vector2(0.5f, 1f);
            div.anchorMax = new Vector2(0.5f, 1f);
            div.pivot = new Vector2(0.5f, 1f);
            div.anchoredPosition = new Vector2(0f, -238f);

            // Telemetry Readouts
            m_AeroBalanceText = CreateTelemetryRow("AERO BALANCE (FRONT)", "42.5%", -256f);
            m_TopSpeedText = CreateTelemetryRow("EST. TOP SPEED", "338 KM/H", -298f);
            m_DownforceText = CreateTelemetryRow("DOWNFORCE INDEX", "72 / 100", -340f);
            m_RakeText = CreateTelemetryRow("CHASSIS RAKE", "+18 MM", -382f);

            // Handling Badge
            m_HandlingBadge = AccertoneUIFactory.CreateText(m_TelemetryPanel, "HandlingBadge", "BALANCED - OPTIMAL", BrandTextRole.Title, Color.green, TextAlignmentOptions.Center);
            var hbRt = (RectTransform)m_HandlingBadge.transform;
            hbRt.anchorMin = new Vector2(0.5f, 1f);
            hbRt.anchorMax = new Vector2(0.5f, 1f);
            hbRt.pivot = new Vector2(0.5f, 1f);
            hbRt.anchoredPosition = new Vector2(0f, -430f);
            hbRt.sizeDelta = new Vector2(490f, 32f);

            // Engineer Feedback Box
            var adviceBox = AccertoneUIFactory.CreatePanel(m_TelemetryPanel, "AdviceBox", new Vector2(490f, 210f), new Color(0.05f, 0.06f, 0.08f), 8f);
            adviceBox.anchorMin = new Vector2(0.5f, 0f);
            adviceBox.anchorMax = new Vector2(0.5f, 0f);
            adviceBox.pivot = new Vector2(0.5f, 0f);
            adviceBox.anchoredPosition = new Vector2(0f, 20f);

            var advHeader = AccertoneUIFactory.CreateText(adviceBox, "AdvHeader", "CHIEF RACE ENGINEER ASSESSMENT", BrandTextRole.Caption, AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.accent : Color.magenta);
            var ahRt = (RectTransform)advHeader.transform;
            ahRt.anchorMin = new Vector2(0f, 1f);
            ahRt.anchorMax = new Vector2(0f, 1f);
            ahRt.pivot = new Vector2(0f, 1f);
            ahRt.anchoredPosition = new Vector2(16f, -14f);

            m_EngineerFeedbackText = AccertoneUIFactory.CreateText(adviceBox, "AdviceText", "Chassis setup is harmonized. Aerodynamic grip and mechanical roll distribution provide stable high-speed turn-in and predictable traction.", BrandTextRole.Body, AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.silver : Color.white);
            var atRt = (RectTransform)m_EngineerFeedbackText.transform;
            atRt.anchorMin = new Vector2(0f, 0f);
            atRt.anchorMax = new Vector2(1f, 1f);
            atRt.offsetMin = new Vector2(16f, 14f);
            atRt.offsetMax = new Vector2(-16f, -40f);
        }

        private TMP_Text CreateTelemetryRow(string labelText, string initialValue, float yPos)
        {
            var l = AccertoneUIFactory.CreateText(m_TelemetryPanel, "Lbl_" + labelText, labelText, BrandTextRole.Caption, AccertoneUIFactory.Palette != null ? AccertoneUIFactory.Palette.textMuted : Color.gray);
            var lRt = (RectTransform)l.transform;
            lRt.anchorMin = new Vector2(0f, 1f);
            lRt.anchorMax = new Vector2(0f, 1f);
            lRt.pivot = new Vector2(0f, 1f);
            lRt.anchoredPosition = new Vector2(24f, yPos);

            var v = AccertoneUIFactory.CreateText(m_TelemetryPanel, "Val_" + labelText, initialValue, BrandTextRole.Data, Color.white, TextAlignmentOptions.Right);
            var vRt = (RectTransform)v.transform;
            vRt.anchorMin = new Vector2(1f, 1f);
            vRt.anchorMax = new Vector2(1f, 1f);
            vRt.pivot = new Vector2(1f, 1f);
            vRt.anchoredPosition = new Vector2(-24f, yPos);

            return v;
        }

        private void UpdateTelemetryDisplay()
        {
            var analysis = CarSetupEngineeringEngine.Analyze(m_WorkingSetup);

            if (m_AeroBalanceText != null) m_AeroBalanceText.text = $"{analysis.aeroBalancePercent:F1}%";
            if (m_TopSpeedText != null) m_TopSpeedText.text = $"{analysis.topSpeedKmh:F0} KM/H  ({analysis.topSpeedMph:F0} MPH)";
            if (m_DownforceText != null) m_DownforceText.text = $"{analysis.downforceIndex:F0} / 100";
            if (m_RakeText != null) m_RakeText.text = $"+{analysis.rakeAngleMm:F0} MM";
            if (m_HandlingBadge != null)
            {
                m_HandlingBadge.text = analysis.handlingSummary;
                m_HandlingBadge.color = analysis.handlingColor;
            }
            if (m_EngineerFeedbackText != null)
            {
                m_EngineerFeedbackText.text = analysis.engineerFeedback;
            }
        }

        private void LoadPresetByIndex(int index)
        {
            if (CarSetupPresetManager.Instance == null) return;
            switch (index)
            {
                case 0: m_WorkingSetup = CarSetupPresetManager.Instance.CreateBalancedPreset(); break;
                case 1: m_WorkingSetup = CarSetupPresetManager.Instance.CreateHighDownforcePreset(); break;
                case 2: m_WorkingSetup = CarSetupPresetManager.Instance.CreateLowDownforcePreset(); break;
                case 3: m_WorkingSetup = CarSetupPresetManager.Instance.CreateWetPreset(); break;
            }
            ShowTab(m_ActiveTab);
            UpdateTelemetryDisplay();
        }

        private void ResetCurrentTabToBaseline()
        {
            if (m_BaselineSetup == null) return;
            switch (m_ActiveTab)
            {
                case SetupTab.Aerodynamics:
                    m_WorkingSetup.frontWingAngle = m_BaselineSetup.frontWingAngle;
                    m_WorkingSetup.rearWingAngle = m_BaselineSetup.rearWingAngle;
                    m_WorkingSetup.drsEfficiency = m_BaselineSetup.drsEfficiency;
                    break;
                case SetupTab.Suspension:
                    m_WorkingSetup.frontSuspension = m_BaselineSetup.frontSuspension;
                    m_WorkingSetup.rearSuspension = m_BaselineSetup.rearSuspension;
                    m_WorkingSetup.frontAntiRollBar = m_BaselineSetup.frontAntiRollBar;
                    m_WorkingSetup.rearAntiRollBar = m_BaselineSetup.rearAntiRollBar;
                    m_WorkingSetup.frontRideHeight = m_BaselineSetup.frontRideHeight;
                    m_WorkingSetup.rearRideHeight = m_BaselineSetup.rearRideHeight;
                    break;
            }
        }

        private void ApplySetupToCar()
        {
            if (CarSetupPresetManager.Instance != null)
            {
                CarSetupPresetManager.Instance.SetActiveSetup(m_WorkingSetup);
            }

            var applier = FindFirstObjectByType<VehicleSetupApplier>();
            if (applier != null)
            {
                applier.ApplyCarSetup(m_WorkingSetup);
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnCloseRequested?.Invoke();
            }
        }
    }
}
