using Enemy;
using Player;
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
        [SerializeField] private SoundProducer soundProducer;
        [SerializeField] private ParticleSystem particle;
        [SerializeField] private float baseCaptureTime = 2.0f;
        [SerializeField] private float starTimeMultiplier = 1.75f;
        [SerializeField] private float validationInterval = 0.5f;
        [SerializeField, Min(0f)] private float lostTargetGracePeriod = 1f;
        [SerializeField, Range(0f, 1f)] private float lostTargetProgressPenalty = 0.2f;
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
        readonly NetworkVariable<Vector3> aimOrigin = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        readonly NetworkVariable<Vector3> aimForward = new(
            Vector3.forward,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
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
        float lostTargetTimer;
        float basePitch = 1f;
        float baseEmissionRate;
        bool targetIsValid;
        bool suppressStopSound;
        AudioClip defaultAudioClip;
        bool defaultAudioLoop;
        float chargeDrainAccumulator;
        CameraMovement aimCamera;
        float nextAimUpdateTime;

        private void Start()
        {
            detector = GetComponent<EntityDetector>();
            if (soundProducer == null) soundProducer = GetComponent<SoundProducer>();
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
            aimCamera = player.GetComponentInChildren<CameraMovement>(true);
            if (aimCamera == null)
            {
                Debug.LogWarning($"---LeafBlower: No owner camera found for {name}");
                return;
            }

            SwitchStateServerRpc(aimCamera.transform.position, aimCamera.transform.forward);
        }

        [Rpc(SendTo.Server)]
        private void SwitchStateServerRpc(Vector3 origin, Vector3 forward, RpcParams rpcParams = default)
        {
            if (!IsHeldByClient(rpcParams.Receive.SenderClientId)) return;
            if (!TrySetAim(rpcParams.Receive.SenderClientId, origin, forward)) return;
            if (!isOn.Value && Charge.Value <= 0) return;

            isOn.Value = !isOn.Value;
        }

        public override void TurnOffServer()
        {
            if (isOn.Value)
            {
                isOn.Value = false;
            }
        }

        private void SwitchState(bool oldV, bool newV)
        {
            if (soundProducer != null)
            {
                if (newV) soundProducer.StartContinuousNoise();
                else soundProducer.StopContinuousNoise();
            }

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
            UpdateOwnerAim();
            UpdateFeedback();

            if (!IsServer || !isOn.Value) return;

            currentHoldTime += targetIsValid ? Time.deltaTime : 0f;
            if (!targetIsValid && currentTarget != null)
            {
                lostTargetTimer += Time.deltaTime;
                ApplyLostTargetPenalty();
            }

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
                if (currentTarget.TryCaptureServer())
                {
                    PlayCaptureSuccessSoundRpc();
                    isOn.Value = false;
                    ResetCaptureProgress();
                }
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

        private void UpdateOwnerAim()
        {
            if (!isOn.Value) return;

            if (aimCamera == null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                aimCamera = NetworkManager.Singleton.LocalClient.PlayerObject
                    .GetComponentInChildren<CameraMovement>(true);
            }

            if (NetworkManager.Singleton == null ||
                !IsHeldByClient(NetworkManager.Singleton.LocalClientId) ||
                aimCamera == null ||
                Time.time < nextAimUpdateTime)
            {
                return;
            }

            nextAimUpdateTime = Time.time + 0.05f;
            UpdateAimServerRpc(aimCamera.transform.position, aimCamera.transform.forward);
        }

        [Rpc(SendTo.Server)]
        private void UpdateAimServerRpc(Vector3 origin, Vector3 forward, RpcParams rpcParams = default)
        {
            TrySetAim(rpcParams.Receive.SenderClientId, origin, forward);
        }

        private bool TrySetAim(ulong clientId, Vector3 origin, Vector3 forward)
        {
            if (!IsHeldByClient(clientId) || forward.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) ||
                client.PlayerObject == null ||
                (origin - client.PlayerObject.transform.position).sqrMagnitude > 9f)
            {
                return false;
            }

            aimOrigin.Value = origin;
            aimForward.Value = forward.normalized;
            return true;
        }

        private void ValidateTarget()
        {
            GameObject entityObject = detector.EntityInSight(aimOrigin.Value, aimForward.Value, true);
            Entity detectedTarget = entityObject != null ? entityObject.GetComponent<Entity>() : null;

            if (detectedTarget == null || detectedTarget.isDead.Value)
            {
                if (currentTarget == null || currentTarget.isDead.Value)
                {
                    ResetCaptureProgress();
                }
                else
                {
                    targetIsValid = false;
                }

                return;
            }

            if (detectedTarget != currentTarget)
            {
                ResetCaptureProgress();
            }

            currentTarget = detectedTarget;
            lostTargetTimer = 0f;
            // Recalculate every tick so weakness stacks (Subwoofer/Beam/Landmine/Taser+Water) are reflected mid-capture.
            currentRequiredHoldTime = CalculateRequiredHoldTime(detectedTarget);
            targetIsValid = true;
        }

        private float CalculateRequiredHoldTime(Entity target)
        {
            int stars = Mathf.RoundToInt(target.dangerLevel);

            // Each weakness stack (Subwoofer, Beam, Landmine, Taser+Water) reduces effective stars by 1.
            stars = Mathf.Max(0, stars - target.GetWeakStacks());

            return baseCaptureTime * Mathf.Pow(starTimeMultiplier, Mathf.Max(0, stars - 1));
        }

        private void ResetCaptureProgress()
        {
            currentTarget = null;
            currentHoldTime = 0f;
            currentRequiredHoldTime = 0f;
            validationTimer = 0f;
            lostTargetTimer = 0f;
            targetIsValid = false;

            if (IsServer) CaptureProgressNormalized.Value = 0f;
        }

        private void ApplyLostTargetPenalty()
        {
            if (lostTargetGracePeriod <= 0f || lostTargetProgressPenalty <= 0f) return;

            while (lostTargetTimer >= lostTargetGracePeriod)
            {
                lostTargetTimer -= lostTargetGracePeriod;
                currentHoldTime = Mathf.Max(
                    0f,
                    currentHoldTime - currentRequiredHoldTime * lostTargetProgressPenalty);
            }
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

        public override Quaternion GetRemoteGripRotation(Inventory inventory)
        {
            return base.GetRemoteGripRotation(inventory);
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