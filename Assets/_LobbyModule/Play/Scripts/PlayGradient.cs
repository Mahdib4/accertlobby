using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// Multi-stop linear gradient (bottom→top or left→right) used for the Play screen's scrims,
    /// hover tints, light sweeps and floor lines. Stop colours can be animated without allocations.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PlayGradient : MaskableGraphic
    {
        public enum Axis { Vertical, Horizontal }

        const int MaxStops = 8;

        [SerializeField] Axis m_Axis = Axis.Vertical;
        [SerializeField] int m_Count = 2;
        [SerializeField] float[] m_Positions = new float[MaxStops];
        [SerializeField] Color[] m_Colors = new Color[MaxStops];
        [Tooltip("Extra subdivisions across the gradient so perspective projection stays accurate.")]
        [SerializeField, Range(1, 8)] int m_CrossSegments = 2;

        public Axis axis { get { return m_Axis; } set { m_Axis = value; SetVerticesDirty(); } }
        public int stopCount { get { return m_Count; } }

        /// <summary>Defines the number of stops (2..8); positions run 0..1 from bottom (or left).</summary>
        public void SetStopCount(int count)
        {
            m_Count = Mathf.Clamp(count, 2, MaxStops);
            SetVerticesDirty();
        }

        public void SetStop(int index, float position, Color c)
        {
            if (index < 0 || index >= MaxStops) return;
            m_Positions[index] = position;
            m_Colors[index] = c;
            SetVerticesDirty();
        }

        public void SetStopColor(int index, Color c)
        {
            if (index < 0 || index >= MaxStops) return;
            if (m_Colors[index] == c) return;
            m_Colors[index] = c;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f || m_Count < 2) return;

            Color tint = color;
            int cross = Mathf.Max(1, m_CrossSegments);
            for (int s = 0; s < m_Count; s++)
            {
                float t = Mathf.Clamp01(m_Positions[s]);
                Color32 c = m_Colors[s] * tint;
                for (int k = 0; k <= cross; k++)
                {
                    float u = (float)k / cross;
                    Vector3 p = m_Axis == Axis.Vertical
                        ? new Vector3(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, t), 0f)
                        : new Vector3(Mathf.Lerp(r.xMin, r.xMax, t), Mathf.Lerp(r.yMin, r.yMax, u), 0f);
                    vh.AddVert(p, c, Vector4.zero);
                }
            }

            int stride = cross + 1;
            for (int s = 0; s < m_Count - 1; s++)
            {
                for (int k = 0; k < cross; k++)
                {
                    int a = s * stride + k;
                    int b = (s + 1) * stride + k;
                    vh.AddTriangle(a, b, b + 1);
                    vh.AddTriangle(a, b + 1, a + 1);
                }
            }
        }
    }
}
