using System;

namespace Accertone.UI
{
    /// <summary>
    /// Player motion preferences shared by every animated element (set from Settings &gt; Interface / Camera).
    /// <see cref="Scale"/> multiplies UI animation durations: 1 = full, ~0.45 = reduced, 0 = instant.
    /// </summary>
    public static class AccertoneMotion
    {
        static float s_Scale = 1f;
        static bool s_CameraMotion = true;

        public static event Action Changed;

        /// <summary>UI animation duration multiplier (0 = no animation).</summary>
        public static float Scale
        {
            get { return s_Scale; }
            set { if (Math.Abs(s_Scale - value) < 1e-4f) return; s_Scale = value; if (Changed != null) Changed(); }
        }

        /// <summary>Whether the showroom camera may drift and follow the mouse.</summary>
        public static bool CameraMotion
        {
            get { return s_CameraMotion; }
            set { if (s_CameraMotion == value) return; s_CameraMotion = value; if (Changed != null) Changed(); }
        }

        /// <summary>Scales a duration; returns 0 when animations are off.</summary>
        public static float Duration(float seconds) { return seconds * s_Scale; }
    }
}
