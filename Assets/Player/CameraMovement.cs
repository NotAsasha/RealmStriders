using Unity.Netcode;
using UnityEngine;

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
        public bool isPaused = false;

        private float xRotation = 0f;
        private const KeyCode EscapeKey = KeyCode.Escape;
        void Start()
        {
            // Checks of you are owner of this Network Object and if Network is turned on
            if (!IsOwner && isNetwork)
                gameObject.SetActive(false);

            // Locks your cursor to the game
            Cursor.lockState = CursorLockMode.Locked;
            mouseSensitivity = PlayerPrefs.GetFloat("MouseSensativity");
        }

        void Update()
        {
            // Checks if there is a Player Body attached
            if (playerBody == null)
            {
                Debug.LogError("Player body not assigned to CameraMovement script!");
                return;
            }

            // Pauses the camera -- In future should be moved to the separated class
            if (Input.GetKeyDown(EscapeKey)) PauseCamera(!isPaused);
            if (Input.GetMouseButton(0)) PauseCamera(false);
            if (isPaused) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            // Rotates the camera and a player body
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
        public void PauseCamera(bool pauseState)
        {
            // Changes state of cursor according to "pauseState"
            Cursor.lockState = pauseState ? CursorLockMode.None : CursorLockMode.Locked;
            isPaused = pauseState;
        }
    }
}
