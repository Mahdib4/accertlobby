using TMPro;
using UnityEngine;

namespace Accertone.Lobby
{
    /// <summary>
    /// A light or emissive surface that takes part in the showroom power-up sequence.
    /// Drives Light intensity, a renderer colour property (via MaterialPropertyBlock, never the shared asset)
    /// and/or TMP alpha.
    /// </summary>
    public class ShowroomPoweredElement : MonoBehaviour
    {
        [Tooltip("Power-up order. Lower groups switch on first.")]
        public int group;
        public bool flicker;
        [Tooltip("Renderer colour property to scale, e.g. _EmissionColor (URP Lit) or _Color (ACCERTONE glow shaders).")]
        public string colorProperty = "_EmissionColor";
        [Header("Idle pulse")]
        public float pulseAmplitude;
        public float pulseSpeed = 0.35f;

        Light m_Light;
        float m_LightBase;
        Renderer m_Renderer;
        Color m_ColorBase;
        bool m_HasColor;
        TMP_Text m_Text;
        float m_TextBase;
        MaterialPropertyBlock m_Block;
        int m_PropertyId;
        float m_Phase;
        bool m_Initialised;

        void Awake()
        {
            Init();
        }

        void Init()
        {
            if (m_Initialised) return;
            m_Initialised = true;
            m_Phase = Random.value * 10f;
            m_Light = GetComponent<Light>();
            if (m_Light != null) m_LightBase = m_Light.intensity;

            m_Text = GetComponent<TMP_Text>();
            if (m_Text != null) m_TextBase = m_Text.alpha;

            m_Renderer = m_Text == null ? GetComponent<Renderer>() : null;
            if (m_Renderer != null && m_Renderer.sharedMaterial != null && !string.IsNullOrEmpty(colorProperty))
            {
                m_PropertyId = Shader.PropertyToID(colorProperty);
                if (m_Renderer.sharedMaterial.HasProperty(m_PropertyId))
                {
                    m_ColorBase = m_Renderer.sharedMaterial.GetColor(m_PropertyId);
                    m_HasColor = true;
                    m_Block = new MaterialPropertyBlock();
                }
            }
        }

        public void SetLevel(float level)
        {
            Init();
            if (pulseAmplitude > 0f)
                level *= 1f + pulseAmplitude * Mathf.Sin((Time.unscaledTime * pulseSpeed + m_Phase) * Mathf.PI * 2f);

            if (m_Light != null) m_Light.intensity = m_LightBase * level;
            if (m_Text != null) m_Text.alpha = m_TextBase * Mathf.Clamp01(level);
            if (m_HasColor)
            {
                // SetVector avoids a second gamma conversion: write the linear colour directly.
                Color lin = m_ColorBase.linear * level;
                m_Renderer.GetPropertyBlock(m_Block);
                m_Block.SetVector(m_PropertyId, new Vector4(lin.r, lin.g, lin.b, m_ColorBase.a));
                m_Renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
