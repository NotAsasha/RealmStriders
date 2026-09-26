using Enemy.Casino;
using UnityEngine;

public class CasinoSounds : EnemySounds
{
    public AudioClip spawnSound;
    public float spawnRadius = 25f;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (enemy is CasinoMonster monster)
        {
            monster.OnSpawn += PlaySpawnSound;
        }
    }
    protected override void OnDisable()
    {
        if (enemy is CasinoMonster monster)
        {
            monster.OnSpawn -= PlaySpawnSound;
        }
        base.OnDisable();
    }
    public void PlaySpawnSound()
    {
        if (spawnSound == null) return;
        SetRadius(spawnRadius);
        source.clip = spawnSound;
        source.Play();
    }
}
