using Enemy;
using UnityEngine;

namespace Player.Equipment.WaterGun
{
    [RequireComponent(typeof(EntityDetector))]
    public class WaterGun : Item
    {
        [SerializeField] private float effectTime = 2f;
        [SerializeField] ParticleSystem particle;

        EntityDetector detector;

        private void Awake()
        {
            detector = GetComponent<EntityDetector>();
        }

        override protected void ExecuteItemAction(GameObject player)
        {
            particle.Play();

            GameObject entityObj = detector.EntityInSight(true);
            if (entityObj)
            {
                Entity entity = entityObj.GetComponent<Entity>();
                entity.ApplyEffectServerRpc(EffectType.Water, effectTime);
                Debug.Log($"---WaterGun: Shot entity: {entityObj.name}");
            }
            else Debug.Log($"---WaterGun: Missed :(");
        }
    }
}