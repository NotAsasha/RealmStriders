using System;
using Unity.Netcode;
using UnityEngine;

namespace Game.Casino
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MissionLightFlicker : NetworkBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer emissionRenderer;
        [SerializeField, Min(0)] private int emissionMaterialIndex = 1;
        [SerializeField] private Vector2 quietInterval = new(18f, 40f);
        [SerializeField] private Vector2 burstDuration = new(0.35f, 1.1f);
        [SerializeField, Min(1f)] private float finalFrequencyMultiplier = 8f;
        [SerializeField, Min(0f)] private float synchronizationDelay = 0.2f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock emissionBlock;
        private Color normalEmission;
        private float normalIntensity;
        private float appliedBrightness = -1f;
        private bool normalEnabled;
        private bool hasEmission;
        private bool presentationInitialized;
        private System.Random random;
        private float initialRemaining;
        private float waitRemaining;
        private bool missionWasActive;
        private double burstStart;
        private float currentBurstDuration;
        private uint burstSeed;

        public override void OnNetworkSpawn()
        {
            normalIntensity = targetLight != null ? targetLight.intensity : 0f;
            normalEnabled = targetLight != null && targetLight.enabled;
            hasEmission = false;
            if (emissionRenderer != null)
            {
                Material[] materials = emissionRenderer.sharedMaterials;
                if (emissionMaterialIndex >= 0 && emissionMaterialIndex < materials.Length &&
                    materials[emissionMaterialIndex] != null &&
                    materials[emissionMaterialIndex].HasProperty(EmissionColor))
                {
                    hasEmission = true;
                    normalEmission = materials[emissionMaterialIndex].GetColor(EmissionColor);
                    emissionBlock = new MaterialPropertyBlock();
                }
            }
            if (IsServer)
            {
                random = new System.Random(unchecked(Environment.TickCount * 31 + (int)NetworkObjectId));
                waitRemaining = Sample(quietInterval);
            }
            appliedBrightness = -1f;
            presentationInitialized = true;
            ApplyBrightness(1f);
        }

        public override void OnNetworkDespawn()
        {
            ResetPresentation();
            presentationInitialized = false;
            missionWasActive = false;
            initialRemaining = 0f;
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        private void Update()
        {
            if (!IsSpawned) return;
            GameManager mission = GameManager.Instance;
            bool active = mission != null && mission.IsSpawned && mission.hasStartedMission.Value;
            if (!active)
            {
                if (missionWasActive) ResetPresentation();
                missionWasActive = false;
                return;
            }

            if (IsServer)
            {
                if (!missionWasActive)
                {
                    initialRemaining = Mathf.Max(1f, mission.missionDuration);
                    waitRemaining = Sample(quietInterval);
                }
                float progress = 1f - Mathf.Clamp01(mission.missionDuration / initialRemaining);
                float rate = Mathf.Lerp(1f, finalFrequencyMultiplier, progress * progress);
                waitRemaining -= Time.deltaTime * rate;
                if (waitRemaining <= 0f)
                {
                    float duration = Sample(burstDuration);
                    FlickerClientRpc(NetworkManager.ServerTime.Time + synchronizationDelay,
                        duration, (uint)random.Next());
                    // Include the burst itself so independent bursts never overlap on one lamp.
                    waitRemaining = Sample(quietInterval) + (duration + synchronizationDelay) * rate;
                }
            }
            missionWasActive = true;
            if (IsClient) UpdatePresentation(NetworkManager.ServerTime.Time);
        }

        [ClientRpc]
        private void FlickerClientRpc(double startTime, float duration, uint seed)
        {
            if (!IsClient || !IsSpawned || !isActiveAndEnabled) return;
            burstStart = startTime;
            currentBurstDuration = duration;
            burstSeed = seed;
        }

        private void UpdatePresentation(double now)
        {
            double elapsed = now - burstStart;
            if (currentBurstDuration <= 0f || elapsed < 0d) return;
            if (elapsed >= currentBurstDuration)
            {
                currentBurstDuration = 0f;
                ApplyBrightness(1f);
                return;
            }
            // A shared seed and server timestamp keep the short irregular pulses aligned.
            uint value = unchecked(burstSeed + (uint)(elapsed / 0.09d) * 747796405u);
            value = unchecked((value ^ (value >> 16)) * 2246822519u);
            float brightness = (value & 255u) < 145u ? 0f : 0.25f + ((value >> 8) & 255u) / 340f;
            ApplyBrightness(brightness);
        }

        private void ApplyBrightness(float brightness)
        {
            if (appliedBrightness == brightness) return;
            appliedBrightness = brightness;
            if (targetLight != null)
            {
                targetLight.intensity = normalIntensity * brightness;
                targetLight.enabled = normalEnabled && brightness > 0f;
            }
            if (hasEmission && emissionRenderer != null)
            {
                emissionRenderer.GetPropertyBlock(emissionBlock, emissionMaterialIndex);
                emissionBlock.SetColor(EmissionColor, normalEmission * brightness);
                emissionRenderer.SetPropertyBlock(emissionBlock, emissionMaterialIndex);
            }
        }

        private void ResetPresentation()
        {
            currentBurstDuration = 0f;
            if (presentationInitialized) ApplyBrightness(1f);
        }

        private float Sample(Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        private void OnValidate()
        {
            quietInterval.x = Mathf.Max(1f, quietInterval.x);
            quietInterval.y = Mathf.Max(quietInterval.x, quietInterval.y);
            burstDuration.x = Mathf.Clamp(burstDuration.x, 0.1f, 2f);
            burstDuration.y = Mathf.Clamp(burstDuration.y, burstDuration.x, 2f);
            finalFrequencyMultiplier = Mathf.Clamp(finalFrequencyMultiplier, 1f, 12f);
            synchronizationDelay = Mathf.Max(0f, synchronizationDelay);
        }
    }
}
