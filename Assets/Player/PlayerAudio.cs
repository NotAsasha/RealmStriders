using System;
using Game.Audio;
using Player.Movement;
using SteamAudio;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAudio : NetworkBehaviour
    {
        [Serializable]
        public sealed class Emitter
        {
            public AudioSource audioSource;
            public SteamAudioSource acousticSource;
        }

        [SerializeField] private Human human;
        [Header("Footsteps")]
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioClip[] footstepClips = Array.Empty<AudioClip>();
        [SerializeField, Min(0.1f)] private float minimumNetworkStepInterval = 0.24f;
        [Header("Acoustics")]
        [SerializeField] private Emitter[] emitters = Array.Empty<Emitter>();
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;

        private float nextLocalStep;
        private double nextServerStep;
        private float nextRefresh;

        public override void OnNetworkSpawn()
        {
            nextLocalStep = 0f;
            nextServerStep = 0d;
            nextRefresh = 0f;
            for (int i = 0; i < emitters.Length; i++)
            {
                Emitter emitter = emitters[i];
                // The owner keeps personal feedback; other players are physical sound sources.
                bool spatial = IsClient && !IsOwner;
                emitter.audioSource.spatialBlend = spatial ? 1f : 0f;
                emitter.audioSource.spatialize = spatial;
                emitter.acousticSource.enabled = spatial;
            }
            if (!IsClient) return;
            SceneAcoustics.Changed += RefreshAcoustics;
            RefreshAcoustics();
        }

        public override void OnNetworkDespawn()
        {
            SceneAcoustics.Changed -= RefreshAcoustics;
            for (int i = 0; i < emitters.Length; i++)
            {
                Emitter emitter = emitters[i];
                emitter.audioSource.Stop();
                emitter.acousticSource.pathing = false;
                emitter.acousticSource.pathingProbeBatch = null;
                emitter.acousticSource.enabled = false;
            }
        }

        public void PlayFootstep(float interval)
        {
            if (!IsSpawned || !IsClient || !IsOwner || !CanStep() || Time.time < nextLocalStep || footstepClips.Length == 0) return;
            int clipIndex = UnityEngine.Random.Range(0, footstepClips.Length);
            float pitch = UnityEngine.Random.Range(0.8f, 1.2f);
            nextLocalStep = Time.time + Mathf.Max(minimumNetworkStepInterval, interval);
            PlayStep(clipIndex, pitch);
            RequestFootstepRpc(clipIndex, pitch);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void RequestFootstepRpc(int clipIndex, float pitch)
        {
            if (!IsServer || !IsSpawned || !CanStep() || Time.unscaledTimeAsDouble < nextServerStep ||
                clipIndex < 0 || clipIndex >= footstepClips.Length || footstepClips[clipIndex] == null ||
                float.IsNaN(pitch) || float.IsInfinity(pitch) || pitch < 0.8f || pitch > 1.2f) return;
            nextServerStep = Time.unscaledTimeAsDouble + minimumNetworkStepInterval;
            PlayFootstepRpc(clipIndex, pitch);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)]
        private void PlayFootstepRpc(int clipIndex, float pitch)
        {
            // Prediction already played the owner's step, including when the owner is the host.
            if (!IsClient || IsOwner || !CanStep()) return;
            PlayStep(clipIndex, pitch);
        }

        private bool CanStep()
        {
            return human == null || (!human.isDead.Value && !human.IsBeingCaptured && !human.IsEffectActive(EffectType.Freeze));
        }

        private void PlayStep(int clipIndex, float pitch)
        {
            if (footstepSource == null || clipIndex < 0 || clipIndex >= footstepClips.Length || footstepClips[clipIndex] == null) return;
            footstepSource.pitch = pitch;
            footstepSource.PlayOneShot(footstepClips[clipIndex]);
        }

        private void Update()
        {
            if (!IsSpawned || !IsClient || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            RefreshAcoustics();
        }

        private void RefreshAcoustics()
        {
            SceneAcoustics.TryGetAtPosition(gameObject.scene, transform.position, out SceneAcoustics environment);
            CameraMovement camera = CameraMovement.Instance;
            bool listenerCovered = !IsOwner && camera != null && environment != null && environment.Contains(camera.transform.position);
            for (int i = 0; i < emitters.Length; i++)
            {
                Emitter emitter = emitters[i];
                float range = emitter.audioSource.maxDistance + 2f;
                bool covered = listenerCovered && emitter.audioSource.isActiveAndEnabled &&
                    environment.Contains(emitter.audioSource.transform.position);
                emitter.acousticSource.pathingProbeBatch = !IsOwner && environment != null ? environment.ProbeBatch : null;
                emitter.acousticSource.pathing = covered &&
                    (emitter.audioSource.transform.position - camera.transform.position).sqrMagnitude <= range * range;
            }
        }
    }
}
