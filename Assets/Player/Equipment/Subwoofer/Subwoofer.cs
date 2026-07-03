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

        public NetworkVariable<bool> isOn = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        protected override void ExecuteItemAction(GameObject player)
        {
            if (!isOn.Value) // turn on
            {
                audioS.Play();
                isOn.Value = true;
            }
            else // turn off
            {
                audioS.Pause();
                isOn.Value = false;
            }
        }

        private float checkTimer = 0f;
        private const float CheckInterval = 0.5f;
        private Collider[] hitColliders = new Collider[10];

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
                if (hitColliders[i].TryGetComponent<Enemy.Enemy>(out var enemy))
                {
                    enemy.ApplyEffect(EffectType.Weak, 2f);
                }
                hitColliders[i] = null;
            }
        }
    }
}