using FileSystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class CameraMovement : NetworkBehaviour
    {
        [Header("Set Up")]
        public Transform playerBody;
        public LayerMask layerMask;
        [Header("Camera")]
        public float mouseSensitivity = 250f;
        public float hitDistance = 2.0f;
        [Header("Other Settings")]
        public bool isNetwork = true;

        private float xRotation = 0f;
        public Vector3 StartPosition;
        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }

            StartPosition = transform.localPosition;

            // Locks your cursor to the game
            Movement.instance._controls.System.Pause.performed += UpdateCursorState;
            Movement.instance._controls.Gameplay.Interact.performed += Interact;
            Cursor.lockState = CursorLockMode.Locked;
            SettingsFile file = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            mouseSensitivity = file.save._sensValue;
        }
        public void UpdateCursorState(InputAction.CallbackContext obj)
        {
            Cursor.lockState = (Movement.instance.isPaused || Movement.instance.isInInteraction)
    ? CursorLockMode.None
    : CursorLockMode.Locked;

        }

        private IInteractable interactable;
        private void Interact(InputAction.CallbackContext obj)
        {
            if (Movement.instance.isPaused) return;
            if (interactable != null)
            {
                interactable.StopInteraction(gameObject); 
                Debug.Log("No Interacted");
                interactable = null;
                return;
            }
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hitDistance, layerMask))
            {
                interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable.IsTaken())
                {
                    interactable = null;
                    return;
                }
                interactable?.Interact(gameObject);
                Debug.Log("Interacted");
            }
        }
        void Update()
        {
            if (Movement.instance.isPaused || Movement.instance.isInInteraction) return;

            // Checks if there is a Player Body attached
            if (playerBody == null)
            {
                Debug.LogError("Player body not assigned to CameraMovement script!");
                return;
            }

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            // Rotates the camera and a player body
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
