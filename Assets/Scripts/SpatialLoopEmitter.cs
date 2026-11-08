using System.Collections;
using Player.Movement;
using SteamAudio;
using UnityEngine;

namespace Game.Audio
{
    [DisallowMultipleComponent]
    public sealed class SpatialLoopEmitter : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SteamAudioSource acousticSource;
        [SerializeField] private SceneAcoustics environment;
        [SerializeField, Min(0f)] private float startOffsetSeconds;

        private Coroutine playback;

        private void OnEnable()
        {
            audioSource.mute = true;
            acousticSource.pathing = false;
            acousticSource.pathingProbeBatch = environment != null ? environment.ProbeBatch : null;
            playback = StartCoroutine(BeginPlayback());
        }

        private IEnumerator BeginPlayback()
        {
            AudioClip clip = audioSource.clip;
            if (clip == null) yield break;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading) yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded) yield break;
            audioSource.timeSamples = (int)(startOffsetSeconds * clip.frequency) % clip.samples;
            audioSource.Play();
            playback = null;
        }

        private void LateUpdate()
        {
            CameraMovement camera = CameraMovement.Instance;
            Transform listener = camera != null ? camera.AudioListenerTransform : null;
            bool nearby = listener != null && (audioSource.transform.position - listener.position).sqrMagnitude <
                audioSource.maxDistance * audioSource.maxDistance;
            // Keep native DSP tails/stale route output from making a local machine audible across the base.
            audioSource.mute = !nearby;
            acousticSource.pathing = nearby && environment != null &&
                environment.Contains(audioSource.transform.position) && environment.Contains(listener.position);
        }

        private void OnDisable()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
            audioSource.Stop();
            acousticSource.pathing = false;
            acousticSource.pathingProbeBatch = null;
        }
    }
}
