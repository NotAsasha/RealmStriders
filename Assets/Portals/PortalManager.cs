using Player;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Portals
{
    public class PortalManager : MonoBehaviour {

        [SerializeField] private Camera cameraA;
        [SerializeField] private Camera cameraB;

        [SerializeField] private Material cameraMatA;
        [SerializeField] private Material cameraMatB;

        public static PortalManager Instance;

        public bool isForward = true;

        public event Action<Human, bool> OnTeleport;

        [FormerlySerializedAs("PortalParent")] [SerializeField] GameObject portalParent;

        void Start () {
            if (Instance == null) Instance = this;
            ChangeState(false);

            if (cameraA.targetTexture != null)
            {
                cameraA.targetTexture.Release();
            }
            cameraA.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            cameraMatA.mainTexture = cameraA.targetTexture;

            if (cameraB.targetTexture != null)
            {
                cameraB.targetTexture.Release();
            }
            cameraB.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            cameraMatB.mainTexture = cameraB.targetTexture;

            cameraA.gameObject.SetActive(true);
            cameraB.gameObject.SetActive(true);
            isForward = true;
            }
	
        public void ChangeState(bool isStarted)
        {
            portalParent?.SetActive(isStarted);
        }

        public void SwitchCameras()
        {
            // Logic moved to PortalCamera.cs for better performance (visibility-based)
            isForward = !isForward;
        }

        public void CallOnTeleport(Human player)
        {
            OnTeleport?.Invoke(player, isForward);
        }
    }
}
