using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

namespace InventorySystem
{
    public class Stunner : Item
    {
        [SerializeField] LayerMask entityLayer;
        [SerializeField] float maxDistance = 20f;
        [SerializeField] private float cooldown = 10f;
        [SerializeField] private float freezeTime = 2f;
        [SerializeField] TMP_Text danger;

        public NetworkVariable<bool> isReady = new(true, 0);

        public override void OnNetworkSpawn()
        {
            isReady.OnValueChanged += ScreenUpdate;
        }
        public override void OnNetworkDespawn()
        {
            isReady.OnValueChanged -= ScreenUpdate;
        }


        #region Item Specific Functionality

        void ScreenUpdate(bool oldV, bool newV)
        {
            danger.text = isReady.Value ? "Ready" : "Charging";
        }

        override protected void ExecuteItemAction(GameObject player)
        {
            if (!isReady.Value) return;

            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, maxDistance, entityLayer))
            {
                if (hit.transform.TryGetComponent<Entity>(out var entity))
                {
                    entity.ApplyEffectServerRpc(EffectType.Freeze, freezeTime);
                    if (entity.IsEffectActive(EffectType.Water)) entity.AddHealth(-0.5f);

                    Debug.Log($"---Taser: Shot entity: {entity.name}");
                }
            }
            else Debug.Log($"---Taser: Missed :(");

            StartCooldownServerRpc();
            // - Particle effects
            // - Sound effects
            // - Physics interactions with enemies

        }
        #endregion

        private float readyTime;
        [ServerRpc(RequireOwnership = false)]
        public void StartCooldownServerRpc()
        {
            isReady.Value = false;
            readyTime = Time.time + cooldown;
        }

        void Update()
        {
            if (IsServer && isReady.Value == false && Time.time >= readyTime)
            {
                isReady.Value = true;
            }
        }
    }
}