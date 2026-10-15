using System;
using System.Collections;
using Accertone.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Accertone.Settings
{
    /// <summary>
    /// Pointer input for settings elements without Unity's Selectable navigation (the settings screen runs its own
    /// keyboard / controller focus, so nothing here can steal arrow keys or the Submit button).
    /// </summary>
    public class SettingsPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action onEnter, onExit, onClick, onDoubleClick;
        public Action<PointerEventData> onDown, onUp, onDrag, onEndDrag;

        public bool Hovered { get; private set; }

        public void OnPointerEnter(PointerEventData e) { Hovered = true; if (onEnter != null) onEnter(); }
        public void OnPointerExit(PointerEventData e) { Hovered = false; if (onExit != null) onExit(); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && onDown != null) onDown(e); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && onUp != null) onUp(e); }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (e.clickCount == 2 && onDoubleClick != null) onDoubleClick();
            else if (onClick != null) onClick();
        }

        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { if (onDrag != null) onDrag(e); }
        public void OnEndDrag(PointerEventData e) { if (onEndDrag != null) onEndDrag(e); }

        void OnDisable() { Hovered = false; }
    }

    /// <summary>Unscaled-time tweens that respect Settings &gt; Interface animations.</summary>
    public static class SettingsTween
    {
        public static Coroutine Run(MonoBehaviour host, float seconds, Action<float> step, Action done = null)
        {
            if (host == null || !host.isActiveAndEnabled) { step(1f); if (done != null) done(); return null; }
            return host.StartCoroutine(Routine(seconds, step, done));
        }

        static IEnumerator Routine(float seconds, Action<float> step, Action done)
        {
            float d = AccertoneMotion.Duration(seconds);
            if (d <= 0.01f) { step(1f); if (done != null) done(); yield break; }
            float t0 = Time.unscaledTime;
            while (true)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / d);
                step(AccertoneEase.OutCubic(k));
                if (k >= 1f) break;
                yield return null;
            }
            if (done != null) done();
        }

        /// <summary>Exponential approach factor for per-frame smoothing (instant when animations are off).</summary>
        public static float Approach(float sharpness)
        {
            float s = AccertoneMotion.Scale;
            if (s <= 0.01f) return 1f;
            return 1f - Mathf.Exp(-sharpness / s * Time.unscaledDeltaTime);
        }
    }
}
