using System;
using Accertone.Branding;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Lobby
{
    /// <summary>Top navigation with an accent indicator that glides to the active tab.</summary>
    public class LobbyNavBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public string id;
            public Button button;
            public TMP_Text label;
            public UIHoverState hover;
        }

        public Tab[] tabs = new Tab[0];
        public RectTransform indicator;
        public int selectedIndex;
        public float indicatorSharpness = 14f;
        public BrandColorRole selectedTone = BrandColorRole.TextPrimary;
        public BrandColorRole hoverTone = BrandColorRole.TextSecondary;
        public BrandColorRole idleTone = BrandColorRole.TextMuted;

        public event Action<int, string> SelectionChanged;

        bool m_Snap = true;

        void Start()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button != null)
                    tabs[i].button.onClick.AddListener(() => Select(index));
            }
            m_Snap = true;
        }

        public void Select(int index)
        {
            if (tabs.Length == 0) return;
            index = Mathf.Clamp(index, 0, tabs.Length - 1);
            if (index == selectedIndex) return;
            selectedIndex = index;
            if (SelectionChanged != null) SelectionChanged(index, tabs[index].id);
        }

        public void Step(int direction)
        {
            if (tabs.Length == 0) return;
            Select((selectedIndex + direction + tabs.Length) % tabs.Length);
        }

        void LateUpdate()
        {
            Refresh(m_Snap);
            m_Snap = false;
        }

        /// <summary>Updates label tones and the indicator. snap = jump straight to the target (used on load and by the scene builder).</summary>
        public void Refresh(bool snap)
        {
            var id = AccertoneBrandIdentity.Default;
            float k = snap ? 1f : 1f - Mathf.Exp(-indicatorSharpness * Time.unscaledDeltaTime);

            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab.label == null || id == null || id.palette == null) continue;
                BrandColorRole role = i == selectedIndex ? selectedTone : (tab.hover != null && tab.hover.Highlighted ? hoverTone : idleTone);
                tab.label.color = Color.Lerp(tab.label.color, id.palette.Get(role), snap ? 1f : 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            }

            if (indicator != null && selectedIndex < tabs.Length && tabs[selectedIndex].label != null)
            {
                var target = tabs[selectedIndex].label.rectTransform;
                var parent = indicator.parent as RectTransform;
                if (parent != null)
                {
                    Vector3[] corners = new Vector3[4];
                    target.GetWorldCorners(corners);
                    Vector2 left = parent.InverseTransformPoint(corners[0]);
                    Vector2 right = parent.InverseTransformPoint(corners[3]);
                    float centre = (left.x + right.x) * 0.5f;
                    float width = Mathf.Abs(right.x - left.x);

                    var lp = indicator.localPosition;
                    lp.x = Mathf.Lerp(lp.x, centre, k);
                    indicator.localPosition = lp;
                    var size = indicator.sizeDelta;
                    size.x = Mathf.Lerp(size.x, width, k);
                    indicator.sizeDelta = size;
                }
            }
        }
    }
}
