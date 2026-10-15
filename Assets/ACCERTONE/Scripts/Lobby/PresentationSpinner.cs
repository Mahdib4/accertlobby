using UnityEngine;

namespace Accertone.Lobby
{
    /// <summary>
    /// A display spinner that presents once and stops: it turns <see cref="target"/> about its local Y axis by
    /// <see cref="travelDegrees"/> from the pose it starts in, easing up to speed, cruising, then easing down to rest
    /// exactly on the end angle. Anything parented under the target rides it.
    ///
    /// Spinners run on the scene clock (<c>Time.timeSinceLevelLoad</c>), so spinners with the same
    /// <see cref="startDelay"/> and <see cref="spinUpSeconds"/> start on the same frame with the same ease-in, then
    /// each stops on its own target. The motion is a pure function of time, so it lands on the end angle exactly
    /// whatever the frame rate.
    /// </summary>
    public class PresentationSpinner : MonoBehaviour
    {
        [Tooltip("The transform that turns (defaults to this one).")]
        public Transform target;
        [Tooltip("Total turn in degrees. Negative turns counter-clockwise seen from above.")]
        public float travelDegrees = -90f;
        [Tooltip("Seconds after the scene starts before the spinner moves.")]
        public float startDelay = 0f;
        [Tooltip("Top speed in degrees per second (lowered automatically for short turns).")]
        public float cruiseSpeed = 10f;
        [Tooltip("Seconds to ease from standstill to speed.")]
        public float spinUpSeconds = 2.5f;
        [Tooltip("Seconds to ease from speed to a complete stop on the end angle.")]
        public float spinDownSeconds = 3f;

        Quaternion m_StartRotation;
        float m_Speed, m_Cruise, m_Duration;
        bool m_Ready;

        /// <summary>Current angular speed in degrees per second (signed like <see cref="travelDegrees"/>).</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>Degrees turned so far (signed).</summary>
        public float CurrentAngle { get; private set; }
        /// <summary>True once the spinner has come to rest on its end angle.</summary>
        public bool IsDone { get; private set; }
        /// <summary>Seconds from the start of the motion to the full stop.</summary>
        public float Duration { get { Plan(); return m_Duration; } }

        void Awake()
        {
            if (target == null) target = transform;
            m_StartRotation = target.localRotation;
            Plan();
        }

        void Plan()
        {
            float distance = Mathf.Abs(travelDegrees);
            float ramps = (Mathf.Max(0f, spinUpSeconds) + Mathf.Max(0f, spinDownSeconds)) * 0.5f;
            m_Speed = Mathf.Max(1e-3f, cruiseSpeed);
            // A short turn never reaches cruise speed: lower the top speed so the two ramps cover it exactly.
            if (ramps > 0f && distance < m_Speed * ramps) m_Speed = Mathf.Max(1e-3f, distance / ramps);
            m_Cruise = Mathf.Max(0f, (distance - m_Speed * ramps) / m_Speed);
            m_Duration = Mathf.Max(0f, spinUpSeconds) + m_Cruise + Mathf.Max(0f, spinDownSeconds);
            m_Ready = true;
        }

        void Update()
        {
            if (!m_Ready || target == null) return;
            float t = Time.timeSinceLevelLoad - startDelay;
            float sign = Mathf.Sign(travelDegrees);
            float angle = Distance(t) * sign;
            CurrentSpeed = (angle - CurrentAngle) / Mathf.Max(Time.deltaTime, 1e-5f);
            CurrentAngle = angle;
            IsDone = t >= m_Duration;
            if (IsDone) CurrentSpeed = 0f;
            target.localRotation = m_StartRotation * Quaternion.Euler(0f, angle, 0f);
        }

        /// <summary>Unsigned degrees covered <paramref name="t"/> seconds into the motion. Speed follows a smoothstep
        /// ramp up and down, so acceleration is zero at the start, at cruise and at the stop (no jolt).</summary>
        float Distance(float t)
        {
            float distance = Mathf.Abs(travelDegrees);
            if (t <= 0f) return 0f;
            if (t >= m_Duration) return distance;
            float up = Mathf.Max(0f, spinUpSeconds), down = Mathf.Max(0f, spinDownSeconds);
            if (t < up) return m_Speed * up * Ramp(t / up);
            if (t < up + m_Cruise) return m_Speed * up * 0.5f + m_Speed * (t - up);
            return distance - m_Speed * down * Ramp((m_Duration - t) / down);
        }

        // Integral of smoothstep(x) = 3x^2 - 2x^3 over [0, x].
        static float Ramp(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x - 0.5f * x * x * x * x;
        }
    }
}
