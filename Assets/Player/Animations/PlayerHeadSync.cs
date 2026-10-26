using Player.Equipment;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(100)]
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
    [Tooltip("Remote pitch interpolation speed")]
    public float smoothSpeed = 15f;

    [Tooltip("Fraction of pitch applied to spine (e.g. 0 = pure head rotation, 0.25 = 25% spine)")]
    [Range(0f, 1f)]
    public float spinePitchFraction = 0.2f;

    [Tooltip("Minimum threshold in degrees before syncing headPitch over network")]
    [SerializeField] private float pitchNetworkThreshold = 0.1f;

    [Header("Pitch Limits")]
    [Tooltip("Maximum pitch angle when looking UP in degrees (negative value, e.g. -50).")]
    [SerializeField] private float minPitchAngle = -50f;

    [Tooltip("Maximum pitch angle when looking DOWN in degrees (positive value, e.g. 45).")]
    [SerializeField] private float maxPitchAngle = 45f;

    public float MinPitchAngle { get => minPitchAngle; set => minPitchAngle = value; }
    public float MaxPitchAngle { get => maxPitchAngle; set => maxPitchAngle = value; }

    private readonly NetworkVariable<float> headPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float currentAppliedPitch = 0f;
    public float CurrentPitch => currentAppliedPitch;

    private Player.Inventory inventory;
    private ItemHolder itemHolder;
    private CameraMovement cachedCameraMovement;
    private Transform playerRoot;

    private Rig handRig;
    private TwoBoneIKConstraint handIK;
    private Transform ikRoot;
    private Transform ikMid;
    private Transform ikTip;
    private Transform ikTarget;
    private Transform ikHint;

    private float maxArmReach = 0.594f;
    private float maxSafeArmReach = 0.575f;
    private float minSafeArmReach = 0.15f;

    public Transform ShoulderBone => ikRoot;
    public float MaxSafeArmReach => maxSafeArmReach;
    public float MinSafeArmReach => minSafeArmReach;

    private void Awake()
    {
        playerRoot = transform.root;
        inventory = GetComponentInParent<Player.Inventory>();
        if (inventory != null && itemHolder == null)
        {
            itemHolder = inventory.GetComponentInChildren<ItemHolder>(true);
        }

        if (cameraTransform != null)
        {
            cachedCameraMovement = cameraTransform.GetComponent<CameraMovement>();
            if (cachedCameraMovement == null)
            {
                cachedCameraMovement = cameraTransform.GetComponentInParent<CameraMovement>();
            }
        }

        handRig = GetComponentInChildren<Rig>(true);
        handIK = GetComponentInChildren<TwoBoneIKConstraint>(true);
        if (handIK != null)
        {
            ikRoot = handIK.data.root;
            ikMid = handIK.data.mid;
            ikTip = handIK.data.tip;
            ikTarget = handIK.data.target != null ? handIK.data.target : remoteHandTarget;
            ikHint = handIK.data.hint;

            if (remoteHandTarget == null)
            {
                remoteHandTarget = ikTarget;
            }

            if (ikRoot != null && ikMid != null && ikTip != null)
            {
                float upperArmLen = Vector3.Distance(ikRoot.position, ikMid.position);
                float forearmLen = Vector3.Distance(ikMid.position, ikTip.position);
                maxArmReach = upperArmLen + forearmLen;
                // Keep a 3% bend margin so the elbow never hits the 180-degree acos(-1) singularity
                maxSafeArmReach = maxArmReach * 0.97f;
                minSafeArmReach = Mathf.Abs(upperArmLen - forearmLen) + 0.03f;
            }

        }

        // Disable secondary RigBuilder and empty Animator on Body so they never evaluate
        // a separate un-animated AnimationStream conflicting with Player's root Animator.
        RigBuilder rigBuilder = GetComponent<RigBuilder>();
        if (rigBuilder != null)
        {
            rigBuilder.enabled = false;
        }

        Animator bodyAnimator = GetComponent<Animator>();
        if (bodyAnimator != null && bodyAnimator.runtimeAnimatorController == null)
        {
            bodyAnimator.enabled = false;
        }

    }

    private void Update()
    {
        if (!IsOwner) return;

        if (PlayerMovement.Instance != null && (PlayerMovement.Instance.isInInteraction || PlayerMovement.Instance.isPaused))
        {
            return;
        }

        float rawPitch = GetOwnerRawPitch();
        float clampedPitch = Mathf.Clamp(rawPitch, minPitchAngle, maxPitchAngle);

        if (Mathf.Abs(clampedPitch - headPitch.Value) >= pitchNetworkThreshold)
        {
            headPitch.Value = clampedPitch;
        }
    }

    private float GetOwnerRawPitch()
    {
        if (CameraMovement.Instance != null)
        {
            return CameraMovement.Instance.CurrentPitch;
        }

        Transform pitchSource = cachedCameraMovement != null ? cachedCameraMovement.transform : cameraTransform;
        if (pitchSource != null)
        {
            float rawPitch = pitchSource.localEulerAngles.x;
            if (rawPitch > 180f) rawPitch -= 360f;
            return rawPitch;
        }

        return headPitch.Value;
    }

    private void LateUpdate()
    {
        // 1. Owner uses instant camera pitch (0 lag); remotes smoothly interpolate network updates
        if (IsOwner)
        {
            currentAppliedPitch = Mathf.Clamp(GetOwnerRawPitch(), minPitchAngle, maxPitchAngle);
        }
        else
        {
            currentAppliedPitch = Mathf.LerpAngle(currentAppliedPitch, headPitch.Value, Time.deltaTime * smoothSpeed);
            currentAppliedPitch = Mathf.Clamp(currentAppliedPitch, minPitchAngle, maxPitchAngle);
        }

        // 2. Apply additive spine and head pitch on top of the current Strut_Walking animation frame
        if (headBone != null)
        {
            if (spineBone != null && spinePitchFraction > 0f)
            {
                float spinePitch = currentAppliedPitch * spinePitchFraction;
                spineBone.localRotation = spineBone.localRotation * Quaternion.Euler(spinePitch, 0f, 0f);
            }

            float headPitchOffset = (spineBone != null && spinePitchFraction > 0f)
                ? currentAppliedPitch * (1f - spinePitchFraction)
                : currentAppliedPitch;

            headBone.localRotation = headBone.localRotation * Quaternion.Euler(headPitchOffset, 0f, 0f);
        }

        // 3. Update IK target AFTER spine rotation so shoulder position (ikRoot) is final for this frame
        if (IsOwner)
        {
            if (itemHolder == null && inventory != null)
            {
                itemHolder = inventory.ItemHolder;
            }

            if (itemHolder != null)
            {
                itemHolder.EvaluateViewModelAndIKTarget(ikRoot, minSafeArmReach, maxSafeArmReach);
            }
        }
        else if (itemHolder != null)
        {
            itemHolder.EvaluateRemotePose(ikRoot, minSafeArmReach, maxSafeArmReach, currentAppliedPitch);
        }

        // 4. Solve Two-Bone IK directly on the animated skeleton
        ApplyRightHandIK();

        // 5. Both views attach the item to the solved palm, including the equip blend.
        if (inventory != null)
        {
            Item activeItem = inventory.GetActiveItem();
            if (activeItem != null && activeItem.gameObject.activeInHierarchy)
            {
                activeItem.UpdateRemoteHeldPose();
            }
        }
    }

    public Vector3 ClampTargetToShoulderReach(Vector3 desiredWorldPos, Vector3 shoulderWorldPos)
    {
        Vector3 offset = desiredWorldPos - shoulderWorldPos;
        float dist = offset.magnitude;
        if (dist > maxSafeArmReach && dist > 1e-5f)
        {
            return shoulderWorldPos + (offset / dist) * maxSafeArmReach;
        }
        if (dist < minSafeArmReach && dist > 1e-5f)
        {
            return shoulderWorldPos + (offset / dist) * minSafeArmReach;
        }
        return desiredWorldPos;
    }

    private void ApplyRightHandIK()
    {
        if (ikRoot == null || ikMid == null || ikTip == null || ikTarget == null || handIK == null)
        {
            return;
        }

        float rigWeight = handRig != null
            ? handRig.weight
            : ((inventory != null && inventory.isHoldingItem.Value) ? 1f : 0f);
        float constraintWeight = rigWeight * handIK.weight;
        if (constraintWeight <= 0.0001f)
        {
            return;
        }

        // When holding an item, enforce full position & rotation lock so walking animation never leaks into fingers
        float posWeight = Mathf.Clamp01(constraintWeight * Mathf.Max(0.999f, handIK.data.targetPositionWeight));
        float rotWeight = Mathf.Clamp01(constraintWeight * Mathf.Max(0.999f, handIK.data.targetRotationWeight));
        float hintWeight = Mathf.Clamp01(constraintWeight * (ikHint != null ? handIK.data.hintWeight : 1f));

        Vector3 targetWorldPos = ClampTargetToShoulderReach(ikTarget.position, ikRoot.position);
        Quaternion targetWorldRot = ikTarget.rotation;

        // Provide an anatomical elbow pole vector even if no Hint transform is assigned in the prefab
        Transform rootRef = playerRoot != null ? playerRoot : transform;
        Vector3 hintWorldPos = ikHint != null
            ? ikHint.position
            : ikRoot.position + rootRef.right * 0.28f - rootRef.up * 0.35f - rootRef.forward * 0.20f;

        SolveTwoBoneIK(
            ikRoot,
            ikMid,
            ikTip,
            targetWorldPos,
            targetWorldRot,
            hintWorldPos,
            true,
            posWeight,
            rotWeight,
            hintWeight);
    }

    private static float TriangleAngle(float aLen, float bLen, float cLen)
    {
        float c = Mathf.Clamp((bLen * bLen + cLen * cLen - aLen * aLen) / (2f * bLen * cLen), -1f, 1f);
        return Mathf.Acos(c);
    }

    private static void SolveTwoBoneIK(
        Transform root,
        Transform mid,
        Transform tip,
        Vector3 targetPos,
        Quaternion targetRot,
        Vector3 hintPos,
        bool hasHint,
        float posWeight,
        float rotWeight,
        float hintWeight)
    {
        Vector3 aPos = root.position;
        Vector3 bPos = mid.position;
        Vector3 cPos = tip.position;
        Vector3 tPos = Vector3.Lerp(cPos, targetPos, posWeight);

        Vector3 ab = bPos - aPos;
        Vector3 bc = cPos - bPos;
        Vector3 ac = cPos - aPos;
        Vector3 at = tPos - aPos;

        float abLen = ab.magnitude;
        float bcLen = bc.magnitude;
        float acLen = ac.magnitude;
        float atLen = at.magnitude;

        if (abLen > 1e-5f && bcLen > 1e-5f && atLen > 1e-5f)
        {
            float oldAbcAngle = TriangleAngle(acLen, abLen, bcLen);
            float newAbcAngle = TriangleAngle(atLen, abLen, bcLen);

            Vector3 axis = Vector3.Cross(ab, bc);
            if (axis.sqrMagnitude < 1e-6f)
            {
                axis = hasHint ? Vector3.Cross(hintPos - aPos, bc) : Vector3.zero;
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(at, bc);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
            }
            axis.Normalize();

            float halfAngle = 0.5f * (oldAbcAngle - newAbcAngle);
            float s = Mathf.Sin(halfAngle);
            float c = Mathf.Cos(halfAngle);
            Quaternion deltaMid = new Quaternion(axis.x * s, axis.y * s, axis.z * s, c);
            mid.rotation = deltaMid * mid.rotation;

            cPos = tip.position;
            ac = cPos - aPos;
            root.rotation = Quaternion.FromToRotation(ac, at) * root.rotation;

            if (hasHint && hintWeight > 0f)
            {
                bPos = mid.position;
                cPos = tip.position;
                ac = cPos - aPos;
                ab = bPos - aPos;
                float acSqr = ac.sqrMagnitude;
                if (acSqr > 1e-8f)
                {
                    Vector3 ah = hintPos - aPos;
                    Vector3 abProj = ab - ac * (Vector3.Dot(ab, ac) / acSqr);
                    Vector3 ahProj = ah - ac * (Vector3.Dot(ah, ac) / acSqr);
                    float maxReach = abLen + bcLen;
                    if (abProj.sqrMagnitude > maxReach * maxReach * 0.0005f && ahProj.sqrMagnitude > 1e-8f)
                    {
                        Quaternion hRot = Quaternion.FromToRotation(abProj, ahProj);
                        hRot = Quaternion.Slerp(Quaternion.identity, hRot, hintWeight);
                        root.rotation = hRot * root.rotation;
                    }
                }
            }
        }

        tip.rotation = Quaternion.Slerp(tip.rotation, targetRot, rotWeight);
    }
}