using Accertone.UI;
using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>
    /// Restrained entrance for the wordmark: the accent slashes draw in, the name is
    /// wiped left-to-right through a soft mask while sliding a few pixels into place,
    /// then the tagline settles its tracking. Nothing flashy.
    /// </summary>
    [RequireComponent(typeof(AccertoneWordmark))]
    public class AccertoneWordmarkReveal : MonoBehaviour
    {
        public bool playOnEnable = true;
        public float delay = 0.35f;
        public float slashDuration = 0.45f;
        public float slashStagger = 0.09f;
        public float typeDelay = 0.18f;
        public float typeDuration = 1.15f;
        public float typeSlide = 20f;
        public int wipeSoftness = 64;
        public float taglineDelay = 0.85f;
        public float taglineDuration = 0.9f;
        public float taglineTrackingFrom = 18f;

        AccertoneWordmark m_Wordmark;
        CanvasGroup m_TypeGroup;
        CanvasGroup m_TaglineGroup;
        RectTransform m_TypeRect;
        Vector2 m_TypeBase;
        float m_TaglineSpacing;
        float m_Start;
        bool m_Playing;
        bool m_Cached;

        public bool IsPlaying { get { return m_Playing; } }
        public float TotalDuration { get { return delay + Mathf.Max(typeDelay + typeDuration, taglineDelay + taglineDuration); } }

        void OnEnable()
        {
            m_Wordmark = GetComponent<AccertoneWordmark>();
            if (playOnEnable && Application.isPlaying) Play();
        }

        void Cache()
        {
            if (m_Cached || m_Wordmark == null) return;
            if (m_Wordmark.typeMask != null) m_TypeGroup = m_Wordmark.typeMask.GetComponent<CanvasGroup>();
            if (m_Wordmark.taglineRoot != null) m_TaglineGroup = m_Wordmark.taglineRoot.GetComponent<CanvasGroup>();
            if (m_Wordmark.typeText != null)
            {
                m_TypeRect = m_Wordmark.typeText.rectTransform;
                m_TypeBase = m_TypeRect.anchoredPosition;
            }
            if (m_Wordmark.taglineText != null) m_TaglineSpacing = m_Wordmark.taglineText.characterSpacing;
            m_Cached = true;
        }

        public void Play()
        {
            if (m_Wordmark == null) m_Wordmark = GetComponent<AccertoneWordmark>();
            Cache();
            m_Start = Time.unscaledTime;
            m_Playing = true;
            Evaluate(0f);
        }

        public void Complete()
        {
            Cache();
            m_Playing = false;
            Evaluate(1000f);
            if (m_Wordmark.typeMask != null)
            {
                m_Wordmark.typeMask.softness = Vector2Int.zero;
                m_Wordmark.typeMask.padding = Vector4.zero;
            }
        }

        void Update()
        {
            if (!m_Playing) return;
            float t = Time.unscaledTime - m_Start - delay;
            Evaluate(t);
            if (t > TotalDuration) Complete();
        }

        void Evaluate(float t)
        {
            var wm = m_Wordmark;
            if (wm == null) return;

            // 1. Accent slashes draw in from the left, staggered.
            for (int i = 0; i < wm.markSlashes.Length; i++)
            {
                var g = wm.markSlashes[i];
                if (g == null) continue;
                float e = AccertoneEase.OutCubic((t - i * slashStagger) / slashDuration);
                g.rectTransform.localScale = new Vector3(Mathf.Max(0.0001f, e), 1f, 1f);
            }

            // 2. Name wipes in through a soft-edged mask while sliding into place.
            float te = AccertoneEase.OutQuint((t - typeDelay) / typeDuration);
            if (wm.typeMask != null)
            {
                float width = wm.typeMask.rectTransform.rect.width + wipeSoftness;
                wm.typeMask.softness = new Vector2Int(te < 1f ? wipeSoftness : 0, 0);
                wm.typeMask.padding = new Vector4(0f, 0f, (1f - te) * width, 0f);
            }
            if (m_TypeGroup != null) m_TypeGroup.alpha = Mathf.Clamp01(te * 1.6f);
            if (m_TypeRect != null) m_TypeRect.anchoredPosition = m_TypeBase + new Vector2(-typeSlide * (1f - te), 0f);

            // 3. Tagline fades up and its tracking settles.
            float ge = AccertoneEase.OutCubic((t - taglineDelay) / taglineDuration);
            if (m_TaglineGroup != null) m_TaglineGroup.alpha = ge;
            if (wm.taglineRule != null) wm.taglineRule.rectTransform.localScale = new Vector3(Mathf.Max(0.0001f, ge), 1f, 1f);
            if (wm.taglineText != null) wm.taglineText.characterSpacing = Mathf.Lerp(m_TaglineSpacing + taglineTrackingFrom, m_TaglineSpacing, ge);
        }
    }
}
