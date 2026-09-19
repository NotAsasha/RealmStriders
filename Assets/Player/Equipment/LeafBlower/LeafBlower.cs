using Enemy;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Player.Equipment.LeafBlower
{
    [RequireComponent(typeof(EntityDetector))]
    public class LeafBlower : Item, IChargeable
    {
        [SerializeField] private ParticleSystem particle;
        [SerializeField] private float baseCaptureTime = 2.0f;
        [SerializeField] private float starTimeMultiplier = 1.75f;
        [SerializeField] private float validationInterval = 0.5f;
        [SerializeField] private float capturePitchIncrease = 0.35f;
        [SerializeField] private Slider captureSlider;
        [SerializeField] private AudioClip captureLoopSound;
        [SerializeField] private AudioClip captureStopSound;
        [SerializeField] private AudioClip captureSuccessSound;
        [SerializeField] private float captureLoopVolume = 1f;
        [SerializeField] private float captureLoopPitchIncrease = 0.25f;
        [SerializeField] private int maxCharge = 100;
        [SerializeField] private float chargeDrainPerSecond = 1f;
        [SerializeField] private TMP_Text chargeText;

        NetworkVariable<bool> isOn = new(false, 0, 0);
        public NetworkVariable<float> CaptureProgressNormalized = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Charge = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        public float CaptureProgress => CaptureProgressNormalized.Value;

        EntityDetector detector;
        Entity currentTarget;
        float currentHoldTime;
        float currentRequiredHoldTime;
        float validationTimer;
        float basePitch = 1f;
        float baseEmissionRate;
        bool targetIsValid;
        bool suppressStopSound;
        AudioClip defaultAudioClip;
        bool defaultAudioLoop;
        float chargeDrainAccumulator;

        private void Start()
        {
            detector = GetComponent<EntityDetector>();
            if (audioSource != null)
            {
                basePitch = audioSource.pitch;
                defaultAudioClip = audioSource.clip;
                defaultAudioLoop = audioSource.loop;
            }
            if (particle != null) baseEmissionRate = particle.emission.rateOverTimeMultiplier;
            if (captureSlider != null)
            {
                captureSlider.minValue = 0f;
                captureSlider.maxValue = 1f;
                captureSlider.SetValueWithoutNotify(0f);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            isOn.OnValueChanged += SwitchState;
            Charge.OnValueChanged += UpdateChargeText;
            UpdateChargeText(0, Charge.Value);
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= SwitchState;
            Charge.OnValueChanged -= UpdateChargeText;
            base.OnNetworkDespawn();
        }

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SwitchStateServerRpc()
        {
            if (!isOn.Value && Charge.Value <= 0) return;

            isOn.Value = !isOn.Value;
        }

        private void SwitchState(bool oldV, bool newV)
        {
            if (particle != null && newV)
            {
                particle.Play();
            }
            else if (particle != null)
            {
                particle.Stop();
            }

            UpdateCaptureAudio(newV);

            if (!newV)
            {
                ResetCaptureProgress();
            }
        }

        private void Update()
        {
            UpdateFeedback();

            if (!IsServer || !isOn.Value) return;

            currentHoldTime += targetIsValid ? Time.deltaTime : 0f;
            validationTimer += Time.deltaTime;
            DrainCharge();

            if (!isOn.Value) return;

            if (validationTimer >= validationInterval)
            {
                validationTimer = 0f;
                ValidateTarget();
            }

            if (targetIsValid && currentHoldTime >= currentRequiredHoldTime)
            {
                currentTarget.TurnIntoSphereServerRpc();
                PlayCaptureSuccessSoundRpc();
                isOn.Value = false;
                ResetCaptureProgress();
            }

            CaptureProgressNormalized.Value = currentRequiredHoldTime > 0f
                ? Mathf.Clamp01(currentHoldTime / currentRequiredHoldTime)
                : 0f;
        }

        private void DrainCharge()
        {
            if (chargeDrainPerSecond <= 0f) return;

            chargeDrainAccumulator += chargeDrainPerSecond * Time.deltaTime;
            int chargeToDrain = Mathf.FloorToInt(chargeDrainAccumulator);
            if (chargeToDrain <= 0) return;

            chargeDrainAccumulator -= chargeToDrain;
            ((IChargeable)this).ModifyCharge(-chargeToDrain);

            if (Charge.Value <= 0)
            {
                isOn.Value = false;
            }
        }

        private void ValidateTarget()
        {
            GameObject entityObject = detector.EntityInSight(true);
            Entity detectedTarget = entityObject != null ? entityObject.GetComponent<Entity>() : null;

            if (detectedTarget == null || detectedTarget.isDead.Value)
            {
                ResetCaptureProgress();
                return;
            }

            if (detectedTarget != currentTarget)
            {
                ResetCaptureProgress();
                currentRequiredHoldTime = CalculateRequiredHoldTime(detectedTarget);
            }

            currentTarget = detectedTarget;
            targetIsValid = true;
        }

        private float CalculateRequiredHoldTime(Entity target)
        {
            int stars = Mathf.RoundToInt(target.dangerLevel);
            return baseCaptureTime * Mathf.Pow(starTimeMultiplier, Mathf.Max(0, stars - 1));
        }

        private void ResetCaptureProgress()
        {
            currentTarget = null;
            currentHoldTime = 0f;
            currentRequiredHoldTime = 0f;
            validationTimer = 0f;
            targetIsValid = false;

            if (IsServer) CaptureProgressNormalized.Value = 0f;
        }

        private void UpdateFeedback()
        {
            float progress = CaptureProgressNormalized.Value;

            if (audioSource != null)
            {
                audioSource.pitch = basePitch + capturePitchIncrease * progress;

                if (isOn.Value && audioSource.isPlaying)
                {
                    audioSource.pitch = basePitch + captureLoopPitchIncrease * progress;
                }
            }

            if (particle != null)
            {
                var emission = particle.emission;
                emission.rateOverTimeMultiplier = baseEmissionRate * (1f + progress);
            }

            if (captureSlider != null)
            {
                captureSlider.SetValueWithoutNotify(progress);
            }

        }

        private void UpdateChargeText(int _, int charge)
        {
            if (chargeText != null)
            {
                chargeText.text = $"{Mathf.Clamp(charge, 0, maxCharge)}%";
            }
        }

        private void UpdateCaptureAudio(bool shouldPlay)
        {
            if (audioSource == null) return;

            if (shouldPlay)
            {
                audioSource.clip = captureLoopSound != null ? captureLoopSound : defaultAudioClip;
                audioSource.loop = true;
                audioSource.volume = captureLoopVolume;
                audioSource.Play();
                return;
            }

            audioSource.Stop();
            audioSource.clip = defaultAudioClip;
            audioSource.loop = defaultAudioLoop;
            audioSource.volume = 1f;
            audioSource.pitch = basePitch;

            if (!suppressStopSound && captureStopSound != null)
            {
                audioSource.PlayOneShot(captureStopSound);
            }

            suppressStopSound = false;
        }

        [Rpc(SendTo.Everyone)]
        private void PlayCaptureSuccessSoundRpc()
        {
            suppressStopSound = true;

            if (audioSource == null || captureSuccessSound == null) return;

            audioSource.PlayOneShot(captureSuccessSound);
        }

        #region IChargeable Implementation

        public int CurrentCharge => Charge.Value;
        public int MaxCharge => maxCharge;

        void IChargeable.ModifyCharge(int amount)
        {
            if (!IsServer) return;

            Charge.Value = Mathf.Clamp(Charge.Value + amount, 0, maxCharge);
        }

        #endregion

        #endregion
    }
}