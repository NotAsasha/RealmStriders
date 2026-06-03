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

    protected AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        enemy.onStateChanged += PlaySound;
        enemy.isDead.OnValueChanged += PlayDeathSound;
    }
    private void OnDisable()
    {
        enemy.onStateChanged -= PlaySound;
        enemy.isDead.OnValueChanged -= PlayDeathSound;
    }

    private void PlaySound(EnemyState state)
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

    private void PlayIdleSound()
    {
        if (idleSounds.Length == 0)
        {
            Debug.LogWarning("Entity has no idleSounds");
            return;
        }

        source.clip = idleSounds[Random.Range(0, idleSounds.Length)];
        source.Play();
    }
    private void PlayAngrySound()
    {
        if (angrySounds.Length == 0)
        {
            Debug.LogWarning("Entity has no angrySounds");
            return;
        }

        source.clip = angrySounds[Random.Range(0, angrySounds.Length)];
        source.Play();
    }

    public void PlayJumpscareSound()
    {
        source.clip = jumpscareSound;
        source.Play();
    }

    private void PlayDeathSound(bool _, bool isDead)
    {
        //If revived, be angry
        if (!isDead)
        {
            PlayAngrySound();
            return;
        }

        source.clip = deathSound;
        source.Play();
    }
}
