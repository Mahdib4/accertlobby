using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// A rectangle whose edges fade out over <see cref="feather"/> pixels with rounded falloff corners.
    /// Used for the card glow and the contact shadow; needs no texture and stays crisp at any resolution.
    /// The solid core is the rect inset by the feather, so size the RectTransform to core + 2 × feather.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PlaySoftRect : MaskableGraphic
    {
        const int CornerSegments = 6;
        const int RingCount = 4;

        [SerializeField, Min(0f)] float m_Feather = 32f;

        static readonly float[] s_RingOffset = { 0f, 0.28f, 0.6f, 1f };
        static readonly float[] s_RingAlpha = { 1f, 0.55f, 0.16f, 0f };

        public float feather
        {
            get { return m_Feather; }
            set { if (Mathf.Abs(m_Feather - value) < 1e-4f) return; m_Feather = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float f = Mathf.Min(m_Feather, Mathf.Min(r.width, r.height) * 0.5f - 1f);
            if (r.width <= 0f || r.height <= 0f || f <= 0f) return;

            Rect core = new Rect(r.xMin + f, r.yMin + f, r.width - 2f * f, r.height - 2f * f);
            Color baseColor = color;

            // Centre vertex for the solid core fan.
            vh.AddVert(new Vector3(core.center.x, core.center.y, 0f), baseColor, Vector4.zero);

            int perRing = 4 * (CornerSegments + 1);
            for (int ring = 0; ring < RingCount; ring++)
            {
                float d = s_RingOffset[ring] * f;
                Color c = baseColor;
                c.a *= s_RingAlpha[ring];
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 pivot;
                    float startAngle;
                    switch (corner)
                    {
                        case 0: pivot = new Vector2(core.xMax, core.yMax); startAngle = 0f; break;          // top-right
                        case 1: pivot = new Vector2(core.xMin, core.yMax); startAngle = 90f; break;         // top-left
                        case 2: pivot = new Vector2(core.xMin, core.yMin); startAngle = 180f; break;        // bottom-left
                        default: pivot = new Vector2(core.xMax, core.yMin); startAngle = 270f; break;       // bottom-right
                    }
                    for (int s = 0; s <= CornerSegments; s++)
                    {
                        float a = (startAngle + 90f * s / CornerSegments) * Mathf.Deg2Rad;
                        vh.AddVert(new Vector3(pivot.x + Mathf.Cos(a) * d, pivot.y + Mathf.Sin(a) * d, 0f), c, Vector4.zero);
                    }
                }
            }

            // Core fan over ring 0.
            for (int i = 0; i < perRing; i++)
                vh.AddTriangle(0, 1 + i, 1 + (i + 1) % perRing);

            // Feather bands between consecutive rings.
            for (int ring = 0; ring < RingCount - 1; ring++)
            {
                int inner = 1 + ring * perRing;
                int outer = inner + perRing;
                for (int i = 0; i < perRing; i++)
                {
                    int j = (i + 1) % perRing;
                    vh.AddTriangle(inner + i, outer + i, outer + j);
                    vh.AddTriangle(inner + i, outer + j, inner + j);
                }
            }
        }
    }
}
