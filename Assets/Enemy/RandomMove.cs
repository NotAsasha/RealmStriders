using UnityEngine;
using UnityEngine.AI;

namespace Enemy
{
    public class RandomMove
    {

        public static bool RandomPoint(Vector3 center, float range, out Vector3 result)
        {
            // Use a flat 2D disc (X/Z only) so the candidate stays near ground level.
            // Random.insideUnitSphere scatters candidates across a 3D sphere, so most
            // points land far above or below the NavMesh and fail the SamplePosition search.
            Vector2 flatRandom = Random.insideUnitCircle * range;
            Vector3 randomPoint = new Vector3(center.x + flatRandom.x, center.y, center.z + flatRandom.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 10.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = Vector3.zero;
            return false;
        }
    }
}
