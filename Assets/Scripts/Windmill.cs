using UnityEngine;

public class Windmill : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField]private float rotationSpeed = 90f;

    private void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, 0f, 0f, Space.Self);
    }
}
