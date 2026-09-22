using Player;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Enemy
{
    public class EntityDetector : MonoBehaviour
    {
        [SerializeField] Vector3 eyeLocalPosition = new(0,1,0);
        [SerializeField] float viewDistance = 20.0f;
        [SerializeField] float viewAngle = 60f;
        [SerializeField, Min(0f)] float verticalPadding = 1.5f;
        [SerializeField] LayerMask playerLayer;
        [SerializeField] LayerMask wallLayer;

        private float halfAngle;
        private void Awake()
        {
            halfAngle = viewAngle * 0.5f;
        }

        private Collider[] nearbyEntities = new Collider[20];
        public GameObject EntityInSight(bool chaseEnemies = false)
        {
            if (viewAngle == 0 || viewDistance == 0) return null;

            Vector3 eyePosition = transform.position + eyeLocalPosition;
            int numColliders = Physics.OverlapCapsuleNonAlloc(
                eyePosition - Vector3.up * verticalPadding,
                eyePosition + Vector3.up * verticalPadding,
                viewDistance,
                nearbyEntities,
                playerLayer,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < numColliders; i++)
            {
                var collider = nearbyEntities[i];
                if (collider == null) continue;

                // Entity colliders commonly live on child bones, so resolve the networked root.
                var entity = collider.GetComponentInParent<Entity>();
                if (entity == null || entity.gameObject == gameObject || !entity.isActiveAndEnabled) continue;

                Vector3 targetPosition = collider.ClosestPoint(eyePosition);
                Vector3 direction = targetPosition - eyePosition;
                if (direction.sqrMagnitude <= Mathf.Epsilon) continue;
                if (Vector3.Angle(direction, transform.forward) <= halfAngle)
                {
                    // if entity is behind a wall
                    if (Physics.Raycast(eyePosition, direction, direction.magnitude, wallLayer)) continue;

                    //if is dead
                    if (entity.isDead.Value) continue;

                    //if is enemy
                    if (!chaseEnemies && entity.GetComponent<Enemy>() != null) continue;

                    return entity.gameObject;
                
                }
            }
            return null;
        }

        public GameObject EntityInHearing()
        {
            var clients = NetworkManager.Singleton.ConnectedClientsList;

            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];

                if (client.PlayerObject == null) continue;

                if (client.PlayerObject.TryGetComponent(out PlayerMovement playerMov))
                {
                    // ignore if silent
                    if (playerMov.human.isDead.Value || playerMov.currentNoiseRadius.Value <= 0.1f) continue;

                    float distanceToPlayer = Vector3.Distance(transform.position, playerMov.transform.position);

                    // ignore if far
                    if (distanceToPlayer <= playerMov.currentNoiseRadius.Value)
                    {
                        return playerMov.gameObject;
                    }
                }
                else
                {
                    Debug.LogError("No PlayerMovement in player.. somehow");
                }
            }
            return null;
        }

        public void OnDrawGizmosSelected()
        {
            Vector3 eyePosition = transform.position + eyeLocalPosition;
            Vector3 left = eyePosition + Quaternion.Euler(new Vector3(0, viewAngle / 2f, 0)) * (transform.forward * viewDistance);
            Vector3 right = eyePosition + Quaternion.Euler(-new Vector3(0, viewAngle / 2f, 0)) * (transform.forward * viewDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(eyePosition, left);
            Gizmos.DrawLine(eyePosition, right);
        }
    }
}
