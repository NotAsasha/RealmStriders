using UnityEngine;

namespace Enemy.Casino
{
    [DisallowMultipleComponent]
    public sealed class CasinoSpinPresentation : MonoBehaviour
    {
        [SerializeField] private Transform handle;
        [SerializeField] private AnimationClip handleClip;
        [SerializeField] private AudioSource effectsAudio;
        [SerializeField] private AudioSource drumAudio;
        [SerializeField] private AudioClip handleSound;
        [SerializeField] private AudioClip drumLoop;
        [SerializeField] private AudioClip stopSound;
        [SerializeField, Min(0.01f)] private float referenceRotationsPerSecond = 5f;
        [SerializeField, Range(0.1f, 1f)] private float minimumDrumPitch = 0.3f;
        [SerializeField, Range(1f, 3f)] private float maximumDrumPitch = 1.2f;

        private Vector3 handleRestPosition;
        private Quaternion handleRestRotation;
        private float handleTime;
        private float drumVolume;
        private bool animatingHandle;

        private void Awake()
        {
            if (handle != null)
            {
                handleRestPosition = handle.localPosition;
                handleRestRotation = handle.localRotation;
            }
            if (drumAudio != null) drumVolume = drumAudio.volume;
        }

        public void BeginSpin()
        {
            StopSpin();
            handleTime = 0f;
            animatingHandle = handle != null && handleClip != null;
            if (effectsAudio != null && handleSound != null) effectsAudio.PlayOneShot(handleSound);
            if (drumAudio == null || drumLoop == null) return;
            drumAudio.clip = drumLoop;
            drumAudio.loop = true;
            drumAudio.volume = 0f;
            drumAudio.pitch = minimumDrumPitch;
            drumAudio.Play();
        }

        public void SetDrumSpeed(float rotationsPerSecond)
        {
            if (drumAudio == null) return;
            float speed = Mathf.Clamp01(Mathf.Abs(rotationsPerSecond) / referenceRotationsPerSecond);
            drumAudio.pitch = Mathf.Lerp(minimumDrumPitch, maximumDrumPitch, speed);
            drumAudio.volume = drumVolume * Mathf.Sqrt(speed);
        }

        public void CompleteSpin()
        {
            StopSpin();
            if (effectsAudio != null && stopSound != null) effectsAudio.PlayOneShot(stopSound);
        }

        public void StopSpin()
        {
            animatingHandle = false;
            if (handle != null)
            {
                handle.localPosition = handleRestPosition;
                handle.localRotation = handleRestRotation;
            }
            if (effectsAudio != null) effectsAudio.Stop();
            if (drumAudio != null)
            {
                drumAudio.Stop();
                drumAudio.volume = drumVolume;
                drumAudio.pitch = 1f;
            }
        }

        private void LateUpdate()
        {
            if (!animatingHandle) return;
            handleTime = Mathf.Min(handleTime + Time.deltaTime, handleClip.length);
            // The dormant body Animator is paused and also keys the handle.
            // Sample only the lever after it, without waking the monster.
            handleClip.SampleAnimation(handle.gameObject, handleTime);
            if (handleTime >= handleClip.length) animatingHandle = false;
        }

        private void OnDisable()
        {
            StopSpin();
        }
    }
}
