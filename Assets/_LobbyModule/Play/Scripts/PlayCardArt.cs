using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// Mode-card artwork that always fills its rect at the rect's own aspect ratio ("cover"), so every
    /// image crops identically and nothing is ever stretched. The crop is framed on a focus point, and
    /// zoom / pan are applied through UVs. The quad is subdivided so the card's perspective tilt does
    /// not warp the photo along a triangle seam.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PlayCardArt : MaskableGraphic
    {
        const int Columns = 8;
        const int Rows = 12;

        [SerializeField] Sprite m_Sprite;
        [Tooltip("Point of interest in the source image (0..1, origin bottom-left) the crop stays centred on.")]
        [SerializeField] Vector2 m_Focus = new Vector2(0.5f, 0.5f);
        [SerializeField, Min(1f)] float m_Zoom = 1f;
        [Tooltip("Offset of the crop window, as a fraction of the window size.")]
        [SerializeField] Vector2 m_Pan;

        public Sprite sprite
        {
            get { return m_Sprite; }
            set { if (m_Sprite == value) return; m_Sprite = value; SetAllDirty(); }
        }

        public Vector2 focus
        {
            get { return m_Focus; }
            set { if (m_Focus == value) return; m_Focus = value; SetVerticesDirty(); }
        }

        public float zoom
        {
            get { return m_Zoom; }
            set
            {
                value = Mathf.Max(1f, value);
                if (Mathf.Abs(m_Zoom - value) < 1e-5f) return;
                m_Zoom = value;
                SetVerticesDirty();
            }
        }

        public Vector2 pan
        {
            get { return m_Pan; }
            set { if ((m_Pan - value).sqrMagnitude < 1e-10f) return; m_Pan = value; SetVerticesDirty(); }
        }

        public override Texture mainTexture
        {
            get { return m_Sprite != null ? m_Sprite.texture : s_WhiteTexture; }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;

            // Sprite bounds inside its texture (normalised).
            Rect uvBounds = new Rect(0f, 0f, 1f, 1f);
            float spriteAspect = 1f;
            if (m_Sprite != null && m_Sprite.texture != null)
            {
                Texture tex = m_Sprite.texture;
                Rect tr = m_Sprite.textureRect;
                uvBounds = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
                spriteAspect = tr.width / Mathf.Max(1f, tr.height);
            }

            // Cover-crop window in sprite space: same aspect as the rect, never stretched.
            float rectAspect = r.width / r.height;
            float w = 1f, h = 1f;
            if (rectAspect < spriteAspect) w = rectAspect / spriteAspect;
            else h = spriteAspect / rectAspect;
            w /= m_Zoom;
            h /= m_Zoom;

            float cx = Mathf.Clamp(m_Focus.x + m_Pan.x * w, w * 0.5f, 1f - w * 0.5f);
            float cy = Mathf.Clamp(m_Focus.y + m_Pan.y * h, h * 0.5f, 1f - h * 0.5f);
            float u0 = cx - w * 0.5f;
            float v0 = cy - h * 0.5f;

            Color32 c = color;
            for (int y = 0; y <= Rows; y++)
            {
                float ty = (float)y / Rows;
                float py = r.yMin + r.height * ty;
                float v = uvBounds.y + (v0 + h * ty) * uvBounds.height;
                for (int x = 0; x <= Columns; x++)
                {
                    float tx = (float)x / Columns;
                    vh.AddVert(new Vector3(r.xMin + r.width * tx, py, 0f), c,
                        new Vector4(uvBounds.x + (u0 + w * tx) * uvBounds.width, v, 0f, 0f));
                }
            }

            const int stride = Columns + 1;
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    int i = y * stride + x;
                    vh.AddTriangle(i, i + stride, i + stride + 1);
                    vh.AddTriangle(i, i + stride + 1, i + 1);
                }
            }
        }
    }
}
