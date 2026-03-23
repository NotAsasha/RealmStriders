using System.Collections;
using Player.Equipment;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace Base
{
    public class Terminal : NetworkBehaviour, IInteractable
    {
        [SerializeField] protected Canvas terminalCanvas;
        [SerializeField] private Vector3 cameraOffset = new Vector3(-1f, 1.5f, -0.5f);
        [SerializeField] private Vector3 cameraEulerRotation = new Vector3(10f, 90f, 0f);
        [SerializeField] private float moveDuration = 0.5f;

        [SerializeField] AudioSource interactSound;

        protected int ownerID = -1;
        public CameraMovement playerCameraComponent;

        #region Interatcion
        public NetworkVariable<bool> isTaken = new(writePerm: NetworkVariableWritePermission.Server);

        Coroutine cameraAnimation;
        public void Interact(GameObject player)
        {
            SetTakenServerRpc(true);


            var camera = player.GetComponentInChildren<Camera>();
            playerCameraComponent = camera.gameObject.GetComponent<CameraMovement>();
            if (camera == null) return;
            cameraAnimation = player.GetComponent<MonoBehaviour>().StartCoroutine(MoveCameraToTerminal(camera));


            ownerID = (int)player.GetComponentInParent<NetworkObject>().OwnerClientId;
            if (interactSound != null) interactSound.Play();
        }
        private IEnumerator MoveCameraToTerminal(Camera playerCamera)
        {
            Vector3 startPos = playerCamera.transform.position;
            Quaternion startRot = playerCamera.transform.rotation;

            Vector3 targetPos = transform.TransformPoint(cameraOffset);
            Quaternion targetRot = Quaternion.Euler(transform.eulerAngles + cameraEulerRotation);

            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / moveDuration);

                playerCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
                playerCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                yield return null;
            }

            playerCamera.transform.position = targetPos;
            playerCamera.transform.rotation = targetRot;

            // activate ui
            terminalCanvas.worldCamera = playerCamera;
        }
        public void StopInteraction(GameObject player)
        {
            SetTakenServerRpc(false);

            player.GetComponent<MonoBehaviour>().StopCoroutine(cameraAnimation);
            player.transform.localPosition = playerCameraComponent.startPosition;
            playerCameraComponent = null;
            ownerID = -1;
            if (interactSound != null) interactSound.Stop();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetTakenServerRpc(bool whatToSet)
        {
            isTaken.Value = whatToSet;
        }

        public bool IsTaken() => isTaken.Value;
        #endregion
    }
}
