using System;
using Accertone.Branding;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Accertone.UI
{
    /// <summary>
    /// Programmatic UI factory for Accert one design system.
    /// Builds high-precision motorsport UI controls matching dark graphite & violet styling.
    /// </summary>
    public static class AccertoneUIFactory
    {
        public static AccertoneBrandIdentity Brand => AccertoneBrandIdentity.Default;
        public static AccertoneBrandPalette Palette => Brand != null ? Brand.palette : null;
        public static AccertoneTypography Typography => Brand != null ? Brand.typography : null;

        public static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Color bgColor, float chamfer = 14f, ChamferCorners corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;

            var shape = go.AddComponent<AccertoneShape>();
            shape.color = bgColor;
            shape.chamfer = chamfer;
            shape.corners = corners;

            var borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(go.transform, false);
            var borderRt = (RectTransform)borderGo.transform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = Vector2.zero;

            var borderShape = borderGo.AddComponent<AccertoneShape>();
            borderShape.color = Palette != null ? Palette.hairline : new Color(0.18f, 0.2f, 0.24f, 1f);
            borderShape.chamfer = chamfer;
            borderShape.corners = corners;
            borderShape.outline = true;
            borderShape.outlineWidth = 1f;

            return rt;
        }

        public static TMP_Text CreateText(Transform parent, string name, string text, BrandTextRole textRole, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;

            if (Typography != null)
            {
                var spec = Typography.Get(textRole);
                if (spec != null)
                {
                    if (spec.font != null) tmp.font = spec.font;
                    tmp.fontSize = spec.size;
                    tmp.characterSpacing = spec.characterSpacing;
                    tmp.fontStyle = spec.fontStyle;
                }
            }
            return tmp;
        }

        public static Button CreateButton(Transform parent, string name, string labelText, Vector2 size, UnityAction onClick, bool isAccent = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;

            var shape = go.AddComponent<AccertoneShape>();
            Color normalColor = isAccent ? (Palette != null ? Palette.accent : new Color(0.75f, 0.25f, 1f)) : (Palette != null ? Palette.panel : new Color(0.12f, 0.14f, 0.18f));
            shape.color = normalColor;
            shape.chamfer = 8f;
            shape.corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft;
            shape.raycastTarget = true;

            var borderGo = new GameObject("Border", typeof(RectTransform));
            borderGo.transform.SetParent(go.transform, false);
            var borderRt = (RectTransform)borderGo.transform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = Vector2.zero;
            var border = borderGo.AddComponent<AccertoneShape>();
            border.color = Palette != null ? Palette.hairline : new Color(0.2f, 0.22f, 0.26f);
            border.chamfer = 8f;
            border.corners = ChamferCorners.TopRight | ChamferCorners.BottomLeft;
            border.outline = true;
            border.outlineWidth = 1f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = shape;
            var colors = btn.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = isAccent ? normalColor * 1.15f : normalColor + new Color(0.08f, 0.08f, 0.08f, 0f);
            colors.pressedColor = isAccent ? (Palette != null ? Palette.accentDeep : normalColor * 0.8f) : normalColor - new Color(0.04f, 0.04f, 0.04f, 0f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = CreateText(go.transform, "Label", labelText, BrandTextRole.Label, Color.white, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontSize = 12f;
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;

            return btn;
        }

        public static GameObject CreateSliderRow(Transform parent, string name, string labelText, string unit, float min, float max, float initialValue, Action<float> onValueChanged)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.sizeDelta = new Vector2(760f, 44f);

            var label = CreateText(rowGo.transform, "Label", labelText, BrandTextRole.Label, Palette != null ? Palette.textPrimary : Color.white);
            var lRt = (RectTransform)label.transform;
            lRt.anchorMin = new Vector2(0f, 0.5f);
            lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = new Vector2(16f, 0f);
            lRt.sizeDelta = new Vector2(300f, 32f);

            var valText = CreateText(rowGo.transform, "ValueText", initialValue.ToString("F1") + " " + unit, BrandTextRole.Data, Palette != null ? Palette.silver : Color.white, TextAlignmentOptions.Right);
            var vRt = (RectTransform)valText.transform;
            vRt.anchorMin = new Vector2(1f, 0.5f);
            vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.pivot = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = new Vector2(-16f, 0f);
            vRt.sizeDelta = new Vector2(120f, 32f);

            // Slider track
            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(rowGo.transform, false);
            var sRt = (RectTransform)sliderGo.transform;
            sRt.anchorMin = new Vector2(0f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.offsetMin = new Vector2(330f, -12f);
            sRt.offsetMax = new Vector2(-150f, 12f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initialValue;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.sizeDelta = Vector2.zero;
            var bgShape = bgGo.AddComponent<AccertoneShape>();
            bgShape.color = Palette != null ? Palette.hairline : new Color(0.2f, 0.22f, 0.26f);
            bgShape.chamfer = 3f;

            // Fill area
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = (RectTransform)fillArea.transform;
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.sizeDelta = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fRt = (RectTransform)fillGo.transform;
            fRt.sizeDelta = Vector2.zero;
            var fillShape = fillGo.AddComponent<AccertoneShape>();
            fillShape.color = Palette != null ? Palette.accent : new Color(0.75f, 0.25f, 1f);
            fillShape.chamfer = 3f;

            slider.fillRect = fRt;

            // Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var haRt = (RectTransform)handleArea.transform;
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.sizeDelta = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(handleArea.transform, false);
            var hRt = (RectTransform)handleGo.transform;
            hRt.sizeDelta = new Vector2(16f, 24f);
            var handleShape = handleGo.AddComponent<AccertoneShape>();
            handleShape.color = Color.white;
            handleShape.chamfer = 2f;
            slider.handleRect = hRt;
            slider.targetGraphic = handleShape;

            slider.onValueChanged.AddListener(val =>
            {
                valText.text = (Mathf.Abs(val % 1f) < 0.001f ? val.ToString("F0") : val.ToString("F1")) + " " + unit;
                onValueChanged?.Invoke(val);
            });

            return rowGo;
        }

        public static GameObject CreateStepperRow(Transform parent, string name, string labelText, string[] options, int currentIndex, Action<int> onIndexChanged)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.sizeDelta = new Vector2(760f, 44f);

            var label = CreateText(rowGo.transform, "Label", labelText, BrandTextRole.Label, Palette != null ? Palette.textPrimary : Color.white);
            var lRt = (RectTransform)label.transform;
            lRt.anchorMin = new Vector2(0f, 0.5f);
            lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = new Vector2(16f, 0f);
            lRt.sizeDelta = new Vector2(340f, 32f);

            int current = currentIndex;
            var valText = CreateText(rowGo.transform, "ValueText", options[Mathf.Clamp(current, 0, options.Length - 1)], BrandTextRole.Data, Palette != null ? Palette.silver : Color.white, TextAlignmentOptions.Center);
            var vRt = (RectTransform)valText.transform;
            vRt.anchorMin = new Vector2(1f, 0.5f);
            vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.pivot = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = new Vector2(-60f, 0f);
            vRt.sizeDelta = new Vector2(180f, 32f);

            // Left arrow button
            var btnPrev = CreateButton(rowGo.transform, "BtnPrev", "<", new Vector2(32f, 32f), () =>
            {
                current = (current - 1 + options.Length) % options.Length;
                valText.text = options[current];
                onIndexChanged?.Invoke(current);
            });
            var pRt = (RectTransform)btnPrev.transform;
            pRt.anchorMin = new Vector2(1f, 0.5f);
            pRt.anchorMax = new Vector2(1f, 0.5f);
            pRt.pivot = new Vector2(1f, 0.5f);
            pRt.anchoredPosition = new Vector2(-248f, 0f);

            // Right arrow button
            var btnNext = CreateButton(rowGo.transform, "BtnNext", ">", new Vector2(32f, 32f), () =>
            {
                current = (current + 1) % options.Length;
                valText.text = options[current];
                onIndexChanged?.Invoke(current);
            });
            var nRt = (RectTransform)btnNext.transform;
            nRt.anchorMin = new Vector2(1f, 0.5f);
            nRt.anchorMax = new Vector2(1f, 0.5f);
            nRt.pivot = new Vector2(1f, 0.5f);
            nRt.anchoredPosition = new Vector2(-16f, 0f);

            return rowGo;
        }

        public static GameObject CreateToggleRow(Transform parent, string name, string labelText, bool initialState, Action<bool> onToggleChanged)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.sizeDelta = new Vector2(760f, 44f);

            var label = CreateText(rowGo.transform, "Label", labelText, BrandTextRole.Label, Palette != null ? Palette.textPrimary : Color.white);
            var lRt = (RectTransform)label.transform;
            lRt.anchorMin = new Vector2(0f, 0.5f);
            lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = new Vector2(16f, 0f);
            lRt.sizeDelta = new Vector2(400f, 32f);

            bool state = initialState;
            var toggleBtn = CreateButton(rowGo.transform, "ToggleBtn", state ? "ON" : "OFF", new Vector2(80f, 32f), null, state);
            var bRt = (RectTransform)toggleBtn.transform;
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-16f, 0f);

            var btnShape = toggleBtn.GetComponent<AccertoneShape>();
            var btnLabel = toggleBtn.GetComponentInChildren<TMP_Text>();

            toggleBtn.onClick.AddListener(() =>
            {
                state = !state;
                btnLabel.text = state ? "ON" : "OFF";
                if (btnShape != null)
                {
                    btnShape.color = state ? (Palette != null ? Palette.accent : new Color(0.75f, 0.25f, 1f)) : (Palette != null ? Palette.panel : new Color(0.12f, 0.14f, 0.18f));
                }
                onToggleChanged?.Invoke(state);
            });

            return rowGo;
        }
    }
}
