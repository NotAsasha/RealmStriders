using UnityEngine;
using UnityEngine.InputSystem;

public class MenuParallax : MonoBehaviour
{
    [Header("Settings")]
    public float movementIntensity = 0.5f;
    public float rotationIntensity = 1.0f;
    public float smoothing = 5.0f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // Get mouse position in normalized screen space (-1 to 1)
        Vector2 mousePos = Mouse.current.position.ReadValue();
        float x = (mousePos.x / Screen.width) * 2 - 1;
        float y = (mousePos.y / Screen.height) * 2 - 1;

        // Target position shift
        Vector3 targetPos = initialPosition + new Vector3(x * movementIntensity, y * movementIntensity, 0);
        
        // Target rotation shift
        Quaternion targetRot = initialRotation * Quaternion.Euler(-y * rotationIntensity, x * rotationIntensity, 0);

        // Smooth transition
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothing);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothing);
    }
}
