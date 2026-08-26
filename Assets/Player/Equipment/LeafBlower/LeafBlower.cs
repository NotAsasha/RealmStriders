using Enemy;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Player.Equipment.LeafBlower
{
    [RequireComponent(typeof(EntityDetector))]
    public class LeafBlower : Item
    {
        [SerializeField] LayerMask entityLayer;
        [SerializeField] ParticleSystem particle;

        NetworkVariable<bool> isOn = new(false, 0, 0);

        EntityDetector detector;

        private void Start()
        {
            detector = GetComponent<EntityDetector>();
        }

        public override void OnNetworkSpawn()
        {
            isOn.OnValueChanged += SwitchState;
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= SwitchState;
        }

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SwitchStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        private void SwitchState(bool oldV, bool newV)
        {
            if (newV)
            {
                particle.Play();
            }
            else
            {
                particle.Stop();
            }
        }

        float cooldown = 0.5f;
        float currTime = 0f;
        private void Update()
        {
            if (!isOn.Value) return;
            detector.DrawViewState();

            //peep once per second
            currTime += Time.deltaTime;
            if (currTime < cooldown) return;
            currTime = 0f;
            GameObject entityObj = detector.EntityInSight(true);
            if (entityObj)
            {
                Entity entity = entityObj.GetComponent<Entity>();
                if (entity.GetHealth() < 1f && !entity.isDead.Value)
                {
                    entity.TurnIntoSphereServerRpc();
                }

                Debug.Log($"---LeafBlower: Entity danger: {entity.GetHealth()}");
                Debug.Log("Blowing leaves with powerful wind!");
            }
        }

        #endregion
    }
}