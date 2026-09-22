using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Player
{
    /// <summary>
    /// Client-side presentation for a capture. It intentionally owns no network state:
    /// Entity starts it with a server timestamp, so each client can animate independently.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureTransformationController : MonoBehaviour
    {
        private const string ImplosionShaderResource = "CaptureImplosion";
        private const string VortexPrefabResource = "CaptureEffects/CaptureVortex";
        private const string FlashPrefabResource = "CaptureEffects/CaptureFlash";
        private const float FlashNormalizedTime = 0.88f;
        private const float EffectTailDuration = 1.25f;
        private const int GeneratedAudioSampleRate = 22050;

        private static AudioClip generatedVacuumLoop;
        private static AudioClip generatedImplosionPop;
        private static GameObject vortexPrefab;
        private static GameObject flashPrefab;

        private static readonly int CaptureProgressId = Shader.PropertyToID("_CaptureProgress");
        private static readonly int CaptureCenterId = Shader.PropertyToID("_CaptureCenterWS");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Optional overrides")]
        [SerializeField] private Renderer[] modelRenderers;
        [SerializeField] private Material implosionMaterialTemplate;
        [SerializeField] private AudioClip vacuumLoop;
        [SerializeField] private AudioClip implosionPop;

        [Header("Procedural VFX")]
        [SerializeField, Min(0.1f)] private float effectRadius = 1.2f;
        [SerializeField, Range(0f, 2f)] private float vacuumVolume = 0.8f;
        [SerializeField] private Color energyColor = new(0.2f, 0.85f, 1f, 1f);

        private RendererBinding[] rendererBindings;
        private Animator[] animators;
        private float[] animatorSpeeds;
        private MaterialPropertyBlock propertyBlock;
        private Shader implosionShader;
        private GameObject effectRoot;
        private ParticleSystem vortexParticles;
        private AudioSource effectAudio;
        private Light effectLight;
        private double localStartTime;
        private float sequenceDuration;
        private Vector3 collapseCenter;
        private bool isPlaying;
        private bool flashPlayed;
        private bool materialsReplaced;

        private sealed class RendererBinding
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public Material[] ImplosionMaterials;
        }

        private void Awake()
        {
            CacheRenderers();
            animators = GetComponentsInChildren<Animator>(true);
            animatorSpeeds = new float[animators.Length];
            propertyBlock = new MaterialPropertyBlock();

            if (implosionMaterialTemplate == null)
            {
                implosionShader = Resources.Load<Shader>(ImplosionShaderResource);
            }
        }

        /// <summary>Called from Entity's Everybody RPC.</summary>
        public void Play(Vector3 center, double serverStartTime, float duration)
        {
            if (isPlaying || duration <= 0f) return;

            collapseCenter = center;
            sequenceDuration = duration;
            double elapsed = Mathf.Max(0f, (float)(GetSynchronizedTime() - serverStartTime));
            localStartTime = Time.unscaledTimeAsDouble - elapsed;
            isPlaying = true;

            FreezeAnimators();
            ReplaceMaterials();
            CreateProceduralEffects();

            // A late packet should still show the correct state on its first visible frame.
            UpdateVisuals(Mathf.Clamp01((float)(elapsed / sequenceDuration)));
        }

        private void Update()
        {
            if (!isPlaying) return;

            float progress = Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - localStartTime) / sequenceDuration));
            UpdateVisuals(progress);

            if (!flashPlayed && progress >= FlashNormalizedTime)
            {
                PlayFlash();
            }

            if (progress < 1f) return;

            isPlaying = false;
            RestoreCaptureVisuals();
            if (vortexParticles != null)
            {
                vortexParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void CacheRenderers()
        {
            if (modelRenderers == null || modelRenderers.Length == 0)
            {
                modelRenderers = GetComponentsInChildren<Renderer>(true);
            }

            int usableRendererCount = 0;
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                if (modelRenderers[i] is MeshRenderer || modelRenderers[i] is SkinnedMeshRenderer)
                {
                    usableRendererCount++;
                }
            }

            rendererBindings = new RendererBinding[usableRendererCount];
            int bindingIndex = 0;
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                Renderer renderer = modelRenderers[i];
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;

                rendererBindings[bindingIndex++] = new RendererBinding
                {
                    Renderer = renderer,
                    OriginalMaterials = renderer.sharedMaterials
                };
            }
        }

        private void FreezeAnimators()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null) continue;

                animatorSpeeds[i] = animator.speed;
                animator.speed = 0f;
            }
        }

        private void ReplaceMaterials()
        {
            if (implosionMaterialTemplate == null && implosionShader == null)
            {
                Debug.LogWarning($"[{name}] Capture shader was not found. Add CaptureImplosion.shader to a Resources folder.", this);
                return;
            }

            for (int i = 0; i < rendererBindings.Length; i++)
            {
                RendererBinding binding = rendererBindings[i];
                if (binding.Renderer == null || binding.OriginalMaterials == null) continue;

                int materialCount = binding.OriginalMaterials.Length;
                binding.ImplosionMaterials = new Material[materialCount];
                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                {
                    Material source = binding.OriginalMaterials[materialIndex];
                    Material implosionMaterial = implosionMaterialTemplate != null
                        ? new Material(implosionMaterialTemplate)
                        : new Material(implosionShader);

                    CopySurfaceProperties(source, implosionMaterial);
                    binding.ImplosionMaterials[materialIndex] = implosionMaterial;
                }

                binding.Renderer.sharedMaterials = binding.ImplosionMaterials;
            }

            materialsReplaced = true;
        }

        private static void CopySurfaceProperties(Material source, Material destination)
        {
            if (source == null) return;

            if (source.HasProperty(BaseMapId))
            {
                destination.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
            }
            else if (source.HasProperty(MainTexId))
            {
                destination.SetTexture(BaseMapId, source.GetTexture(MainTexId));
            }

            if (source.HasProperty(BaseColorId))
            {
                destination.SetColor(BaseColorId, source.GetColor(BaseColorId));
            }
            else if (source.HasProperty(ColorId))
            {
                destination.SetColor(BaseColorId, source.GetColor(ColorId));
            }
        }

        private void UpdateVisuals(float progress)
        {
            for (int i = 0; i < rendererBindings.Length; i++)
            {
                RendererBinding binding = rendererBindings[i];
                if (binding.Renderer == null || binding.ImplosionMaterials == null) continue;

                for (int materialIndex = 0; materialIndex < binding.ImplosionMaterials.Length; materialIndex++)
                {
                    propertyBlock.Clear();
                    propertyBlock.SetFloat(CaptureProgressId, progress);
                    propertyBlock.SetVector(CaptureCenterId, collapseCenter);
                    binding.Renderer.SetPropertyBlock(propertyBlock, materialIndex);
                }
            }

            if (effectAudio != null && effectAudio.clip != null)
            {
                effectAudio.volume = vacuumVolume * progress;
                effectAudio.pitch = Mathf.Lerp(0.8f, 1.35f, progress);
            }

            if (effectLight != null)
            {
                effectLight.intensity = Mathf.Lerp(0f, 7f, progress * progress);
            }
        }

        private void CreateProceduralEffects()
        {
            effectRoot = new GameObject("Capture Implosion VFX");
            effectRoot.transform.position = collapseCenter;
            if (gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(effectRoot, gameObject.scene);
            }

            vortexParticles = CreateParticleEffect(VortexPrefabResource, CaptureParticleEffectKind.Vortex);
            effectAudio = effectRoot.AddComponent<AudioSource>();
            effectAudio.spatialBlend = 1f;
            effectAudio.rolloffMode = AudioRolloffMode.Linear;
            effectAudio.maxDistance = 18f;
            EnsureAudioClips();
            effectAudio.clip = vacuumLoop;
            effectAudio.loop = true;
            effectAudio.volume = 0f;
            if (vacuumLoop != null) effectAudio.Play();

            effectLight = effectRoot.AddComponent<Light>();
            effectLight.type = LightType.Point;
            effectLight.color = energyColor;
            effectLight.range = effectRadius * 5f;
            effectLight.intensity = 0f;

            Destroy(effectRoot, sequenceDuration + EffectTailDuration);
        }

        private void EnsureAudioClips()
        {
            if (vacuumLoop == null)
            {
                generatedVacuumLoop ??= CreateVacuumLoop();
                vacuumLoop = generatedVacuumLoop;
            }

            if (implosionPop == null)
            {
                generatedImplosionPop ??= CreateImplosionPop();
                implosionPop = generatedImplosionPop;
            }
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

        private ParticleSystem CreateParticleEffect(string resourcePath, CaptureParticleEffectKind fallbackKind)
        {
            GameObject prefab = LoadParticlePrefab(resourcePath, fallbackKind == CaptureParticleEffectKind.Vortex);
            GameObject effectObject = prefab != null
                ? Instantiate(prefab, effectRoot.transform, false)
                : new GameObject(fallbackKind == CaptureParticleEffectKind.Vortex ? "Capture Vortex" : "Capture Flash");

            if (prefab == null)
            {
                effectObject.transform.SetParent(effectRoot.transform, false);
            }

            if (!effectObject.TryGetComponent(out CaptureParticleEffect particleEffect))
            {
                particleEffect = effectObject.AddComponent<CaptureParticleEffect>();
                particleEffect.SetKind(fallbackKind);
            }

            return particleEffect.Play(energyColor);
        }

        private static GameObject LoadParticlePrefab(string resourcePath, bool isVortex)
        {
            if (isVortex)
            {
                vortexPrefab ??= Resources.Load<GameObject>(resourcePath);
                return vortexPrefab;
            }

            flashPrefab ??= Resources.Load<GameObject>(resourcePath);
            return flashPrefab;
        }

        private void PlayFlash()
        {
            flashPlayed = true;
            if (effectAudio != null)
            {
                effectAudio.Stop();
                if (implosionPop != null) effectAudio.PlayOneShot(implosionPop);
            }

            if (effectLight != null) effectLight.intensity = 12f;

            CreateParticleEffect(FlashPrefabResource, CaptureParticleEffectKind.Flash);
        }

        private static double GetSynchronizedTime()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening
                ? networkManager.ServerTime.Time
                : Time.unscaledTimeAsDouble;
        }

        private void OnDisable()
        {
            RestoreCaptureVisuals();
        }

        private void OnDestroy()
        {
            RestoreCaptureVisuals();
        }

        private void RestoreCaptureVisuals()
        {
            RestoreAnimators();
            RestoreMaterials();

            if (rendererBindings == null || propertyBlock == null) return;

            for (int i = 0; i < rendererBindings.Length; i++)
            {
                Renderer renderer = rendererBindings[i].Renderer;
                if (renderer == null) continue;

                propertyBlock.Clear();
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void RestoreAnimators()
        {
            if (animators == null) return;
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null) animators[i].speed = animatorSpeeds[i];
            }
        }

        private void RestoreMaterials()
        {
            if (!materialsReplaced || rendererBindings == null) return;

            for (int i = 0; i < rendererBindings.Length; i++)
            {
                RendererBinding binding = rendererBindings[i];
                if (binding.Renderer != null)
                {
                    binding.Renderer.sharedMaterials = binding.OriginalMaterials;
                }

                if (binding.ImplosionMaterials == null) continue;
                for (int materialIndex = 0; materialIndex < binding.ImplosionMaterials.Length; materialIndex++)
                {
                    if (binding.ImplosionMaterials[materialIndex] != null)
                    {
                        Destroy(binding.ImplosionMaterials[materialIndex]);
                    }
                }

                binding.ImplosionMaterials = null;
            }

            materialsReplaced = false;
        }
    }
}
