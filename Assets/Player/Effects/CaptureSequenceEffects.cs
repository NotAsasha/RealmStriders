using UnityEngine;
using UnityEngine.SceneManagement;

namespace Player
{
    // Lives at the capture point so the light and particles finish after the entity despawns.
    [DisallowMultipleComponent]
    public sealed class CaptureSequenceEffects : MonoBehaviour
    {
        private const float ImpactProgress = 0.88f;
        private const float LightFadeDuration = 0.78f;
        private const float EffectTailDuration = 1.25f;
        private const int GeneratedAudioSampleRate = 22050;
        private static AudioClip generatedVacuumLoop;
        private static AudioClip generatedImplosionPop;

        private double startTime;
        private float impactTime;
        private float endTime;
        private float radius;
        private Color energyColor;
        private float volume;
        private Light glow;
        private AudioSource audioSource;
        private AudioClip pop;
        private ParticleSystem vortex;
        private bool released;

        public static void Play(Scene scene, Vector3 center, double startTime, float duration,
            float radius, Color color, float volume, AudioClip loop, AudioClip pop)
        {
            if (Time.unscaledTimeAsDouble - startTime >= duration + EffectTailDuration) return;

            GameObject root = new("Capture Implosion VFX");
            if (scene.IsValid()) SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = center;
            root.AddComponent<CaptureSequenceEffects>().Initialize(startTime, duration, radius, color, volume, loop, pop);
        }

        private void Initialize(double localStartTime, float duration, float effectRadius, Color color,
            float vacuumVolume, AudioClip loop, AudioClip impactClip)
        {
            startTime = localStartTime;
            impactTime = duration * ImpactProgress;
            endTime = duration + EffectTailDuration;
            radius = effectRadius;
            energyColor = color;
            volume = vacuumVolume;
            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.shadows = LightShadows.None;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = 18f;
            pop = impactClip != null ? impactClip : (generatedImplosionPop ??= CreateImplosionPop());

            float elapsed = (float)(Time.unscaledTimeAsDouble - startTime);
            if (elapsed < impactTime)
            {
                vortex = CreateParticleEffect("CaptureEffects/CaptureVortex", CaptureParticleEffectKind.Vortex);
                if (elapsed > 0f)
                {
                    vortex.Simulate(Mathf.Min(elapsed, vortex.main.startLifetime.constantMax), true, true, false);
                    vortex.Play(true);
                }
                audioSource.clip = loop != null ? loop : (generatedVacuumLoop ??= CreateVacuumLoop());
                audioSource.loop = true;
                audioSource.Play();
            }
            ApplyElapsedTime(elapsed);
        }

        private void Update()
        {
            ApplyElapsedTime((float)(Time.unscaledTimeAsDouble - startTime));
        }

        private void ApplyElapsedTime(float elapsed)
        {
            if (elapsed >= endTime)
            {
                Destroy(gameObject);
                return;
            }

            float charge = Mathf.SmoothStep(0f, 1f, elapsed / impactTime);
            float sinceImpact = elapsed - impactTime;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, sinceImpact / LightFadeDuration);
            glow.intensity = 4.8f * charge * fade * fade;
            glow.range = radius * Mathf.Lerp(1.8f, 4.1f, charge * fade);
            glow.color = Color.Lerp(energyColor, Color.white, 0.32f * charge * fade);

            if (sinceImpact < 0f)
            {
                audioSource.volume = volume * charge;
                audioSource.pitch = Mathf.Lerp(0.8f, 1.35f, charge);
                return;
            }

            if (released) return;
            released = true;
            if (vortex != null) vortex.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            audioSource.Stop();
            audioSource.pitch = 1.35f;
            float clipOffset = sinceImpact * audioSource.pitch;
            if (clipOffset < pop.length)
            {
                audioSource.loop = false;
                audioSource.clip = pop;
                audioSource.volume = volume;
                audioSource.time = clipOffset;
                audioSource.Play();
            }

            ParticleSystem flash = CreateParticleEffect("CaptureEffects/CaptureFlash", CaptureParticleEffectKind.Flash);
            if (sinceImpact < flash.main.startLifetime.constantMax)
            {
                flash.Simulate(sinceImpact, true, true, false);
                flash.Play(true);
            }
            else flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private ParticleSystem CreateParticleEffect(string resourcePath, CaptureParticleEffectKind fallbackKind)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            GameObject effectObject = prefab != null
                ? Instantiate(prefab, transform, false)
                : new GameObject(fallbackKind == CaptureParticleEffectKind.Vortex ? "Capture Vortex" : "Capture Flash");

            if (prefab == null)
            {
                effectObject.transform.SetParent(transform, false);
            }

            if (!effectObject.TryGetComponent(out CaptureParticleEffect particleEffect))
            {
                particleEffect = effectObject.AddComponent<CaptureParticleEffect>();
                particleEffect.SetKind(fallbackKind);
            }

            return particleEffect.Play(energyColor);
        }

        private static AudioClip CreateVacuumLoop()
        {
            const float duration = 1f;
            int sampleCount = Mathf.RoundToInt(GeneratedAudioSampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)GeneratedAudioSampleRate;
                float lowTone = Mathf.Sin(time * Mathf.PI * 2f * 68f) * 0.24f;
                float highTone = Mathf.Sin(time * Mathf.PI * 2f * 137f) * 0.08f;
                float air = (DeterministicNoise(i) - 0.5f) * 0.05f;
                samples[i] = lowTone + highTone + air;
            }

            AudioClip clip = AudioClip.Create("Generated Capture Vacuum", sampleCount, 1, GeneratedAudioSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateImplosionPop()
        {
            const float duration = 0.42f;
            int sampleCount = Mathf.RoundToInt(GeneratedAudioSampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)GeneratedAudioSampleRate;
                float normalizedTime = time / duration;
                float envelope = Mathf.Exp(-normalizedTime * 8f);
                float frequency = Mathf.Lerp(190f, 48f, normalizedTime);
                float tone = Mathf.Sin(time * Mathf.PI * 2f * frequency);
                float impact = (DeterministicNoise(i * 3) - 0.5f) * 0.9f * Mathf.Exp(-normalizedTime * 18f);
                samples[i] = (tone * 0.6f + impact) * envelope;
            }

            AudioClip clip = AudioClip.Create("Generated Capture Implosion", sampleCount, 1, GeneratedAudioSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float DeterministicNoise(int sampleIndex)
        {
            float value = Mathf.Sin(sampleIndex * 12.9898f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
