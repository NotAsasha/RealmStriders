using System;
using System.Collections;
using Player;
using Player.Movement;
using Portals;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Scenes
{
    public class EffectManager : MonoBehaviour
    {
        private Volume effects;
        private AudioSource sound;
        [SerializeField] AudioClip[] ambientSounds;

        private float defaultTime;
        private float playerPresenceWeight = 0f;
        private Coroutine fadeCoroutine;

        private void Start()
        {
            effects = GetComponent<Volume>();
            sound = GetComponent<AudioSource>();
            effects.weight = 0;

            defaultTime = GameManager.Instance.defaultMissionTime + GameManager.Instance.maxTimeSpread;

            

            InvokeRepeating(nameof(UpdateEffects), 1f, 0.1f); 
            InvokeRepeating(nameof(PlayAmbientSounds), 5f, 1f);
        }

        private void OnEnable() => PortalManager.Instance.OnTeleport += OnPlayerTeleports;
        private void OnDisable() => PortalManager.Instance.OnTeleport -= OnPlayerTeleports;

        private void UpdateEffects()
        {
            float timeStrength = GetEffectStrength(GameManager.Instance.missionDuration, defaultTime);

            effects.weight = timeStrength * playerPresenceWeight;
        }

        private void OnPlayerTeleports(Human human, bool isForward)
        {
            if (human.gameObject == PlayerMovement.Instance.gameObject)
            {
                StartFade(isForward ? 1f : 0f);
            }
        }

        private void StartFade(float target)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeRoutine(target));
        }

        private IEnumerator FadeRoutine(float target)
        {
            float duration = 1.5f;
            float startValue = playerPresenceWeight;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                playerPresenceWeight = Mathf.Lerp(startValue, target, elapsed / duration);
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
                sound.clip = ambientSounds[Random.Range(0, ambientSounds.Length)];
                sound.Play();
            }
        }
    }
}