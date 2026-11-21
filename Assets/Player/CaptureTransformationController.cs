using Unity.Netcode;
using UnityEngine;

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
        private double localStartTime;
        private float sequenceDuration;
        private Vector3 collapseCenter;
        private bool isPlaying;
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
            CaptureSequenceEffects.Play(gameObject.scene, center, localStartTime, duration, effectRadius,
                energyColor, vacuumVolume, vacuumLoop, implosionPop);

            // A late packet should still show the correct state on its first visible frame.
            UpdateVisuals(Mathf.Clamp01((float)(elapsed / sequenceDuration)));
        }

        private void Update()
        {
            if (!isPlaying) return;

            float progress = Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - localStartTime) / sequenceDuration));
            UpdateVisuals(progress);

            if (progress < 1f) return;

            isPlaying = false;
            RestoreCaptureVisuals();
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
                if (binding.Renderer == null || binding.OriginalMaterials == null || !binding.Renderer.transform.IsChildOf(transform)) continue;

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
                if (binding.Renderer == null || binding.ImplosionMaterials == null || !binding.Renderer.transform.IsChildOf(transform)) continue;

                for (int materialIndex = 0; materialIndex < binding.ImplosionMaterials.Length; materialIndex++)
                {
                    propertyBlock.Clear();
                    propertyBlock.SetFloat(CaptureProgressId, progress);
                    propertyBlock.SetVector(CaptureCenterId, collapseCenter);
                    binding.Renderer.SetPropertyBlock(propertyBlock, materialIndex);
                }
            }
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
