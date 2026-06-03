using Enemy.Casino;
using UnityEngine;

public class CasinoSounds : EnemySounds
{
    public AudioClip spawnSound;

    private void OnEnable()
    {
        (enemy as CasinoMonster).OnSpawn += PlaySpawnSound;
    }
    private void OnDisable()
    {
        (enemy as CasinoMonster).OnSpawn -= PlaySpawnSound;
    }
    public void PlaySpawnSound()
    {
        source.clip = spawnSound;
        source.Play();
    }
}
