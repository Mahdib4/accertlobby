using System.Collections.Generic;
using UnityEngine;

namespace Accertone.LobbyModule.EditorTools
{
    /// <summary>
    /// ACCERTONE works livery, after the reference render: satin black body, violet-to-magenta flames
    /// licking back from the nose and sidepods, violet front wing, violet rear-wing endplates and
    /// underside, white sponsor type, #13, and a glowing ACCERTONE mark on the engine cover.
    ///
    /// Design space = the car's upright space: X lateral (+X = car's right), Y up, Z forward (nose at +2.7 m).
    /// All logos are the user's / game's own brands; the reference's third-party sponsor was replaced.
    /// </summary>
    public class AccertoneCarLivery : ILiveryDesign
    {
        public static readonly Color Body = new Color(0.078f, 0.071f, 0.094f);
        public static readonly Color Violet = Hex(0x7C3AFF);
        public static readonly Color Magenta = Hex(0xE840D6);
        public static readonly Color Hot = Hex(0xFF8BEE);
        public static readonly Color Deep = Hex(0x2A1052);
        public static readonly Color White = Hex(0xF3F5F8);

        struct Tongue
        {
            public float start, offset, length, halfWidth, amp, freq, phase, rise;
            public Tongue(float start, float offset, float length, float halfWidth, float amp, float freq, float phase, float rise)
            {
                this.start = start; this.offset = offset; this.length = length; this.halfWidth = halfWidth;
                this.amp = amp; this.freq = freq; this.phase = phase; this.rise = rise;
            }
        }

        struct Decal
        {
            public Texture2D tex;
            public bool top;        // false = side projection (both sides, readable), true = top projection (readable from the front)
            public float a, b;      // side: z centre, y centre   | top: x centre, z centre
            public float width;
            public float minAbsX;
            public float maxY;      // top decals: ignore surfaces above this height (e.g. stacked wing flaps)
            public Color color;
            public bool glow;
        }

        // Side flames: (start z, centre y, length back along -z, half width, wave amp, wave freq, phase, rise)
        static readonly Tongue[] SideFlames =
        {
            new Tongue(2.58f, 0.42f, 1.30f, 0.050f, 0.030f, 1.10f, 0.0f, 0.02f),
            new Tongue(2.46f, 0.35f, 1.05f, 0.040f, 0.025f, 1.40f, 1.3f, 0.00f),
            new Tongue(2.38f, 0.49f, 0.95f, 0.036f, 0.030f, 1.60f, 2.1f, 0.03f),
            new Tongue(1.15f, 0.53f, 1.55f, 0.060f, 0.045f, 1.05f, 0.4f, 0.06f),
            new Tongue(1.02f, 0.42f, 1.90f, 0.058f, 0.050f, 1.25f, 2.2f, 0.07f),
            new Tongue(0.86f, 0.32f, 2.05f, 0.052f, 0.040f, 1.55f, 4.0f, 0.05f),
            new Tongue(0.70f, 0.61f, 1.30f, 0.044f, 0.040f, 1.85f, 1.0f, 0.09f),
            new Tongue(-0.30f, 0.73f, 1.35f, 0.048f, 0.055f, 1.20f, 0.5f, 0.10f),
            new Tongue(-0.45f, 0.85f, 1.05f, 0.038f, 0.050f, 1.45f, 2.6f, 0.08f),
        };

        // Top flames, mirrored about the centreline: offset = distance from centreline.
        static readonly Tongue[] TopFlames =
        {
            new Tongue(2.60f, 0.000f, 1.25f, 0.042f, 0.020f, 1.20f, 0.0f, 0.0f),
            new Tongue(2.45f, 0.078f, 1.10f, 0.030f, 0.020f, 1.50f, 1.7f, 0.0f),
            new Tongue(-0.42f, 0.060f, 1.40f, 0.040f, 0.030f, 1.10f, 0.9f, 0.0f),
            new Tongue(-0.55f, 0.165f, 1.20f, 0.034f, 0.030f, 1.40f, 2.4f, 0.0f),
        };

        readonly List<Decal> m_Decals = new List<Decal>();

        public AccertoneCarLivery(Texture2D accertone, Texture2D octa, Texture2D dezx, Texture2D zeriotic, Texture2D number, Texture2D mark)
        {
            // Sidepods: the game's own wordmark (the reference's third-party sponsor slot).
            Side(accertone, -0.55f, 0.46f, 1.15f, 0.50f, White);
            Side(octa, 0.12f, 0.74f, 0.40f, 0.20f, White);
            Side(dezx, 0.82f, 0.29f, 0.34f, 0.45f, White);
            Side(number, 2.02f, 0.43f, 0.15f, 0.04f, White);
            Side(number, -1.35f, 0.63f, 0.19f, 0.15f, White);
            Side(mark, -0.78f, 0.68f, 0.16f, 0.15f, Magenta, true);
            Side(octa, -2.56f, 0.72f, 0.36f, 0.45f, White);

            Top(number, 0f, 1.95f, 0.15f, White);
            Top(mark, 0f, 2.43f, 0.09f, White);
            // Front-wing main plane only, ahead of the flap stack (flaps end at z ~2.49, y > 0.29).
            Top(zeriotic, -0.50f, 2.565f, 0.30f, White, 0.29f);
            Top(zeriotic, 0.50f, 2.565f, 0.30f, White, 0.29f);
            Top(octa, 0f, -2.60f, 0.78f, White);
        }

        void Side(Texture2D tex, float z, float y, float width, float minAbsX, Color color, bool glow = false)
        {
            if (tex == null) return;
            m_Decals.Add(new Decal { tex = tex, top = false, a = z, b = y, width = width, minAbsX = minAbsX, color = color, glow = glow });
        }

        void Top(Texture2D tex, float x, float z, float width, Color color, float maxY = 9f)
        {
            if (tex == null) return;
            m_Decals.Add(new Decal { tex = tex, top = true, a = x, b = z, width = width, color = color, maxY = maxY });
        }

        public void Evaluate(Vector3 p, Vector3 n, ref Color baseColor, ref Color emission)
        {
            float ax = Mathf.Abs(p.x);
            Color col = Body;
            Color emi = Color.black;

            bool frontWing = p.z > 1.92f && p.y < 0.42f;
            bool rearWing = p.z < -2.25f && p.y > 0.5f;

            if (frontWing)
            {
                float t = Mathf.Clamp01(1f - ax / 0.98f);
                col = Color.Lerp(Violet * 0.82f, Magenta, t * t);
                if (n.y < -0.3f) col = Color.Lerp(col, Body, 0.55f);
            }
            else if (rearWing)
            {
                if (ax > 0.42f) col = Color.Lerp(Deep, Violet, Mathf.InverseLerp(0.5f, 1.0f, p.y));
                else if (n.y < -0.25f) col = Color.Lerp(Body, Violet, 0.55f);
            }
            else
            {
                float sideW = Smooth(0.18f, 0.5f, Mathf.Abs(n.x));
                float topW = Smooth(0.2f, 0.55f, n.y);
                float uSide = 0f, uTop = 0f;
                float dSide = sideW > 0f ? Distance(SideFlames, p.z, p.y, out uSide) : 9f;
                float dTop = topW > 0f ? Distance(TopFlames, p.z, ax, out uTop) : 9f;

                // Pick the projection that dominates this surface.
                float d, u, w;
                if (sideW >= topW) { d = dSide; u = uSide; w = sideW; }
                else { d = dTop; u = uTop; w = topW; }

                const float aa = 0.0028f;
                float mask = Mathf.Clamp01(0.5f - d / aa) * w;
                float outline = d > 0f ? Mathf.Clamp01(1f - d / 0.009f) * w : 0f;
                float rim = d < 0f ? Mathf.Clamp01(1f + d / 0.011f) : 0f;

                Color fill = Color.Lerp(Violet, Magenta, Smooth(0.08f, 0.8f, u));
                fill = Color.Lerp(fill, Hot, Smooth(0.82f, 1f, u) * 0.5f);
                fill = Color.Lerp(fill, Hot, rim * 0.55f);

                col = Color.Lerp(Body, Deep, outline * 0.85f);
                col = Color.Lerp(col, fill, mask);
                emi = Magenta * (rim * mask * 0.10f);
            }

            for (int i = 0; i < m_Decals.Count; i++)
            {
                var dcl = m_Decals[i];
                float alpha = DecalAlpha(dcl, p, n, ax);
                if (alpha <= 0f) continue;
                col = Color.Lerp(col, dcl.color, alpha);
                if (dcl.glow) emi = Color.Lerp(emi, dcl.color, alpha);
            }

            col.a = 1f;
            emi.a = 1f;
            baseColor = col;
            emission = emi;
        }

        static float DecalAlpha(Decal d, Vector3 p, Vector3 n, float ax)
        {
            float aspect = (float)d.tex.height / d.tex.width;
            float h = d.width * aspect;
            float u, v;
            if (d.top)
            {
                if (n.y < 0.45f || p.y > d.maxY) return 0f;
                u = (d.a - p.x) / d.width + 0.5f;
                v = (d.b - p.z) / h + 0.5f;
            }
            else
            {
                // Outward-facing, mostly vertical surfaces only: no mirrored text on inner faces,
                // no stretched copies on near-horizontal panels.
                float outward = n.x * Mathf.Sign(p.x);
                if (outward < 0.5f || Mathf.Abs(n.y) > 0.6f || ax < d.minAbsX) return 0f;
                u = p.x < 0f ? (d.a - p.z) / d.width + 0.5f : (p.z - d.a) / d.width + 0.5f;
                v = (p.y - d.b) / h + 0.5f;
            }
            if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;
            return d.tex.GetPixelBilinear(u, v).a;
        }

        static float Distance(Tongue[] tongues, float along, float across, out float bestU)
        {
            float best = 9f;
            bestU = 0f;
            for (int i = 0; i < tongues.Length; i++)
            {
                var t = tongues[i];
                float u = (t.start - along) / t.length;
                if (u < -0.08f || u > 1.02f) continue;
                float uc = Mathf.Clamp01(u);
                float centre = t.offset + t.rise * uc + t.amp * Mathf.Sin(uc * Mathf.PI * 2f * t.freq + t.phase) * (0.25f + uc);
                float width = t.halfWidth * Mathf.Pow(1f - uc, 0.9f) * (0.85f + 0.3f * Mathf.Sin(uc * 9f + t.phase * 1.7f));
                width *= Mathf.Sqrt(Mathf.Clamp01(u / 0.05f));
                float d = Mathf.Abs(across - centre) - width;
                if (u < 0f) d = Mathf.Max(d, -u * t.length);
                if (d < best)
                {
                    best = d;
                    bestU = uc;
                }
            }
            return best;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }

    /// <summary>
    /// Team badge for the driver suit: ACCERTONE mark + wordmark printed on the left chest.
    /// Design space = the driver mesh's local space (faces +Z, +X = driver's right).
    /// </summary>
    public class DriverSuitBadge : ILiveryDesign
    {
        readonly Texture2D m_Mark;
        readonly Texture2D m_Word;
        readonly Vector3 m_Centre;
        readonly float m_Scale;

        public DriverSuitBadge(Texture2D mark, Texture2D word, Bounds meshBounds)
        {
            m_Mark = mark;
            m_Word = word;
            float h = meshBounds.size.y;
            m_Scale = h / 1.8f;
            m_Centre = new Vector3(meshBounds.center.x - 0.085f * m_Scale, meshBounds.min.y + 0.715f * h, 0f);
        }

        public void Evaluate(Vector3 p, Vector3 n, ref Color baseColor, ref Color emission)
        {
            if (n.z < 0.45f || p.z < 0f) return;
            Color graphite = new Color(0.16f, 0.17f, 0.2f);
            float markW = 0.045f * m_Scale;
            float wordW = 0.105f * m_Scale;
            float a = Sample(m_Mark, p, m_Centre + new Vector3(0f, 0.028f * m_Scale, 0f), markW);
            if (a > 0f) baseColor = Color.Lerp(baseColor, AccertoneCarLivery.Magenta, a);
            float w = Sample(m_Word, p, m_Centre - new Vector3(0f, 0.012f * m_Scale, 0f), wordW);
            if (w > 0f) baseColor = Color.Lerp(baseColor, graphite, w);
        }

        static float Sample(Texture2D tex, Vector3 p, Vector3 centre, float width)
        {
            float h = width * tex.height / tex.width;
            float u = (centre.x - p.x) / width + 0.5f;
            float v = (p.y - centre.y) / h + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;
            return tex.GetPixelBilinear(u, v).a;
        }
    }
}
