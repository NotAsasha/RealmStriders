using UnityEngine;
using UnityEngine.Rendering;

namespace Scenes
{
    public class EffectManager : MonoBehaviour
    {
        private Volume effects;
        private AudioSource sound;

        [SerializeField] AudioClip[] ambientSounds;

        float defaultTime;

        private void Start()
        {
            effects = GetComponent<Volume>();
            sound = GetComponent<AudioSource>();
            defaultTime = GameManager.Instance.defaultMissionTime + GameManager.Instance.maxTimeSpread;
       
            InvokeRepeating(nameof(UpdateEffects), defaultTime / 2, 3f);
            InvokeRepeating(nameof(PlayAmbientSounds), 5f, 1f);
        }

        private void UpdateEffects()
        {
            effects.weight = GetEffectStrength(GameManager.Instance.missionDuration, defaultTime);
        }

        private void PlayAmbientSounds()
        {
            if (Random.Range(0, 50) == 1)
            {
                sound.clip = ambientSounds[Random.Range(0, ambientSounds.Length)];
                sound.Play();
            }
        }

        private float GetEffectStrength(float timeLeft, float totalTime)
        {
            float t = 1f - (timeLeft / totalTime);
            return Mathf.Pow(t, 5f);
        }
    }
}