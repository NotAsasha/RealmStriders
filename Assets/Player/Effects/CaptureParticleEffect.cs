using UnityEngine;

namespace Player
{
    public enum CaptureParticleEffectKind
    {
        Vortex,
        Flash
    }

    /// <summary>
    /// Editable particle effect used by the capture prefabs. The component creates its
    /// ParticleSystem if needed, allowing the prefab to remain small and portable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureParticleEffect : MonoBehaviour
    {
        private const string ParticleShaderResource = "CaptureParticles";
        private const string ParticleMaterialResource = "CaptureEffects/CaptureParticleMaterial";

        private static Material runtimeParticleMaterial;

        [SerializeField] private CaptureParticleEffectKind kind;
        [SerializeField] private ParticleSystem effectParticles;
        [SerializeField, Min(0.01f)] private float radius = 1.2f;
        [SerializeField, Min(1)] private int maxParticles = 96;
        [SerializeField, Min(0f)] private float emissionRate = 75f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.6f;
        [SerializeField, Min(0.01f)] private float startSize = 0.08f;
        [SerializeField] private float inwardSpeed = 3f;
        [SerializeField] private float velocityStretch = 0.35f;

        public void SetKind(CaptureParticleEffectKind value)
        {
            kind = value;
        }

        public ParticleSystem Play(Color color)
        {
            EnsureParticleSystem();
            Configure(color);
            effectParticles.Play(true);
            return effectParticles;
        }

        private void Awake()
        {
            EnsureParticleSystem();
        }

        private void OnValidate()
        {
            if (effectParticles != null) Configure(Color.white);
        }

        private void EnsureParticleSystem()
        {
            if (effectParticles == null)
            {
                effectParticles = GetComponent<ParticleSystem>();
                if (effectParticles == null) effectParticles = gameObject.AddComponent<ParticleSystem>();
            }

            ParticleSystemRenderer renderer = effectParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetParticleMaterial();
            }
        }

        private void Configure(Color color)
        {
            ParticleSystem.MainModule main = effectParticles.main;
            main.loop = kind == CaptureParticleEffectKind.Vortex;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = kind == CaptureParticleEffectKind.Vortex ? lifetime : 0.35f;
            main.startSpeed = kind == CaptureParticleEffectKind.Vortex ? 0.1f : radius * 4f;
            main.startSize = kind == CaptureParticleEffectKind.Vortex ? startSize : radius * 0.35f;
            main.startColor = kind == CaptureParticleEffectKind.Vortex ? color : Color.white;
            main.maxParticles = kind == CaptureParticleEffectKind.Vortex ? maxParticles : 48;

            ParticleSystem.EmissionModule emission = effectParticles.emission;
            if (kind == CaptureParticleEffectKind.Vortex)
            {
                emission.rateOverTime = emissionRate;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            }

            ParticleSystem.ShapeModule shape = effectParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = kind == CaptureParticleEffectKind.Vortex ? radius : 0.03f;

            ParticleSystem.VelocityOverLifetimeModule velocity = effectParticles.velocityOverLifetime;
            velocity.enabled = kind == CaptureParticleEffectKind.Vortex;
            if (velocity.enabled)
            {
                velocity.radial = new ParticleSystem.MinMaxCurve(-inwardSpeed);
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = effectParticles.colorOverLifetime;
            colorOverLifetime.enabled = kind == CaptureParticleEffectKind.Vortex;
            if (colorOverLifetime.enabled)
            {
                Gradient gradient = new();
                gradient.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 0.8f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
            }

            ParticleSystemRenderer renderer = effectParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            renderer.sharedMaterial = GetParticleMaterial();
            renderer.renderMode = kind == CaptureParticleEffectKind.Vortex
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = velocityStretch;
        }

        private static Material GetParticleMaterial()
        {
            if (runtimeParticleMaterial != null) return runtimeParticleMaterial;

            runtimeParticleMaterial = Resources.Load<Material>(ParticleMaterialResource);
            if (runtimeParticleMaterial != null) return runtimeParticleMaterial;

            Shader shader = Resources.Load<Shader>(ParticleShaderResource);
            if (shader == null)
            {
                Debug.LogError("Capture particle shader is missing from Resources.");
                return null;
            }

            runtimeParticleMaterial = new Material(shader)
            {
                name = "Runtime Capture Particle Material"
            };
            return runtimeParticleMaterial;
        }
    }
}
