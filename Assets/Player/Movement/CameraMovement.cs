using FileSystem;
using Unity.Netcode;
using UnityEditor.ShaderGraph;
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
                interactable = null;
                var movement = Movement.instance;
                movement.isInInteraction = false;
                Cursor.lockState = CursorLockMode.Locked;
                movement._controls.Gameplay.Enable();
                movement._controls.UI.Disable();

                GetComponentInParent<Inventory>().userInterface.gameObject.SetActive(true);

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
                if (!interactable.IsSingleUse())
                {
                    var movement = Movement.instance;
                    movement.isInInteraction = true;
                    movement._controls.Gameplay.Disable();
                    movement._controls.Gameplay.Interact.Enable();
                    movement._controls.UI.Enable();
                    Cursor.lockState = CursorLockMode.None;

                    GetComponentInParent<Inventory>().userInterface.gameObject.SetActive(false);
                }

                interactable.Interact(gameObject);

                if (interactable.IsSingleUse())
                {
                    interactable = null;
                    return;
                }


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
