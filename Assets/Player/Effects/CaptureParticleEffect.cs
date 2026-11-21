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
        private static readonly int CaptureRingId = Shader.PropertyToID("_CaptureRing");

        [SerializeField] private CaptureParticleEffectKind kind;
        [SerializeField] private ParticleSystem effectParticles;
        [SerializeField, Min(0.01f)] private float radius = 1.2f;
        [SerializeField, Min(1)] private int maxParticles = 96;
        [SerializeField, Min(0f)] private float emissionRate = 75f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.6f;
        [SerializeField, Min(0.01f)] private float startSize = 0.08f;
        [SerializeField] private float inwardSpeed = 3f;
        [SerializeField] private float velocityStretch = 0.35f;
        private ParticleSystem impactRing;

        public void SetKind(CaptureParticleEffectKind value)
        {
            kind = value;
        }

        public ParticleSystem Play(Color color)
        {
            EnsureParticleSystem();
            Configure(color);
            if (kind == CaptureParticleEffectKind.Flash) ConfigureImpactRing();
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
            bool isVortex = kind == CaptureParticleEffectKind.Vortex;
            main.loop = isVortex;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = isVortex ? new ParticleSystem.MinMaxCurve(0f)
                : new ParticleSystem.MinMaxCurve(radius * 0.65f, radius * 1.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.55f, startSize);
            main.startColor = Color.white;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = effectParticles.emission;
            if (isVortex)
            {
                emission.rateOverTime = emissionRate;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(maxParticles, 1, 20)) });
            }

            ParticleSystem.ShapeModule shape = effectParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = isVortex ? radius : 0.045f;
            shape.radiusThickness = isVortex ? 0.35f : 1f;

            ParticleSystem.VelocityOverLifetimeModule velocity = effectParticles.velocityOverLifetime;
            velocity.enabled = isVortex;
            if (velocity.enabled)
            {
                velocity.radial = new ParticleSystem.MinMaxCurve(-inwardSpeed);
                velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.65f);
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = effectParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Color core = Color.Lerp(color, Color.white, 0.75f);
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(isVortex ? color : core, 0f), new GradientColorKey(isVortex ? core : color, 0.75f) },
                new[] { new GradientAlphaKey(isVortex ? 0f : 1f, 0f), new GradientAlphaKey(0.9f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = effectParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.18f));

            ParticleSystemRenderer renderer = effectParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            renderer.sharedMaterial = GetParticleMaterial();
            renderer.renderMode = isVortex
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = velocityStretch;
            renderer.lengthScale = 1.4f;
        }

        private void ConfigureImpactRing()
        {
            if (impactRing == null)
            {
                GameObject ring = new("Capture ring");
                ring.transform.SetParent(effectParticles.transform, false);
                impactRing = ring.AddComponent<ParticleSystem>();
                var renderer = ring.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = GetParticleMaterial();
                MaterialPropertyBlock properties = new();
                properties.SetFloat(CaptureRingId, 1f);
                renderer.SetPropertyBlock(properties);
            }

            impactRing.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = impactRing.main;
            main.loop = false;
            main.useUnscaledTime = true;
            main.startLifetime = lifetime * 0.8f;
            main.startSpeed = 0f;
            main.startSize = radius;
            main.maxParticles = 1;
            var shape = impactRing.shape;
            shape.enabled = false;
            var emission = impactRing.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var size = impactRing.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.12f, 1f, 1f));
            var tint = impactRing.colorOverLifetime;
            tint.enabled = true;
            tint.color = effectParticles.colorOverLifetime.color;
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
