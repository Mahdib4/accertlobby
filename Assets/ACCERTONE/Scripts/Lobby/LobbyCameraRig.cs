using Accertone.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.Lobby
{
    /// <summary>
    /// Cinematic lobby camera: a slow push-in on load, a barely perceptible drift and mouse parallax.
    /// Optionally follows a walking character with a heavily damped, partial follow so the showroom
    /// composition (and the Accert one wall branding) is preserved. No snapping, no third-person chase.
    /// </summary>
    public class LobbyCameraRig : MonoBehaviour
    {
        public Transform focus;
        [Header("Intro")]
        public bool playIntro = true;
        public Vector3 introOffset = new Vector3(0.9f, 0.22f, -2.6f);
        public float introDuration = 3.4f;
        [Header("Idle")]
        public float driftAmplitude = 0.05f;
        public float driftSpeed = 0.11f;
        public float parallax = 0.28f;
        public float parallaxSharpness = 2.5f;
        [Header("Character follow (optional)")]
        [Tooltip("A character walking around the showroom. The camera leans toward it without leaving its cinematic framing.")]
        public Transform followTarget;
        [Range(0f, 1f)] public float followFocusWeight = 0.4f;
        [Range(0f, 1f)] public float followPositionWeight = 0.18f;
        public float followHeight = 1.15f;
        public float followSharpness = 1.1f;
        [Tooltip("Maximum lateral camera shift caused by following (metres).")]
        public float maxFollowShift = 1.6f;

        Vector3 m_Rest;
        Vector3 m_RestFocus;
        float m_Start;
        Vector2 m_Parallax;
        Vector3 m_FollowFocus;
        Vector3 m_FollowShift;
        bool m_FollowInit;
        float m_Motion = 1f;

        void Start()
        {
            m_Motion = AccertoneMotion.CameraMotion ? 1f : 0f;
            m_Rest = transform.position;
            if (focus != null) m_RestFocus = focus.position;
            m_Start = Time.unscaledTime;
            if (!playIntro) m_Start -= introDuration;
        }

        void LateUpdate()
        {
            float intro = AccertoneEase.InOutCubic((Time.unscaledTime - m_Start) / Mathf.Max(0.01f, introDuration));
            Vector3 pos = Vector3.Lerp(m_Rest + introOffset, m_Rest, intro);

            // Settings > Camera > Showroom camera motion fades the idle drift and mouse follow in and out.
            m_Motion = Mathf.MoveTowards(m_Motion, AccertoneMotion.CameraMotion ? 1f : 0f, Time.unscaledDeltaTime * 0.8f);

            float t = Time.unscaledTime * driftSpeed;
            pos += new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * driftAmplitude * m_Motion);

            Vector2 target = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0 && AccertoneMotion.CameraMotion)
            {
                Vector2 m = mouse.position.ReadValue();
                target = new Vector2(m.x / Screen.width - 0.5f, m.y / Screen.height - 0.5f) * 2f;
                target = Vector2.ClampMagnitude(target, 1.2f);
            }
#endif
            m_Parallax = Vector2.Lerp(m_Parallax, target, 1f - Mathf.Exp(-parallaxSharpness * Time.unscaledDeltaTime));
            pos += transform.right * (m_Parallax.x * parallax) + Vector3.up * (m_Parallax.y * parallax * 0.4f);

            Vector3 lookAt = focus != null ? m_RestFocus : pos + transform.forward;
            if (followTarget != null && focus != null)
            {
                Vector3 subject = followTarget.position + Vector3.up * followHeight;
                Vector3 desiredFocus = Vector3.Lerp(m_RestFocus, subject, followFocusWeight);
                Vector3 offset = Vector3.ProjectOnPlane(subject - m_RestFocus, Vector3.up) * followPositionWeight;
                offset = Vector3.ClampMagnitude(Vector3.Project(offset, transform.right), maxFollowShift);
                if (!m_FollowInit)
                {
                    m_FollowFocus = desiredFocus;
                    m_FollowShift = offset;
                    m_FollowInit = true;
                }
                float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
                m_FollowFocus = Vector3.Lerp(m_FollowFocus, desiredFocus, k);
                m_FollowShift = Vector3.Lerp(m_FollowShift, offset, k);
                lookAt = m_FollowFocus;
                pos += m_FollowShift;
            }

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(lookAt - pos, Vector3.up);
        }
    }
}
