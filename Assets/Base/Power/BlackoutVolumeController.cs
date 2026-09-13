using System.Collections;
using Base.BaseUpgrader;
using UnityEngine;
using UnityEngine.Rendering;

namespace Base
{
    [DisallowMultipleComponent]
    public class BlackoutVolumeController : MonoBehaviour
    {
        [Header("Grid & Volume References")]
        [SerializeField] private PowerGrid powerGrid;
        [SerializeField] private Volume blackoutVolume;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip powerDownClip;
        [SerializeField] private AudioClip powerRestoreClip;
        [SerializeField] private AudioSource loopHumSource;

        [Header("Transition Settings")]
        [SerializeField] private float fadeDuration = 0.35f;

        private Coroutine fadeCoroutine;
        private bool isLocalPlayerInsideBase = true;
        private bool isCurrentlyOverloaded = false;

        private void Awake()
        {
            if (blackoutVolume != null)
            {
                blackoutVolume.weight = 0f;
            }

            if (audioSource == null) TryGetComponent(out audioSource);
        }

        private void OnEnable()
        {
            if (powerGrid != null)
            {
                powerGrid.IsGridOverloaded.OnValueChanged += HandleOverloadStateChanged;
            }
        }

        private void OnDisable()
        {
            if (powerGrid != null)
            {
                powerGrid.IsGridOverloaded.OnValueChanged -= HandleOverloadStateChanged;
            }
        }

        private void HandleOverloadStateChanged(bool last, bool isOverloaded)
        {
            isCurrentlyOverloaded = isOverloaded;
            if (last != isOverloaded) EvaluateBlackoutState();
        }

        private void EvaluateBlackoutState()
        {
            bool shouldBeBlackout = isCurrentlyOverloaded && isLocalPlayerInsideBase;
            float targetWeight = shouldBeBlackout ? 1f : 0f;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeVolumeWeight(targetWeight));

            if (isLocalPlayerInsideBase)
            {
                PlayAudio(shouldBeBlackout);
            }
            else if (loopHumSource != null && loopHumSource.isPlaying)
            {
                loopHumSource.Stop();
            }
        }

        private IEnumerator FadeVolumeWeight(float targetWeight)
        {
            if (blackoutVolume == null) yield break;

            float startWeight = blackoutVolume.weight;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                blackoutVolume.weight = Mathf.Lerp(startWeight, targetWeight, elapsed / fadeDuration);
                yield return null;
            }

            blackoutVolume.weight = targetWeight;
        }

        private void PlayAudio(bool isBlackout)
        {
            if (audioSource != null)
            {
                AudioClip clip = isBlackout ? powerDownClip : powerRestoreClip;
                if (clip != null) audioSource.PlayOneShot(clip);
            }

            if (loopHumSource != null)
            {
                if (isBlackout && !loopHumSource.isPlaying) loopHumSource.Play();
                else if (!isBlackout && loopHumSource.isPlaying) loopHumSource.Stop();
            }
        }
    }
}