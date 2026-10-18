using System.Collections;
using Player.Movement;
using UnityEngine;

namespace Base.Radar
{
    public class OrbitalStrikeVisuals : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] private MeshRenderer beamRenderer;
        [SerializeField] private MeshRenderer shockwaveRenderer;
        [SerializeField] private MeshRenderer craterRenderer;

        [Header("Lighting")]
        [SerializeField] private Light impactLight;
        [SerializeField] private float peakLightIntensity = 24f;
        [SerializeField] private float sustainLightIntensity = 9f;
        [SerializeField] private float lightRange = 35f;

        [Header("Timeline Settings")]
        [SerializeField] private float totalDuration = 7.0f;
        [SerializeField] private float ignitionDuration = 0.35f;
        [SerializeField] private float collapseDuration = 0.65f;
        [SerializeField] private float shockwaveDuration = 0.85f;
        [SerializeField] private float maxShockwaveScale = 26f;

        [Header("Camera Shake")]
        [SerializeField] private float maxShakeDistance = 60f;
        [SerializeField] private float shakeAmplitude = 0.14f;

        private MaterialPropertyBlock beamMpb;
        private MaterialPropertyBlock shockwaveMpb;
        private MaterialPropertyBlock craterMpb;

        private static readonly int StrikeWidthScaleId = Shader.PropertyToID("_StrikeWidthScale");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int CoolDownId = Shader.PropertyToID("_CoolDown");

        private float elapsedTime;
        private bool shockwaveActive = true;
        private Vector3 initialShockwaveScale;

        // Camera shake state
        private Transform targetCameraTransform;
        private Vector3 appliedShakeOffset;
        private float shakeRemainingDuration;
        private float currentShakeStrength;

        private void Awake()
        {
            beamMpb = new MaterialPropertyBlock();
            shockwaveMpb = new MaterialPropertyBlock();
            craterMpb = new MaterialPropertyBlock();

            if (shockwaveRenderer != null)
            {
                initialShockwaveScale = shockwaveRenderer.transform.localScale;
            }

            if (impactLight != null)
            {
                impactLight.range = lightRange;
                impactLight.intensity = 0f;
            }

            SetBeamWidth(0f);
            SetShockwaveProgress(0f);
            SetCraterCooldown(0f);
        }

        private void Start()
        {
            TriggerCameraShake();
        }

        private void Update()
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime;

            // 1. Shockwave expansion
            if (shockwaveActive && shockwaveRenderer != null)
            {
                if (t <= shockwaveDuration)
                {
                    float p = Mathf.Clamp01(t / shockwaveDuration);
                    float easeOutP = 1f - (1f - p) * (1f - p);
                    SetShockwaveProgress(easeOutP);

                    float currentScale = Mathf.Lerp(initialShockwaveScale.x, maxShockwaveScale, easeOutP);
                    shockwaveRenderer.transform.localScale = new Vector3(currentScale, initialShockwaveScale.y, currentScale);
                }
                else
                {
                    shockwaveActive = false;
                    shockwaveRenderer.gameObject.SetActive(false);
                }
            }

            // 2. Beam width ignition -> sustain -> collapse
            float width = 0f;
            if (t < ignitionDuration)
            {
                float p = t / ignitionDuration;
                width = Mathf.SmoothStep(0f, 1f, p);
            }
            else if (t < totalDuration - collapseDuration)
            {
                float pulse = Mathf.Sin(t * 14f) * 0.035f;
                width = 1.0f + pulse;
            }
            else
            {
                float collapseElapsed = t - (totalDuration - collapseDuration);
                float p = Mathf.Clamp01(collapseElapsed / collapseDuration);
                width = Mathf.Lerp(1.0f, 0f, p * p);
            }
            SetBeamWidth(width);

            // 3. Crater cooling down over lifetime
            if (craterRenderer != null)
            {
                float coolDown = Mathf.Clamp01(t / totalDuration);
                SetCraterCooldown(coolDown);
            }

            // 4. Dynamic Lighting
            if (impactLight != null)
            {
                if (t < 0.22f)
                {
                    impactLight.intensity = Mathf.Lerp(0f, peakLightIntensity, t / 0.22f);
                }
                else if (t < 0.6f)
                {
                    float p = (t - 0.22f) / 0.38f;
                    impactLight.intensity = Mathf.Lerp(peakLightIntensity, sustainLightIntensity, p);
                }
                else if (t < totalDuration - collapseDuration)
                {
                    float flicker = Mathf.Sin(t * 18f) * 1.4f + Mathf.Cos(t * 26f) * 0.7f;
                    impactLight.intensity = sustainLightIntensity + flicker;
                }
                else
                {
                    float collapseElapsed = t - (totalDuration - collapseDuration);
                    float p = Mathf.Clamp01(collapseElapsed / collapseDuration);
                    impactLight.intensity = Mathf.Lerp(sustainLightIntensity, 0f, p);
                }
            }
        }

        private void LateUpdate()
        {
            // Clean camera shake without accumulating position drift
            if (targetCameraTransform != null)
            {
                if (appliedShakeOffset != Vector3.zero)
                {
                    targetCameraTransform.localPosition -= appliedShakeOffset;
                    appliedShakeOffset = Vector3.zero;
                }

                if (shakeRemainingDuration > 0f)
                {
                    shakeRemainingDuration -= Time.deltaTime;
                    float damp = Mathf.Clamp01(shakeRemainingDuration / 0.7f);
                    float x = (Mathf.PerlinNoise(Time.time * 28f, 0f) * 2f - 1f) * currentShakeStrength * damp;
                    float y = (Mathf.PerlinNoise(0f, Time.time * 28f) * 2f - 1f) * currentShakeStrength * damp;

                    appliedShakeOffset = new Vector3(x, y, 0f);
                    targetCameraTransform.localPosition += appliedShakeOffset;
                }
            }
        }

        private void OnDisable()
        {
            if (targetCameraTransform != null && appliedShakeOffset != Vector3.zero)
            {
                targetCameraTransform.localPosition -= appliedShakeOffset;
                appliedShakeOffset = Vector3.zero;
            }
        }

        private void SetBeamWidth(float width)
        {
            if (beamRenderer == null) return;
            beamRenderer.GetPropertyBlock(beamMpb);
            beamMpb.SetFloat(StrikeWidthScaleId, width);
            beamRenderer.SetPropertyBlock(beamMpb);
        }

        private void SetShockwaveProgress(float progress)
        {
            if (shockwaveRenderer == null) return;
            shockwaveRenderer.GetPropertyBlock(shockwaveMpb);
            shockwaveMpb.SetFloat(ProgressId, progress);
            shockwaveRenderer.SetPropertyBlock(shockwaveMpb);
        }

        private void SetCraterCooldown(float coolDown)
        {
            if (craterRenderer == null) return;
            craterRenderer.GetPropertyBlock(craterMpb);
            craterMpb.SetFloat(CoolDownId, coolDown);
            craterRenderer.SetPropertyBlock(craterMpb);
        }

        private void TriggerCameraShake()
        {
            Camera cam = CameraMovement.Instance != null ? CameraMovement.Instance.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            targetCameraTransform = cam.transform;
            Vector3 playerPos = targetCameraTransform.position;
            Vector3 impactPos = transform.position + Vector3.down * 100f;
            float dist = Vector3.Distance(playerPos, impactPos);

            if (dist < maxShakeDistance)
            {
                float falloff = 1f - (dist / maxShakeDistance);
                currentShakeStrength = shakeAmplitude * falloff;
                shakeRemainingDuration = 0.7f;
            }
        }
    }
}
