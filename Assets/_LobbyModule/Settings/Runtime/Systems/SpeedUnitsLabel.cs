using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Accertone.Settings
{
    /// <summary>
    /// Shows a distance or speed in the player's units (Settings &gt; Interface &gt; Speed units).
    /// Put it on the number's text; <see cref="unitText"/> is the separate unit label, if any.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedUnitsLabel : MonoBehaviour
    {
        public enum Quantity { Distance, Speed }

        static readonly List<SpeedUnitsLabel> s_Active = new List<SpeedUnitsLabel>();
        static bool s_Imperial;

        public TMP_Text valueText;
        public TMP_Text unitText;
        public Quantity quantity = Quantity.Distance;
        [Tooltip("Value in kilometres (distance) or km/h (speed).")]
        public float metricValue = 5.218f;
        public int decimals = 3;

        public static void ApplyAll(bool imperial)
        {
            s_Imperial = imperial;
            foreach (var l in s_Active) l.Apply();
        }

        void OnEnable() { s_Active.Add(this); Apply(); }
        void OnDisable() { s_Active.Remove(this); }

        public void Apply()
        {
            if (valueText == null) valueText = GetComponent<TMP_Text>();
            float v = s_Imperial ? metricValue * 0.621371f : metricValue;
            if (valueText != null) valueText.text = v.ToString("F" + decimals, CultureInfo.InvariantCulture);
            if (unitText != null)
                unitText.text = quantity == Quantity.Distance ? (s_Imperial ? "MI" : "KM") : (s_Imperial ? "MPH" : "KM/H");
        }
    }
}
