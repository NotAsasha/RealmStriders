using System;
using Game.Audio;
using Player.Movement;
using SteamAudio;
using Unity.Netcode;
using UnityEngine;

namespace Enemy
{
    [DisallowMultipleComponent]
    public sealed class EnemyAcoustics : NetworkBehaviour
    {
        [Serializable]
        public sealed class Emitter
        {
            public AudioSource audioSource;
            public SteamAudioSource acousticSource;
        }

        [SerializeField] private Emitter[] emitters = Array.Empty<Emitter>();
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;
        private float nextRefresh;

        public override void OnNetworkSpawn()
        {
            if (!IsClient)
            {
                Clear();
                return;
            }
            SceneAcoustics.Changed += Refresh;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            SceneAcoustics.Changed -= Refresh;
            Clear();
        }

        private void Update()
        {
            if (!IsSpawned || !IsClient || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            SceneAcoustics.TryGet(gameObject.scene, out SceneAcoustics environment);
            CameraMovement camera = CameraMovement.Instance;
            bool listenerCovered = camera != null && environment != null && environment.Contains(camera.transform.position);
            for (int i = 0; i < emitters.Length; i++)
            {
                Emitter emitter = emitters[i];
                if (emitter.audioSource == null || emitter.acousticSource == null) continue;
                bool covered = listenerCovered && emitter.audioSource.isActiveAndEnabled &&
                    environment.Contains(emitter.audioSource.transform.position);
                float range = emitter.audioSource.maxDistance + 2f;
                bool nearby = covered && (emitter.audioSource.transform.position - camera.transform.position).sqrMagnitude <= range * range;
                emitter.acousticSource.pathingProbeBatch = environment != null ? environment.ProbeBatch : null;
                // Keep direct spatial audio outside a baked mission, and avoid computing
                // inaudible routes for distant enemies. No scene/component searches.
                emitter.acousticSource.pathing = nearby;
            }
        }

        private void Clear()
        {
            for (int i = 0; i < emitters.Length; i++)
            {
                SteamAudioSource source = emitters[i].acousticSource;
                if (source == null) continue;
                source.pathing = false;
                source.pathingProbeBatch = null;
            }
        }
    }
}
