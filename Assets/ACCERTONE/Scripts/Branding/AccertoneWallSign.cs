using TMPro;
using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>
    /// Dimensional, halo-lit ACCERTONE signage for the showroom rear wall.
    /// Face + stacked depth layers give the letters physical thickness; a soft underlay
    /// layer sitting on the wall reads as controlled backlighting.
    /// If an official signage logo is assigned in the brand identity it replaces the lettering.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AccertoneWallSign : MonoBehaviour
    {
        public AccertoneBrandIdentity identityOverride;

        [Header("Lettering")]
        public TMP_Text face;
        public TMP_Text[] depthLayers = new TMP_Text[0];
        public TMP_Text haloLayer;
        public GameObject markRoot;
        public GameObject lettersRoot;

        [Header("Official logo")]
        public SpriteRenderer logoRenderer;

        [Header("Tones")]
        public BrandColorRole primaryTone = BrandColorRole.White;
        public BrandColorRole secondaryTone = BrandColorRole.Silver;
        public Color depthColor = new Color(0.16f, 0.17f, 0.2f, 1f);

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

        public void Refresh()
        {
            var id = AccertoneBrandIdentity.Resolve(identityOverride);
            if (id == null) return;

            var logo = id.SignageLogo;
            bool useLogo = logo != null && logoRenderer != null;
            if (logoRenderer != null)
            {
                logoRenderer.gameObject.SetActive(useLogo);
                if (useLogo) logoRenderer.sprite = logo;
            }
            if (lettersRoot != null) lettersRoot.SetActive(!useLogo);
            if (markRoot != null) markRoot.SetActive(!useLogo);
            if (useLogo || id.palette == null) return;

            string rich = id.BuildRichWordmark(id.palette.Get(primaryTone), id.palette.Get(secondaryTone));
            string plain = id.DisplayName;

            if (face != null)
            {
                face.color = Color.white;
                face.text = rich;
            }
            for (int i = 0; i < depthLayers.Length; i++)
            {
                if (depthLayers[i] == null) continue;
                depthLayers[i].text = plain;
                depthLayers[i].color = depthColor;
            }
            if (haloLayer != null) haloLayer.text = plain;
        }
    }
}
