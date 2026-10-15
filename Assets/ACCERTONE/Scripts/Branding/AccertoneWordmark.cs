using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Branding
{
    /// <summary>
    /// Reusable ACCERTONE wordmark for any UI screen (main menu, loading, lobby, pause, results).
    ///
    /// TEMPORARY TREATMENT: until an official logo exists, the mark is built from TextMeshPro
    /// plus two forward-leaning accent slashes. Assign a sprite to
    /// AccertoneBrandIdentity.officialLogo and every instance swaps to the final artwork automatically.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AccertoneWordmark : MonoBehaviour
    {
        public AccertoneBrandIdentity identityOverride;

        [Header("Parts")]
        public RectTransform markRoot;
        public Graphic[] markSlashes = new Graphic[0];
        public RectMask2D typeMask;
        public TMP_Text typeText;
        public RectTransform taglineRoot;
        public Graphic taglineRule;
        public TMP_Text taglineText;
        public Image officialLogoImage;

        [Header("Options")]
        public bool showMark = true;
        public bool showTagline = true;
        public BrandColorRole primaryTone = BrandColorRole.White;
        public BrandColorRole secondaryTone = BrandColorRole.Silver;
        public BrandColorRole markTone = BrandColorRole.Accent;
        [Tooltip("Alpha of each slash in order (the trailing slash is the quieter 'echo').")]
        public float[] slashAlpha = { 1f, 0.42f };

        void OnEnable()
        {
            BrandEvents.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            BrandEvents.Changed -= Refresh;
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Refresh(); };
#endif
        }

        public AccertoneBrandIdentity Identity
        {
            get { return AccertoneBrandIdentity.Resolve(identityOverride); }
        }

        public void Refresh()
        {
            var id = Identity;
            if (id == null) return;

            bool logo = id.HasOfficialLogo;
            if (officialLogoImage != null)
            {
                officialLogoImage.gameObject.SetActive(logo);
                if (logo)
                {
                    officialLogoImage.sprite = id.officialLogo;
                    officialLogoImage.preserveAspect = true;
                }
            }
            if (markRoot != null) markRoot.gameObject.SetActive(!logo && showMark);
            if (typeMask != null) typeMask.gameObject.SetActive(!logo);
            if (taglineRoot != null) taglineRoot.gameObject.SetActive(showTagline);

            if (id.palette != null)
            {
                if (typeText != null)
                {
                    typeText.color = Color.white;
                    typeText.text = id.BuildRichWordmark(id.palette.Get(primaryTone), id.palette.Get(secondaryTone));
                }
                for (int i = 0; i < markSlashes.Length; i++)
                {
                    if (markSlashes[i] == null) continue;
                    var c = id.palette.Get(markTone);
                    c.a = i < slashAlpha.Length ? slashAlpha[i] : 1f;
                    markSlashes[i].color = c;
                }
            }
            if (taglineText != null) taglineText.text = id.tagline;
        }
    }
}
