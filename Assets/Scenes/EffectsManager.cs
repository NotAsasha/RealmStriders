using Player;
using Player.Movement;
using Portals;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Scenes
{
    [RequireComponent(typeof(Volume), typeof(AudioSource))]
    public class EffectManager : MonoBehaviour
    {
        [SerializeField] Volume defaultEffects;
        [SerializeField] Volume timeEffects;

        [SerializeField] AudioClip[] ambientSounds;
        [SerializeField] AudioSource[] sceneAudios;


        private AudioSource sound;

        private float defaultTime;
        private float playerPresenceWeight = 0f;
        private Coroutine fadeCoroutine;

        private void Start()
        {
            if (!TryGetComponent<AudioSource>(out sound)) throw new Exception("There is no AudioSource to play.");

            timeEffects.weight = 0;
            defaultEffects.weight = 0;
            foreach (AudioSource source in sceneAudios)
            {
                source.volume = 0;
            }

            defaultTime = GameManager.Instance.defaultMissionTime + GameManager.Instance.maxTimeSpread;

            

            InvokeRepeating(nameof(UpdateEffects), 1f, 0.1f);

            if (ambientSounds.Length > 0)
            {
                InvokeRepeating(nameof(PlayAmbientSounds), 5f, 1f);
            }
        }

        private void OnEnable() => PortalManager.Instance.OnTeleport += OnPlayerTeleports;
        private void OnDisable() => PortalManager.Instance.OnTeleport -= OnPlayerTeleports;

        private void UpdateEffects()
        {
            float timeStrength = GetEffectStrength(GameManager.Instance.missionDuration, defaultTime);


            if (timeEffects != null)
            {
                timeEffects.weight = timeStrength * playerPresenceWeight;
            }
        }

        private void OnPlayerTeleports(Human human, bool isForward)
        {
            Debug.Log("Effects OnPlayerTeleports");
            if (human.gameObject == PlayerMovement.Instance.gameObject)
            {
                StartFade(isForward ? 0f : 1f);
            }
        }

        private void StartFade(float target)
        {
            Debug.Log("Effects StartFade");
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeRoutine(target));
        }

        private IEnumerator FadeRoutine(float target)
        {
            Debug.Log("FadeRoutine");
            float duration = 1.5f;
            float startValue = playerPresenceWeight;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                playerPresenceWeight = Mathf.Lerp(startValue, target, elapsed / duration);
                Debug.Log(playerPresenceWeight);


                defaultEffects.weight = playerPresenceWeight;

                foreach (AudioSource source in sceneAudios)
                {
                    source.volume = playerPresenceWeight;
                }

                yield return null;
            }
            playerPresenceWeight = target;
        }

        private float GetEffectStrength(float timeLeft, float totalTime)
        {
            float t = Mathf.Clamp01(1f - (timeLeft / totalTime));
            return Mathf.Pow(t, 5f);
        }

        private void PlayAmbientSounds()
        {
            if (Random.Range(0, 50) == 1)
            {
                sound.PlayOneShot(ambientSounds[Random.Range(0, ambientSounds.Length)]);
            }
        }
    }
}