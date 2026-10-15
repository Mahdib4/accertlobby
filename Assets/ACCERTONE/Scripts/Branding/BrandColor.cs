using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Branding
{
    /// <summary>Binds any UI Graphic (Image, TMP, AccertoneShape...) to a palette role.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BrandColor : MonoBehaviour
    {
        public BrandColorRole role = BrandColorRole.White;
        [Range(0f, 1f)] public float alpha = 1f;
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
            if (id == null || id.palette == null) return;
            var g = GetComponent<Graphic>();
            if (g == null) return;
            var c = id.palette.Get(role);
            c.a *= alpha;
            g.color = c;
        }
    }
}
