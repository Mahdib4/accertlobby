using UnityEngine;

namespace Accertone.UI
{
    /// <summary>Places a marker on a circuit map; optionally laps it as a quiet "live telemetry" detail.</summary>
    [ExecuteAlways]
    public class TrackMapMarker : MonoBehaviour
    {
        public UIPolyline path;
        public bool animate = true;
        [Tooltip("Seconds per lap.")]
        public float lapTime = 14f;
        [Range(0f, 1f)] public float startOffset;
        public bool alignToPath;

        RectTransform m_Rect;

        void OnEnable()
        {
            m_Rect = transform as RectTransform;
        }

        void LateUpdate()
        {
            if (path == null || m_Rect == null) return;
            bool playing = Application.isPlaying;
            float t = startOffset + (animate && playing ? Time.unscaledTime / Mathf.Max(1f, lapTime) : 0f);
            Vector2 tangent;
            Vector2 local = path.Evaluate(t, out tangent);
            Vector3 world = path.rectTransform.TransformPoint(local);
            Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);

            // In edit mode only write when something actually moved, so the scene is not dirtied every frame.
            if (!playing && (m_Rect.position - world).sqrMagnitude < 1e-6f && (!alignToPath || Quaternion.Angle(m_Rect.localRotation, rot) < 0.01f)) return;
            m_Rect.position = world;
            if (alignToPath) m_Rect.localRotation = rot;
        }
    }
}
