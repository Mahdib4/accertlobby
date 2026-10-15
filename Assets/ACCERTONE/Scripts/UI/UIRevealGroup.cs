using UnityEngine;

namespace Accertone.UI
{
    /// <summary>Fades and slides a UI block into place on load. Used to stagger lobby panels.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIRevealGroup : MonoBehaviour
    {
        public bool playOnEnable = true;
        public float delay;
        public float duration = 0.7f;
        public Vector2 offset = new Vector2(0f, -14f);

        CanvasGroup m_Group;
        RectTransform m_Rect;
        Vector2 m_BasePosition;
        bool m_HasBase;
        float m_StartTime;
        bool m_Playing;

        void Awake()
        {
            m_Group = GetComponent<CanvasGroup>();
            m_Rect = transform as RectTransform;
        }

        void OnEnable()
        {
            if (playOnEnable && Application.isPlaying) Play();
        }

        public void Play()
        {
            if (m_Rect == null) return;
            if (!m_HasBase)
            {
                m_BasePosition = m_Rect.anchoredPosition;
                m_HasBase = true;
            }
            if (AccertoneMotion.Scale <= 0.01f) { Complete(); return; }
            m_StartTime = Time.unscaledTime;
            m_Playing = true;
            Apply(0f);
        }

        public void Complete()
        {
            m_Playing = false;
            Apply(1f);
        }

        void Update()
        {
            if (!m_Playing) return;
            float k = Mathf.Max(0.01f, AccertoneMotion.Scale);
            float t = (Time.unscaledTime - m_StartTime - delay * k) / Mathf.Max(0.01f, duration * k);
            if (t < 0f) return;
            Apply(AccertoneEase.OutCubic(t));
            if (t >= 1f) m_Playing = false;
        }

        void Apply(float e)
        {
            if (m_Group != null) m_Group.alpha = e;
            if (m_HasBase) m_Rect.anchoredPosition = m_BasePosition + offset * (1f - e);
        }
    }
}
