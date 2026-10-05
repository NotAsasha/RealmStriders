using System.Collections;
using UnityEngine;

namespace Enemy.Shocker
{
    /// <summary>
    /// Animates an EMP energy-field sphere on the Shocker — expands from zero to
    /// dischargeRange, glowing with a Fresnel rim + scanline pattern (Crabsquid style),
    /// then fades out. Driven by the Shocker's DischargeClientRpc via PlayDischarge().
    ///
    /// Setup:
    ///   1. Add a child GameObject with a Sphere mesh (MeshFilter + MeshRenderer).
    ///   2. Assign the ShockerEnergyField material (using ShockerEnergyField.shader).
    ///   3. Drag that MeshRenderer into the 'sphereRenderer' field on this component.
    ///   4. Set 'maxRadius' to match Shocker.dischargeRange.
    ///   5. In the Shocker prefab, drag this component into the 'Energy Field' field.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShockerEnergyField : MonoBehaviour
    {
        [Header("Expansion")]
        [Tooltip("Should match Shocker.dischargeRange")]
        [SerializeField] private float maxRadius      = 6f;
        [SerializeField] private float expandDuration = 0.5f;
        [SerializeField] private AnimationCurve expandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("References")]
        [SerializeField] private MeshRenderer sphereRenderer;

        // Cached property IDs — resolved once, zero GC per frame
        private static readonly int PropProgress = Shader.PropertyToID("_Progress");
        private static readonly int PropAlpha    = Shader.PropertyToID("_Alpha");

        private MaterialPropertyBlock _mpb;
        private Coroutine _animation;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (sphereRenderer != null) sphereRenderer.enabled = false;
        }

        /// <summary>Called from Shocker.PlayDischargeVFX() on every client.</summary>
        public void PlayDischarge()
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(ExpandAndFade());
        }

        private IEnumerator ExpandAndFade()
        {
            if (sphereRenderer == null) yield break;

            sphereRenderer.enabled = true;
            float elapsed = 0f;

            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                float t      = Mathf.Clamp01(elapsed / expandDuration);
                float radius = expandCurve.Evaluate(t) * maxRadius;

                // Sphere primitive has radius 0.5 at scale 1, so multiply by 2
                transform.localScale = Vector3.one * (radius * 2f);

                // Non-linear fade: sharp bright start, slow fade tail
                float alpha = 1f - (t * t);

                sphereRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(PropProgress, t);
                _mpb.SetFloat(PropAlpha,    alpha);
                sphereRenderer.SetPropertyBlock(_mpb);

                yield return null;
            }

            sphereRenderer.enabled = false;
            _animation = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawSphere(transform.position, maxRadius);
        }
    }
}
