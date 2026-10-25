using System.Collections;
using UnityEngine;

namespace Player.Equipment
{
    /// <summary>
    /// Applies a temporary emission-based highlight to a dropped item and fades it out smoothly.
    /// Attach to the same GameObject as <see cref="Item"/>; call <see cref="Play"/> whenever the
    /// item lands in the world so players can spot loot quickly.
    /// </summary>
    [DisallowMultipleComponent]
    public class DroppedItemHighlight : MonoBehaviour
    {
        [Header("Highlight Settings")]
        [Tooltip("Color of the emission glow when the item is first dropped.")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("Emission intensity multiplier applied to the highlight color (HDR brightness).")]
        [SerializeField, Min(0f)] private float emissionIntensity = 2.5f;

        [Tooltip("Seconds the full-brightness highlight is held before the fade begins.")]
        [SerializeField, Min(0f)] private float holdDuration = 2f;

        [Tooltip("Duration of the smooth fade-out in seconds.")]
        [SerializeField, Min(0f)] private float fadeDuration = 1.5f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Renderer[] renderers;
        private MaterialPropertyBlock mpb;
        private Coroutine highlightCoroutine;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            mpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Triggers the highlight sequence: full glow for <see cref="holdDuration"/> seconds,
        /// then a smooth fade to zero emission over <see cref="fadeDuration"/> seconds.
        /// Safe to call multiple times — restarts the sequence from full brightness.
        /// </summary>
        public void Play()
        {
            if (highlightCoroutine != null)
            {
                StopCoroutine(highlightCoroutine);
            }
            highlightCoroutine = StartCoroutine(HighlightRoutine());
        }

        /// <summary>
        /// Immediately cancels the highlight and clears the emission property block.
        /// </summary>
        public void Stop()
        {
            if (highlightCoroutine != null)
            {
                StopCoroutine(highlightCoroutine);
                highlightCoroutine = null;
            }
            ClearEmission();
        }

        private IEnumerator HighlightRoutine()
        {
            Color peakColor = highlightColor * emissionIntensity;
            ApplyEmission(peakColor);

            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            // Smooth fade-out
            if (fadeDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    ApplyEmission(Color.Lerp(peakColor, Color.black, t));
                    yield return null;
                }
            }

            ClearEmission();
            highlightCoroutine = null;
        }

        private void ApplyEmission(Color color)
        {
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorId, color);
                rend.SetPropertyBlock(mpb);
            }
        }

        private void ClearEmission()
        {
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorId, Color.black);
                rend.SetPropertyBlock(mpb);
            }
        }

        private void OnDisable()
        {
            // Silently cancel if the GameObject is deactivated (e.g. item picked up)
            if (highlightCoroutine != null)
            {
                StopCoroutine(highlightCoroutine);
                highlightCoroutine = null;
            }
        }
    }
}
