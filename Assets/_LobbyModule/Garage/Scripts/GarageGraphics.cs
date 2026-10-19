using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Garage
{
    public enum GarageIconKind { Paint, Aero, Suspension, Brakes, Tyres, Powertrain, Setup, Info, Rotate, Lock, Camera, Save, Reset, Check }

    /// <summary>
    /// Thin technical line icons drawn as UI geometry (no textures, crisp at any scale). Strokes are authored in a
    /// 0..1 box and stroked with <see cref="thickness"/> pixels.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GarageIcon : MaskableGraphic
    {
        [SerializeField] GarageIconKind m_Kind;
        [SerializeField] float m_Thickness = 1.6f;

        static readonly List<Vector2> s_Points = new List<Vector2>(64);

        public GarageIconKind kind { get { return m_Kind; } set { if (m_Kind == value) return; m_Kind = value; SetVerticesDirty(); } }
        public float thickness { get { return m_Thickness; } set { if (Mathf.Abs(m_Thickness - value) < 1e-3f) return; m_Thickness = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            switch (m_Kind)
            {
                case GarageIconKind.Paint:
                    // A drop and two livery stripes.
                    Begin(); Arc(0.36f, 0.36f, 0.2f, 200f, 520f, 20); Add(0.36f, 0.84f); Close(); Stroke(vh, r, true);
                    Begin(); Add(0.62f, 0.18f); Add(0.9f, 0.62f); Stroke(vh, r, false);
                    Begin(); Add(0.74f, 0.14f); Add(0.96f, 0.48f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Aero:
                    // Wing profile with airflow lines.
                    Begin(); Add(0.06f, 0.42f); Add(0.3f, 0.56f); Add(0.6f, 0.58f); Add(0.94f, 0.46f); Add(0.6f, 0.4f); Add(0.3f, 0.38f); Close(); Stroke(vh, r, true);
                    Begin(); Add(0.1f, 0.74f); Add(0.62f, 0.78f); Add(0.9f, 0.7f); Stroke(vh, r, false);
                    Begin(); Add(0.2f, 0.2f); Add(0.7f, 0.22f); Add(0.9f, 0.3f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Suspension:
                    // Coil spring over a damper, mounted between two plates.
                    Begin(); Add(0.2f, 0.92f); Add(0.8f, 0.92f); Stroke(vh, r, false);
                    Begin(); Add(0.2f, 0.08f); Add(0.8f, 0.08f); Stroke(vh, r, false);
                    Begin();
                    for (int i = 0; i <= 8; i++) Add(i % 2 == 0 ? 0.3f : 0.7f, 0.82f - i * 0.08f);
                    Stroke(vh, r, false);
                    Begin(); Add(0.5f, 0.18f); Add(0.5f, 0.08f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Brakes:
                    // Ventilated disc with a caliper.
                    Begin(); Arc(0.46f, 0.5f, 0.38f, 0f, 360f, 32); Close(); Stroke(vh, r, true);
                    Begin(); Arc(0.46f, 0.5f, 0.13f, 0f, 360f, 16); Close(); Stroke(vh, r, true);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Begin(); Arc(0.46f + Mathf.Cos(a) * 0.26f, 0.5f + Mathf.Sin(a) * 0.26f, 0.025f, 0f, 360f, 6); Close(); Stroke(vh, r, true);
                    }
                    Begin(); Arc(0.46f, 0.5f, 0.47f, -40f, 40f, 10); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Tyres:
                    Begin(); Arc(0.5f, 0.5f, 0.42f, 0f, 360f, 36); Close(); Stroke(vh, r, true);
                    Begin(); Arc(0.5f, 0.5f, 0.26f, 0f, 360f, 28); Close(); Stroke(vh, r, true);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = (90f + i * 72f) * Mathf.Deg2Rad;
                        Begin(); Add(0.5f + Mathf.Cos(a) * 0.07f, 0.5f + Mathf.Sin(a) * 0.07f); Add(0.5f + Mathf.Cos(a) * 0.25f, 0.5f + Mathf.Sin(a) * 0.25f); Stroke(vh, r, false);
                    }
                    break;
                case GarageIconKind.Powertrain:
                    // Piston, con-rod and crank.
                    Begin(); Add(0.3f, 0.94f); Add(0.7f, 0.94f); Add(0.7f, 0.66f); Add(0.3f, 0.66f); Close(); Stroke(vh, r, true);
                    Begin(); Add(0.3f, 0.84f); Add(0.7f, 0.84f); Stroke(vh, r, false);
                    Begin(); Add(0.5f, 0.66f); Add(0.62f, 0.3f); Stroke(vh, r, false);
                    Begin(); Arc(0.5f, 0.24f, 0.18f, 0f, 360f, 24); Close(); Stroke(vh, r, true);
                    break;
                case GarageIconKind.Setup:
                    // Three slider tracks.
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.22f + i * 0.28f;
                        float k = i == 0 ? 0.68f : (i == 1 ? 0.32f : 0.56f);
                        Begin(); Add(0.06f, y); Add(k - 0.08f, y); Stroke(vh, r, false);
                        Begin(); Add(k + 0.08f, y); Add(0.94f, y); Stroke(vh, r, false);
                        Begin(); Arc(k, y, 0.075f, 0f, 360f, 12); Close(); Stroke(vh, r, true);
                    }
                    break;
                case GarageIconKind.Info:
                    // Spec sheet with a dimension arrow.
                    Begin(); Add(0.2f, 0.94f); Add(0.66f, 0.94f); Add(0.82f, 0.78f); Add(0.82f, 0.06f); Add(0.2f, 0.06f); Close(); Stroke(vh, r, true);
                    Begin(); Add(0.32f, 0.7f); Add(0.7f, 0.7f); Stroke(vh, r, false);
                    Begin(); Add(0.32f, 0.54f); Add(0.7f, 0.54f); Stroke(vh, r, false);
                    Begin(); Add(0.32f, 0.38f); Add(0.58f, 0.38f); Stroke(vh, r, false);
                    Begin(); Add(0.32f, 0.2f); Add(0.7f, 0.2f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Rotate:
                    Begin(); Arc(0.5f, 0.5f, 0.36f, 40f, 320f, 28); Stroke(vh, r, false);
                    Begin(); Add(0.72f, 0.92f); Add(0.78f, 0.74f); Add(0.6f, 0.7f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Lock:
                    Begin(); Add(0.22f, 0.56f); Add(0.78f, 0.56f); Add(0.78f, 0.08f); Add(0.22f, 0.08f); Close(); Stroke(vh, r, true);
                    Begin(); Arc(0.5f, 0.62f, 0.2f, 0f, 180f, 12); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Camera:
                    Begin(); Add(0.06f, 0.74f); Add(0.66f, 0.74f); Add(0.66f, 0.26f); Add(0.06f, 0.26f); Close(); Stroke(vh, r, true);
                    Begin(); Add(0.66f, 0.56f); Add(0.94f, 0.72f); Add(0.94f, 0.28f); Add(0.66f, 0.44f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Save:
                    Begin(); Add(0.14f, 0.92f); Add(0.74f, 0.92f); Add(0.88f, 0.78f); Add(0.88f, 0.08f); Add(0.14f, 0.08f); Close(); Stroke(vh, r, true);
                    Begin(); Add(0.3f, 0.92f); Add(0.3f, 0.66f); Add(0.66f, 0.66f); Add(0.66f, 0.92f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Reset:
                    Begin(); Arc(0.5f, 0.5f, 0.36f, 120f, 420f, 28); Stroke(vh, r, false);
                    Begin(); Add(0.14f, 0.86f); Add(0.3f, 0.82f); Add(0.26f, 0.66f); Stroke(vh, r, false);
                    break;
                case GarageIconKind.Check:
                    Begin(); Add(0.16f, 0.5f); Add(0.42f, 0.24f); Add(0.86f, 0.76f); Stroke(vh, r, false);
                    break;
            }
        }

        static void Begin() { s_Points.Clear(); }
        static void Add(float x, float y) { s_Points.Add(new Vector2(x, y)); }
        static void Close() { if (s_Points.Count > 1) s_Points.Add(s_Points[0]); }

        static void Arc(float cx, float cy, float radius, float from, float to, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
                s_Points.Add(new Vector2(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius));
            }
        }

        void Stroke(VertexHelper vh, Rect r, bool closed)
        {
            int n = s_Points.Count;
            if (n < 2) return;
            float half = m_Thickness * 0.5f;
            Color32 c = color;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 a = new Vector2(r.xMin + s_Points[i].x * r.width, r.yMin + s_Points[i].y * r.height);
                Vector2 b = new Vector2(r.xMin + s_Points[i + 1].x * r.width, r.yMin + s_Points[i + 1].y * r.height);
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 1e-4f) continue;
                d /= len;
                // Extend each segment by half the stroke so joints close without gaps.
                a -= d * half * 0.9f;
                b += d * half * 0.9f;
                Vector2 nrm = new Vector2(-d.y, d.x) * half;
                int start = vh.currentVertCount;
                vh.AddVert(a - nrm, c, Vector4.zero);
                vh.AddVert(a + nrm, c, Vector4.zero);
                vh.AddVert(b + nrm, c, Vector4.zero);
                vh.AddVert(b - nrm, c, Vector4.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }

    /// <summary>
    /// A polyline in the graphic's own pixel space with a drawn-on <see cref="progress"/> and an alpha fade along its
    /// length. Used for the callout leaders from the menu to the car.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GarageLine : MaskableGraphic
    {
        readonly List<Vector2> m_Points = new List<Vector2>(8);
        float m_Thickness = 1.4f, m_Progress = 1f, m_StartAlpha = 1f, m_EndAlpha = 1f;

        public float thickness { get { return m_Thickness; } set { m_Thickness = value; SetVerticesDirty(); } }
        public float progress { get { return m_Progress; } set { value = Mathf.Clamp01(value); if (Mathf.Abs(m_Progress - value) < 1e-4f) return; m_Progress = value; SetVerticesDirty(); } }

        public void SetFade(float start, float end)
        {
            m_StartAlpha = start;
            m_EndAlpha = end;
            SetVerticesDirty();
        }

        public void SetPoints(Vector2 a, Vector2 b, Vector2 c)
        {
            if (m_Points.Count == 3 && m_Points[0] == a && m_Points[1] == b && m_Points[2] == c) return;
            m_Points.Clear();
            m_Points.Add(a);
            m_Points.Add(b);
            m_Points.Add(c);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Points.Count < 2 || m_Progress <= 0f) return;
            float total = 0f;
            for (int i = 0; i < m_Points.Count - 1; i++) total += Vector2.Distance(m_Points[i], m_Points[i + 1]);
            if (total < 1e-3f) return;
            float drawn = total * m_Progress;
            float walked = 0f;
            float half = m_Thickness * 0.5f;
            Color baseColor = color;
            for (int i = 0; i < m_Points.Count - 1 && walked < drawn; i++)
            {
                Vector2 a = m_Points[i], b = m_Points[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 1e-4f) continue;
                float take = Mathf.Min(len, drawn - walked);
                Vector2 d = (b - a) / len;
                Vector2 e = a + d * take;
                Vector2 nrm = new Vector2(-d.y, d.x) * half;
                Color ca = baseColor, ce = baseColor;
                ca.a *= Mathf.Lerp(m_StartAlpha, m_EndAlpha, walked / total);
                ce.a *= Mathf.Lerp(m_StartAlpha, m_EndAlpha, (walked + take) / total);
                Vector2 a2 = a - d * (i > 0 ? half : 0f);
                int s = vh.currentVertCount;
                vh.AddVert(a2 - nrm, ca, Vector4.zero);
                vh.AddVert(a2 + nrm, ca, Vector4.zero);
                vh.AddVert(e + nrm, ce, Vector4.zero);
                vh.AddVert(e - nrm, ce, Vector4.zero);
                vh.AddTriangle(s, s + 1, s + 2);
                vh.AddTriangle(s, s + 2, s + 3);
                walked += take;
            }
        }
    }

    /// <summary>A thin ring (optionally dashed) used as the target marker on the car.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GarageRing : MaskableGraphic
    {
        float m_Thickness = 1.5f;
        int m_Dashes;

        public float thickness { get { return m_Thickness; } set { m_Thickness = value; SetVerticesDirty(); } }
        public int dashes { get { return m_Dashes; } set { m_Dashes = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            if (radius <= 1f) return;
            Vector2 c = r.center;
            const int segs = 48;
            float inner = Mathf.Max(0f, radius - m_Thickness);
            Color32 col = color;
            for (int i = 0; i < segs; i++)
            {
                if (m_Dashes > 0 && (i * m_Dashes * 2 / segs) % 2 == 1) continue;
                float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                int s = vh.currentVertCount;
                vh.AddVert(c + d0 * inner, col, Vector4.zero);
                vh.AddVert(c + d0 * radius, col, Vector4.zero);
                vh.AddVert(c + d1 * radius, col, Vector4.zero);
                vh.AddVert(c + d1 * inner, col, Vector4.zero);
                vh.AddTriangle(s, s + 1, s + 2);
                vh.AddTriangle(s, s + 2, s + 3);
            }
        }
    }
}
