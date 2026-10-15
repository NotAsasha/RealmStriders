using Player;
using UnityEngine;
using UnityEngine.AI;

namespace Enemy.Runner
{
    public class Runner : Enemy
    {
        [Header("Sound Flee")]
        [SerializeField, Min(0.5f)] private float fleeDistance = 12f;
        [SerializeField, Range(1, 8)] private int fleeDirectionSamples = 5;
        [SerializeField, Min(0.5f)] private float navMeshSampleDistance = 4f;

        private NavMeshPath fleePath;

        protected override void Start()
        {
            base.Start();
            entityHealth.OnValueChanged += OnDamaged;
            fleePath = new NavMeshPath();
        }

        private void OnDamaged(float oldV, float newV)
        {
            if (newV <= 0 || newV >= oldV) return;
            Run();
        }

        public override void Lure(Vector3 coords)
        {
            if (!IsServer || IsEffectActive(EffectType.Asleep) || agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            Vector3 fleeDirection = transform.position - coords;
            fleeDirection.y = 0f;
            if (fleeDirection.sqrMagnitude <= 0.001f)
            {
                fleeDirection = -transform.forward;
            }
            else
            {
                fleeDirection.Normalize();
            }

            if (!TryFindFleePoint(fleeDirection, out Vector3 fleePoint))
            {
                // Keep the Runner moving even when the NavMesh has no complete escape path.
                Run();
                return;
            }

            agent.SetDestination(fleePoint);
            enemyState = EnemyState.IsRunning;
            if (!IsEffectActive(EffectType.Freeze))
            {
                agent.speed = chaseSpeed;
            }
        }

        private bool TryFindFleePoint(Vector3 fleeDirection, out Vector3 fleePoint)
        {
            if (fleePath == null)
            {
                fleePath = new NavMeshPath();
            }

            Vector3 bestPoint = transform.position;
            float bestDistance = -1f;
            int sampleCount = Mathf.Max(1, fleeDirectionSamples);
            float angleStep = sampleCount == 1 ? 0f : 120f / (sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float angle = -60f + angleStep * i;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * fleeDirection;
                Vector3 candidate = transform.position + direction * fleeDistance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, fleePath) ||
                    fleePath.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float distance = (hit.position - transform.position).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = hit.position;
                }
            }

            fleePoint = bestPoint;
            return bestDistance >= 0f;
        }

    }
}
