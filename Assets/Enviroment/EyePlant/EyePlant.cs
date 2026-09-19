using UnityEngine;
using Enemy;

public class EyePlant : MonoBehaviour
{
    [SerializeField] private Transform eye;

    [SerializeField] protected EntityDetector eyeDetector;
    [SerializeField] private float rotationSpeed = 10f;

    private Vector3 eyeTarget;
    private float lookTimer = 0f;

    private void Start()
    {
        if (eye != null)
            eyeTarget = eye.position + transform.forward * 5f;
    }

    protected void Update()
    {
        TurnEye();

        lookTimer -= Time.deltaTime;

        if (lookTimer <= 0f)
        {
            DetectEntities();
            lookTimer = 0.3f;
        }
    }

    private void DetectEntities()
    {
        var player = eyeDetector.EntityInSight(true);

        if (player != null)
        {
            Vector3 target = player.transform.position;

            eyeTarget = target;
        }
        else
        {
            HandleRandomEyeLook();
        }
    }

    private void TurnEye()
    {
        if (eye != null)
        {
            Vector3 direction = eyeTarget - eye.position;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                eye.rotation = Quaternion.Slerp(eye.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
    
    private void HandleRandomEyeLook()
    {
        float randomAngleX = Random.Range(-45f, 45f);
        float randomAngleY = Random.Range(0, 360f);
        Vector3 lookDirection = Quaternion.Euler(randomAngleX, randomAngleY, 0f) * transform.forward;

        eyeTarget = eye.position + lookDirection * 5f;

    }
}
