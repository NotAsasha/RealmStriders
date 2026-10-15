using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Base.SellPoint
{
    /// <summary>
    /// Controls the procedural CRT TV shutdown screen visual effect.
    /// Manages the shader's _Progress property via MaterialPropertyBlock to avoid runtime material allocations.
    /// </summary>
    public class CRTSellScreen : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private MeshRenderer screenRenderer;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip crtOffSound;

        [Header("Animation Settings")]
        [SerializeField, Range(0.2f, 1.0f)] private float sequenceDuration = 0.4f;
        [SerializeField, Range(0.1f, 0.9f)] private float collapseProgressThreshold = 0.6f;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int ProgressPropertyId = Shader.PropertyToID("_Progress");
        private Coroutine currentSequence;
        private bool isPlayingSequence;

        public bool IsPlayingSequence => isPlayingSequence;
        public float SequenceDuration => sequenceDuration;

        private void Awake()
        {
            if (screenRenderer == null)
            {
                screenRenderer = GetComponent<MeshRenderer>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            propertyBlock = new MaterialPropertyBlock();
            SetProgress(0f);
        }

        private void OnDisable()
        {
            if (currentSequence != null)
            {
                StopCoroutine(currentSequence);
                currentSequence = null;
            }

            isPlayingSequence = false;
            SetProgress(0f);
        }

        /// <summary>
        /// Plays the CRT TV shutdown sequence.
        /// Visually hides the sold items at the moment of line collapse (~0.6 progress).
        /// </summary>
        /// <param name="soldItems">List of GameObjects being sold to visually hide during collapse.</param>
        /// <param name="onComplete">Optional callback invoked after the CRT screen returns to transparency.</param>
        public void PlaySellSequence(List<GameObject> soldItems, Action onComplete = null)
        {
            if (currentSequence != null)
            {
                StopCoroutine(currentSequence);
            }

            currentSequence = StartCoroutine(SellSequenceRoutine(soldItems, onComplete));
        }

        private IEnumerator SellSequenceRoutine(List<GameObject> soldItems, Action onComplete)
        {
            isPlayingSequence = true;

            if (audioSource != null && crtOffSound != null)
            {
                audioSource.PlayOneShot(crtOffSound);
            }

            float elapsed = 0f;
            bool itemsHidden = false;

            while (elapsed < sequenceDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / sequenceDuration);
                SetProgress(progress);

                if (!itemsHidden && progress >= collapseProgressThreshold)
                {
                    itemsHidden = true;
                    HideSoldItems(soldItems);
                }

                yield return null;
            }

            if (!itemsHidden)
            {
                HideSoldItems(soldItems);
            }

            // Return to completely transparent state (progress = 0 causes shader to discard)
            SetProgress(0f);

            isPlayingSequence = false;
            currentSequence = null;

            onComplete?.Invoke();
        }

        private void HideSoldItems(List<GameObject> items)
        {
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var itemObj = items[i];
                if (itemObj == null) continue;

                // Disable renderers locally so objects disappear cleanly at the collapse line
                // without destroying components or disrupting networking before server despawns them.
                var renderers = itemObj.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] != null)
                    {
                        renderers[r].enabled = false;
                    }
                }
            }
        }

        private void SetProgress(float value)
        {
            if (screenRenderer == null) return;

            screenRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ProgressPropertyId, value);
            screenRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}

