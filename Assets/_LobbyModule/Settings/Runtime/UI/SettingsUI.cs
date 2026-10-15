using Accertone.Branding;
using Accertone.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>Colours, fonts and type sizes of the settings screen, taken from the Accert one brand assets.</summary>
    public static class SettingsTheme
    {
        public static readonly Color Graphite = Hex(0x0A0B0E);
        public static readonly Color Carbon = Hex(0x111318);
        public static readonly Color Panel = Hex(0x1A1D23);
        public static readonly Color Hairline = Hex(0x2C313A);
        public static readonly Color Metallic = Hex(0x8A919C);
        public static readonly Color Silver = Hex(0xC4C9D1);
        public static readonly Color White = Hex(0xF3F5F8);
        public static readonly Color Text2 = Hex(0xA9AFB9);
        public static readonly Color Muted = Hex(0x6B717C);
        public static readonly Color Accent = Hex(0xC042FF);
        public static readonly Color AccentDeep = Hex(0x6B1FB0);
        public static readonly Color AccentLight = Hex(0xD58BFF);
        public static readonly Color Positive = Hex(0x3FD69A);
        public static readonly Color Warning = Hex(0xFFB547);
        public static readonly Color Danger = Hex(0xFF4D5E);
        public static readonly Color Field = new Color(0.051f, 0.059f, 0.075f, 0.92f);
        public static readonly Color KeycapFill = Hex(0x262A32);

        static AccertoneTypography Typography
        {
            get { var id = AccertoneBrandIdentity.Default; return id != null ? id.typography : null; }
        }

        static TMP_FontAsset Pick(System.Func<AccertoneTypography, TMP_FontAsset> f)
        {
            var t = Typography;
            var font = t != null ? f(t) : null;
            return font != null ? font : TMP_Settings.defaultFontAsset;
        }

        public static TMP_FontAsset Display { get { return Pick(t => t.displayFont); } }     // Roboto Black Italic
        public static TMP_FontAsset Headline { get { return Pick(t => t.headlineFont); } }   // Roboto Black
        public static TMP_FontAsset Title { get { return Pick(t => t.titleFont); } }         // Roboto Bold
        public static TMP_FontAsset Label { get { return Pick(t => t.labelFont); } }         // Inter SemiBold
        public static TMP_FontAsset Body { get { return Pick(t => t.bodyFont); } }           // Inter Regular
        public static TMP_FontAsset Data { get { return Pick(t => t.dataFont); } }           // Roboto Mono
        public static TMP_FontAsset Light { get { return Pick(t => t.lightFont); } }         // Roboto Light

        public static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }

        public static Color A(Color c, float a) { c.a = a; return c; }
    }

    /// <summary>Small factory for the settings screen's UI elements. Layout uses a 1920×1080 design frame, origin top-left.</summary>
    public static class SettingsUI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Places a rect by its top-left corner inside its parent (y grows downward).</summary>
        public static RectTransform At(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Places a rect by its top-right corner (x measured from the parent's right edge).</summary>
        public static RectTransform AtRight(RectTransform rt, float right, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Fill(Transform parent, string name, Color c, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        public static AccertoneShape Shape(Transform parent, string name, Color c, float chamfer = 0f, ChamferCorners corners = ChamferCorners.None,
            float skew = 0f, bool outline = false, float outlineWidth = 1f, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var s = rt.gameObject.AddComponent<AccertoneShape>();
            s.color = c;
            s.chamfer = chamfer;
            s.corners = corners;
            s.skew = skew;
            s.outline = outline;
            s.outlineWidth = outlineWidth;
            s.raycastTarget = raycast;
            return s;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            float spacing = 0f, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool upper = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            t.alignment = align;
            t.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        /// <summary>Wrapping paragraph text.</summary>
        public static TextMeshProUGUI Paragraph(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, float lineSpacing = 8f)
        {
            var t = Text(parent, name, text, font, size, color, 0f, TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = lineSpacing;
            return t;
        }

        /// <summary>A chevron drawn as a two-segment line (points in a 0..1 box).</summary>
        public static UIPolyline Chevron(Transform parent, string name, int direction, Color c, float size = 14f, float thickness = 2f)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var line = rt.gameObject.AddComponent<UIPolyline>();
            line.color = c;
            line.raycastTarget = false;
            line.thickness = thickness;
            Vector2[] pts;
            switch (direction)
            {
                case -1: pts = new[] { new Vector2(0.68f, 0.12f), new Vector2(0.3f, 0.5f), new Vector2(0.68f, 0.88f) }; break;     // left
                case 1: pts = new[] { new Vector2(0.32f, 0.12f), new Vector2(0.7f, 0.5f), new Vector2(0.32f, 0.88f) }; break;      // right
                case 2: pts = new[] { new Vector2(0.12f, 0.68f), new Vector2(0.5f, 0.3f), new Vector2(0.88f, 0.68f) }; break;      // down
                default: pts = new[] { new Vector2(0.2f, 0.5f), new Vector2(0.42f, 0.25f), new Vector2(0.82f, 0.75f) }; break;     // check
            }
            SetSharp(line, pts);
            return line;
        }

        public static void SetSharp(UIPolyline line, Vector2[] pts)
        {
            var f = typeof(UIPolyline).GetField("m_Smoothing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(line, 0);
            line.SetPoints(pts, false);
        }

        /// <summary>A key glyph (ESC, Q, ↑...). Controller face buttons are round.</summary>
        public static RectTransform Keycap(Transform parent, string label, bool pad = false, Color? text = null)
        {
            var rt = Rect("Keycap", parent);
            bool round = pad && label.Length == 1;
            var fill = Shape(rt, "Fill", SettingsTheme.KeycapFill, round ? 6f : 1.5f, ChamferCorners.All);
            Stretch(fill.rectTransform);
            var border = Shape(rt, "Border", SettingsTheme.A(SettingsTheme.Metallic, 0.5f), round ? 6f : 1.5f, ChamferCorners.All, 0f, true, 1f);
            Stretch(border.rectTransform);
            var t = Text(rt, "Label", label, SettingsTheme.Label, 11f, text ?? SettingsTheme.White, 3f, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            float w = Mathf.Max(round ? 22f : 24f, t.GetPreferredValues(label, 200f, 22f).x + 12f);
            rt.sizeDelta = new Vector2(w, 22f);
            return rt;
        }

        /// <summary>Diamond marker (rotated square).</summary>
        public static Image Diamond(Transform parent, string name, Color c, float size = 6f)
        {
            var img = Fill(parent, name, c);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return img;
        }

        public static float PreferredWidth(TMP_Text t)
        {
            return t.GetPreferredValues(t.text, 2000f, 100f).x;
        }

        static Texture2D s_Glow;

        /// <summary>A soft radial falloff (white, alpha only) for glows; tint it with the image colour.</summary>
        public static Texture2D RadialGlow()
        {
            if (s_Glow != null) return s_Glow;
            const int n = 128;
            s_Glow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SettingsGlow" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 1f - d;
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            s_Glow.SetPixels32(px);
            s_Glow.Apply();
            return s_Glow;
        }
    }
}
