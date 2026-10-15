using UnityEngine;

namespace Accertone.UI
{
    /// <summary>Restrained, cinematic easing curves shared by every ACCERTONE animation.</summary>
    public static class AccertoneEase
    {
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float f = 1f - t;
            return 1f - f * f * f;
        }

        public static float OutQuint(float t)
        {
            t = Mathf.Clamp01(t);
            float f = 1f - t;
            return 1f - f * f * f * f * f;
        }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float OutExpo(float t)
        {
            t = Mathf.Clamp01(t);
            return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }
    }
}
