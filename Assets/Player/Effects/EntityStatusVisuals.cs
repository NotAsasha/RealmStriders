using System;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Manages world-space visual representations for status effects (Water, Freeze, Weak)
    /// on entities, allowing statuses to be recognized in gameplay without opening menus.
    /// Supports direct particle references, child auto-discovery, or loading a shared
    /// prefab from Resources (e.g. Resources/Effects/EntityStatusVFX).
    /// </summary>
    [DisallowMultipleComponent]
    public class EntityStatusVisuals : MonoBehaviour
    {
        private const string DefaultResourcePath = "Effects/EntityStatusVFX";
        private const string FallbackResourcePath = "EntityStatusVFX";

        [Header("Particle Systems (Optional Direct Assignment)")]
        [Tooltip("Particle system played while the entity has the Water effect.")]
        [SerializeField] private ParticleSystem waterParticles;

        [Tooltip("Particle system played while the entity has the Freeze effect.")]
        [SerializeField] private ParticleSystem freezeParticles;

        [Tooltip("Particle system played while the entity has Weak stacks active.")]
        [SerializeField] private ParticleSystem weakParticles;

        [Header("Shared Prefab Fallback")]
        [Tooltip("Optional prefab containing status effect particles. If null, loads from Resources/Effects/EntityStatusVFX.")]
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField] private bool autoInstantiatePrefab = true;

        [Header("Attachment & Placement")]
        [Tooltip("Optional transform/bone to parent effects to. If null, centers automatically using colliders/renderers.")]
        [SerializeField] private Transform attachPoint;

        [Header("Weakness Settings")]
        [Tooltip("When enabled, emission rate scales proportionally to the number of weak stacks.")]
        [SerializeField] private bool scaleWeakEmissionWithStacks = true;
        [SerializeField, Min(1f)] private float weakEmissionPerStack = 10f;

        [Header("Freeze Material Tint (Optional)")]
        [SerializeField] private bool applyFreezeTint = false;
        [SerializeField] private Color freezeColor = new(0.4f, 0.75f, 1f, 1f);

        private bool isInitialized;
        private bool isDead;
        private bool isWaterActive;
        private bool isFreezeActive;
        private int currentWeakStacks;

        private float baseWeakEmissionRate = 10f;
        private GameObject spawnedVfxInstance;
        private Renderer[] entityRenderers;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public bool IsDead => isDead;
        public ParticleSystem WaterParticles => waterParticles;
        public ParticleSystem FreezeParticles => freezeParticles;
        public ParticleSystem WeakParticles => weakParticles;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            // Ensure particles do not play on awake if active state is false
            UpdateWaterVisuals();
            UpdateFreezeVisuals();
            UpdateWeakVisuals();
        }

        /// <summary>
        /// Ensures all particle bindings and default prefabs are loaded and configured.
        /// Safe to call multiple times.
        /// </summary>
        public void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            // 1. Try finding particles in existing children if direct references are not set
            FindParticlesInChildren();

            // 2. If any particles are still missing and auto-instantiate is allowed, try loading shared prefab
            if ((waterParticles == null || freezeParticles == null || weakParticles == null) && autoInstantiatePrefab)
            {
                TryInstantiateSharedPrefab();
            }

            // 3. Cache base emission rate for weakness scaling
            if (weakParticles != null)
            {
                var emission = weakParticles.emission;
                if (emission.rateOverTime.constant > 0f)
                {
                    baseWeakEmissionRate = emission.rateOverTime.constant;
                }
                else
                {
                    baseWeakEmissionRate = weakEmissionPerStack;
                }
            }

            // 4. Cache renderers for optional freeze tint
            if (applyFreezeTint)
            {
                entityRenderers = GetComponentsInChildren<Renderer>(true);
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void FindParticlesInChildren()
        {
            var childSystems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in childSystems)
            {
                string lowerName = ps.gameObject.name.ToLowerInvariant();
                if (waterParticles == null && lowerName.Contains("water"))
                {
                    waterParticles = ps;
                }
                else if (freezeParticles == null && (lowerName.Contains("freeze") || lowerName.Contains("ice") || lowerName.Contains("frost")))
                {
                    freezeParticles = ps;
                }
                else if (weakParticles == null && (lowerName.Contains("weak") || lowerName.Contains("debuff")))
                {
                    weakParticles = ps;
                }
            }
        }

        private void TryInstantiateSharedPrefab()
        {
            GameObject template = vfxPrefab;
            if (template == null)
            {
                template = Resources.Load<GameObject>(DefaultResourcePath);
                if (template == null)
                {
                    template = Resources.Load<GameObject>(FallbackResourcePath);
                }
            }

            if (template == null)
            {
                // No template found in Resources. User will supply prefabs or references in Unity.
                return;
            }

            Transform targetParent = attachPoint != null ? attachPoint : transform;
            spawnedVfxInstance = Instantiate(template, targetParent);
            spawnedVfxInstance.name = "StatusVFX_Instance";

            if (attachPoint == null)
            {
                spawnedVfxInstance.transform.localPosition = CalculateLocalCenterOffset();
            }
            else
            {
                spawnedVfxInstance.transform.localPosition = Vector3.zero;
            }
            spawnedVfxInstance.transform.localRotation = Quaternion.identity;

            // Check if spawned instance has an EntityStatusVisuals component with pre-assigned slots
            if (spawnedVfxInstance.TryGetComponent<EntityStatusVisuals>(out var prefabVisuals))
            {
                if (waterParticles == null) waterParticles = prefabVisuals.waterParticles;
                if (freezeParticles == null) freezeParticles = prefabVisuals.freezeParticles;
                if (weakParticles == null) weakParticles = prefabVisuals.weakParticles;

                // Disable the child component so it doesn't double-process
                prefabVisuals.enabled = false;
            }

            // Fallback: search child systems inside the spawned instance
            var spawnedSystems = spawnedVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in spawnedSystems)
            {
                string lowerName = ps.gameObject.name.ToLowerInvariant();
                if (waterParticles == null && lowerName.Contains("water"))
                {
                    waterParticles = ps;
                }
                else if (freezeParticles == null && (lowerName.Contains("freeze") || lowerName.Contains("ice") || lowerName.Contains("frost")))
                {
                    freezeParticles = ps;
                }
                else if (weakParticles == null && (lowerName.Contains("weak") || lowerName.Contains("debuff")))
                {
                    weakParticles = ps;
                }
            }
        }

        private Vector3 CalculateLocalCenterOffset()
        {
            if (TryGetComponent<CharacterController>(out var cc))
            {
                return cc.center;
            }

            if (TryGetComponent<Collider>(out var col))
            {
                return col.bounds.center - transform.position;
            }

            var childCol = GetComponentInChildren<Collider>();
            if (childCol != null)
            {
                return childCol.bounds.center - transform.position;
            }

            var rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                return rend.bounds.center - transform.position;
            }

            return Vector3.up * 1f;
        }

        #region Public Visual Controls

        public void SetDead(bool dead)
        {
            isDead = dead;
            if (isDead)
            {
                OnDeath();
            }
            else
            {
                if (spawnedVfxInstance != null)
                {
                    spawnedVfxInstance.SetActive(true);
                }
            }
        }

        public void SetWater(bool active)
        {
            EnsureInitialized();
            if (isDead && active) return;
            isWaterActive = active;
            UpdateWaterVisuals();
        }

        public void SetFreeze(bool active)
        {
            EnsureInitialized();
            if (isDead && active) return;
            isFreezeActive = active;
            UpdateFreezeVisuals();
        }

        public void SetWeakStacks(int stacks)
        {
            EnsureInitialized();
            if (isDead && stacks > 0) return;
            currentWeakStacks = Mathf.Max(0, stacks);
            UpdateWeakVisuals();
        }

        public void OnDeath()
        {
            isDead = true;
            EnsureInitialized();
            isWaterActive = false;
            isFreezeActive = false;
            currentWeakStacks = 0;

            StopParticleSystem(waterParticles, clear: true);
            StopParticleSystem(freezeParticles, clear: true);
            StopParticleSystem(weakParticles, clear: true);

            // Also search and stop/clear all ParticleSystems under spawned VFX root and children
            if (spawnedVfxInstance != null)
            {
                var allParticles = spawnedVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in allParticles)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Clear(true);
                    }
                }
                spawnedVfxInstance.SetActive(false);
            }

            ApplyFreezeMaterialTint(false);
        }

        public void OnCapture()
        {
            OnDeath();
        }

        public void OnRevive(bool isFrozen, bool isWater, int weakStacks)
        {
            isDead = false;
            if (spawnedVfxInstance != null)
            {
                spawnedVfxInstance.SetActive(true);
            }
            EnsureInitialized();
            SetFreeze(isFrozen);
            SetWater(isWater);
            SetWeakStacks(weakStacks);
        }

        #endregion

        #region Visual Updaters

        private void UpdateWaterVisuals()
        {
            if (waterParticles == null) return;

            if (isWaterActive && !isDead)
            {
                PlayParticleSystem(waterParticles);
            }
            else
            {
                StopParticleSystem(waterParticles, clear: true);
            }
        }

        private void UpdateFreezeVisuals()
        {
            if (freezeParticles != null)
            {
                if (isFreezeActive && !isDead)
                {
                    PlayParticleSystem(freezeParticles);
                }
                else
                {
                    StopParticleSystem(freezeParticles, clear: true);
                }
            }

            ApplyFreezeMaterialTint(isFreezeActive && !isDead);
        }

        private void UpdateWeakVisuals()
        {
            if (weakParticles == null) return;

            if (currentWeakStacks > 0 && !isDead)
            {
                if (scaleWeakEmissionWithStacks)
                {
                    var emission = weakParticles.emission;
                    emission.rateOverTime = baseWeakEmissionRate * currentWeakStacks;
                }

                PlayParticleSystem(weakParticles);
            }
            else
            {
                StopParticleSystem(weakParticles, clear: true);
            }
        }

        private void PlayParticleSystem(ParticleSystem ps)
        {
            if (ps == null || isDead) return;
            if (!ps.isPlaying)
            {
                ps.Play(true);
            }
        }

        private void StopParticleSystem(ParticleSystem ps, bool clear)
        {
            if (ps == null) return;
            var stopBehavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            ps.Stop(true, stopBehavior);
            if (clear)
            {
                ps.Clear(true);
            }
        }

        private void ApplyFreezeMaterialTint(bool active)
        {
            if (!applyFreezeTint || entityRenderers == null || propertyBlock == null) return;

            foreach (var rend in entityRenderers)
            {
                if (rend == null) continue;

                rend.GetPropertyBlock(propertyBlock);
                if (active)
                {
                    propertyBlock.SetColor(BaseColorId, freezeColor);
                    propertyBlock.SetColor(ColorId, freezeColor);
                }
                else
                {
                    propertyBlock.Clear();
                }
                rend.SetPropertyBlock(propertyBlock);
            }
        }

        #endregion
    }
}
