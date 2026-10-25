using Player;
using Player.Movement;
using Portals;
using System.Collections;
using UnityEngine;

/// <summary>
/// Triggers a fullscreen distortion flash on the player camera whenever
/// the local player teleports through the portal to completely mask visual snapping.
///
/// Can be attached to:
///   - PortalSystem prefab / GameObject (alongside PortalManager)
///   - Player prefab (e.g. on Camera alongside CameraMovement)
///   - Any persistent manager GameObject
/// </summary>
public class PortalTransitionEffect : MonoBehaviour
{
    public static PortalTransitionEffect Instance { get; private set; }

    [Header("Timing")]
    [Tooltip("How long (seconds) to ramp up to peak. Keep at 0 to peak instantly on the exact teleport frame and hide the camera jump.")]
    [SerializeField, Range(0.0f, 0.2f)] private float rampUpDuration = 0.0f;

    [Tooltip("How long (seconds) the distortion fades back to normal vision.")]
    [SerializeField, Range(0.1f, 1.0f)] private float fadeDuration = 0.45f;

    [Header("Intensity")]
    [Tooltip("Peak distortion strength (0 = none, 1 = max).")]
    [SerializeField, Range(0f, 1f)] private float peakIntensity = 1.0f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportSound;

    private Coroutine _flashRoutine;
    private PortalManager _subscribedManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void Update()
    {
        // Fallback subscription check in case PortalManager spawned later
        if (_subscribedManager == null)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        PortalManager target = GetComponent<PortalManager>();
        if (target == null)
        {
            target = PortalManager.Instance;
        }

        if (target != null && target != _subscribedManager)
        {
            if (_subscribedManager != null)
            {
                _subscribedManager.OnTeleport -= OnPlayerTeleport;
            }

            _subscribedManager = target;
            _subscribedManager.OnTeleport += OnPlayerTeleport;
        }
    }

    private void OnDisable()
    {
        if (_subscribedManager != null)
        {
            _subscribedManager.OnTeleport -= OnPlayerTeleport;
            _subscribedManager = null;
        }

        SetGlobals(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SetGlobals(0f);
    }

    private void OnPlayerTeleport(Human human, bool isForward)
    {
        // Only react to the local player's teleport
        if (PlayerMovement.Instance == null) return;
        if (human == null || human.gameObject != PlayerMovement.Instance.gameObject) return;

        TriggerEffect();
    }

    /// <summary>
    /// Triggers the full-screen portal distortion transition flash.
    /// </summary>
    public void TriggerEffect()
    {
        // Only play if PortalManager didn't already play the exact same sound clip
        if (audioSource != null && teleportSound != null)
        {
            if (PortalManager.Instance == null || PortalManager.Instance.PortalTeleportSound != teleportSound)
            {
                audioSource.PlayOneShot(teleportSound);
            }
        }

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
        }

        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // 1. Optional ramp-up (if > 0)
        if (rampUpDuration > 0.001f)
        {
            float elapsed = 0f;
            while (elapsed < rampUpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rampUpDuration);
                SetGlobals(Mathf.Lerp(0f, peakIntensity, t));
                yield return null;
            }
        }

        // Peak frame (instant full coverage over teleport jump)
        SetGlobals(peakIntensity);

        // 2. Smooth cubic ease-out back to neutral
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
            // Cubic ease-out
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            SetGlobals(Mathf.Lerp(peakIntensity, 0f, eased));
            yield return null;
        }

        SetGlobals(0f);
        _flashRoutine = null;
    }

    private static void SetGlobals(float intensity)
    {
        Shader.SetGlobalFloat(PortalDistortionFeature.IntensityId, intensity);
        Shader.SetGlobalFloat(PortalDistortionFeature.TimeId, Time.time);
    }
}
