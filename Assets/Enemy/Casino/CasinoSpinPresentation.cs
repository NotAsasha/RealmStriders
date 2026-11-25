using UnityEngine;

namespace Enemy.Casino
{
    [DisallowMultipleComponent]
    public sealed class CasinoSpinPresentation : MonoBehaviour
    {
        private const float FastClickInterval = 0.075f;
        private const float SlowClickInterval = 0.28f;
        private const float SlowdownStartProgress = 0.35f;

        [SerializeField] private Transform handle;
        [SerializeField] private AnimationClip handleClip;
        [SerializeField] private AudioSource effectsAudio;
        [SerializeField] private AudioSource drumAudio;
        [SerializeField] private AudioClip handleSound;
        [SerializeField] private AudioClip drumClick;
        [SerializeField] private AudioClip stopSound;

        private Vector3 handleRestPosition;
        private Quaternion handleRestRotation;
        private float handleTime;
        private float nextDrumClickTime = float.PositiveInfinity;
        private bool animatingHandle;

        private void Awake()
        {
            if (handle != null)
            {
                handleRestPosition = handle.localPosition;
                handleRestRotation = handle.localRotation;
            }
        }

        public void BeginSpin()
        {
            StopSpin();
            handleTime = 0f;
            nextDrumClickTime = FastClickInterval;
            animatingHandle = handle != null && handleClip != null;
            if (effectsAudio != null && handleSound != null) effectsAudio.PlayOneShot(handleSound);
        }

        public void UpdateDrumClicks(float elapsedTime, float progress)
        {
            if (elapsedTime < nextDrumClickTime || progress >= 1f || drumAudio == null || drumClick == null) return;

            drumAudio.PlayOneShot(drumClick);
            float slowdown = Mathf.InverseLerp(SlowdownStartProgress, 1f, progress);
            nextDrumClickTime = elapsedTime + Mathf.SmoothStep(FastClickInterval, SlowClickInterval, slowdown);
        }

        public void CompleteSpin()
        {
            StopSpin();
            if (effectsAudio != null && stopSound != null) effectsAudio.PlayOneShot(stopSound);
        }

        public void StopSpin()
        {
            animatingHandle = false;
            nextDrumClickTime = float.PositiveInfinity;
            if (handle != null)
            {
                handle.localPosition = handleRestPosition;
                handle.localRotation = handleRestRotation;
            }
            if (effectsAudio != null) effectsAudio.Stop();
            if (drumAudio != null) drumAudio.Stop();
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
