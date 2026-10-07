using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Enemy
{
    public class SoundProducer : NetworkBehaviour
    {
        [SerializeField] Transform soundEmitor;
        [SerializeField] float soundRadius = 20.0f;
        [SerializeField] float continuousNoiseInterval = 0.35f;
        [SerializeField] float refreshedNoiseTimeout = 0.5f;
        [SerializeField] LayerMask entityLayer;
        //[SerializeField] LayerMask wallLayer;

        [SerializeField] AudioSource source;
        [SerializeField] List<AudioClip> clip;

        bool continuousNoiseRequested;
        bool continuousNoisePersistent;
        float lastNoiseRefreshTime = float.NegativeInfinity;
        float nextContinuousNoiseTime;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EmitSoundServerRpc(int soundIndex = 0, bool singleLure = false)
        {
            if (!soundEmitor) soundEmitor = transform;

            if (source != null && clip != null && soundIndex >= 0 && soundIndex < clip.Count)
            {
                source.clip = clip[soundIndex];
                source.Play();
                EmitSoundClientRpc(soundIndex);
            }

            LureEnemies(soundEmitor.position, singleLure);
        }

        public void StartContinuousNoise()
        {
            if (!IsServer) return;
            if (continuousNoiseRequested && continuousNoisePersistent) return;

            continuousNoiseRequested = true;
            continuousNoisePersistent = true;
            nextContinuousNoiseTime = 0f;
        }

        public void RefreshContinuousNoise()
        {
            if (!IsServer) return;

            continuousNoiseRequested = true;
            continuousNoisePersistent = false;
            lastNoiseRefreshTime = Time.time;
        }

        public void StopContinuousNoise()
        {
            if (!IsServer) return;

            continuousNoiseRequested = false;
            continuousNoisePersistent = false;
        }

        private void Update()
        {
            if (!IsServer || !continuousNoiseRequested) return;
            if (!continuousNoisePersistent && Time.time > lastNoiseRefreshTime + refreshedNoiseTimeout)
            {
                continuousNoiseRequested = false;
                return;
            }

            if (Time.time < nextContinuousNoiseTime) return;

            nextContinuousNoiseTime = Time.time + Mathf.Max(continuousNoiseInterval, 0.05f);
            LureEnemies(soundEmitor != null ? soundEmitor.position : transform.position, false);
        }

        private void LureEnemies(Vector3 position, bool singleLure)
        {
            Collider[] entities = Physics.OverlapSphere(position, soundRadius, entityLayer);
            foreach (Collider entity in entities)
            {
                if (entity.gameObject == gameObject) continue;

                var enemy = entity.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    enemy.Lure(position);
                    if (singleLure) return;
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void EmitSoundClientRpc(int soundIndex)
        {
            if (IsServer || source == null || clip == null || soundIndex < 0 || soundIndex >= clip.Count) return;

            source.clip = clip[soundIndex];
            source.Play();
        }

        public void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blueViolet;
            if (soundEmitor == null || soundRadius == 0) return;
            Gizmos.DrawWireSphere(soundEmitor.position, soundRadius);
        }
    }
}
