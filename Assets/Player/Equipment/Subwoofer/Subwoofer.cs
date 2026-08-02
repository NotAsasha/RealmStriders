using UnityEngine;
using Player.Equipment;
using Unity.Netcode;

namespace Player.Equipment.Subwoofer
{
    public class Subwoofer : Item
    {
        [SerializeField] float effectRadius = 5f;
        [SerializeField] LayerMask entityLayer;
        [SerializeField] AudioSource audioS;

        [SerializeField] GameObject effectBubble;
        [SerializeField] Renderer indicator;

        private float checkTimer = 0f;
        private const float CheckInterval = 0.5f;
        private Collider[] hitColliders = new Collider[10];

        public NetworkVariable<bool> isOn = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private void OnEnable()
        {
            isOn.OnValueChanged += TurnOnEffects;
            TurnOnEffects(false, isOn.Value);
        }
        private void OnDisable()
        {
            isOn.OnValueChanged -= TurnOnEffects;
        }

        private void TurnOnEffects(bool _, bool newV)
        {
            effectBubble.SetActive(newV);

            if (isOn.Value) // turn on
            {
                indicator.material.color = Color.green;
                audioS.Play();
            }
            else // turn off
            {
                indicator.material.color = Color.red;
                audioS.Pause();
            }
        }

        override protected void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SwitchStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        private void Update()
        {
            if (!isOn.Value || isCurrentlyHeld) return;

            checkTimer += Time.deltaTime;
            if (checkTimer >= CheckInterval)
            {
                checkTimer = 0f;
                CheckAuraEffects();
            }
        }

        private void CheckAuraEffects()
        {
            int numColliders = Physics.OverlapSphereNonAlloc(
                transform.position,
                effectRadius,
                hitColliders,
                entityLayer,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < numColliders; i++)
            {
                if (hitColliders[i].TryGetComponent<Entity>(out var enemy))
                {
                    if (enemy.isDead.Value) continue;
                    Debug.Log($"---SubWoofer: Applied Weekness to {enemy.name}");
                    enemy.ApplyEffect(EffectType.Weak, 2f);
                }
                hitColliders[i] = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentColor = Color.blueViolet;
            transparentColor.a = 0.15f;
            Gizmos.color = transparentColor;

            Gizmos.DrawSphere(transform.position, effectRadius);

            Color wireColor = Color.blueViolet;
            wireColor.a = 0.7f;
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(transform.position, effectRadius);
        }
    }
}