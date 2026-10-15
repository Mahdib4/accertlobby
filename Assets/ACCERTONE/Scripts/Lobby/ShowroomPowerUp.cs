using Accertone.UI;
using UnityEngine;

namespace Accertone.Lobby
{
    /// <summary>Showroom lights come up in sequence when the lobby loads: overhead LEDs, key light, rims, then the brand wall.</summary>
    public class ShowroomPowerUp : MonoBehaviour
    {
        public bool playOnStart = true;
        public float startDelay = 0.15f;
        public float groupInterval = 0.42f;
        public float rampDuration = 0.9f;

        ShowroomPoweredElement[] m_Elements;
        float m_Start;
        bool m_Running;

        void Start()
        {
            m_Elements = GetComponentsInChildren<ShowroomPoweredElement>(true);
            if (playOnStart) Play();
            else Apply(float.MaxValue);
        }

        public void Play()
        {
            m_Start = Time.unscaledTime;
            m_Running = true;
            Apply(0f);
        }

        void Update()
        {
            if (m_Elements == null) return;
            Apply(m_Running ? Time.unscaledTime - m_Start - startDelay : float.MaxValue);
        }

        void Apply(float time)
        {
            for (int i = 0; i < m_Elements.Length; i++)
            {
                var e = m_Elements[i];
                if (e == null) continue;
                float t = time - e.group * groupInterval;
                e.SetLevel(Level(t, e.flicker));
            }
        }

        float Level(float t, bool flicker)
        {
            if (t <= 0f) return 0f;
            if (flicker)
            {
                if (t < 0.05f) return 0.85f;
                if (t < 0.11f) return 0.04f;
                if (t < 0.17f) return 0.6f;
                if (t < 0.26f) return 0.1f;
                return Mathf.Lerp(0.1f, 1f, AccertoneEase.OutCubic((t - 0.26f) / rampDuration));
            }
            return AccertoneEase.OutCubic(t / rampDuration);
        }
    }
}
