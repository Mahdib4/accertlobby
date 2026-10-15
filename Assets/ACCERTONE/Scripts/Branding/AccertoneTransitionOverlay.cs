using System;
using System.Collections;
using Accertone.UI;
using TMPro;
using UnityEngine;

namespace Accertone.Branding
{
    /// <summary>
    /// Full-screen ACCERTONE transition (race launch, loading, returning to lobby).
    /// Drop the prefab into any scene and call Play().
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class AccertoneTransitionOverlay : MonoBehaviour
    {
        public AccertoneWordmarkReveal wordmarkReveal;
        public TMP_Text statusText;
        public RectTransform progressFill;
        public float fadeIn = 0.45f;
        public float fadeOut = 0.6f;

        CanvasGroup m_Group;

        public bool IsBusy { get; private set; }

        void Awake()
        {
            m_Group = GetComponent<CanvasGroup>();
            Hide();
        }

        public void Hide()
        {
            if (m_Group == null) m_Group = GetComponent<CanvasGroup>();
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = false;
            m_Group.interactable = false;
            SetProgress(0f);
        }

        /// <summary>Covers the screen, runs the progress bar for holdSeconds, then calls onCovered.</summary>
        public void Play(string status, float holdSeconds, Action onCovered, bool revealAfter)
        {
            if (IsBusy) return;
            StartCoroutine(Run(status, holdSeconds, onCovered, revealAfter));
        }

        IEnumerator Run(string status, float hold, Action onCovered, bool revealAfter)
        {
            IsBusy = true;
            m_Group.blocksRaycasts = true;
            if (statusText != null) statusText.text = status;
            SetProgress(0f);

            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                m_Group.alpha = AccertoneEase.OutCubic(t / fadeIn);
                yield return null;
            }
            m_Group.alpha = 1f;
            if (wordmarkReveal != null) wordmarkReveal.Play();

            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime)
            {
                SetProgress(AccertoneEase.InOutCubic(t / hold));
                yield return null;
            }
            SetProgress(1f);

            if (onCovered != null) onCovered();

            if (revealAfter)
            {
                for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
                {
                    m_Group.alpha = 1f - AccertoneEase.OutCubic(t / fadeOut);
                    yield return null;
                }
                Hide();
            }
            IsBusy = false;
        }

        void SetProgress(float p)
        {
            if (progressFill == null) return;
            var max = progressFill.anchorMax;
            max.x = Mathf.Clamp01(p);
            progressFill.anchorMax = max;
        }
    }
}
