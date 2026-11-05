using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRandomAudio : NetworkBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Min(0.1f)] private float interval = 10f;
        [SerializeField, Min(0f)] private float intervalDeviation = 2f;
        [SerializeField, Range(0.01f, 3f)] private float minPitch = 0.9f;
        [SerializeField, Range(0.01f, 3f)] private float maxPitch = 1.1f;
        [Tooltip("Extra time for the RPC to reach clients before playback, in seconds.")]
        [SerializeField, Min(0f)] private float synchronizationDelay = 0.3f;

        private double nextTriggerTime;

        public override void OnNetworkSpawn()
        {
            if (IsClient && audioSource != null)
            {
                audioSource.Stop();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
            }

            if (IsServer) ScheduleNext();
        }

        public override void OnNetworkDespawn()
        {
            StopPlayback();
        }

        private void OnDisable()
        {
            StopPlayback();
        }

        private void OnEnable()
        {
            if (IsSpawned && IsServer) ScheduleNext();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || NetworkManager.ServerTime.Time < nextTriggerTime) return;

            ScheduleNext();
            int clipIndex = ChooseClip();
            if (clipIndex < 0) return;

            float pitch = Random.Range(minPitch, Mathf.Max(minPitch, maxPitch));
            PlaySoundClientRpc(clipIndex, pitch,
                NetworkManager.ServerTime.Time + Mathf.Max(0f, synchronizationDelay));
        }

        private void ScheduleNext()
        {
            float deviation = Mathf.Max(0f, intervalDeviation);
            nextTriggerTime = NetworkManager.ServerTime.Time +
                Mathf.Max(0.1f, interval + Random.Range(-deviation, deviation));
        }

        private int ChooseClip()
        {
            if (clips == null) return -1;

            // Reservoir sampling keeps empty Inspector slots out of the random selection.
            int selected = -1;
            int count = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && Random.Range(0, ++count) == 0) selected = i;
            }
            return selected;
        }

        [ClientRpc]
        private void PlaySoundClientRpc(int clipIndex, float pitch, double startTime)
        {
            if (!IsClient || !IsSpawned || !isActiveAndEnabled || audioSource == null ||
                clips == null || clipIndex < 0 || clipIndex >= clips.Length || clips[clipIndex] == null) return;

            StartCoroutine(PlayAtTime(clips[clipIndex], pitch, startTime));
        }

        private IEnumerator PlayAtTime(AudioClip clip, float pitch, double startTime)
        {
            while (IsSpawned && NetworkManager.ServerTime.Time < startTime) yield return null;

            if (!IsSpawned || audioSource == null || !audioSource.isActiveAndEnabled) yield break;

            // Late packets seek forward instead of playing the entire sound out of sync.
            double offset = System.Math.Max(0d, NetworkManager.ServerTime.Time - startTime) * pitch;
            if (offset >= clip.length) yield break;

            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = clip;
            audioSource.pitch = pitch;
            audioSource.time = (float)offset;
            audioSource.Play();
        }

        private void StopPlayback()
        {
            StopAllCoroutines();
            if (audioSource != null) audioSource.Stop();
        }

        private void OnValidate()
        {
            interval = Mathf.Max(0.1f, interval);
            intervalDeviation = Mathf.Max(0f, intervalDeviation);
            minPitch = Mathf.Clamp(minPitch, 0.01f, 3f);
            maxPitch = Mathf.Clamp(maxPitch, minPitch, 3f);
            synchronizationDelay = Mathf.Max(0f, synchronizationDelay);
        }
    }
}
