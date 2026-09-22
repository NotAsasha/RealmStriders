using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public abstract class Entity : NetworkBehaviour
    {
        public float dangerLevel = 1f;
        [SerializeField] private NetworkObject glassCage;
        [Header("Capture sequence")]
        [SerializeField, Min(0.1f)] private float captureSequenceDuration = 0.85f;
        [SerializeField, Min(0f)] private float captureLift = 1.1f;
        [SerializeField] private CaptureTransformationController captureTransformation;

        public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> entityHealth = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isFrozenNet = new(false);
        private readonly NetworkVariable<bool> isWaterNet = new(false);
        private readonly NetworkVariable<bool> isFireNet = new(false);
        private readonly NetworkVariable<bool> isInvincibleNet = new(false);
        private readonly NetworkVariable<bool> isWeakNet = new(false);
        private readonly NetworkVariable<bool> isAsleepNet = new(false);
        private readonly NetworkVariable<bool> isCapturingNet = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        protected Dictionary<EffectType, NetworkVariable<bool>> effects;

        private Dictionary<EffectType, Coroutine> activeCoroutines = new();

        public bool IsBeingCaptured => isCapturingNet.Value;

        #region Initialization

        protected virtual void Awake()
        {
            if (captureTransformation == null)
            {
                captureTransformation = GetComponent<CaptureTransformationController>();
                if (captureTransformation == null)
                {
                    captureTransformation = gameObject.AddComponent<CaptureTransformationController>();
                }
            }

            effects = new Dictionary<EffectType, NetworkVariable<bool>>()
            {
                { EffectType.Freeze,     isFrozenNet },
                { EffectType.Water,      isWaterNet },
                { EffectType.Fire,       isFireNet },
                { EffectType.Invincible, isInvincibleNet },
                { EffectType.Weak, isWeakNet },
                { EffectType.Asleep, isAsleepNet }
            };
        }

        public override void OnNetworkSpawn()
        {
            isDead.OnValueChanged += OnDeathStateChange;
            effects[EffectType.Freeze].OnValueChanged += OnFreezeStateChange;
            effects[EffectType.Weak].OnValueChanged += OnWeakStateChange;
        }

        public override void OnNetworkDespawn()
        {
            isDead.OnValueChanged -= OnDeathStateChange;
            effects[EffectType.Freeze].OnValueChanged -= OnFreezeStateChange;
            effects[EffectType.Weak].OnValueChanged -= OnWeakStateChange;

        }

        #endregion

        public bool IsDead() => isDead.Value;
        public float GetHealth() => entityHealth.Value - (IsEffectActive(EffectType.Weak)? 1f : 0f);

        public void AddHealth(float health)
        {
            if (!IsServer) return;
            if (IsBeingCaptured) return;
            if (health < 0 && IsEffectActive(EffectType.Invincible)) return;

            entityHealth.Value += health;
            if (entityHealth.Value <= 0 && !isDead.Value)
            {
                isDead.Value = true;
                Debug.Log($"---Enemy {name} was killed!");
            }
        }

        /// <summary>
        /// Starts the server-authoritative capture sequence. The visual work is performed by
        /// clients after <see cref="PlayCaptureSequenceRpc"/>; this coroutine only schedules
        /// the final network state change and, for enemies, the network spawn/despawn.
        /// </summary>
        public bool TryCaptureServer()
        {
            if (!IsServer || !IsSpawned || isDead.Value || IsBeingCaptured)
            {
                return false;
            }

            if (this is not Human && glassCage == null)
            {
                return false;
            }

            isCapturingNet.Value = true;
            BeginCaptureLockServer();

            Vector3 collapsePoint = transform.position + Vector3.up * captureLift;
            double serverStartTime = NetworkManager.ServerTime.Time;
            PlayCaptureSequenceRpc(collapsePoint, serverStartTime);
            StartCoroutine(FinalizeCaptureAfterDelay(collapsePoint));
            return true;
        }

        private IEnumerator FinalizeCaptureAfterDelay(Vector3 collapsePoint)
        {
            yield return new WaitForSecondsRealtime(captureSequenceDuration);

            if (!IsServer || !IsSpawned || !IsBeingCaptured) yield break;

            if (this is Human)
            {
                isDead.Value = true;
                isCapturingNet.Value = false;
                yield break;
            }

            var glass = Instantiate(glassCage, collapsePoint, Quaternion.identity);
            glass.Spawn();

            // Keep existing death-dependent systems consistent until this entity is removed.
            isDead.Value = true;
            OnCaptureFinalizedServer();
            NetworkObject.Despawn(true);
        }

        [Rpc(SendTo.Everyone)]
        private void PlayCaptureSequenceRpc(Vector3 collapsePoint, double serverStartTime)
        {
            captureTransformation?.Play(collapsePoint, serverStartTime, captureSequenceDuration);
        }

        #region Effects

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ApplyEffectServerRpc(EffectType type, float seconds)
        {
            ApplyEffect(type, seconds);
        }

        public void ApplyEffect(EffectType type, float seconds)
        {
            if (!IsServer || isDead.Value || IsBeingCaptured) return;

            if (IsEffectActive(EffectType.Invincible) && type != EffectType.Invincible) return;

            if (activeCoroutines.TryGetValue(type, out Coroutine existingCor))
            {
                if (existingCor != null) StopCoroutine(existingCor);
                activeCoroutines.Remove(type);
            }

            activeCoroutines[type] = StartCoroutine(EffectTimer(type, seconds));
        }

        private IEnumerator EffectTimer(EffectType type, float duration)
        {
            var status = effects[type];
            status.Value = true;

            yield return new WaitForSeconds(duration);

            status.Value = false;
            activeCoroutines.Remove(type);
        }

        public bool IsEffectActive(EffectType type) => effects[type].Value;

        #endregion

        protected void OnDeathStateChange(bool oldValue, bool isDead)
        {
            if (this.isDead.Value) KillEntity();
            else ReviveEntity();
        }

        virtual protected void OnFreezeStateChange(bool oldV, bool newV) { }
        virtual protected void OnWeakStateChange(bool oldV, bool newV) { }
        protected virtual void BeginCaptureLockServer() { }
        protected virtual void OnCaptureFinalizedServer() { }

        virtual protected void KillEntity() { }
        virtual protected void ReviveEntity() { }
    }

    public enum EffectType
    {
        Freeze,
        Water,
        Fire,
        Invincible,
        Weak,
        Asleep
    }
}
