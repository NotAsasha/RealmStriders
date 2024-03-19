using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class CameraMovement : NetworkBehaviour
    {
        [Header("Set Up")]
        public Transform playerBody;
        [Header("Camera")]
        public float mouseSensitivity = 250f;

        [Header("Other Settings")]
        public bool isNetwork = true;

        private float xRotation = 0f;
        void Start()
        {
            // Checks of you are owner of this Network Object and if Network is turned on
            if (!IsOwner && isNetwork)
                gameObject.SetActive(false);

            // Locks your cursor to the game
            Movement._controls.System.Pause.performed += UpdateCursorState;
            Cursor.lockState = CursorLockMode.Locked;
            mouseSensitivity = PlayerPrefs.GetFloat("MouseSensativity");
        }
        private void UpdateCursorState(InputAction.CallbackContext obj)
        {
            Cursor.lockState = Movement.isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        }
        void Update()
        {
            if (Movement.isPaused) 
            {
                return;
            }

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
