using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Base.RatingScreen
{
    public class RatingScreen : MonoBehaviour
    {
        private const int MaxPointsPerStar = 3;

        [SerializeField] private Sprite[] starTextures; // 0 = пуста, 1 = 1/3, 2 = 2/3, 3 = повна
        [SerializeField] private Image[] stars;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioSource loadingAudioSource;
        [SerializeField] private AudioClip ratingStepSound;
        [SerializeField] private AudioClip fullStarSound;
        [SerializeField] private AudioClip ratingCompleteSound;
        [SerializeField] private AudioClip ratingLostSound;
        [SerializeField, Min(0f)] private float loadingLeadInDuration = 2.5f;
        [SerializeField, Min(0.01f)] private float stepDuration = 0.12f;
        [SerializeField, Min(1f)] private float starPulseScale = 1.15f;

        private Coroutine ratingAnimation;

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.teamRating.OnValueChanged += UpdateCounter;

            RefreshUI();
        }

        private void Start()
        {
            RefreshUI();
        }

        private void OnDisable()
        {
            StopLoadingSound();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.teamRating.OnValueChanged -= UpdateCounter;
            }
        }

        private void RefreshUI()
        {
            if (GameManager.Instance != null)
            {
                ApplyRating(GameManager.Instance.teamRating.Value);
            }
        }

        private void UpdateCounter(int oldRating, int rating)
        {
            if (ratingAnimation != null)
            {
                StopCoroutine(ratingAnimation);
            }

            ratingAnimation = StartCoroutine(AnimateRating(oldRating, rating));
        }

        private IEnumerator AnimateRating(int oldRating, int newRating)
        {
            int clampedOldRating = Mathf.Max(0, oldRating);
            int clampedNewRating = Mathf.Max(0, newRating);
            int direction = clampedNewRating.CompareTo(clampedOldRating);

            if (direction == 0)
            {
                ApplyRating(clampedNewRating);
                ratingAnimation = null;
                yield break;
            }

            int currentRating = clampedOldRating;
            StartLoadingSound();
            if (loadingLeadInDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(loadingLeadInDuration);
            }

            while (currentRating != clampedNewRating)
            {
                currentRating += direction;
                ApplyRating(currentRating);

                if (stars.Length > 0)
                {
                    int changedStarIndex = Mathf.Clamp((currentRating - 1) / MaxPointsPerStar, 0, stars.Length - 1);
                    StartCoroutine(PulseStar(stars[changedStarIndex]));
                }

                PlayStepSound(currentRating);
                if (direction > 0 && currentRating % MaxPointsPerStar == 0)
                {
                    PlayFullStarSound();
                }

                yield return new WaitForSeconds(stepDuration);
            }

            StopLoadingSound();
            PlayFinalSound(clampedNewRating < clampedOldRating);
            ratingAnimation = null;
        }

        private void ApplyRating(int rating)
        {
            int currentRating = rating;

            for (int i = 0; i < stars.Length; i++)
            {
                int starState = Mathf.Clamp(currentRating, 0, MaxPointsPerStar);
                if (starState < starTextures.Length)
                {
                    stars[i].sprite = starTextures[starState];
                }

                currentRating -= MaxPointsPerStar;
            }
        }

        private IEnumerator PulseStar(Image star)
        {
            Transform starTransform = star.transform;
            Vector3 originalScale = starTransform.localScale;
            starTransform.localScale = originalScale * starPulseScale;

            float elapsed = 0f;
            while (elapsed < stepDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                starTransform.localScale = Vector3.Lerp(originalScale * starPulseScale, originalScale, elapsed / stepDuration);
                yield return null;
            }

            starTransform.localScale = originalScale;
        }

        private void PlayStepSound(int rating)
        {
            if (audioSource == null || ratingStepSound == null) return;

            audioSource.pitch = 0.95f + (rating % MaxPointsPerStar) * 0.05f;
            audioSource.PlayOneShot(ratingStepSound);
        }

        private void PlayFullStarSound()
        {
            if (audioSource == null || fullStarSound == null) return;

            audioSource.pitch = 1f;
            audioSource.PlayOneShot(fullStarSound);
        }

        private void StartLoadingSound()
        {
            if (loadingAudioSource == null || loadingAudioSource.clip == null) return;

            loadingAudioSource.loop = true;
            loadingAudioSource.Play();
        }

        private void StopLoadingSound()
        {
            if (loadingAudioSource != null && loadingAudioSource.isPlaying)
            {
                loadingAudioSource.Stop();
            }
        }

        private void PlayFinalSound(bool lostRating)
        {
            if (audioSource == null) return;

            AudioClip finalSound = lostRating ? ratingLostSound : ratingCompleteSound;
            if (finalSound != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(finalSound);
            }
        }
    }
}