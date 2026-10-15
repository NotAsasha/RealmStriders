using Enemy;
using Player;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EnemySounds : MonoBehaviour
{
    public Enemy.Enemy enemy;

    public AudioClip[] idleSounds;
    public AudioClip[] angrySounds;

    public AudioClip deathSound;
    public AudioClip jumpscareSound;

    [Header("Sound Radii")]
    public float idleRadius = 15f;
    public float angryRadius = 25f;
    public float deathRadius = 30f;
    public float jumpscareRadius = 40f;

    [Header("Audio Sources")]
    [Tooltip("Optional dedicated AudioSource for vocal sounds. If null, a dedicated AudioSource will be initialized automatically.")]
    public AudioSource vocalAudioSource;

    [Header("Vocal Intervals")]
    [Tooltip("Whether idle sounds play periodically while roaming/moving.")]
    public bool enablePeriodicIdleSounds = true;
    [Min(1f)] public float minIdleInterval = 7f;
    [Min(1f)] public float maxIdleInterval = 15f;

    [Tooltip("Whether angry sounds repeat periodically while in chase.")]
    public bool enablePeriodicAngrySounds = true;
    [Min(1f)] public float minAngryInterval = 3.5f;
    [Min(1f)] public float maxAngryInterval = 6.5f;

    [Header("Audio Variation")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;
    [Range(0f, 0.3f)] public float volumeVariation = 0.05f;

    protected AudioSource source;

    private float nextIdleSoundTime;
    private float nextAngrySoundTime;

    private void Awake()
    {
        // Guard against duplicate EnemySounds if a specialized subclass (e.g. ShockerSounds) is present
        if (GetType() == typeof(EnemySounds))
        {
            EnemySounds[] all = GetComponents<EnemySounds>();
            foreach (var comp in all)
            {
                if (comp != this && comp.GetType() != typeof(EnemySounds))
                {
                    enabled = false;
                    return;
                }
            }
        }

        if (enemy == null) enemy = GetComponentInParent<Enemy.Enemy>();
        EnsureDedicatedAudioSource();
        ResetTimers();
    }

    private void Start()
    {
        EnsureDedicatedAudioSource();
    }

    private void ResetTimers()
    {
        // Stagger initial idle time so monsters don't all vocalize simultaneously on start
        nextIdleSoundTime = Time.time + Random.Range(minIdleInterval * 0.5f, maxIdleInterval);
        nextAngrySoundTime = Time.time + Random.Range(minAngryInterval, maxAngryInterval);
    }

    protected virtual void OnEnable()
    {
        if (enemy != null)
        {
            enemy.onStateChanged += PlaySound;
            if (enemy.isDead != null)
            {
                enemy.isDead.OnValueChanged += PlayDeathSound;
            }
            enemy.onBite += PlayJumpscareSound;
        }
    }

    protected virtual void OnDisable()
    {
        if (enemy != null)
        {
            enemy.onStateChanged -= PlaySound;
            if (enemy.isDead != null)
            {
                enemy.isDead.OnValueChanged -= PlayDeathSound;
            }
            enemy.onBite -= PlayJumpscareSound;
        }
    }

    protected virtual void Update()
    {
        if (enemy == null || !CanPlayVocals()) return;

        EnemyState state = enemy.enemyState;

        if (state == EnemyState.IsMoving)
        {
            if (enablePeriodicIdleSounds && Time.time >= nextIdleSoundTime)
            {
                PlayIdleSound();
                nextIdleSoundTime = Time.time + Random.Range(minIdleInterval, maxIdleInterval);
            }
        }
        else if (state == EnemyState.IsChasingPlayer || state == EnemyState.IsChasingSound)
        {
            if (enablePeriodicAngrySounds && Time.time >= nextAngrySoundTime)
            {
                PlayAngrySound();
                nextAngrySoundTime = Time.time + Random.Range(minAngryInterval, maxAngryInterval);
            }
        }
    }

    public virtual bool CanPlayVocals()
    {
        if (enemy == null) return false;
        if (enemy.isDead != null && enemy.isDead.Value) return false;
        if (enemy.IsBeingCaptured) return false;

        try
        {
            if (enemy.IsEffectActive(EffectType.Asleep) || enemy.IsEffectActive(EffectType.Freeze))
            {
                return false;
            }
        }
        catch
        {
            // Effects might be uninitialized in isolated testing
        }

        return true;
    }

    protected virtual void EnsureDedicatedAudioSource()
    {
        if (source != null && source != enemy?.audioSource) return;

        if (vocalAudioSource != null)
        {
            source = vocalAudioSource;
            return;
        }

        AudioSource[] sources = GetComponents<AudioSource>();
        if (enemy != null && enemy.audioSource != null)
        {
            foreach (var s in sources)
            {
                if (s != enemy.audioSource)
                {
                    source = s;
                    return;
                }
            }

            // Both step sounds and vocal sounds share the same AudioSource.
            // Dynamically add a dedicated vocal source to avoid stepping pitch/distance collisions.
            vocalAudioSource = gameObject.AddComponent<AudioSource>();
            vocalAudioSource.spatialBlend = 1f;
            vocalAudioSource.playOnAwake = false;
            vocalAudioSource.rolloffMode = enemy.audioSource.rolloffMode;
            vocalAudioSource.minDistance = 2f;
            vocalAudioSource.maxDistance = idleRadius;
            source = vocalAudioSource;
            return;
        }

        if (sources.Length > 0)
        {
            source = sources[0];
        }
        else
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        if (source != null)
        {
            source.spatialBlend = 1f;
            source.playOnAwake = false;
        }
    }

    public void SetRadius(float radius)
    {
        if (source != null && radius > 0f)
        {
            source.spatialBlend = 1f;
            source.maxDistance = radius;
            if (source.minDistance <= 0f)
            {
                source.minDistance = 1.5f;
            }
        }
    }

    protected virtual void PlaySound(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.IsMoving:
                if (enablePeriodicIdleSounds)
                {
                    nextIdleSoundTime = Time.time + Random.Range(minIdleInterval, maxIdleInterval);
                }
                else
                {
                    PlayIdleSound();
                }
                break;

            case EnemyState.IsChasingPlayer:
            case EnemyState.IsChasingSound:
                PlayAngrySound();
                nextAngrySoundTime = Time.time + Random.Range(minAngryInterval, maxAngryInterval);
                break;

            default:
                if (!enablePeriodicIdleSounds)
                {
                    PlayIdleSound();
                }
                break;
        }
    }

    protected virtual void PlayVocalClip(AudioClip clip, float radius, float baseVolume = 1f)
    {
        if (clip == null) return;
        EnsureDedicatedAudioSource();
        if (source == null) return;

        SetRadius(radius);
        float pitchOffset = Random.Range(-pitchVariation, pitchVariation);
        float volumeOffset = Random.Range(-volumeVariation, volumeVariation);

        source.pitch = Mathf.Clamp(1.0f + pitchOffset, 0.5f, 2.0f);
        float finalVolume = Mathf.Clamp01(baseVolume + volumeOffset);
        source.PlayOneShot(clip, finalVolume);
    }

    public virtual void PlayIdleSound()
    {
        if (idleSounds == null || idleSounds.Length == 0 || !CanPlayVocals()) return;
        PlayVocalClip(idleSounds[Random.Range(0, idleSounds.Length)], idleRadius);
    }

    public virtual void PlayAngrySound()
    {
        if (angrySounds == null || angrySounds.Length == 0 || !CanPlayVocals()) return;
        PlayVocalClip(angrySounds[Random.Range(0, angrySounds.Length)], angryRadius);
    }

    public virtual void PlayJumpscareSound()
    {
        if (jumpscareSound == null || !CanPlayVocals()) return;
        PlayVocalClip(jumpscareSound, jumpscareRadius);
    }

    protected virtual void PlayDeathSound(bool _, bool isDead)
    {
        // If revived, be angry
        if (!isDead)
        {
            PlayAngrySound();
            return;
        }

        if (deathSound == null) return;
        PlayVocalClip(deathSound, deathRadius);
    }
}
