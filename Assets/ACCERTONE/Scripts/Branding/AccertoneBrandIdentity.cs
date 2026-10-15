using System.Text;
using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>
    /// The single source of truth for the ACCERTONE identity.
    /// Lives in a Resources folder so any screen (main menu, loading, lobby, pause, results)
    /// can resolve it without wiring references by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Accert one/Brand Identity", fileName = "ACCERTONE_BrandIdentity")]
    public class AccertoneBrandIdentity : ScriptableObject
    {
        public const string ResourcePath = "ACCERTONE_BrandIdentity";

        /// <summary>The official game name, written exactly like this everywhere players can see it.</summary>
        public const string OfficialName = "Accert one";

        [Header("Name")]
        [Tooltip("Displayed exactly as written (case and spacing are preserved): \"Accert one\".")]
        public string gameName = OfficialName;
        [Tooltip("Characters from this index onward use the secondary wordmark tone (\"Accert\" | \" one\"). 0 disables the split.")]
        public int toneSplitIndex = 6;
        public string tagline = "FORMULA RACING";

        [Header("Official logo (optional)")]
        [Tooltip("Assign the final Accert one logo here. Every wordmark in every scene swaps the temporary typographic treatment for this artwork.")]
        public Sprite officialLogo;
        [Tooltip("Optional variant for in-world signage (rear wall). Falls back to Official Logo.")]
        public Sprite officialLogoSignage;

        [Header("Systems")]
        public AccertoneBrandPalette palette;
        public AccertoneTypography typography;

        public bool HasOfficialLogo { get { return officialLogo != null; } }
        public Sprite SignageLogo { get { return officialLogoSignage != null ? officialLogoSignage : officialLogo; } }

        static AccertoneBrandIdentity s_Default;

        public static AccertoneBrandIdentity Default
        {
            get
            {
                if (s_Default == null) s_Default = Resources.Load<AccertoneBrandIdentity>(ResourcePath);
                return s_Default;
            }
        }

        public static AccertoneBrandIdentity Resolve(AccertoneBrandIdentity overrideIdentity)
        {
            return overrideIdentity != null ? overrideIdentity : Default;
        }

        public Color GetColor(BrandColorRole role)
        {
            return palette != null ? palette.Get(role) : Color.white;
        }

        /// <summary>The name to display: exactly as authored, never re-cased.</summary>
        public string DisplayName
        {
            get { return string.IsNullOrEmpty(gameName) ? OfficialName : gameName; }
        }

        /// <summary>Wordmark as TMP rich text, with the two-tone "Accert" | " one" split.</summary>
        public string BuildRichWordmark(Color primary, Color secondary)
        {
            string name = DisplayName;
            int split = Mathf.Clamp(toneSplitIndex, 0, name.Length);
            var sb = new StringBuilder();
            if (split <= 0 || split >= name.Length)
            {
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(primary)).Append('>').Append(name).Append("</color>");
                return sb.ToString();
            }
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(primary)).Append('>').Append(name, 0, split).Append("</color>");
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(secondary)).Append('>').Append(name, split, name.Length - split).Append("</color>");
            return sb.ToString();
        }

        void OnValidate()
        {
            BrandEvents.RaiseDeferred();
        }
    }
}
