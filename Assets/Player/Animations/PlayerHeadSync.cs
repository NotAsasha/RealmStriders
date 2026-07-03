using Player.Movement;
using System.Diagnostics.CodeAnalysis;
using Unity.Netcode;
using UnityEngine;

public class PlayerHeadSync : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Put the bone here (Armature)")]
    public Transform headBone;

    [Tooltip("Local Camera (null for other players)")]
    public Transform cameraTransform;

    [Header("Settings")]
    [Tooltip("Camera animation speed")]
    public float smoothSpeed = 15f;

    private NetworkVariable<float> headPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float currentAppliedPitch = 0f;

    void Update()
    {
        // only for owner
        if (IsOwner && cameraTransform != null)
        {
            //idk if it has to be here
            //if (PlayerMovement.Instance.isInInteraction || PlayerMovement.Instance.isPaused) return;

            float pitch = cameraTransform.localEulerAngles.x;

            // -180 to 180
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            headPitch.Value = pitch;
        }
    }

    void LateUpdate()
    {
        if (headBone == null) return;

        currentAppliedPitch = Mathf.LerpAngle(currentAppliedPitch, headPitch.Value, Time.deltaTime * smoothSpeed);
        Vector3 animatorRotation = headBone.localEulerAngles;
        headBone.localEulerAngles = new Vector3(currentAppliedPitch, animatorRotation.y, animatorRotation.z);
    }
}