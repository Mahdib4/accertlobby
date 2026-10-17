using Accertone.Settings;
using Accertone.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// Lights a sub-panel button's border in the accent colour while it holds keyboard / gamepad focus.
    /// The factory buttons' own selected tint lands on the panel colour, which hides the focused control.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlayFocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        const float FocusWidth = 2f;

        AccertoneShape m_Border;
        Color m_RestColor;
        float m_RestWidth;

        /// <summary>Adds a ring to every button under <paramref name="root"/> and lights the one already focused.</summary>
        public static void Attach(GameObject root)
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<PlayFocusRing>() != null) continue;
                // Keep the fill as it is when focused; the ring carries the focus.
                var colors = button.colors;
                colors.selectedColor = colors.normalColor;
                button.colors = colors;
                var ring = button.gameObject.AddComponent<PlayFocusRing>();
                if (button.gameObject == selected) ring.SetLit(true);
            }
        }

        void Awake()
        {
            var t = transform.Find("Border");
            m_Border = t != null ? t.GetComponent<AccertoneShape>() : null;
            if (m_Border == null) return;
            m_RestColor = m_Border.color;
            m_RestWidth = m_Border.outlineWidth;
        }

        void OnDisable()
        {
            SetLit(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetLit(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetLit(false);
        }

        void SetLit(bool lit)
        {
            if (m_Border == null) return;
            m_Border.color = lit ? SettingsTheme.Accent : m_RestColor;
            m_Border.outlineWidth = lit ? FocusWidth : m_RestWidth;
        }
    }
}
