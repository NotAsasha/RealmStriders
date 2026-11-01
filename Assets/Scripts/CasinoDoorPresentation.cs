using UnityEngine;

namespace Game.Casino
{
    public sealed class CasinoDoorPresentation : MonoBehaviour
    {
        [Header("Authored Animator (optional until configured)")]
        [SerializeField] private Animator doorAnimator;
        [Tooltip("Full state path, e.g. Base Layer.Closed")]
        [SerializeField] private string closedState = "Base Layer.Closed";
        [Tooltip("Non-looping opening state. Its final pose must keep the door open.")]
        [SerializeField] private string openingState = "Base Layer.Opening";
        [SerializeField, Min(0)] private int animatorLayer;
        [Tooltip("Match the opening state's duration at its configured playback speed. Used for late joins.")]
        [SerializeField, Min(0.01f)] private float openingDuration = 4.65f;

        [Header("Enable when the cage is installed")]
        [Tooltip("Lights are enabled without changing their authored color or intensity.")]
        [SerializeField] private Light[] activationLights = System.Array.Empty<Light>();
        [Tooltip("Keep these objects active and turn Play On Awake off. Looping effects also start for late joiners.")]
        [SerializeField] private ParticleSystem[] activationParticles = System.Array.Empty<ParticleSystem>();
        [SerializeField] private AudioSource openingSound;

        private bool activated;
        public bool IsComplete { get; private set; }
        public float Duration => Mathf.Max(0.01f, openingDuration);

        public void ResetPresentation()
        {
            activated = false;
            IsComplete = false;
            PlayState(closedState, 0f);
            foreach (var light in activationLights)
                if (light != null) light.enabled = false;
            foreach (var particles in activationParticles)
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (openingSound != null) openingSound.Stop();
        }

        public void Sample(float elapsed, bool playSound)
        {
            elapsed = Mathf.Max(0f, elapsed);
            IsComplete = elapsed >= Duration;
            if (activated) return;
            activated = true;

            // Seek once from server time; the authored Animator owns all subsequent motion.
            PlayState(openingState, Mathf.Clamp01(elapsed / Duration));
            foreach (var light in activationLights)
                if (light != null) light.enabled = true;
            foreach (var particles in activationParticles)
            {
                if (particles == null) continue;
                if (playSound || particles.main.loop) particles.Play(true);
            }
            if (playSound && elapsed < 0.5f && openingSound != null) openingSound.Play();
        }

        private void PlayState(string state, float normalizedTime)
        {
            if (doorAnimator == null || doorAnimator.runtimeAnimatorController == null ||
                !doorAnimator.isActiveAndEnabled || string.IsNullOrEmpty(state)) return;
            if (animatorLayer < 0 || animatorLayer >= doorAnimator.layerCount)
            {
                Debug.LogWarning($"Casino gate Animator layer index {animatorLayer} is invalid; controller has {doorAnimator.layerCount} layers.", this);
                return;
            }
            int hash = Animator.StringToHash(state);
            if (!doorAnimator.HasState(animatorLayer, hash))
            {
                Debug.LogWarning($"Casino gate Animator state '{state}' was not found in layer '{doorAnimator.GetLayerName(animatorLayer)}' (index {animatorLayer}). Closed and Opening must be states inside the layer, not layer names.", this);
                return;
            }
            doorAnimator.Play(hash, animatorLayer, normalizedTime);
            doorAnimator.Update(0f);
        }
    }
}
