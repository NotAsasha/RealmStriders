using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public abstract class Entity : NetworkBehaviour
    {
        public float dangerLevel = 1f;
        [SerializeField] NetworkObject glassCage; 

        public NetworkVariable<bool> isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<float> entityHealth = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        protected Dictionary<EffectType, NetworkVariable<bool>> effects;


        #region Initialization

        protected virtual void Awake()
        {
            effects = new Dictionary<EffectType, NetworkVariable<bool>>()
            {
                { EffectType.Freeze,     new NetworkVariable<bool>(false) },
                { EffectType.Water,      new NetworkVariable<bool>(false) },
                { EffectType.Fire,       new NetworkVariable<bool>(false) },
                { EffectType.Invincible, new NetworkVariable<bool>(false) }
            };
        }

        public override void OnNetworkSpawn()
        {
            isDead.OnValueChanged += OnDeathStateChange;
            effects[EffectType.Freeze].OnValueChanged += OnFreezeStateChange;
        }
        public override void OnNetworkDespawn()
        {
            isDead.OnValueChanged -= OnDeathStateChange;
            effects[EffectType.Freeze].OnValueChanged -= OnFreezeStateChange;
        }

        #endregion

        public bool IsDead() => isDead.Value;
        public float GetHealth() => entityHealth.Value;
        public void AddHealth(float health)
        {
            if (IsEffectActive(EffectType.Invincible)) return;


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


        Coroutine cor;
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ApplyEffectServerRpc(EffectType type, float seconds)
        {
            if (IsEffectActive(EffectType.Invincible)) return;


            if (IsEffectActive(type) && cor != null)
            {
                StopCoroutine(cor);
            }
            cor = StartCoroutine(EffectTimer(type, seconds));
        }

        private IEnumerator EffectTimer(EffectType type, float duration)
        {
            var status = effects[type];

            status.Value = true;
            yield return new WaitForSeconds(duration);
            status.Value = false;
            cor = null;
        }

        public bool IsEffectActive(EffectType type) => effects[type].Value;

        #endregion

        protected void OnDeathStateChange(bool oldValue, bool isDead)
        {
            if (this.isDead.Value) KillEntity();
            else ReviveEntity();
        }
        virtual protected void OnFreezeStateChange(bool oldV, bool newV) { }

        virtual protected void KillEntity() { }
        virtual protected void ReviveEntity() { }
    }
    public enum EffectType
    {
        Freeze,
        Water,
        Fire,
        Invincible
    }
}