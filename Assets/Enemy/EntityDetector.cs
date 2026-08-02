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
            Vector3 eyePosition = transform.position + eyeLocalPosition;
            int numColliders = Physics.OverlapSphereNonAlloc(eyePosition, viewDistance, nearbyEntities, playerLayer);

            for (int i = 0; i < numColliders; i++)
            {
                if (nearbyEntities[i].gameObject == gameObject) continue;
                Vector3 direction = nearbyEntities[i].transform.position - transform.position;
                if (Vector3.Angle(direction, transform.forward) <= halfAngle)
                {
                    // if entity is behind a wall
                    if (Physics.Raycast(eyePosition, direction, direction.magnitude, wallLayer)) continue;

                    //if is dead
                    var entity = nearbyEntities[i].GetComponent<Entity>();
                    if (entity == null || entity.isDead.Value) continue;

                    //if is enemy
                    if (!chaseEnemies && nearbyEntities[i].GetComponent<Enemy>() != null) continue;

                    return nearbyEntities[i].gameObject;
                
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
