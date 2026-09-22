using Enemy.Casino;
using UnityEngine;

public class CasinoSounds : EnemySounds
{
    public AudioClip spawnSound;

    protected override void OnEnable()
    {
        base.OnEnable();
        (enemy as CasinoMonster).OnSpawn += PlaySpawnSound;
    }
    protected override void OnDisable()
    {
        (enemy as CasinoMonster).OnSpawn -= PlaySpawnSound;
        base.OnDisable();
    }
    public void PlaySpawnSound()
    {
        source.clip = spawnSound;
        source.Play();
    }
}
