using System;
using System.Collections.Generic;
using Accertone.Settings;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.Garage
{
    /// <summary>
    /// The GARAGE tab: the car on its turntable in the middle, four systems on each side, and a configuration
    /// panel that slides in from the selected side while the camera moves to that part of the car.
    /// Owns the 2D half; <see cref="GarageStage"/> owns the camera, turntable, driver and lights.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class GarageScreen : MonoBehaviour
    {
        const float SideMargin = 112f, ColumnTop = 352f, ItemGap = 92f;
        const float PanelTop = 258f, PanelBottomMargin = 104f, PanelWidth = 608f, PanelEdge = 96f, PanelPad = 36f;
        const float RailX = 36f, RailGap = 58f;
        // How far the car moves away from an open panel (fraction of half the screen width).
        const float PanelFraming = 0.27f;

        enum State { Closed, Entering, Hub, Panel, Exiting }

        static readonly string[] s_HudPanels = { "PlayerCard", "SessionPanel", "ChassisInfo", "RaceAction", "InputHints" };
        static readonly string[] s_PartNames = { "BODYWORK", "FRONT WING", "REAR WING · DRS", "SUSPENSION", "FRONT BRAKE", "FRONT TYRE", "POWER UNIT", "SURVIVAL CELL", "AC-26" };

        public Action OnCloseRequested;
        public Action OnOpenEngineeringSheet;

        State m_State = State.Closed;
        bool m_Built;
        RectTransform m_Root;
        CanvasGroup m_Group;
        GarageStage m_Stage;

        // Pieces.
        RectTransform m_Header, m_Footer, m_InspectBar, m_Frame, m_PanelHost;
        CanvasGroup m_HeaderGroup, m_FooterGroup, m_InspectGroup, m_FrameGroup;
        TextMeshProUGUI m_Breadcrumb, m_SaveText, m_ViewText;
        Image m_SaveDot;
        GarageButton m_BackButton;
        readonly GarageNavItem[] m_Items = new GarageNavItem[8];
        readonly GaragePanel[] m_Panels = new GaragePanel[8];
        readonly RectTransform[] m_Corners = new RectTransform[8];
        readonly List<TextMeshProUGUI> m_ViewChips = new List<TextMeshProUGUI>();
        GarageLine m_Callout;
        GarageRing m_Ring;
        Image m_RingDot;
        TextMeshProUGUI m_RingLabel;
        RectTransform m_RingRt, m_RingLabelRt;

        // Panel shell.
        GaragePanelShell m_Shell, m_OldShell;
        int m_PanelIndex = -1, m_LastItem;
        float m_ShotAt = -1f;
        GarageControl m_PendingShotControl;

        // Timeline.
        float m_OpenTime, m_InputReady;
        int m_PendingPage = -1;
        Action m_OnExited;
        float m_HeaderShow, m_FooterShow, m_FrameShow, m_InspectShow, m_HeaderT, m_FooterT, m_FrameT, m_InspectT;
        float m_CalloutShow, m_CalloutT, m_FrameX;
        int m_ShownView = -1, m_ShownMode = -1;
        int m_View = -1;

        // HUD that the garage fades out (and the side shades it softens).
        readonly List<CanvasGroup> m_HudGroups = new List<CanvasGroup>();
        CanvasGroup m_Shades;
        float m_HudFade = 1f, m_HudFadeTarget = 1f;

        public bool IsOpen { get { return m_State != State.Closed && m_State != State.Exiting; } }
        public bool IsActive { get { return m_State != State.Closed; } }
        public bool PanelOpen { get { return m_State == State.Panel; } }

        // =====================================================================================================
        // LIFECYCLE
        // =====================================================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            GarageSession.ResetStatics();
        }

        /// <summary>Called when the lobby starts: applies the saved Garage state to the car straight away.</summary>
        public static void Boot()
        {
            var stage = GarageStage.Ensure();
            GarageSession.Boot(stage.CarRig);
        }

        public void Open(int page = -1)
        {
            if (m_State == State.Entering || m_State == State.Hub || m_State == State.Panel)
            {
                if (page >= 0) OpenPanel(page);
                return;
            }
            // Active first: TextMeshPro measures text wrongly on inactive objects, and the layout relies on it.
            gameObject.SetActive(true);
            Build();
            PlaceInLobbyHierarchy();
            m_Stage = GarageStage.Ensure();
            if (GarageSession.Current == null) GarageSession.Boot(m_Stage.CarRig);
            GarageSession.Current.Changed -= OnDataChanged;
            GarageSession.Current.Changed += OnDataChanged;

            // A clicked nav tab stays selected in the EventSystem; take the selection so arrows / Enter stay here.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            m_State = State.Entering;
            m_OpenTime = Time.unscaledTime;
            m_InputReady = m_OpenTime + AccertoneMotion.Duration(0.95f);
            m_PendingPage = page;
            m_OnExited = null;
            m_Group.alpha = 1f;
            m_Group.blocksRaycasts = true;
            m_View = -1;
            m_FrameX = 0f;
            CollectHud();
            m_HudFadeTarget = 0f;

            for (int i = 0; i < 8; i++)
            {
                m_Items[i].SetRail(false);
                m_Items[i].SetActiveState(false);
                m_Items[i].Snap(false);
                m_Items[i].Show(0.62f + (i % 4) * 0.07f + (i >= 4 ? 0.035f : 0f));
            }
            m_HeaderShow = m_FooterShow = m_FrameShow = m_InspectShow = 0f;
            m_HeaderT = m_FooterT = m_FrameT = m_InspectT = 0f;
            m_CalloutShow = m_CalloutT = 0f;
            RefreshStatuses();
            m_Stage.Enter();
            m_Stage.SetFraming(0f);
            GarageTheme.PlaySound(UiSound.Category);
        }

        /// <summary>Animates out and hands the lobby back. <paramref name="onExited"/> runs once the camera has landed.</summary>
        public void Close(Action onExited)
        {
            if (m_State == State.Closed) { if (onExited != null) onExited(); return; }
            m_OnExited = onExited;
            if (m_State == State.Exiting) return;
            m_State = State.Exiting;
            if (m_Shell != null)
            {
                m_Panels[m_PanelIndex].OnClose();
                m_Shell.Hide();
                m_OldShell = m_Shell;
                m_Shell = null;
            }
            m_PanelIndex = -1;
            for (int i = 0; i < 8; i++) { m_Items[i].Hide((3 - i % 4) * 0.03f); m_Items[i].SetActiveState(false); }
            m_HeaderT = m_FooterT = m_FrameT = m_InspectT = 0f;
            m_CalloutT = 0f;
            m_Group.blocksRaycasts = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (GarageSession.Current != null) GarageSession.Current.SaveNow();
            if (m_Stage != null) m_Stage.Exit(FinishClose);
            else FinishClose();
            GarageTheme.PlaySound(UiSound.Back);
        }

        void FinishClose()
        {
            m_State = State.Closed;
            if (m_OldShell != null) { Destroy(m_OldShell.gameObject); m_OldShell = null; }
            if (m_Shades != null) m_Shades.alpha = 1f;
            m_HudFade = m_HudFadeTarget = 1f;
            if (GarageSession.Current != null) GarageSession.Current.Changed -= OnDataChanged;
            var done = m_OnExited;
            m_OnExited = null;
            gameObject.SetActive(false);
            if (done != null) done();
        }

        /// <summary>Brings the lobby HUD back with a quick staggered reveal (instead of the slow load-in delays).</summary>
        public void RestoreHud()
        {
            var canvas = transform.parent;
            if (canvas == null) return;
            for (int i = 0; i < s_HudPanels.Length; i++)
            {
                var t = canvas.Find(s_HudPanels[i]);
                if (t == null) continue;
                var group = t.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
                if (t.gameObject.activeSelf) continue;
                var reveal = t.GetComponent<UIRevealGroup>();
                if (reveal != null) GarageRevealOverride.Apply(reveal, 0.05f + i * 0.06f);
                t.gameObject.SetActive(true);
            }
        }

        /// <summary>Back one level: panel → systems → lobby.</summary>
        public void RequestBack()
        {
            if (m_State == State.Panel) { ClosePanel(); return; }
            if (m_State == State.Hub || m_State == State.Entering)
            {
                if (OnCloseRequested != null) OnCloseRequested();
                else Close(null);
            }
        }

        public void OpenEngineeringSheet()
        {
            if (OnOpenEngineeringSheet != null) OnOpenEngineeringSheet();
        }

        void PlaceInLobbyHierarchy()
        {
            var parent = transform.parent;
            if (parent == null) return;
            var anchor = parent.Find("Screen_Settings");
            if (anchor == null) anchor = parent.Find("Backdrop_Shades");
            if (anchor == null || anchor == transform) return;
            int current = transform.GetSiblingIndex();
            int target = anchor.GetSiblingIndex() + 1;
            if (current < target) target -= 1;
            if (current != target) transform.SetSiblingIndex(target);
        }

        void CollectHud()
        {
            m_HudGroups.Clear();
            var canvas = transform.parent;
            if (canvas == null) return;
            foreach (var n in s_HudPanels)
            {
                var t = canvas.Find(n);
                if (t == null || !t.gameObject.activeSelf) continue;
                var g = t.GetComponent<CanvasGroup>();
                if (g == null) g = t.gameObject.AddComponent<CanvasGroup>();
                m_HudGroups.Add(g);
            }
            var shades = canvas.Find("Backdrop_Shades");
            if (shades != null)
            {
                m_Shades = shades.GetComponent<CanvasGroup>();
                if (m_Shades == null) m_Shades = shades.gameObject.AddComponent<CanvasGroup>();
                m_Shades.blocksRaycasts = false;
                m_Shades.interactable = false;
            }
            m_HudFade = 1f;
        }

        // =====================================================================================================
        // BUILD
        // =====================================================================================================
        void Build()
        {
            if (m_Built) return;
            m_Built = true;
            m_Root = (RectTransform)transform;
            SettingsUI.Stretch(m_Root);
            gameObject.layer = 5;
            m_Group = gameObject.GetComponent<CanvasGroup>();
            if (m_Group == null) m_Group = gameObject.AddComponent<CanvasGroup>();
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            // Drag anywhere off the menus to turn the car; scroll to zoom.
            var drag = SettingsUI.Fill(m_Root, "DragSurface", new Color(0f, 0f, 0f, 0f), true);
            SettingsUI.Stretch(drag.rectTransform);
            drag.gameObject.AddComponent<GarageDragSurface>();

            BuildFrame();
            BuildCallout();
            m_PanelHost = SettingsUI.Rect("Panels", m_Root);
            SettingsUI.Stretch(m_PanelHost);
            BuildColumns();
            BuildHeader();
            BuildInspectBar();
            BuildFooter();
        }

        void BuildHeader()
        {
            m_Header = SettingsUI.Rect("Header", m_Root);
            SettingsUI.Stretch(m_Header);
            m_HeaderGroup = m_Header.gameObject.AddComponent<CanvasGroup>();
            m_HeaderGroup.blocksRaycasts = true;
            var tick = SettingsUI.Shape(m_Header, "Tick", SettingsTheme.Accent);
            SettingsUI.At(tick.rectTransform, SideMargin, 156f, 18f, 2f);
            var eyebrow = GarageTheme.Text(m_Header, "Eyebrow", "GARAGE", SettingsTheme.Label, 11f, SettingsTheme.Muted, 26f);
            SettingsUI.At(eyebrow.rectTransform, SideMargin + 28f, 148f, 300f, 18f);
            var markA = SettingsUI.Shape(m_Header, "MarkA", SettingsTheme.Accent, 0f, ChamferCorners.None, 9f);
            SettingsUI.At(markA.rectTransform, SideMargin, 172f, 15f, 34f);
            var markB = SettingsUI.Shape(m_Header, "MarkB", SettingsTheme.A(SettingsTheme.Accent, 0.55f), 0f, ChamferCorners.None, 9f);
            SettingsUI.At(markB.rectTransform, SideMargin + 14f, 172f, 15f, 34f);
            var title = GarageTheme.Text(m_Header, "Title", "AC-26", SettingsTheme.Display, 46f, SettingsTheme.White, 7f);
            title.fontStyle = FontStyles.UpperCase;
            SettingsUI.At(title.rectTransform, SideMargin + 46f, 164f, 300f, 50f);
            title.ForceMeshUpdate();
            float tw = Mathf.Max(150f, Mathf.Ceil(title.preferredWidth));
            var tag = GarageTheme.Text(m_Header, "Tag", "WORKS PROTOTYPE  ·  CHASSIS 01", SettingsTheme.Label, 11f, SettingsTheme.AccentLight, 22f);
            SettingsUI.At(tag.rectTransform, SideMargin + 46f + tw + 18f, 182f, 420f, 18f);
            m_Breadcrumb = GarageTheme.Text(m_Header, "Breadcrumb", "", SettingsTheme.Label, 11f, SettingsTheme.Text2, 22f);
            SettingsUI.At(m_Breadcrumb.rectTransform, SideMargin, 222f, 900f, 18f);

            m_BackButton = GarageButton.Create(m_Header, "RETURN TO SHOWROOM", 232f, () => RequestBack(), false, false, GarageEffect.Spec, null, 38f);
            var brt = (RectTransform)m_BackButton.transform;
            SettingsUI.AtRight(brt, SideMargin, 170f, 232f, 38f);
            m_BackButton.navigation = new Navigation { mode = Navigation.Mode.None };
            m_BackButton.onClick = () =>
            {
                if (m_State == State.Panel) ClosePanel();
                else RequestBack();
            };
        }

        void BuildColumns()
        {
            string[] titles = { "Paint & Livery", "Aerodynamics", "Suspension", "Braking System", "Tires & Wheels", "Powertrain", "Car Setup", "Vehicle Information" };
            GarageIconKind[] icons = { GarageIconKind.Paint, GarageIconKind.Aero, GarageIconKind.Suspension, GarageIconKind.Brakes, GarageIconKind.Tyres, GarageIconKind.Powertrain, GarageIconKind.Setup, GarageIconKind.Info };
            m_Panels[0] = new GaragePaintPanel();
            m_Panels[1] = new GarageAeroPanel();
            m_Panels[2] = new GarageSuspensionPanel();
            m_Panels[3] = new GarageBrakesPanel();
            m_Panels[4] = new GarageTyresPanel();
            m_Panels[5] = new GaragePowertrainPanel();
            m_Panels[6] = new GarageSetupPanel();
            m_Panels[7] = new GarageInfoPanel();
            var columns = SettingsUI.Rect("Columns", m_Root);
            SettingsUI.Stretch(columns);
            for (int i = 0; i < 8; i++)
            {
                bool right = i >= 4;
                int row = i % 4;
                var item = GarageNavItem.Create(columns, i, right, titles[i], icons[i]);
                float railTop = PanelTop + (1080f - PanelTop - PanelBottomMargin) * 0.5f - (RailGap * 4f - 12f) * 0.5f;
                item.SetPositions(new Vector2(SideMargin, ColumnTop + row * ItemGap), new Vector2(RailX, railTop + row * RailGap));
                item.activated = OnItemActivated;
                item.focusedItem = OnItemFocused;
                m_Items[i] = item;
                m_Panels[i].screen = this;
            }
            // Explicit navigation: up / down within a column, left / right across.
            for (int i = 0; i < 8; i++)
            {
                int row = i % 4;
                bool right = i >= 4;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = row > 0 ? m_Items[i - 1] : null;
                nav.selectOnDown = row < 3 ? m_Items[i + 1] : null;
                nav.selectOnRight = right ? null : m_Items[i + 4];
                nav.selectOnLeft = right ? m_Items[i - 4] : null;
                m_Items[i].navigation = nav;
            }
        }

        void BuildFrame()
        {
            // Viewfinder corners around the car; they follow the framing when a panel opens.
            m_Frame = SettingsUI.Rect("Viewfinder", m_Root);
            m_Frame.anchorMin = m_Frame.anchorMax = new Vector2(0.5f, 0.5f);
            m_Frame.pivot = new Vector2(0.5f, 0.5f);
            m_Frame.sizeDelta = new Vector2(820f, 440f);
            m_Frame.anchoredPosition = new Vector2(0f, -20f);
            m_FrameGroup = m_Frame.gameObject.AddComponent<CanvasGroup>();
            m_FrameGroup.blocksRaycasts = false;
            for (int i = 0; i < 4; i++)
            {
                var h = SettingsUI.Shape(m_Frame, "CornerH" + i, SettingsTheme.A(SettingsTheme.Silver, 0.42f));
                var v = SettingsUI.Shape(m_Frame, "CornerV" + i, SettingsTheme.A(SettingsTheme.Silver, 0.42f));
                h.rectTransform.anchorMin = h.rectTransform.anchorMax = h.rectTransform.pivot = new Vector2(i % 2, i / 2);
                v.rectTransform.anchorMin = v.rectTransform.anchorMax = v.rectTransform.pivot = new Vector2(i % 2, i / 2);
                m_Corners[i * 2] = h.rectTransform;
                m_Corners[i * 2 + 1] = v.rectTransform;
            }
            m_ViewText = GarageTheme.Text(m_Frame, "View", "", SettingsTheme.Data, 10f, SettingsTheme.Muted, 2f, TextAlignmentOptions.Center);
            m_ViewText.rectTransform.anchorMin = m_ViewText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            m_ViewText.rectTransform.pivot = new Vector2(0.5f, 1f);
            m_ViewText.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            m_ViewText.rectTransform.sizeDelta = new Vector2(600f, 16f);
        }

        void BuildCallout()
        {
            var lineRt = SettingsUI.Rect("Callout", m_Root);
            SettingsUI.Stretch(lineRt);
            m_Callout = lineRt.gameObject.AddComponent<GarageLine>();
            m_Callout.color = SettingsTheme.AccentLight;
            m_Callout.raycastTarget = false;
            m_Callout.thickness = 1.3f;
            m_Callout.SetFade(0.95f, 0.3f);

            m_RingRt = SettingsUI.Rect("Target", m_Root);
            m_RingRt.anchorMin = m_RingRt.anchorMax = new Vector2(0f, 0f);
            m_RingRt.pivot = new Vector2(0.5f, 0.5f);
            m_RingRt.sizeDelta = new Vector2(30f, 30f);
            m_Ring = m_RingRt.gameObject.AddComponent<GarageRing>();
            m_Ring.color = SettingsTheme.AccentLight;
            m_Ring.thickness = 1.4f;
            m_Ring.dashes = 6;
            m_Ring.raycastTarget = false;
            m_RingDot = SettingsUI.Diamond(m_RingRt, "Dot", SettingsTheme.White, 5f);
            m_RingDot.rectTransform.anchorMin = m_RingDot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            m_RingDot.rectTransform.anchoredPosition = Vector2.zero;
            m_RingLabel = GarageTheme.Text(m_Root, "TargetLabel", "", SettingsTheme.Label, 9f, SettingsTheme.AccentLight, 20f);
            m_RingLabelRt = m_RingLabel.rectTransform;
            m_RingLabelRt.anchorMin = m_RingLabelRt.anchorMax = new Vector2(0f, 0f);
            m_RingLabelRt.pivot = new Vector2(0f, 0.5f);
            m_RingLabelRt.sizeDelta = new Vector2(240f, 14f);
        }

        void BuildInspectBar()
        {
            m_InspectBar = SettingsUI.Rect("Inspect", m_Root);
            m_InspectBar.anchorMin = m_InspectBar.anchorMax = new Vector2(0.5f, 0f);
            m_InspectBar.pivot = new Vector2(0.5f, 0f);
            m_InspectBar.sizeDelta = new Vector2(760f, 56f);
            m_InspectBar.anchoredPosition = new Vector2(0f, 104f);
            m_InspectGroup = m_InspectBar.gameObject.AddComponent<CanvasGroup>();
            string[] views = { "3/4 FRONT", "FRONT", "SIDE", "REAR", "3/4 REAR", "TOP", "AUTO SPIN" };
            float chipW = 100f, gap = 8f;
            float total = views.Length * chipW + (views.Length - 1) * gap;
            for (int i = 0; i < views.Length; i++)
            {
                int index = i;
                var chip = SettingsUI.Rect("View_" + i, m_InspectBar);
                SettingsUI.At(chip, (760f - total) * 0.5f + i * (chipW + gap), 0f, chipW, 28f);
                var hit = SettingsUI.Fill(chip, "Hit", new Color(0f, 0f, 0f, 0f), true);
                SettingsUI.Stretch(hit.rectTransform);
                var frame = SettingsUI.Shape(chip, "Frame", SettingsTheme.A(SettingsTheme.Metallic, 0.3f), 5f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, 0f, true, 1f);
                SettingsUI.Stretch(frame.rectTransform);
                var t = GarageTheme.Text(chip, "Label", views[i], SettingsTheme.Label, 9.5f, SettingsTheme.Text2, 14f, TextAlignmentOptions.Center);
                SettingsUI.Stretch(t.rectTransform);
                m_ViewChips.Add(t);
                var click = chip.gameObject.AddComponent<GarageClick>();
                click.onClick = () => SetView(index);
            }
            var hint = GarageTheme.Text(m_InspectBar, "Hint", "DRAG TO ROTATE  ·  SCROLL TO ZOOM", SettingsTheme.Label, 9f, SettingsTheme.Muted, 22f, TextAlignmentOptions.Center);
            SettingsUI.At(hint.rectTransform, 0f, 36f, 760f, 16f);
        }

        void BuildFooter()
        {
            m_Footer = SettingsUI.Rect("Footer", m_Root);
            SettingsUI.Stretch(m_Footer);
            m_FooterGroup = m_Footer.gameObject.AddComponent<CanvasGroup>();
            m_FooterGroup.blocksRaycasts = false;
            float x = SideMargin;
            x = Prompt(x, new[] { "ESC" }, "BACK");
            x = Prompt(x, new[] { "ENTER" }, "SELECT");
            x = Prompt(x, new[] { "Q", "E" }, "SYSTEM");
            x = Prompt(x, new[] { "A", "D" }, "ROTATE");
            x = Prompt(x, new[] { "V" }, "VIEW");
            Prompt(x, new[] { "1–8" }, "JUMP");

            m_SaveDot = SettingsUI.Diamond(m_Footer, "SaveDot", SettingsTheme.Positive, 6f);
            m_SaveDot.rectTransform.anchorMin = m_SaveDot.rectTransform.anchorMax = new Vector2(1f, 0f);
            m_SaveDot.rectTransform.anchoredPosition = new Vector2(-SideMargin - 258f, 53f);
            m_SaveText = GarageTheme.Text(m_Footer, "Save", "ALL CHANGES SAVED", SettingsTheme.Label, 10f, SettingsTheme.Text2, 22f, TextAlignmentOptions.MidlineRight);
            m_SaveText.rectTransform.anchorMin = m_SaveText.rectTransform.anchorMax = new Vector2(1f, 0f);
            m_SaveText.rectTransform.pivot = new Vector2(1f, 0.5f);
            m_SaveText.rectTransform.anchoredPosition = new Vector2(-SideMargin, 53f);
            m_SaveText.rectTransform.sizeDelta = new Vector2(250f, 16f);
        }

        float Prompt(float x, string[] keys, string label)
        {
            foreach (var k in keys)
            {
                var cap = SettingsUI.Keycap(m_Footer, k);
                cap.anchorMin = cap.anchorMax = new Vector2(0f, 0f);
                cap.pivot = new Vector2(0f, 0.5f);
                cap.anchoredPosition = new Vector2(x, 53f);
                x += cap.sizeDelta.x + 4f;
            }
            var t = GarageTheme.Text(m_Footer, "Prompt_" + label, label, SettingsTheme.Label, 10f, SettingsTheme.Text2, 22f);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 0f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            float w = Mathf.Ceil(t.GetPreferredValues(label, 400f, 16f).x);
            t.rectTransform.sizeDelta = new Vector2(w + 4f, 16f);
            t.rectTransform.anchoredPosition = new Vector2(x + 4f, 53f);
            return x + w + 34f;
        }

        // =====================================================================================================
        // PANELS
        // =====================================================================================================
        void OnItemActivated(GarageNavItem item)
        {
            if (Time.unscaledTime < m_InputReady || m_State == State.Exiting) return;
            if (m_State == State.Panel && m_PanelIndex == item.index) { ClosePanel(); return; }
            OpenPanel(item.index);
        }

        void OnItemFocused(GarageNavItem item)
        {
            m_LastItem = item.index;
            if (m_State == State.Hub) m_CalloutT = 1f;
        }

        public void OpenPanel(int index)
        {
            if (index < 0 || index >= 8) return;
            if (m_State == State.Entering) { m_PendingPage = index; return; }
            if (m_State != State.Hub && m_State != State.Panel) return;
            if (m_PanelIndex == index && m_Shell != null) return;
            bool right = index >= 4;

            if (m_Shell != null)
            {
                m_Panels[m_PanelIndex].OnClose();
                if (m_OldShell != null) Destroy(m_OldShell.gameObject);
                m_OldShell = m_Shell;
                m_OldShell.Hide();
            }
            m_PanelIndex = index;
            m_LastItem = index;
            m_State = State.Panel;

            for (int i = 0; i < 8; i++)
            {
                m_Items[i].SetRail(true);
                m_Items[i].SetActiveState(i == index);
            }
            RefreshStatuses();
            m_Shell = GaragePanelShell.Create(m_PanelHost, m_Panels[index], index, right, PanelWidth, PanelTop, PanelBottomMargin, PanelEdge, PanelPad, this);
            m_Panels[index].OnOpen();
            m_Panels[index].Refresh();
            m_Stage.SetShot(m_Panels[index].Shot);
            m_Stage.SetFraming(right ? -PanelFraming : PanelFraming);
            m_View = -1;
            m_CalloutT = 1f;
            SetBreadcrumb();
            GarageTheme.PlaySound(UiSound.Category);
            m_Shell.FocusFirst();
        }

        public void ClosePanel()
        {
            if (m_State != State.Panel || m_Shell == null) return;
            int index = m_PanelIndex;
            m_Panels[index].OnClose();
            if (m_OldShell != null) Destroy(m_OldShell.gameObject);
            m_OldShell = m_Shell;
            m_OldShell.Hide();
            m_Shell = null;
            m_PanelIndex = -1;
            m_State = State.Hub;
            for (int i = 0; i < 8; i++) { m_Items[i].SetRail(false); m_Items[i].SetActiveState(false); }
            m_Stage.SetShot(GarageStage.HeroShot());
            m_Stage.SetFraming(0f);
            if (!AccertoneMotion.CameraMotion) { var s = GarageStage.HeroShot(); s.spin = false; m_Stage.SetShot(s); }
            m_View = -1;
            SetBreadcrumb();
            GarageTheme.PlaySound(UiSound.Back);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(m_Items[index].gameObject);
            RefreshStatuses();
        }

        /// <summary>Rebuilds the open panel (lists that changed: saved setups, liveries).</summary>
        public void RebuildPanel()
        {
            if (m_Shell == null || m_PanelIndex < 0) return;
            m_Shell.Rebuild();
        }

        void StepPanel(int direction)
        {
            int next = ((m_PanelIndex < 0 ? m_LastItem : m_PanelIndex) + direction + 8) % 8;
            if (m_State == State.Panel) OpenPanel(next);
            else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(m_Items[next].gameObject);
        }

        /// <summary>A control took focus: point the target at its part and, after a short pause, move the camera there.</summary>
        public void OnControlFocused(GarageControl control)
        {
            m_PendingShotControl = control;
            m_ShotAt = Time.unscaledTime + 0.22f;
            m_CalloutT = 1f;
        }

        void OnDataChanged()
        {
            RefreshStatuses();
            if (m_Shell != null && m_PanelIndex >= 0) m_Panels[m_PanelIndex].Refresh();
        }

        void RefreshStatuses()
        {
            if (GarageSession.Current == null) return;
            for (int i = 0; i < 8; i++)
            {
                try { m_Items[i].SetStatus(m_Panels[i].Status()); }
                catch (Exception) { m_Items[i].SetStatus(""); }
            }
        }

        void SetBreadcrumb()
        {
            if (m_Breadcrumb == null) return;
            m_Breadcrumb.text = m_PanelIndex >= 0
                ? "GARAGE  //  " + (m_PanelIndex + 1).ToString("00") + "  " + m_Panels[m_PanelIndex].Title.ToUpperInvariant()
                : "GARAGE  //  SELECT A SYSTEM  ·  " + "AC-26 ON THE DISPLAY TURNTABLE";
        }

        // =====================================================================================================
        // VIEWS
        // =====================================================================================================
        void SetView(int index)
        {
            if (m_State != State.Hub && m_State != State.Panel) return;
            m_View = index;
            GarageShot shot;
            if (index >= 6)
            {
                shot = GarageStage.HeroShot();
                if (!AccertoneMotion.CameraMotion) shot.spin = false;
                m_View = -1;
            }
            else shot = GarageStage.ViewShot((GarageView)index);
            m_Stage.SetShot(shot);
            GarageTheme.PlaySound(UiSound.Change);
        }

        // =====================================================================================================
        // FRAME
        // =====================================================================================================
        void Update()
        {
            if (m_State == State.Closed) return;
            var session = GarageSession.Current;
            if (session != null) session.Tick();
            float now = Time.unscaledTime;
            float t = now - m_OpenTime;

            if (m_State == State.Entering)
            {
                if (t > AccertoneMotion.Duration(0.5f)) m_HeaderT = 1f;
                if (t > AccertoneMotion.Duration(0.95f)) { m_FrameT = 1f; m_InspectT = 1f; m_FooterT = 1f; }
                if (t > AccertoneMotion.Duration(1.0f))
                {
                    m_State = State.Hub;
                    SetBreadcrumb();
                    if (m_PendingPage >= 0) { int p = m_PendingPage; m_PendingPage = -1; OpenPanel(p); }
                }
            }

            // HUD and shades.
            m_HudFade = Mathf.MoveTowards(m_HudFade, m_HudFadeTarget, Time.unscaledDeltaTime / Mathf.Max(0.01f, AccertoneMotion.Duration(0.28f)));
            if (AccertoneMotion.Scale <= 0.01f) m_HudFade = m_HudFadeTarget;
            for (int i = 0; i < m_HudGroups.Count; i++)
            {
                var g = m_HudGroups[i];
                if (g == null) continue;
                g.alpha = m_HudFade;
                if (m_HudFade <= 0.001f && g.gameObject.activeSelf) { g.gameObject.SetActive(false); g.alpha = 1f; }
            }
            if (m_HudFade <= 0.001f) m_HudGroups.Clear();
            if (m_Shades != null)
            {
                float w = m_Stage != null ? m_Stage.Weight : 0f;
                m_Shades.alpha = Mathf.Lerp(1f, 0.52f, w);
            }

            float k = GarageTheme.Approach(9f);
            m_HeaderShow = Mathf.Lerp(m_HeaderShow, m_HeaderT, k);
            m_FooterShow = Mathf.Lerp(m_FooterShow, m_FooterT, k);
            m_FrameShow = Mathf.Lerp(m_FrameShow, m_FrameT, GarageTheme.Approach(6f));
            m_InspectShow = Mathf.Lerp(m_InspectShow, m_State == State.Panel ? 0f : m_InspectT, k);
            if (AccertoneMotion.Scale <= 0.01f) { m_HeaderShow = m_HeaderT; m_FooterShow = m_FooterT; m_FrameShow = m_FrameT; m_InspectShow = m_State == State.Panel ? 0f : m_InspectT; }
            m_HeaderGroup.alpha = m_HeaderShow;
            m_Header.anchoredPosition = new Vector2(-14f * (1f - m_HeaderShow), 0f);
            m_FooterGroup.alpha = m_FooterShow;
            m_InspectGroup.alpha = m_InspectShow;
            m_InspectGroup.blocksRaycasts = m_InspectShow > 0.5f;
            m_InspectBar.anchoredPosition = new Vector2(0f, 104f - 12f * (1f - m_InspectShow));

            UpdateSaveStatus(session);
            HandleInput(now);

            if (m_PendingShotControl != null && now >= m_ShotAt && m_State == State.Panel)
            {
                var c = m_PendingShotControl;
                m_PendingShotControl = null;
                if (c != null && m_PanelIndex >= 0) m_Stage.SetShot(c.hasShot ? c.shot : m_Panels[m_PanelIndex].Shot);
            }
        }

        void LateUpdate()
        {
            if (m_State == State.Closed || m_Stage == null) return;
            UpdateFrame();
            UpdateCallout();
        }

        void UpdateFrame()
        {
            // Viewfinder corners draw out from the centre, and slide with the car's framing.
            float framing = m_State == State.Panel ? (m_PanelIndex >= 4 ? -PanelFraming : PanelFraming) : 0f;
            m_FrameX = Mathf.Lerp(m_FrameX, framing, GarageTheme.Approach(4.5f));
            float half = m_Root.rect.width * 0.5f;
            m_Frame.anchoredPosition = new Vector2(m_FrameX * half, -20f);
            float size = m_State == State.Panel ? 0.82f : 1f;
            m_Frame.localScale = Vector3.Lerp(m_Frame.localScale, Vector3.one * size, GarageTheme.Approach(5f));
            m_FrameGroup.alpha = m_FrameShow * (m_State == State.Panel ? 0.55f : 1f);
            float len = Mathf.Lerp(0f, 26f, m_FrameShow);
            for (int i = 0; i < 4; i++)
            {
                var h = m_Corners[i * 2];
                var v = m_Corners[i * 2 + 1];
                h.sizeDelta = new Vector2(len, 1f);
                v.sizeDelta = new Vector2(1f, len);
                h.anchoredPosition = v.anchoredPosition = Vector2.zero;
            }
            if (m_ViewText != null)
            {
                // Rebuilt only when the shown angle or mode changes (no per-frame string garbage).
                int mode = m_Stage.Dragging ? 0 : (Mathf.Abs(m_Stage.SpinSpeed) > 0.5f && m_Stage.Shot.spin ? 1 : 2);
                int view = Mathf.RoundToInt(m_Stage.CurrentView) % 360;
                if (view != m_ShownView || mode != m_ShownMode)
                {
                    m_ShownView = view;
                    m_ShownMode = mode;
                    m_ViewText.text = "VIEW " + view.ToString("000") + "°  ·  " + (mode == 0 ? "MANUAL" : mode == 1 ? "AUTO SPIN" : "HOLD");
                }
            }
            for (int i = 0; i < m_ViewChips.Count; i++)
            {
                bool on = (i == m_View) || (i == 6 && m_View < 0 && m_Stage.Shot.spin);
                m_ViewChips[i].color = on ? SettingsTheme.White : SettingsTheme.Text2;
            }
        }

        void UpdateCallout()
        {
            // Hub: leader from the focused item to its part. Panel: leader from the focused row to the part it changes.
            GaragePart part = GaragePart.Centre;
            Vector2 start = Vector2.zero;
            bool has = false;
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (m_State == State.Hub && selected != null)
            {
                var item = selected.GetComponent<GarageNavItem>();
                if (item != null)
                {
                    part = m_Panels[item.index].Part;
                    start = WorldToRoot(item.LineEndWorld());
                    has = true;
                }
            }
            else if (m_State == State.Panel && m_Shell != null)
            {
                var control = selected != null ? selected.GetComponent<GarageControl>() : null;
                part = control != null ? control.part : m_Panels[m_PanelIndex].Part;
                start = m_Shell.EdgePoint(control, m_Root);
                has = true;
            }
            m_CalloutT = has ? 1f : 0f;
            m_CalloutShow = Mathf.MoveTowards(m_CalloutShow, m_CalloutT, Time.unscaledDeltaTime * (m_CalloutT > 0.5f ? 2.6f : 5f));
            if (m_Stage.Busy && m_State != State.Panel) m_CalloutShow = Mathf.Min(m_CalloutShow, 0f);

            Vector2 screen;
            bool visible = has && m_Stage.PartScreenPoint(part, out screen) && m_CalloutShow > 0.001f;
            if (!visible)
            {
                m_Callout.progress = 0f;
                m_Ring.canvasRenderer.SetAlpha(0f);
                m_RingDot.canvasRenderer.SetAlpha(0f);
                m_RingLabel.canvasRenderer.SetAlpha(0f);
                return;
            }
            m_Stage.PartScreenPoint(part, out screen);
            Vector2 target;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Root, screen, null, out target);
            target += m_Root.rect.size * m_Root.pivot; // bottom-left origin
            Vector2 s = start + m_Root.rect.size * m_Root.pivot;
            float dir = target.x >= s.x ? 1f : -1f;
            Vector2 elbow = new Vector2(s.x + dir * 34f, s.y);
            Vector2 end = target - (target - elbow).normalized * 15f;
            m_Callout.SetPoints(ToLine(s), ToLine(elbow), ToLine(end));
            m_Callout.progress = Smooth(m_CalloutShow);

            float ring = Mathf.Clamp01((m_CalloutShow - 0.55f) / 0.45f);
            m_RingRt.anchoredPosition = target;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.4f);
            m_RingRt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Smooth(ring)) * pulse;
            m_RingRt.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 40f);
            m_Ring.canvasRenderer.SetAlpha(ring);
            m_RingDot.canvasRenderer.SetAlpha(ring);
            m_RingLabel.canvasRenderer.SetAlpha(ring);
            string label = s_PartNames[Mathf.Clamp((int)part, 0, s_PartNames.Length - 1)];
            if (m_RingLabel.text != label) m_RingLabel.text = label;
            m_RingLabelRt.anchoredPosition = target + new Vector2(22f, 18f);
        }

        Vector2 ToLine(Vector2 bottomLeft)
        {
            // GarageLine draws in its own rect space (centre pivot, stretched over the root).
            return bottomLeft - m_Root.rect.size * 0.5f;
        }

        Vector2 WorldToRoot(Vector3 world)
        {
            return m_Root.InverseTransformPoint(world);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        void UpdateSaveStatus(GarageSession session)
        {
            if (m_SaveText == null || session == null) return;
            string text;
            Color color;
            if (session.SavePending) { text = "SAVING…"; color = SettingsTheme.Warning; }
            else if (session.LastSaveFailed) { text = "SAVE FAILED"; color = SettingsTheme.Danger; }
            else { text = "ALL CHANGES SAVED"; color = SettingsTheme.Positive; }
            if (m_SaveText.text != text) m_SaveText.text = text;
            m_SaveDot.color = color;
        }

        // =====================================================================================================
        // INPUT
        // =====================================================================================================
        void HandleInput(float now)
        {
            if (m_State == State.Exiting || m_State == State.Closed) return;
            float rotate = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) rotate -= 1f;
                if (kb.dKey.isPressed) rotate += 1f;
            }
            if (pad != null)
            {
                float stick = pad.rightStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.2f) rotate += stick;
                if (pad.buttonEast.wasPressedThisFrame && now >= m_InputReady) { RequestBack(); return; }
            }
            m_Stage.Rotate(rotate);
            if (now < m_InputReady) return;

            if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) StepPanel(-1);
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) StepPanel(1);
            if ((kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))
                SetView(m_View < 0 ? 0 : (m_View + 1) % 7);
            if (kb != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame || kb[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame)
                    {
                        OpenPanel(i);
                        return;
                    }
                }
            }

            // Nothing focused yet: the first directional / confirm press picks up focus, as the Play screen does.
            var es = EventSystem.current;
            if (es != null && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy))
            {
                bool nav = false;
                if (kb != null) nav = kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame ||
                                      kb.rightArrowKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame;
                if (!nav && pad != null) nav = pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame ||
                                               pad.dpad.right.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame ||
                                               pad.leftStick.up.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame;
                if (nav)
                {
                    if (m_State == State.Panel && m_Shell != null) m_Shell.FocusFirst();
                    else es.SetSelectedGameObject(m_Items[Mathf.Clamp(m_LastItem, 0, 7)].gameObject);
                }
            }
#endif
        }
    }

    /// <summary>Full-screen catcher behind the menus: drag turns the car, the wheel zooms.</summary>
    public class GarageDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IInitializePotentialDragHandler
    {
        public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = true; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || GarageStage.Instance == null) return;
            GarageStage.Instance.BeginDrag();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || GarageStage.Instance == null) return;
            // Pixels in reference (1080p) units so the feel is the same at any resolution.
            float scale = Screen.height > 0 ? 1080f / Screen.height : 1f;
            GarageStage.Instance.Drag(eventData.delta.x * scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (GarageStage.Instance != null) GarageStage.Instance.EndDrag();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (GarageStage.Instance == null) return;
            float d = eventData.scrollDelta.y;
            if (Mathf.Abs(d) > 0.01f) GarageStage.Instance.Zoom(Mathf.Sign(d));
        }
    }

    /// <summary>Temporarily shortens a lobby panel's reveal delay for the return from the Garage, then puts it back.</summary>
    public class GarageRevealOverride : MonoBehaviour
    {
        UIRevealGroup m_Reveal;
        float m_Original, m_Until;

        public static void Apply(UIRevealGroup reveal, float delay)
        {
            var o = reveal.GetComponent<GarageRevealOverride>();
            if (o == null)
            {
                o = reveal.gameObject.AddComponent<GarageRevealOverride>();
                o.m_Original = reveal.delay;
            }
            o.m_Reveal = reveal;
            reveal.delay = delay;
            o.m_Until = Time.unscaledTime + 2.5f;
        }

        void Update()
        {
            if (Time.unscaledTime < m_Until) return;
            if (m_Reveal != null) m_Reveal.delay = m_Original;
            Destroy(this);
        }
    }
}
