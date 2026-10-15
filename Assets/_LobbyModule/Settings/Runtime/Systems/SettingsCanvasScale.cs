using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Settings
{
    /// <summary>
    /// Lets Settings &gt; Display &gt; Interface scale resize a canvas. Add it next to a <see cref="CanvasScaler"/>
    /// (Scale With Screen Size); the scaler's reference resolution is divided by the chosen scale.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [DisallowMultipleComponent]
    public class SettingsCanvasScale : MonoBehaviour
    {
        static readonly List<SettingsCanvasScale> s_Active = new List<SettingsCanvasScale>();
        static float s_Scale = 1f;

        CanvasScaler m_Scaler;
        Vector2 m_BaseReference;
        bool m_HasBase;

        public static void ApplyAll(float scale)
        {
            s_Scale = Mathf.Clamp(scale, 0.5f, 2f);
            foreach (var c in s_Active) c.Apply();
        }

        void OnEnable()
        {
            if (m_Scaler == null) m_Scaler = GetComponent<CanvasScaler>();
            if (!m_HasBase) { m_BaseReference = m_Scaler.referenceResolution; m_HasBase = true; }
            s_Active.Add(this);
            Apply();
        }

        void OnDisable()
        {
            s_Active.Remove(this);
            if (m_HasBase && m_Scaler != null) m_Scaler.referenceResolution = m_BaseReference;
        }

        void Apply()
        {
            if (m_Scaler != null && m_HasBase) m_Scaler.referenceResolution = m_BaseReference / s_Scale;
        }
    }
}
