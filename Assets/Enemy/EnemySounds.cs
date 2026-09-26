using Enemy;
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

    protected AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (enemy == null) enemy = GetComponent<Enemy.Enemy>();
    }

    protected virtual void OnEnable()
    {
        if (enemy != null)
        {
            enemy.onStateChanged += PlaySound;
            enemy.isDead.OnValueChanged += PlayDeathSound;
        }
    }
    protected virtual void OnDisable()
    {
        if (enemy != null)
        {
            enemy.onStateChanged -= PlaySound;
            enemy.isDead.OnValueChanged -= PlayDeathSound;
        }
    }

    public void SetRadius(float radius)
    {
        if (source != null && radius > 0f)
        {
            source.maxDistance = radius;
        }
    }

    protected virtual void PlaySound(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.IsMoving:
                PlayIdleSound();
                break;

            case EnemyState.IsChasingPlayer:
            case EnemyState.IsChasingSound:
                PlayAngrySound();
                break;

            default:
                PlayIdleSound();
                break;
        }
    }

    public virtual void PlayIdleSound()
    {
        if (idleSounds == null || idleSounds.Length == 0)
        {
            Debug.LogWarning("Entity has no idleSounds");
            return;
        }

        SetRadius(idleRadius);
        source.PlayOneShot(idleSounds[Random.Range(0, idleSounds.Length)]);
    }

    public virtual void PlayAngrySound()
    {
        if (angrySounds == null || angrySounds.Length == 0)
        {
            Debug.LogWarning("Entity has no angrySounds");
            return;
        }

        SetRadius(angryRadius);
        source.PlayOneShot(angrySounds[Random.Range(0, angrySounds.Length)]);
    }

    public virtual void PlayJumpscareSound()
    {
        if (jumpscareSound == null) return;
        SetRadius(jumpscareRadius);
        source.PlayOneShot(jumpscareSound);
    }

    protected virtual void PlayDeathSound(bool _, bool isDead)
    {
        //If revived, be angry
        if (!isDead)
        {
            PlayAngrySound();
            return;
        }

        if (deathSound == null) return;
        SetRadius(deathRadius);
        source.PlayOneShot(deathSound); 
    }
}
