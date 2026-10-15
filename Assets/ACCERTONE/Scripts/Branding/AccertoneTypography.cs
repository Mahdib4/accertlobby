using System;
using TMPro;
using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>Typographic roles. Precision + speed + technology.</summary>
    public enum BrandTextRole
    {
        Wordmark,   // the "Accert one" name (temporary typographic logo)
        Display,    // hero actions such as RACE
        Headline,   // track / screen titles
        Title,      // player names, card titles
        Label,      // navigation, section labels
        Body,       // descriptive copy
        Caption,    // small spaced labels
        Data,       // telemetry values (monospaced)
        DataLarge,  // large numerals
        Tagline     // brand tagline under the wordmark
    }

    [Serializable]
    public class BrandTextStyleSpec
    {
        public BrandTextRole role;
        public TMP_FontAsset font;
        public float size = 16f;
        [Tooltip("TextMeshPro character spacing (em/100).")]
        public float characterSpacing;
        public bool uppercase = true;
        public FontStyles fontStyle = FontStyles.Normal;
        public float lineSpacing;
    }

    [CreateAssetMenu(menuName = "Accert one/Typography", fileName = "ACCERTONE_Typography")]
    public class AccertoneTypography : ScriptableObject
    {
        [Header("Font families")]
        [Tooltip("Roboto Black Italic: wordmark and hero actions.")]
        public TMP_FontAsset displayFont;
        [Tooltip("Roboto Black: headlines.")]
        public TMP_FontAsset headlineFont;
        [Tooltip("Roboto Bold: titles.")]
        public TMP_FontAsset titleFont;
        [Tooltip("Inter SemiBold: navigation and labels.")]
        public TMP_FontAsset labelFont;
        [Tooltip("Inter Regular: body copy.")]
        public TMP_FontAsset bodyFont;
        [Tooltip("Roboto Mono: telemetry and timing data.")]
        public TMP_FontAsset dataFont;
        [Tooltip("Roboto Light: large numerals.")]
        public TMP_FontAsset lightFont;

        [Header("Role styles")]
        public BrandTextStyleSpec[] styles = new BrandTextStyleSpec[0];

        public BrandTextStyleSpec Get(BrandTextRole role)
        {
            if (styles == null) return null;
            for (int i = 0; i < styles.Length; i++)
                if (styles[i] != null && styles[i].role == role) return styles[i];
            return null;
        }

        void OnValidate()
        {
            BrandEvents.RaiseDeferred();
        }
    }
}
