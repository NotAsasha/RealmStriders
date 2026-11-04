using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Game.Tutorial
{
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup cassette;
        [SerializeField] private RectTransform cassetteCard;
        [SerializeField] private TMP_Text cassetteLabel;
        [SerializeField] private Volume grainVolume;
        [SerializeField] private RectTransform trackingLine;
        [SerializeField] private AudioSource transitionAudio;

        public void PlayTransitionAudio()
        {
            if (transitionAudio != null && transitionAudio.isActiveAndEnabled && transitionAudio.clip != null)
                transitionAudio.Play();
        }

        public void CarryTransitionToMenu()
        {
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnMenuLoaded;
        }

        private void OnMenuLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Menu") return;
            SceneManager.sceneLoaded -= OnMenuLoaded;
            if (!isActiveAndEnabled)
            {
                Destroy(gameObject);
                return;
            }
            StartCoroutine(RevealMenu());
        }

        private IEnumerator RevealMenu()
        {
            // Scene loading can make the first frame's delta include the entire load.
            yield return null;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.4f)
            {
                SetTransition(1f - (Time.realtimeSinceStartup - start) / 0.4f, Time.unscaledTime, "TRAINING COMPLETE");
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnMenuLoaded;

        public void SetTransition(float opacity, float phase, string label)
        {
            // Presentation is optional: removing cassette visuals must not interrupt stage flow.
            if (cassette != null)
            {
                cassette.alpha = opacity;
                cassette.blocksRaycasts = opacity > 0f;
            }
            if (cassetteLabel != null) cassetteLabel.text = label;
            if (cassetteCard != null)
                cassetteCard.anchoredPosition = new Vector2(Mathf.Sin(phase * Mathf.PI * 2f) * 45f, 0f);
            if (grainVolume != null) grainVolume.weight = Mathf.Clamp01(opacity);
            if (trackingLine != null)
            {
                trackingLine.anchorMin = new Vector2(0f, Mathf.Repeat(phase * 2.4f, 1f));
                trackingLine.anchorMax = new Vector2(1f, trackingLine.anchorMin.y);
            }
        }
    }
}
