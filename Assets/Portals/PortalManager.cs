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

        public bool isCameraA = true;

        [FormerlySerializedAs("PortalParent")] [SerializeField] GameObject portalParent;

        void Start () {
            if (Instance == null) Instance = this;
            ChangeState(false);

            if (cameraA.targetTexture != null)
            {
                cameraA.targetTexture.Release();
            }
            cameraA.targetTexture = new RenderTexture(Screen.width / 2, Screen.height / 2, 24);
            cameraMatA.mainTexture = cameraA.targetTexture;

            if (cameraB.targetTexture != null)
            {
                cameraB.targetTexture.Release();
            }
            cameraB.targetTexture = new RenderTexture(Screen.width / 2, Screen.height / 2, 24);
            cameraMatB.mainTexture = cameraB.targetTexture;

            SwitchCameras();
        }
	
        public void ChangeState(bool isStarted)
        {
            portalParent?.SetActive(isStarted);
        }

        public void SwitchCameras()
        {
            cameraA.gameObject.SetActive(isCameraA);
            cameraB.gameObject.SetActive(!isCameraA);
         
            isCameraA = !isCameraA;
        }
    }
}
