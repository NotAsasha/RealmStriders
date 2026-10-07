using Enemy;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace Player.Equipment.Scanner
{
    [RequireComponent(typeof(EntityDetector))]
    public class Scanner : Item
    {
        [SerializeField] TMP_Text danger;
        [SerializeField] Image freezeIcon;
        [SerializeField] Image waterIcon;
        [SerializeField] Transform arrow;
        [SerializeField] private Renderer indicatorRenderer;

        [Header("Arrow rotation")]
        [SerializeField] private float minimumHealthRotationX = -75f;
        [SerializeField] private float maximumHealthRotationX = 75f;
        [SerializeField, UnityEngine.Min(0.01f)] private float rotationSmoothTime = 0.2f;


        [SerializeField] AudioClip toggleSound;
        [SerializeField] AudioClip scanSound;
        [SerializeField] [MinMax(0f, 1f)] float pitchDiff = 0.2f;

        NetworkVariable<bool> isOn = new(false, 0, 0);

        EntityDetector detector;
        private float basePitch = 1f;
        private float initialArrowRotationY;
        private float initialArrowRotationZ;
        private float targetArrowRotationX;
        private float currentArrowRotationX;
        private float arrowRotationVelocity;
        private Color defaultIndicatorColor = Color.white;

        private void Start()
        {
            detector = GetComponent<EntityDetector>();
            basePitch = audioSource.pitch;

            if (indicatorRenderer != null && indicatorRenderer.material.HasProperty("_Color"))
            {
                defaultIndicatorColor = indicatorRenderer.material.color;
            }

            if (arrow != null)
            {
                Vector3 initialRotation = arrow.localEulerAngles;
                initialArrowRotationY = initialRotation.y;
                initialArrowRotationZ = initialRotation.z;
                currentArrowRotationX = Mathf.DeltaAngle(0f, initialRotation.x);
                targetArrowRotationX = currentArrowRotationX;
            }

            isOn.OnValueChanged += SwitchState;
            SetIndicatorColor(isOn.Value);
        }

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SwitchStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        private void SwitchState(bool oldV, bool newV)
        {
            SetIndicatorColor(newV);

            if (!newV)
            {
                danger.text = " ";
                SetArrowRotation(0f);
            }

            audioSource.pitch = basePitch;
            audioSource.PlayOneShot(toggleSound);
            audioSource.clip = scanSound;
        }

        private void SetIndicatorColor(bool isScannerOn)
        {
            if (indicatorRenderer == null || !indicatorRenderer.material.HasProperty("_Color")) return;

            indicatorRenderer.material.color = isScannerOn ? defaultIndicatorColor : Color.black;
        }

        float cooldown = 1f;
        float currTime = 0f;
        private void Update()
        {
            UpdateArrowRotation();

            if (!isOn.Value) return;

            //peep once per second
            currTime += Time.deltaTime;
            if (currTime < cooldown) return;
            currTime = 0f;

            GameObject entityObj = detector.EntityInSight(true);
            if (entityObj)
            {
                Entity entity = entityObj.GetComponent<Entity>();

                float activeHealth = entity.GetHealth();
                danger.text = activeHealth.ToString();
                SetArrowRotation(activeHealth);
                freezeIcon.color = entity.IsEffectActive(EffectType.Freeze) ? Color.white : Color.black;
                waterIcon.color = entity.IsEffectActive(EffectType.Water) ? Color.white : Color.black;

                audioSource.pitch = basePitch + pitchDiff;
                cooldown = 0.75f;
                Debug.Log($"---Scanner: Entity danger: {entity.GetHealth()}");
            }
            else
            {
                freezeIcon.color = Color.black;
                waterIcon.color = Color.black;
                danger.text = "Not Found";
                SetArrowRotation(0f);

                audioSource.pitch = basePitch;
                cooldown = 1f;
            }
            audioSource.Play();

            // - Particle effects
            // - Sound effects --- DONE
            // - Physics interactions with enemies --- DONE
            // - Cooldown mechanics --- DONE
        }

        private void SetArrowRotation(float activeHealth)
        {
            if (arrow == null) return;

            float normalizedHealth = Mathf.InverseLerp(0f, 5f, Mathf.Clamp(activeHealth, 0f, 5f));
            targetArrowRotationX = Mathf.Lerp(
                minimumHealthRotationX,
                maximumHealthRotationX,
                normalizedHealth);
        }

        private void UpdateArrowRotation()
        {
            if (arrow == null) return;

            currentArrowRotationX = Mathf.SmoothDamp(
                currentArrowRotationX,
                targetArrowRotationX,
                ref arrowRotationVelocity,
                rotationSmoothTime);

            arrow.localRotation = Quaternion.Euler(
                currentArrowRotationX,
                initialArrowRotationY,
                initialArrowRotationZ);
        }

        #endregion
    }
}