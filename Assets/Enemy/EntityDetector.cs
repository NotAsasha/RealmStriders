using Player;
using UnityEngine;

namespace Enemy
{
    public class EntityDetector : MonoBehaviour
    {
        [SerializeField] Vector3 eyeLocalPosition = new(0,1,0);
        [SerializeField] float viewDistance = 20.0f;
        [SerializeField] float viewAngle = 60f;
        [SerializeField] LayerMask playerLayer;
        [SerializeField] LayerMask wallLayer;

        private float halfAngle;
        private void Awake()
        {
            halfAngle = viewAngle * 0.5f;
        }

        public GameObject EntityInSight(bool chaseEnemies = false)
        {
            Vector3 eyePosition = transform.position + eyeLocalPosition;
            Collider[] nearbyEntities = Physics.OverlapSphere(eyePosition, viewDistance, playerLayer);

            foreach (Collider entityColl in nearbyEntities)
            {
                if (entityColl.gameObject == gameObject) continue;
                Vector3 direction = entityColl.transform.position - transform.position;
                if (Vector3.Angle(direction, transform.forward) <= halfAngle)
                {
                    // if entity is behind a wall
                    if (Physics.Raycast(eyePosition, direction, direction.magnitude, wallLayer)) continue;

                    //if is dead
                    var entity = entityColl.GetComponent<Entity>();
                    if (entity == null || entity.isDead.Value) continue;

                    //if is enemy
                    if (!chaseEnemies && entityColl.GetComponent<Enemy>() != null) continue;

                    return entityColl.gameObject;
                
                }
            }
            return null;
        }

        public void DrawViewState()
        {
            Vector3 eyePosition = transform.position + eyeLocalPosition;
            Vector3 left = eyePosition + Quaternion.Euler(new Vector3(0, viewAngle / 2f, 0)) * (transform.forward * viewDistance);
            Vector3 right = eyePosition + Quaternion.Euler(-new Vector3(0, viewAngle / 2f, 0)) * (transform.forward * viewDistance);
            Debug.DrawLine(eyePosition, left, Color.yellow);
            Debug.DrawLine(eyePosition, right, Color.yellow);
        }
    }
}
