using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using static Unity.VisualScripting.Member;

public class SoundProducer : NetworkBehaviour
{
    [SerializeField] Transform soundEmitor;
    [SerializeField] float soundRadius = 20.0f;
    [SerializeField] LayerMask entityLayer;
    //[SerializeField] LayerMask wallLayer;

    [SerializeField] AudioSource source;
    [SerializeField] List<AudioClip> clip;
    //private void Awake() { }

    [ServerRpc(RequireOwnership = false)]
    public void EmitSoundServerRpc(int soundIndex = 0, bool singleLure = false)
    {
        if (!soundEmitor) soundEmitor = transform;

        source.clip = clip[soundIndex];
        source.Play();
        EmitSoundClientRpc();

        Collider[] entities = Physics.OverlapSphere(soundEmitor.position, soundRadius, entityLayer);
        foreach (Collider entity in entities)
        {
            if (entity.gameObject == gameObject) continue;

            var enemy = entity.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Lure(soundEmitor.position);
                if (singleLure) return;
            }
        }
    }

    [ClientRpc]
    private void EmitSoundClientRpc(int soundIndex = 0)
    {
        if (IsServer) return;
        source.clip = clip[soundIndex];
        source.Play();
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blueViolet;
        if (soundEmitor == null || soundRadius == 0) return;
        Gizmos.DrawWireSphere(soundEmitor.position, soundRadius);
    }
}
