using UnityEngine;
using TMPro;
using UnityEngine.Rendering;
using Player;
using Unity.VisualScripting;

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
        defaultTime = GameManager.instance.defaultMissionTime + GameManager.instance.maxTimeSpread;
       
        InvokeRepeating(nameof(UpdateEffects), defaultTime / 2, 1f);
        InvokeRepeating(nameof(PlayEmbientSounds), 5f, 1f);
    }

    void UpdateEffects()
    {
        effects.weight = GetEffectStrength(GameManager.instance.missionDuration, defaultTime);
    }

    void PlayEmbientSounds()
    {
        if (Random.Range(0, 50) == 1)
        {
            sound.clip = ambientSounds[Random.Range(0, ambientSounds.Length)];
            sound.Play();
        }
    }

    float GetEffectStrength(float timeLeft, float totalTime)
    {
        // Exponential growth over time
        float t = 1f - (timeLeft / totalTime);
        return Mathf.Pow(t, 5f);
    }
}