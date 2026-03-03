using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalManager : MonoBehaviour {

	public Camera cameraA;
	public Camera cameraB;

	public Material cameraMatA;
	public Material cameraMatB;

	public static PortalManager instance;

	[SerializeField] GameObject PortalParent;

	void Start () {
		if (instance == null) instance = this;
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
	}
	
	public void ChangeState(bool isStarted)
	{
		PortalParent?.SetActive(isStarted);
    }
}
