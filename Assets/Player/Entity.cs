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

        public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> entityHealth = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isFrozenNet = new(false);
        private readonly NetworkVariable<bool> isWaterNet = new(false);
        private readonly NetworkVariable<bool> isFireNet = new(false);
        private readonly NetworkVariable<bool> isInvincibleNet = new(false);
        private readonly NetworkVariable<bool> isWeakNet = new(false);

        protected Dictionary<EffectType, NetworkVariable<bool>> effects;

        private Dictionary<EffectType, Coroutine> activeCoroutines = new();

        #region Initialization

        protected virtual void Awake()
        {
            effects = new Dictionary<EffectType, NetworkVariable<bool>>()
            {
                { EffectType.Freeze,     isFrozenNet },
                { EffectType.Water,      isWaterNet },
                { EffectType.Fire,       isFireNet },
                { EffectType.Invincible, isInvincibleNet },
                { EffectType.Weak, isWeakNet }
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
        public float GetHealth() => entityHealth.Value;

        public void AddHealth(float health)
        {
            if (!IsServer) return;
            if (health < 0 && IsEffectActive(EffectType.Invincible)) return;

            entityHealth.Value += health;
            if (entityHealth.Value <= 0 && !isDead.Value)
            {
                isDead.Value = true;
                Debug.Log($"---Enemy {name} was killed!");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TurnIntoSphereServerRpc()
        {
            if (isDead.Value || GetHealth() >= 1) return;
            var glass = Instantiate(glassCage, transform.position, Quaternion.identity);
            glass.Spawn();
            isDead.Value = true;
        }

        #region Effects

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ApplyEffectServerRpc(EffectType type, float seconds)
        {
            ApplyEffect(type, seconds);
        }

        public void ApplyEffect(EffectType type, float seconds)
        {
            if (!IsServer) return;

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

        virtual protected void KillEntity() { }
        virtual protected void ReviveEntity() { }
    }

    public enum EffectType
    {
        Freeze,
        Water,
        Fire,
        Invincible,
        Weak
    }
}