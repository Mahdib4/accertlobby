using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.UI
{
    /// <summary>
    /// Anti-jagged polyline graphic for circuit maps and telemetry traces.
    /// Points are normalised (0..1) inside the RectTransform and optionally smoothed with Catmull-Rom.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIPolyline : MaskableGraphic
    {
        [SerializeField] List<Vector2> m_Points = new List<Vector2>();
        [SerializeField] float m_Thickness = 3f;
        [SerializeField] bool m_Closed = true;
        [SerializeField, Range(0, 12)] int m_Smoothing = 6;
        [SerializeField, Range(0f, 1f)] float m_DrawProgress = 1f;

        readonly List<Vector2> m_Path = new List<Vector2>();
        readonly List<float> m_Lengths = new List<float>();
        float m_TotalLength;
        Rect m_CachedRect;
        bool m_PathDirty = true;

        public List<Vector2> points { get { return m_Points; } }
        public float thickness { get { return m_Thickness; } set { m_Thickness = value; SetVerticesDirty(); } }
        public float drawProgress { get { return m_DrawProgress; } set { m_DrawProgress = Mathf.Clamp01(value); SetVerticesDirty(); } }

        public void SetPoints(IList<Vector2> pts, bool closed)
        {
            m_Points.Clear();
            m_Points.AddRange(pts);
            m_Closed = closed;
            m_PathDirty = true;
            SetVerticesDirty();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            m_PathDirty = true;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            m_PathDirty = true;
        }

        void EnsurePath()
        {
            Rect r = rectTransform.rect;
            if (!m_PathDirty && r == m_CachedRect) return;
            m_CachedRect = r;
            m_PathDirty = false;
            m_Path.Clear();
            m_Lengths.Clear();
            m_TotalLength = 0f;
            int n = m_Points.Count;
            if (n < 2) return;

            int segs = m_Closed ? n : n - 1;
            int sub = Mathf.Max(1, m_Smoothing);
            for (int i = 0; i < segs; i++)
            {
                Vector2 p0 = m_Points[Wrap(i - 1, n)];
                Vector2 p1 = m_Points[i];
                Vector2 p2 = m_Points[Wrap(i + 1, n)];
                Vector2 p3 = m_Points[Wrap(i + 2, n)];
                if (!m_Closed)
                {
                    if (i == 0) p0 = p1;
                    if (i + 2 >= n) p3 = p2;
                }
                for (int s = 0; s < sub; s++)
                {
                    float t = s / (float)sub;
                    Vector2 q = m_Smoothing > 0 ? CatmullRom(p0, p1, p2, p3, t) : Vector2.Lerp(p1, p2, t);
                    m_Path.Add(ToLocal(q, r));
                }
            }
            m_Path.Add(m_Closed ? m_Path[0] : ToLocal(m_Points[n - 1], r));

            m_Lengths.Add(0f);
            for (int i = 1; i < m_Path.Count; i++)
            {
                m_TotalLength += Vector2.Distance(m_Path[i - 1], m_Path[i]);
                m_Lengths.Add(m_TotalLength);
            }
        }

        static int Wrap(int i, int n) { return ((i % n) + n) % n; }

        static Vector2 ToLocal(Vector2 n, Rect r)
        {
            return new Vector2(Mathf.Lerp(r.xMin, r.xMax, n.x), Mathf.Lerp(r.yMin, r.yMax, n.y));
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Local-space position at normalised distance t (0..1) along the path.</summary>
        public Vector2 Evaluate(float t, out Vector2 tangent)
        {
            EnsurePath();
            tangent = Vector2.right;
            if (m_Path.Count < 2) return Vector2.zero;
            float target = Mathf.Repeat(t, 1f) * m_TotalLength;
            for (int i = 1; i < m_Path.Count; i++)
            {
                if (m_Lengths[i] >= target)
                {
                    float seg = m_Lengths[i] - m_Lengths[i - 1];
                    float k = seg > 0f ? (target - m_Lengths[i - 1]) / seg : 0f;
                    tangent = (m_Path[i] - m_Path[i - 1]).normalized;
                    return Vector2.Lerp(m_Path[i - 1], m_Path[i], k);
                }
            }
            return m_Path[m_Path.Count - 1];
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            m_PathDirty = true;
            EnsurePath();
            if (m_Path.Count < 2 || m_DrawProgress <= 0f) return;

            // Truncate the path at the draw progress.
            var pts = new List<Vector2>(m_Path.Count);
            float limit = m_TotalLength * m_DrawProgress;
            pts.Add(m_Path[0]);
            for (int i = 1; i < m_Path.Count; i++)
            {
                if (m_Lengths[i] <= limit) { pts.Add(m_Path[i]); continue; }
                float seg = m_Lengths[i] - m_Lengths[i - 1];
                float k = seg > 0f ? (limit - m_Lengths[i - 1]) / seg : 0f;
                pts.Add(Vector2.Lerp(m_Path[i - 1], m_Path[i], k));
                break;
            }
            bool loop = m_Closed && m_DrawProgress >= 0.999f;
            if (loop && pts.Count > 2) pts.RemoveAt(pts.Count - 1);

            int count = pts.Count;
            if (count < 2) return;
            float half = m_Thickness * 0.5f;
            Color32 c = color;

            for (int i = 0; i < count; i++)
            {
                Vector2 prev, next;
                if (loop)
                {
                    prev = pts[Wrap(i - 1, count)];
                    next = pts[Wrap(i + 1, count)];
                }
                else
                {
                    prev = i > 0 ? pts[i - 1] : pts[i] - (pts[i + 1] - pts[i]);
                    next = i < count - 1 ? pts[i + 1] : pts[i] + (pts[i] - pts[i - 1]);
                }
                Vector2 d1 = (pts[i] - prev).normalized;
                Vector2 d2 = (next - pts[i]).normalized;
                Vector2 n1 = new Vector2(-d1.y, d1.x);
                Vector2 n2 = new Vector2(-d2.y, d2.x);
                Vector2 miter = (n1 + n2);
                float ml = miter.magnitude;
                miter = ml > 1e-4f ? miter / ml : n2;
                float scale = Mathf.Min(half / Mathf.Max(0.25f, Vector2.Dot(miter, n2)), half * 2.5f);

                var v = UIVertex.simpleVert;
                v.color = c;
                v.position = pts[i] + miter * scale;
                vh.AddVert(v);
                v.position = pts[i] - miter * scale;
                vh.AddVert(v);
            }

            int segCount = loop ? count : count - 1;
            for (int i = 0; i < segCount; i++)
            {
                int a = i * 2;
                int b = ((i + 1) % count) * 2;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
            }
        }
    }
}
