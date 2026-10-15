using TMPro;
using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>Applies an ACCERTONE typographic role (font, size, tracking, case, colour) to a TMP text.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BrandTextStyle : MonoBehaviour
    {
        public BrandTextRole role = BrandTextRole.Body;
        public bool applyColor = true;
        public BrandColorRole color = BrandColorRole.TextPrimary;
        [Range(0f, 1f)] public float alpha = 1f;
        [Tooltip("0 uses the role's default size.")]
        public float sizeOverride;
        [Tooltip("Added to the role's character spacing.")]
        public float extraSpacing;
        public AccertoneBrandIdentity identityOverride;

        void OnEnable()
        {
            BrandEvents.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            BrandEvents.Changed -= Apply;
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif
        }

        public void Apply()
        {
            var id = AccertoneBrandIdentity.Resolve(identityOverride);
            var text = GetComponent<TMP_Text>();
            if (id == null || text == null) return;

            if (id.typography != null)
            {
                var spec = id.typography.Get(role);
                if (spec != null)
                {
                    if (spec.font != null && text.font != spec.font) text.font = spec.font;
                    text.fontSize = sizeOverride > 0f ? sizeOverride : spec.size;
                    text.characterSpacing = spec.characterSpacing + extraSpacing;
                    text.lineSpacing = spec.lineSpacing;
                    var style = spec.fontStyle;
                    if (spec.uppercase) style |= FontStyles.UpperCase;
                    text.fontStyle = style;
                }
            }

            if (applyColor && id.palette != null)
            {
                var c = id.palette.Get(color);
                c.a *= alpha;
                text.color = c;
            }
        }
    }
}
