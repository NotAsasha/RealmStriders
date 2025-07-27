using UnityEngine;

public class ChasePlayer : MonoBehaviour
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

    public GameObject PlayerInSight()
    {
        Vector3 eyePosition = transform.position + eyeLocalPosition;
        Collider[] players = Physics.OverlapSphere(eyePosition, viewDistance, playerLayer);
        foreach (Collider player in players)
        {
            if (player.gameObject == gameObject) continue;
            Vector3 direction = player.transform.position - transform.position;
            if (Vector3.Angle(direction, transform.forward) <= halfAngle)
            {
                if (!Physics.Raycast(eyePosition, direction, direction.magnitude, wallLayer))
                {
                    return player.gameObject;
                }
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
