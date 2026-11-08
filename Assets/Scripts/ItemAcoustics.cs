using Game.Audio;
using Player.Movement;
using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Game.Audio
{
    [DisallowMultipleComponent]
    public sealed class ItemAcoustics : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SteamAudioSource acousticSource;
        private float nextRefresh;

        private void OnEnable()
        {
            SceneAcoustics.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            SceneAcoustics.Changed -= Refresh;
            if (acousticSource == null) return;
            acousticSource.pathing = false;
            acousticSource.pathingProbeBatch = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            if (audioSource == null || acousticSource == null) return;
            SceneAcoustics.TryGetAtPosition(gameObject.scene, transform.position, out SceneAcoustics environment);
            Transform listener = CameraMovement.Instance != null ? CameraMovement.Instance.AudioListenerTransform : null;
            acousticSource.pathingProbeBatch = environment != null ? environment.ProbeBatch : null;
            float range = audioSource.maxDistance;
            // Inventory owns mute/playback; acoustic binding must never unmute a stored item.
            acousticSource.pathing = audioSource.isActiveAndEnabled && !audioSource.mute &&
                listener != null && environment != null && environment.Contains(listener.position) &&
                (transform.position - listener.position).sqrMagnitude < range * range;
        }

        public static void Configure(SteamAudioSource source)
        {
            source.distanceAttenuation = true;
            source.distanceAttenuationInput = DistanceAttenuationInput.CurveDriven;
            source.interpolation = HRTFInterpolation.Bilinear;
            source.occlusion = true;
            source.transmission = true;
            source.transmissionType = TransmissionType.FrequencyDependent;
            source.maxTransmissionSurfaces = 2;
            source.directMixLevel = 1f;
            source.pathingMixLevel = 0.8f;
            source.pathing = false;
            source.pathingProbeBatch = null;
            source.pathValidation = true;
            source.findAlternatePaths = true;
            source.applyHRTFToPathing = true;
            source.reflections = false;
        }

        public static void PlayAt(AudioClip clip, Vector3 position, AudioSource template)
        {
            if (clip == null || template == null) return;
            var emitter = new GameObject("Item one-shot audio");
            emitter.SetActive(false);
            emitter.transform.position = position;
            var audio = emitter.AddComponent<AudioSource>();
            audio.clip = clip;
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.spatialize = true;
            audio.spatializePostEffects = true;
            audio.dopplerLevel = 0f;
            audio.volume = template.volume;
            audio.pitch = template.pitch;
            audio.priority = template.priority;
            audio.outputAudioMixerGroup = template.outputAudioMixerGroup;
            audio.minDistance = template.minDistance;
            audio.maxDistance = template.maxDistance;
            audio.rolloffMode = template.rolloffMode;
            if (audio.rolloffMode == AudioRolloffMode.Custom)
                audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, template.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            var acoustic = emitter.AddComponent<SteamAudioSource>();
            Configure(acoustic);
            var binding = emitter.AddComponent<ItemAcoustics>();
            binding.audioSource = audio;
            binding.acousticSource = acoustic;
            emitter.SetActive(true);
            audio.Play();
            Destroy(emitter, clip.length / Mathf.Max(0.01f, Mathf.Abs(audio.pitch)) + 0.25f);
        }
    }
}
