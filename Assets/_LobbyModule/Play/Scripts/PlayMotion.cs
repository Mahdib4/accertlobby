using Accertone.UI;
using UnityEngine;

namespace Accertone.Play
{
    public enum PlayEase
    {
        Linear,
        OutCubic,
        OutQuint,
        InCubic,
        InOutCubic
    }

    /// <summary>Allocation-free motion helpers shared by the Play screen (all timing is unscaled and honours <see cref="AccertoneMotion.Scale"/>).</summary>
    public static class PlayMotion
    {
        public static float Now { get { return Time.unscaledTime; } }

        /// <summary>True when the player has turned interface animations off.</summary>
        public static bool Instant { get { return AccertoneMotion.Scale <= 0.01f; } }

        public static float Evaluate(PlayEase ease, float t)
        {
            switch (ease)
            {
                case PlayEase.OutCubic: return AccertoneEase.OutCubic(t);
                case PlayEase.OutQuint: return AccertoneEase.OutQuint(t);
                case PlayEase.InOutCubic: return AccertoneEase.InOutCubic(t);
                case PlayEase.InCubic: t = Mathf.Clamp01(t); return t * t * t;
                default: return Mathf.Clamp01(t);
            }
        }
    }

    /// <summary>A timed value animation with a start delay. Evaluate() can be called every frame.</summary>
    public struct PlayTween
    {
        public float from;
        public float to;
        float m_Start;
        float m_Duration;
        PlayEase m_Ease;

        public void Start(float fromValue, float toValue, float duration, float delay = 0f, PlayEase ease = PlayEase.OutCubic)
        {
            float k = AccertoneMotion.Scale;
            from = fromValue;
            to = toValue;
            m_Ease = ease;
            m_Duration = duration * k;
            m_Start = PlayMotion.Now + delay * k;
        }

        /// <summary>Restarts from wherever the tween currently is, so interrupted motion never jumps.</summary>
        public void Retarget(float toValue, float duration, float delay = 0f, PlayEase ease = PlayEase.OutCubic)
        {
            Start(Value, toValue, duration, delay, ease);
        }

        public void Set(float value)
        {
            from = to = value;
            m_Duration = 0f;
            m_Start = 0f;
        }

        public float Progress
        {
            get
            {
                if (m_Duration <= 0f) return PlayMotion.Now >= m_Start ? 1f : 0f;
                return Mathf.Clamp01((PlayMotion.Now - m_Start) / m_Duration);
            }
        }

        public float Value { get { return Mathf.LerpUnclamped(from, to, PlayMotion.Evaluate(m_Ease, Progress)); } }
        public bool Done { get { return Progress >= 1f; } }
    }

    /// <summary>Critically damped spring: continuous velocity, so hover reversals never snap.</summary>
    public struct PlaySpring
    {
        public float value;
        public float velocity;

        public void Step(float target, float smoothTime, float dt)
        {
            float time = smoothTime * AccertoneMotion.Scale;
            if (time <= 0.0001f)
            {
                value = target;
                velocity = 0f;
                return;
            }
            value = Mathf.SmoothDamp(value, target, ref velocity, time, Mathf.Infinity, Mathf.Max(dt, 1e-5f));
        }

        public void Snap(float target)
        {
            value = target;
            velocity = 0f;
        }

        public bool Settled(float target)
        {
            return Mathf.Abs(value - target) < 0.0005f && Mathf.Abs(velocity) < 0.002f;
        }
    }
}
