using Enemy;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Player.Movement
{
    /// <summary>
    /// Client-only procedural camera FX that reacts to nearby enemies.
    /// Attach to the same GameObject as <see cref="CameraMovement"/> (the FPS camera).
    ///
    /// Scene setup checklist:
    ///   1. Add this component alongside CameraMovement on the local player camera prefab.
    ///   2. Assign a URP Global Volume (or a local one on the camera layer) to <see cref="dangerVolume"/>.
    ///      The Volume's profile must contain Vignette and ChromaticAberration overrides
    ///      with their checkboxes enabled but initial intensity set to 0.
    ///   3. Tune <see cref="dangerDistance"/> / <see cref="safeDistance"/> / FOV delta in the Inspector.
    ///   4. Assign the enemy physics layer to <see cref="enemyLayerMask"/> for the overlap scan.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerProximityCameraFX : NetworkBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        //  Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Detection")]
        [Tooltip("Enemies closer than this distance start contributing to threat.")]
        public float dangerDistance = 9f;

        [Tooltip("At this distance threat intensity reaches 1.0 (maximum FX).")]
        public float safeDistance = 1.8f;

        [Tooltip("How quickly intensity rises / falls (units per second).")]
        public float intensitySpeed = 2.5f;

        [Tooltip("Seconds between full enemy-list scans. Lower = more responsive, higher = cheaper.")]
        public float scanInterval = 0.2f;

        [Header("Camera Jitter")]
        [Tooltip("Max positional shake radius at full threat.")]
        public float maxJitterAmplitude = 0.012f;

        [Tooltip("Perlin noise scroll speed.")]
        public float jitterFrequency = 8f;

        [Header("FOV Squeeze")]
        [Tooltip("Degrees subtracted from base FOV at full threat (tunnel-vision effect).")]
        public float maxFovReduction = 7f;

        [Tooltip("FOV lerp speed.")]
        public float fovLerpSpeed = 4f;

        [Header("Post-Processing Volume")]
        [Tooltip("URP Volume whose profile contains Vignette and ChromaticAberration.")]
        public Volume dangerVolume;

        [Tooltip("Max Vignette intensity at full threat.")]
        [Range(0f, 1f)] public float maxVignetteIntensity = 0.45f;

        [Tooltip("Max Chromatic Aberration intensity at full threat.")]
        [Range(0f, 1f)] public float maxChromaticIntensity = 0.6f;

        [Header("Enemy Detection")]
        [Tooltip("Physics layer(s) that enemies occupy. Used by OverlapSphere scan.")]
        public LayerMask enemyLayerMask;

        [Tooltip("Buffer size for OverlapSphereNonAlloc. Must be >= expected max enemies in range.")]
        public int overlapBufferSize = 16;

        // ──────────────────────────────────────────────────────────────────────
        //  Private state — zero GC in Update
        // ──────────────────────────────────────────────────────────────────────

        private Camera _cam;
        private float _baseFov;
        private float _threatIntensity;   // current smoothed 0–1 value
        private float _nextScanTime;
        private float _closestSqDist;     // result of last scan, squared distance

        // Perlin offsets — randomised per session to avoid identical shakes across clients
        private float _noiseOffsetX;
        private float _noiseOffsetY;

        // Jitter offset applied additively in LateUpdate AFTER CameraMovement + CameraBob
        // have already written their localPosition. Stored so we can remove it next frame.
        private Vector3 _appliedJitter;

        // URP post-processing references — resolved once
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private bool _ppReady;

        // Non-alloc physics buffer
        private Collider[] _overlapBuffer;

        // ──────────────────────────────────────────────────────────────────────
        //  NetworkBehaviour lifecycle
        // ──────────────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            // Only the owning client runs this effect.
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _cam = GetComponent<Camera>();
            _baseFov = _cam.fieldOfView;

            _noiseOffsetX = Random.Range(0f, 100f);
            _noiseOffsetY = Random.Range(0f, 100f);

            _overlapBuffer = new Collider[overlapBufferSize];

            ResolvePostProcessingOverrides();
        }

        public override void OnNetworkDespawn()
        {
            // Restore camera to neutral state on disconnect / death cleanup.
            if (!IsOwner) return;
            ResetFX();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Per-frame update
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            // Periodic enemy scan — avoids per-frame FindObjectsByType.
            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + scanInterval;
                _closestSqDist = ScanForNearestEnemySqDist();
            }

            // Compute target intensity from last known closest distance.
            float targetIntensity = ComputeTargetIntensity(_closestSqDist);

            // Smooth the transition to avoid popping when enemy disappears or dies.
            _threatIntensity = Mathf.MoveTowards(_threatIntensity, targetIntensity, intensitySpeed * Time.deltaTime);

            ApplyFovSqueeze();
            ApplyPostProcessing();
        }

        // LateUpdate runs after CameraMovement (Update) and PlayerCameraBob (LateUpdate with
        // default order) have already written their localPosition values.  We strip the
        // previous frame's jitter, then add the new one — so we never clobber the base pose.
        private void LateUpdate()
        {
            transform.localPosition -= _appliedJitter;
            _appliedJitter = ComputeJitterOffset();
            transform.localPosition += _appliedJitter;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Detection
        // ──────────────────────────────────────────────────────────────────────

        /// <returns>Squared distance to the nearest alive enemy, or float.MaxValue if none.</returns>
        private float ScanForNearestEnemySqDist()
        {
            float nearest = float.MaxValue;

            // Primary path: use GameManager's already-tracked list (zero allocations).
            if (GameManager.Instance != null && GameManager.Instance.activeEnemies.Count > 0)
            {
                Vector3 myPos = transform.position;
                float dangerSq = dangerDistance * dangerDistance;

                foreach (Enemy.Enemy enemy in GameManager.Instance.activeEnemies)
                {
                    // Skip dead or inactive/sleeping enemies (e.g. static CasinoMonster on base).
                    if (enemy == null || enemy.isDead.Value || enemy.IsEffectActive(EffectType.Asleep)) continue;

                    float sqDist = (enemy.transform.position - myPos).sqrMagnitude;
                    if (sqDist < dangerSq && sqDist < nearest)
                        nearest = sqDist;
                }
                return nearest;
            }

            // Fallback path: Physics.OverlapSphereNonAlloc when GameManager list is unavailable.
            // Still zero heap allocations (buffer pre-allocated in OnNetworkSpawn).
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, dangerDistance, _overlapBuffer, enemyLayerMask);

            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<Enemy.Enemy>();
                // Skip dead or sleeping enemies to ignore static in-base decorations.
                if (enemy == null || enemy.isDead.Value || enemy.IsEffectActive(EffectType.Asleep)) continue;

                float sqDist = (_overlapBuffer[i].transform.position - transform.position).sqrMagnitude;
                if (sqDist < nearest) nearest = sqDist;
            }
            return nearest;
        }

        private float ComputeTargetIntensity(float closestSqDist)
        {
            if (closestSqDist >= float.MaxValue) return 0f;

            float closestDist = Mathf.Sqrt(closestSqDist);

            // Remap [dangerDistance → safeDistance] to [0 → 1].
            return 1f - Mathf.Clamp01((closestDist - safeDistance) / Mathf.Max(dangerDistance - safeDistance, 0.01f));
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Visual effects
        // ──────────────────────────────────────────────────────────────────────

        // Returns the XY jitter delta for this frame (zero when intensity is 0).
        // Applied additively in LateUpdate — never overwrites the base localPosition.
        private Vector3 ComputeJitterOffset()
        {
            if (_threatIntensity <= 0f) return Vector3.zero;

            float t = Time.time * jitterFrequency;
            // Perlin returns 0–1; remap to -1…1 for a signed, centred offset.
            float ox = (Mathf.PerlinNoise(_noiseOffsetX + t, 0f) - 0.5f) * 2f;
            float oy = (Mathf.PerlinNoise(0f, _noiseOffsetY + t) - 0.5f) * 2f;

            float amplitude = maxJitterAmplitude * _threatIntensity;
            return new Vector3(ox, oy, 0f) * amplitude;
        }

        private void ApplyFovSqueeze()
        {
            float targetFov = _baseFov - maxFovReduction * _threatIntensity;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);
        }

        private void ApplyPostProcessing()
        {
            if (!_ppReady) return;

            if (_vignette != null)
                _vignette.intensity.value = maxVignetteIntensity * _threatIntensity;

            if (_chromaticAberration != null)
                _chromaticAberration.intensity.value = maxChromaticIntensity * _threatIntensity;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void ResolvePostProcessingOverrides()
        {
            if (dangerVolume == null)
            {
                Debug.LogWarning("[ProximityCameraFX] No Volume assigned — post-processing effects disabled.");
                return;
            }

            dangerVolume.profile.TryGet(out _vignette);
            dangerVolume.profile.TryGet(out _chromaticAberration);
            _ppReady = true;
        }

        private void ResetFX()
        {
            if (_cam != null) _cam.fieldOfView = _baseFov;

            if (_vignette != null)             _vignette.intensity.value = 0f;
            if (_chromaticAberration != null)  _chromaticAberration.intensity.value = 0f;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Editor gizmo
        // ──────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
            Gizmos.DrawSphere(transform.position, dangerDistance);

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, safeDistance);
        }
#endif
    }
}
