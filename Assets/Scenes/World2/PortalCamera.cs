using Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalCamera : MonoBehaviour {

	public Transform playerCamera;
	public Transform portal;
	public Transform otherPortal;

    private void Start()
    {
        playerCamera = Movement.instance.GetComponentInChildren<Camera>().transform;
    }

    void Update()
    {
        // позиц≥€
        Vector3 playerOffsetFromPortal = playerCamera.position - portal.position;
        transform.position = otherPortal.position + playerOffsetFromPortal;

        // р≥зниц€ у поворот≥
        Quaternion portalRotationalDifference = otherPortal.rotation * Quaternion.Inverse(portal.rotation);


        // новий напр€мок
        Vector3 newCameraDirection = portalRotationalDifference * playerCamera.forward;

        transform.rotation = Quaternion.LookRotation(newCameraDirection, Vector3.up);
    }

}
