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
        private readonly NetworkVariable<bool> isAsleepNet = new(false);
        private readonly NetworkVariable<bool> isCapturingNet = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Each stack represents one level of weakness applied by a different tool.
        // Stacks are independent: each has its own duration coroutine.
        public readonly NetworkVariable<int> weakStacks = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        protected Dictionary<EffectType, NetworkVariable<bool>> effects;

        // Tracks one active coroutine per weakness slot (tool source).
        private readonly Dictionary<int, Coroutine> weakStackCoroutines = new();
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
                { EffectType.Asleep,     isAsleepNet }
            };
        }

        public override void OnNetworkSpawn()
        {
            isDead.OnValueChanged += OnDeathStateChange;
            effects[EffectType.Freeze].OnValueChanged += OnFreezeStateChange;
            weakStacks.OnValueChanged += OnWeakStacksChanged;
        }

        public override void OnNetworkDespawn()
        {
            isDead.OnValueChanged -= OnDeathStateChange;
            effects[EffectType.Freeze].OnValueChanged -= OnFreezeStateChange;
            weakStacks.OnValueChanged -= OnWeakStacksChanged;
        }

        #endregion

        public bool IsDead() => isDead.Value;

        /// <summary>Returns the entity's effective danger level, reduced by active weakness stacks.</summary>
        public float GetHealth() => entityHealth.Value - weakStacks.Value;

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

        // ----- Weakness stacking -----

        /// <summary>
        /// Adds <paramref name="stacks"/> weakness levels for <paramref name="duration"/> seconds.
        /// Each slot is identified by a key so the same source refreshes rather than double-stacks.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ApplyWeakStackServerRpc(int stacks, float duration, int slot)
        {
            ApplyWeakStack(stacks, duration, slot);
        }

        public void ApplyWeakStack(int stacks, float duration, int slot)
        {
            if (!IsServer || isDead.Value || IsBeingCaptured) return;
            if (IsEffectActive(EffectType.Invincible)) return;

            // Refresh: cancel existing timer for this slot before starting a new one.
            if (weakStackCoroutines.TryGetValue(slot, out Coroutine existing))
            {
                if (existing != null) StopCoroutine(existing);
                weakStackCoroutines.Remove(slot);
            }

            weakStackCoroutines[slot] = StartCoroutine(WeakStackTimer(stacks, duration, slot));
        }

        private IEnumerator WeakStackTimer(int stacks, float duration, int slot)
        {
            weakStacks.Value += stacks;

            yield return new WaitForSeconds(duration);

            weakStacks.Value = Mathf.Max(0, weakStacks.Value - stacks);
            weakStackCoroutines.Remove(slot);
        }

        /// <summary>Returns how many weakness levels are currently active on this entity.</summary>
        public int GetWeakStacks() => weakStacks.Value;

        #endregion

        protected void OnDeathStateChange(bool oldValue, bool isDead)
        {
            if (this.isDead.Value) KillEntity();
            else ReviveEntity();
        }

        virtual protected void OnFreezeStateChange(bool oldV, bool newV) { }
        virtual protected void OnWeakStacksChanged(int oldV, int newV) { }
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
        Asleep
    }
}
