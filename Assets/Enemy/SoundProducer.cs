using Unity.Netcode;
using UnityEngine;

public class SoundProducer : NetworkBehaviour
{
    [SerializeField] Transform soundEmitor;
    [SerializeField] float soundRadius = 20.0f;
    [SerializeField] LayerMask entityLayer;
    //[SerializeField] LayerMask wallLayer;

    [SerializeField] AudioSource source;

    //private void Awake() { }

    [ServerRpc(RequireOwnership = false)]
    public void EmitSoundServerRpc(bool singleLure = false)
    {
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
    private void EmitSoundClientRpc()
    {
        if (IsServer) return;
        source.Play();
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blueViolet;
        if (soundEmitor == null || soundRadius == 0) return;
        Gizmos.DrawWireSphere(soundEmitor.position, soundRadius);
    }
}
