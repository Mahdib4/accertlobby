using System;
using Accertone.CarSetup;
using UnityEngine;

namespace Accertone.Garage
{
    public enum PaintFinish { Gloss = 0, Metallic = 1, Satin = 2, Matte = 3 }
    public enum LetteringMode { Auto = 0, White = 1, Dark = 2 }
    public enum SidewallMarking { Compound = 0, Team = 1 }

    /// <summary>
    /// How the car looks: livery colours and finish, rims and tyre sidewall markings. Kept apart from the
    /// performance setup (<see cref="F1CarSetupData"/>) and saved to its own file.
    /// </summary>
    [Serializable]
    public class GarageAppearance
    {
        public int version = 1;
        public string schemeId = GarageCatalog.FactorySchemeId;
        public Color primary = GarageCatalog.SourceBody;
        public Color accentA = GarageCatalog.SourceViolet;
        public Color accentB = GarageCatalog.SourceMagenta;
        public LetteringMode lettering = LetteringMode.Auto;
        public PaintFinish finish = PaintFinish.Gloss;
        /// <summary>Strength of the livery's glowing accents (1 = factory).</summary>
        public float glow = 1f;
        public string rimId = GarageCatalog.FactoryRimId;
        public SidewallMarking sidewall = SidewallMarking.Compound;

        public static GarageAppearance Factory()
        {
            return new GarageAppearance();
        }

        public GarageAppearance Clone()
        {
            return JsonUtility.FromJson<GarageAppearance>(JsonUtility.ToJson(this));
        }

        /// <summary>True when the livery colours are the ones baked into the source textures (no recolour needed).</summary>
        public bool UsesSourceLivery
        {
            get
            {
                return Near(primary, GarageCatalog.SourceBody) && Near(accentA, GarageCatalog.SourceViolet) &&
                       Near(accentB, GarageCatalog.SourceMagenta) && ResolvedLettering() == LetteringMode.White;
            }
        }

        /// <summary>Lettering colour actually used: Auto picks dark lettering on light paint.</summary>
        public LetteringMode ResolvedLettering()
        {
            if (lettering != LetteringMode.Auto) return lettering;
            return Luma(primary) > 0.55f ? LetteringMode.Dark : LetteringMode.White;
        }

        public bool SameAs(GarageAppearance other)
        {
            if (other == null) return false;
            return schemeId == other.schemeId && Near(primary, other.primary) && Near(accentA, other.accentA) &&
                   Near(accentB, other.accentB) && lettering == other.lettering && finish == other.finish && Mathf.Abs(glow - other.glow) < 0.005f &&
                   rimId == other.rimId && sidewall == other.sidewall;
        }

        public void Validate()
        {
            primary.a = accentA.a = accentB.a = 1f;
            if (string.IsNullOrEmpty(schemeId)) schemeId = GarageCatalog.CustomSchemeId;
            if (GarageCatalog.FindRim(rimId) == null) rimId = GarageCatalog.FactoryRimId;
            if (!Enum.IsDefined(typeof(PaintFinish), finish)) finish = PaintFinish.Gloss;
            if (!Enum.IsDefined(typeof(LetteringMode), lettering)) lettering = LetteringMode.Auto;
            if (!Enum.IsDefined(typeof(SidewallMarking), sidewall)) sidewall = SidewallMarking.Compound;
            glow = Mathf.Clamp(glow, 0f, 1.6f);
        }

        public static float Luma(Color c)
        {
            return c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
        }

        static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }
    }

    /// <summary>A named, curated colour scheme for the livery.</summary>
    public class LiveryScheme
    {
        public string id, name, note;
        public Color primary, accentA, accentB;
        public PaintFinish finish;
    }

    public class SwatchColor
    {
        public string name;
        public Color color;
        public Color colorB; // second stop for accent ramps
    }

    public class RimStyle
    {
        public string id, name;
        public bool source;        // keep the original rim texture
        public bool matchLivery;   // follow the livery's primary / accent
        public Color primary, accent;
        public float metallic, smoothness;
    }

    /// <summary>The curated options shown in the Garage. All colours are sRGB (gamma) values.</summary>
    public static class GarageCatalog
    {
        public const string FactorySchemeId = "works";
        public const string CustomSchemeId = "custom";
        public const string FactoryRimId = "works";

        // The palette the source livery was baked with (Editor/AccertoneLiveryDesigns.cs).
        public static readonly Color SourceBody = Hex(0x141218);
        public static readonly Color SourceViolet = Hex(0x7C3AFF);
        public static readonly Color SourceMagenta = Hex(0xE840D6);
        public static readonly Color DarkLettering = Hex(0x15171C);
        public static readonly Color WhiteLettering = Hex(0xF3F5F8);

        public static readonly LiveryScheme[] Schemes =
        {
            Scheme(FactorySchemeId, "WORKS", "Factory graphite with the violet flare", 0x141218, 0x7C3AFF, 0xE840D6, PaintFinish.Gloss),
            Scheme("midnight", "MIDNIGHT", "Deep navy with electric cyan", 0x0B1222, 0x2E6BFF, 0x19D3FF, PaintFinish.Metallic),
            Scheme("rosso", "ROSSO", "Racing red with solar gold", 0xA8101C, 0xFFB21F, 0xFFE07A, PaintFinish.Gloss),
            Scheme("papaya", "PAPAYA", "Papaya orange with ocean blue", 0xFF7417, 0x1747D6, 0x2F9BFF, PaintFinish.Gloss),
            Scheme("arctic", "ARCTIC", "Arctic white with cobalt", 0xE7EAF0, 0x1B3FA8, 0x3D7BFF, PaintFinish.Satin),
            Scheme("heritage", "HERITAGE", "Racing green with brushed gold", 0x0C3A2C, 0xB8902A, 0xF0D27A, PaintFinish.Metallic),
            Scheme("stealth", "STEALTH", "Obsidian with graphite steel", 0x0A0A0D, 0x3B3F48, 0x8F96A2, PaintFinish.Matte),
            Scheme("volt", "VOLT", "Carbon black with acid volt", 0x111317, 0x6FE000, 0xD9FF3D, PaintFinish.Satin),
        };

        public static readonly SwatchColor[] Primaries =
        {
            Swatch("GRAPHITE", 0x141218), Swatch("OBSIDIAN", 0x0A0A0D), Swatch("MIDNIGHT", 0x0B1222), Swatch("GUNMETAL", 0x2D3138),
            Swatch("SILVER", 0xBFC5CE), Swatch("ARCTIC", 0xE7EAF0), Swatch("ROSSO", 0xA8101C), Swatch("PAPAYA", 0xFF7417),
            Swatch("SOLAR", 0xFFC21A), Swatch("RACING GREEN", 0x0C3A2C), Swatch("OCEAN", 0x0D4A92), Swatch("ROYAL VIOLET", 0x4C1D9E),
        };

        public static readonly SwatchColor[] Accents =
        {
            Ramp("VIOLET FLARE", 0x7C3AFF, 0xE840D6), Ramp("ELECTRIC", 0x2E6BFF, 0x19D3FF), Ramp("SOLAR GOLD", 0xFFB21F, 0xFFE07A),
            Ramp("EMBER", 0xFF4A1F, 0xFF9A3D), Ramp("VOLT", 0x6FE000, 0xD9FF3D), Ramp("PEARL", 0xC9CFD8, 0xFFFFFF),
            Ramp("TEAL", 0x00A88F, 0x3DF0D0), Ramp("CRIMSON", 0xC3102A, 0xFF4F7A), Ramp("COBALT", 0x1B3FA8, 0x3D7BFF),
            Ramp("STEEL", 0x3B3F48, 0x8F96A2),
        };

        public static readonly RimStyle[] Rims =
        {
            new RimStyle { id = FactoryRimId, name = "WORKS", source = true, metallic = 0.88f, smoothness = 0.62f },
            Rim("gunmetal", "GUNMETAL", 0x3A3F47, 0x8F96A2, 0.9f, 0.55f),
            Rim("silver", "SILVER", 0xC9CED6, 0x2A2D33, 0.95f, 0.74f),
            Rim("gold", "GOLD", 0xC9A24B, 0x15171C, 0.95f, 0.7f),
            Rim("blackchrome", "BLACK CHROME", 0x101115, 0x8F96A2, 0.96f, 0.86f),
            new RimStyle { id = "livery", name = "LIVERY MATCH", matchLivery = true, metallic = 0.6f, smoothness = 0.6f },
        };

        public static LiveryScheme FindScheme(string id)
        {
            foreach (var s in Schemes) if (s.id == id) return s;
            return null;
        }

        public static RimStyle FindRim(string id)
        {
            foreach (var r in Rims) if (r.id == id) return r;
            return null;
        }

        /// <summary>Sidewall marking colour for a compound (the usual F1 code: soft red, medium yellow, hard white...).</summary>
        public static Color CompoundColor(TireCompound compound)
        {
            switch (compound)
            {
                case TireCompound.C3_Soft: return Hex(0xE8322E);
                case TireCompound.C2_Medium: return Hex(0xF5C518);
                case TireCompound.C1_Hard: return Hex(0xF2F3F5);
                case TireCompound.Intermediate: return Hex(0x2BB673);
                case TireCompound.Wet: return Hex(0x2F7DE1);
            }
            return SourceMagenta;
        }

        public static string CompoundName(TireCompound compound)
        {
            switch (compound)
            {
                case TireCompound.C3_Soft: return "SOFT";
                case TireCompound.C2_Medium: return "MEDIUM";
                case TireCompound.C1_Hard: return "HARD";
                case TireCompound.Intermediate: return "INTERMEDIATE";
                case TireCompound.Wet: return "WET";
            }
            return compound.ToString().ToUpperInvariant();
        }

        public static string FinishName(PaintFinish f)
        {
            return f.ToString().ToUpperInvariant();
        }

        public static void ApplyScheme(GarageAppearance a, LiveryScheme s)
        {
            a.schemeId = s.id;
            a.primary = s.primary;
            a.accentA = s.accentA;
            a.accentB = s.accentB;
            a.finish = s.finish;
        }

        static LiveryScheme Scheme(string id, string name, string note, int body, int a, int b, PaintFinish finish)
        {
            return new LiveryScheme { id = id, name = name, note = note, primary = Hex(body), accentA = Hex(a), accentB = Hex(b), finish = finish };
        }

        static SwatchColor Swatch(string name, int rgb)
        {
            var c = Hex(rgb);
            return new SwatchColor { name = name, color = c, colorB = c };
        }

        static SwatchColor Ramp(string name, int a, int b)
        {
            return new SwatchColor { name = name, color = Hex(a), colorB = Hex(b) };
        }

        static RimStyle Rim(string id, string name, int primary, int accent, float metallic, float smoothness)
        {
            return new RimStyle { id = id, name = name, primary = Hex(primary), accent = Hex(accent), metallic = metallic, smoothness = smoothness };
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
