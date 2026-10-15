using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>
    /// Semantic colour roles. UI and environment code asks for a role, never a raw colour,
    /// so the whole game can be re-skinned from one palette asset.
    /// </summary>
    public enum BrandColorRole
    {
        Graphite,       // deepest background / void
        Carbon,         // panel base
        Panel,          // raised surface
        Hairline,       // dividers, borders, inactive segments
        Metallic,       // brushed-metal grey, secondary marks
        Silver,         // light chrome, secondary wordmark tone
        White,          // primary type and marks
        Accent,         // ACCERTONE Signal Red: selection, primary action, LEDs. Use sparingly.
        AccentDeep,     // pressed / shadowed accent
        Positive,       // functional status only (online, ready)
        TextPrimary,
        TextSecondary,
        TextMuted
    }

    [CreateAssetMenu(menuName = "Accert one/Brand Palette", fileName = "ACCERTONE_BrandPalette")]
    public class AccertoneBrandPalette : ScriptableObject
    {
        [Header("Neutrals")]
        public Color graphite = Hex(0x0A0B0E);
        public Color carbon = Hex(0x111318);
        public Color panel = Hex(0x1A1D23);
        public Color hairline = Hex(0x2C313A);
        public Color metallic = Hex(0x8A919C);
        public Color silver = Hex(0xC4C9D1);
        public Color white = Hex(0xF3F5F8);

        [Header("Accent (one controlled accent)")]
        public Color accent = Hex(0xC042FF);     // ACCERTONE violet-magenta (from the AC-26 livery)
        public Color accentDeep = Hex(0x6B1FB0);

        [Header("Functional")]
        public Color positive = Hex(0x3FD69A);

        [Header("Type")]
        public Color textPrimary = Hex(0xF3F5F8);
        public Color textSecondary = Hex(0xA9AFB9);
        public Color textMuted = Hex(0x6B717C);

        public Color Get(BrandColorRole role)
        {
            switch (role)
            {
                case BrandColorRole.Graphite: return graphite;
                case BrandColorRole.Carbon: return carbon;
                case BrandColorRole.Panel: return panel;
                case BrandColorRole.Hairline: return hairline;
                case BrandColorRole.Metallic: return metallic;
                case BrandColorRole.Silver: return silver;
                case BrandColorRole.White: return white;
                case BrandColorRole.Accent: return accent;
                case BrandColorRole.AccentDeep: return accentDeep;
                case BrandColorRole.Positive: return positive;
                case BrandColorRole.TextPrimary: return textPrimary;
                case BrandColorRole.TextSecondary: return textSecondary;
                case BrandColorRole.TextMuted: return textMuted;
                default: return white;
            }
        }

        public static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        void OnValidate()
        {
            BrandEvents.RaiseDeferred();
        }
    }
}
