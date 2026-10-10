using Player.Movement;
using System.Diagnostics.CodeAnalysis;
using Unity.Netcode;
using UnityEngine;

public class PlayerHeadSync : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Put the head bone here (Armature)")]
    public Transform headBone;

    [Tooltip("Upper chest / spine bone (mixamorig:Spine2)")]
    public Transform spineBone;

    [Tooltip("IK Target for Right Hand (HandTarget)")]
    public Transform remoteHandTarget;

    [Tooltip("Local Camera (null for other players)")]
    public Transform cameraTransform;

    [Header("Settings")]
    [Tooltip("Camera animation speed")]
    public float smoothSpeed = 15f;

    [Tooltip("Fraction of pitch applied to spine (e.g. 0 = pure head rotation, 0.2 = 20% spine)")]
    [Range(0f, 1f)]
    public float spinePitchFraction = 0f;

    [Header("Pitch Limits")]
    [Tooltip("Maximum pitch angle when looking UP in degrees (negative value, e.g. -50).")]
    [SerializeField] private float minPitchAngle = -50f;

    [Tooltip("Maximum pitch angle when looking DOWN in degrees (positive value, e.g. 45).")]
    [SerializeField] private float maxPitchAngle = 45f;

    public float MinPitchAngle { get => minPitchAngle; set => minPitchAngle = value; }
    public float MaxPitchAngle { get => maxPitchAngle; set => maxPitchAngle = value; }

    private NetworkVariable<float> headPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float currentAppliedPitch = 0f;
    public float CurrentPitch => currentAppliedPitch;

    private Vector3 defaultHandTargetLocalPos;
    private Quaternion defaultHandTargetLocalRot;
    private Player.Inventory inventory;

    private void Awake()
    {
        inventory = GetComponentInParent<Player.Inventory>();
        if (remoteHandTarget != null)
        {
            defaultHandTargetLocalPos = remoteHandTarget.localPosition;
            defaultHandTargetLocalRot = remoteHandTarget.localRotation;
        }
    }

    void Update()
    {
        // only for owner
        if (IsOwner)
        {
            if (PlayerMovement.Instance != null && (PlayerMovement.Instance.isInInteraction || PlayerMovement.Instance.isPaused)) return;

            float rawPitch = 0f;
            if (CameraMovement.Instance != null)
            {
                rawPitch = CameraMovement.Instance.CurrentPitch;
            }
            else
            {
                Transform pitchSource = null;
                if (cameraTransform != null)
                {
                    CameraMovement camMove = cameraTransform.GetComponent<CameraMovement>();
                    if (camMove == null) camMove = cameraTransform.GetComponentInParent<CameraMovement>();
                    pitchSource = camMove != null ? camMove.transform : cameraTransform;
                }

                if (pitchSource != null)
                {
                    rawPitch = pitchSource.localEulerAngles.x;
                    if (rawPitch > 180f) rawPitch -= 360f;
                }
            }

            headPitch.Value = Mathf.Clamp(rawPitch, minPitchAngle, maxPitchAngle);
        }
    }

    void LateUpdate()
    {
        if (headBone == null) return;

        currentAppliedPitch = Mathf.LerpAngle(currentAppliedPitch, headPitch.Value, Time.deltaTime * smoothSpeed);
        currentAppliedPitch = Mathf.Clamp(currentAppliedPitch, minPitchAngle, maxPitchAngle);

        // 1. Optional spine pitch (0 = head only)
        if (spineBone != null && spinePitchFraction > 0f)
        {
            float spinePitch = currentAppliedPitch * spinePitchFraction;
            Vector3 spineRot = spineBone.localEulerAngles;
            spineBone.localEulerAngles = new Vector3(spinePitch, spineRot.y, spineRot.z);
        }

        // 2. Head pitch
        float headPitchOffset = (spineBone != null && spinePitchFraction > 0f)
            ? currentAppliedPitch * (1f - spinePitchFraction)
            : currentAppliedPitch;

        Vector3 animatorRotation = headBone.localEulerAngles;
        headBone.localEulerAngles = new Vector3(headPitchOffset, animatorRotation.y, animatorRotation.z);

        // 3. Remote player hand aiming (syncs arm pitch for teammates in multiplayer)
        if (!IsOwner && remoteHandTarget != null && inventory != null && inventory.isHoldingItem.Value)
        {
            Vector3 shoulderPivotLocal = new Vector3(0.18f, 0.45f, 0f);
            Quaternion pitchRot = Quaternion.Euler(currentAppliedPitch, 0f, 0f);
            remoteHandTarget.localPosition = shoulderPivotLocal + pitchRot * (defaultHandTargetLocalPos - shoulderPivotLocal);
            remoteHandTarget.localRotation = pitchRot * defaultHandTargetLocalRot;
        }
    }
}