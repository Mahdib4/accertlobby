using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.UI
{
    [System.Flags]
    public enum ChamferCorners
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomRight = 4,
        BottomLeft = 8,
        All = 15
    }

    /// <summary>
    /// Procedural UI shape for the ACCERTONE visual language: chamfered panels,
    /// forward-leaning parallelograms (speed slashes, action buttons) and hairline outlines.
    /// Resolution independent and needs no sprite assets.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class AccertoneShape : MaskableGraphic
    {
        [SerializeField] float m_Chamfer;
        [SerializeField] ChamferCorners m_Corners = ChamferCorners.None;
        [Tooltip("Horizontal offset of the top edge relative to the bottom edge (px). Positive leans forward like italic type.")]
        [SerializeField] float m_Skew;
        [SerializeField] bool m_Outline;
        [SerializeField] float m_OutlineWidth = 1f;
        [Header("Horizontal alpha fade")]
        [SerializeField, Range(0f, 1f)] float m_AlphaLeft = 1f;
        [SerializeField, Range(0f, 1f)] float m_AlphaRight = 1f;

        static readonly List<Vector2> s_Poly = new List<Vector2>(16);
        static readonly List<Vector2> s_Inner = new List<Vector2>(16);

        public float chamfer { get { return m_Chamfer; } set { m_Chamfer = value; SetVerticesDirty(); } }
        public ChamferCorners corners { get { return m_Corners; } set { m_Corners = value; SetVerticesDirty(); } }
        public float skew { get { return m_Skew; } set { m_Skew = value; SetVerticesDirty(); } }
        public bool outline { get { return m_Outline; } set { m_Outline = value; SetVerticesDirty(); } }
        public float outlineWidth { get { return m_OutlineWidth; } set { m_OutlineWidth = value; SetVerticesDirty(); } }
        public float alphaLeft { get { return m_AlphaLeft; } set { m_AlphaLeft = value; SetVerticesDirty(); } }
        public float alphaRight { get { return m_AlphaRight; } set { m_AlphaRight = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;

            BuildPolygon(r, s_Poly);
            if (s_Poly.Count < 3) return;

            if (m_Outline && m_OutlineWidth > 0f)
                BuildOutline(vh, r);
            else
                BuildFill(vh, r);
        }

        void BuildPolygon(Rect r, List<Vector2> poly)
        {
            poly.Clear();
            float s = Mathf.Clamp(m_Skew, -r.width * 0.9f, r.width * 0.9f);
            Vector2 bl, tl, tr, br;
            if (s >= 0f)
            {
                bl = new Vector2(r.xMin, r.yMin);
                tl = new Vector2(r.xMin + s, r.yMax);
                tr = new Vector2(r.xMax, r.yMax);
                br = new Vector2(r.xMax - s, r.yMin);
            }
            else
            {
                bl = new Vector2(r.xMin - s, r.yMin);
                tl = new Vector2(r.xMin, r.yMax);
                tr = new Vector2(r.xMax + s, r.yMax);
                br = new Vector2(r.xMax, r.yMin);
            }

            var c = new Vector2[] { bl, tl, tr, br };
            var flags = new ChamferCorners[] { ChamferCorners.BottomLeft, ChamferCorners.TopLeft, ChamferCorners.TopRight, ChamferCorners.BottomRight };
            float maxCut = Mathf.Min(r.width, r.height) * 0.5f;
            float cut = Mathf.Clamp(m_Chamfer, 0f, maxCut);

            for (int i = 0; i < 4; i++)
            {
                if (cut > 0f && (m_Corners & flags[i]) != 0)
                {
                    Vector2 prev = c[(i + 3) % 4];
                    Vector2 next = c[(i + 1) % 4];
                    poly.Add(c[i] + (prev - c[i]).normalized * cut);
                    poly.Add(c[i] + (next - c[i]).normalized * cut);
                }
                else
                {
                    poly.Add(c[i]);
                }
            }
        }

        UIVertex MakeVertex(Vector2 p, Rect r)
        {
            float t = r.width > 0f ? Mathf.InverseLerp(r.xMin, r.xMax, p.x) : 0f;
            Color32 c = color;
            float a = (c.a / 255f) * Mathf.Lerp(m_AlphaLeft, m_AlphaRight, t);
            c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            v.uv0 = new Vector4((p.x - r.xMin) / r.width, (p.y - r.yMin) / r.height, 0f, 0f);
            return v;
        }

        void BuildFill(VertexHelper vh, Rect r)
        {
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < s_Poly.Count; i++) centre += s_Poly[i];
            centre /= s_Poly.Count;

            vh.AddVert(MakeVertex(centre, r));
            for (int i = 0; i < s_Poly.Count; i++) vh.AddVert(MakeVertex(s_Poly[i], r));
            for (int i = 0; i < s_Poly.Count; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % s_Poly.Count;
                vh.AddTriangle(0, a, b);
            }
        }

        void BuildOutline(VertexHelper vh, Rect r)
        {
            int n = s_Poly.Count;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = s_Poly[i], q = s_Poly[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }
            float inward = area < 0f ? 1f : -1f; // clockwise => interior on the right

            float w = Mathf.Min(m_OutlineWidth, Mathf.Min(r.width, r.height) * 0.5f);
            s_Inner.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = s_Poly[(i + n - 1) % n];
                Vector2 cur = s_Poly[i];
                Vector2 next = s_Poly[(i + 1) % n];
                Vector2 d1 = (cur - prev).normalized;
                Vector2 d2 = (next - cur).normalized;
                Vector2 n1 = new Vector2(d1.y, -d1.x) * inward;
                Vector2 n2 = new Vector2(d2.y, -d2.x) * inward;
                float denom = 1f + Vector2.Dot(n1, n2);
                Vector2 offset = denom > 0.05f ? (n1 + n2) * (w / denom) : n1 * w;
                s_Inner.Add(cur + offset);
            }

            for (int i = 0; i < n; i++)
            {
                vh.AddVert(MakeVertex(s_Poly[i], r));
                vh.AddVert(MakeVertex(s_Inner[i], r));
            }
            for (int i = 0; i < n; i++)
            {
                int o0 = i * 2, i0 = i * 2 + 1;
                int o1 = ((i + 1) % n) * 2, i1 = ((i + 1) % n) * 2 + 1;
                vh.AddTriangle(o0, o1, i1);
                vh.AddTriangle(o0, i1, i0);
            }
        }
    }
}
