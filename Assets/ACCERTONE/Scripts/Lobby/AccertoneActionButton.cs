using Accertone.Branding;
using Accertone.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Lobby
{
    /// <summary>Primary action visuals (RACE): accent fill, breathing glow, chevrons that lean forward on hover.</summary>
    [RequireComponent(typeof(UIHoverState))]
    public class AccertoneActionButton : MonoBehaviour
    {
        public Graphic fill;
        public Graphic glow;
        public RectTransform chevrons;
        public RectTransform content;
        public float chevronTravel = 10f;
        public float glowIdle = 0.22f;
        public float glowHover = 0.5f;
        public float breathe = 0.06f;

        UIHoverState m_State;
        float m_Highlight;
        Vector2 m_ChevronBase;

        void Awake()
        {
            m_State = GetComponent<UIHoverState>();
            if (chevrons != null) m_ChevronBase = chevrons.anchoredPosition;
        }

        void Update()
        {
            var id = AccertoneBrandIdentity.Default;
            if (id == null || id.palette == null) return;

            float target = m_State.Highlighted ? 1f : 0f;
            m_Highlight = Mathf.MoveTowards(m_Highlight, target, Time.unscaledDeltaTime * 6f);
            float h = AccertoneEase.OutCubic(m_Highlight);

            Color accent = id.palette.accent;
            if (fill != null)
            {
                Color c = m_State.Pressed ? id.palette.accentDeep : Color.Lerp(accent, Color.Lerp(accent, Color.white, 0.14f), h);
                fill.color = c;
            }
            if (glow != null)
            {
                Color g = accent;
                g.a = Mathf.Lerp(glowIdle, glowHover, h) + Mathf.Sin(Time.unscaledTime * 1.6f) * breathe;
                glow.color = g;
            }
            if (chevrons != null) chevrons.anchoredPosition = m_ChevronBase + new Vector2(chevronTravel * h, 0f);
            if (content != null)
            {
                float s = m_State.Pressed ? 0.985f : 1f;
                content.localScale = Vector3.Lerp(content.localScale, new Vector3(s, s, 1f), 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
            }
        }
    }
}
