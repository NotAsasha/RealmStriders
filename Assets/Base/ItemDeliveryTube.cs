using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Base
{
    public abstract class ItemDeliveryTube : NetworkBehaviour
    {
        [FormerlySerializedAs("tubeAudio")]
        [SerializeField] private AudioSource TubeAudio;
        [FormerlySerializedAs("tubeParticle")]
        [SerializeField] private ParticleSystem TubeParticle;

        protected abstract Pose OutletPose { get; }

        public NetworkObject DispenseServer(NetworkObject prefab)
        {
            if (!IsSpawned || !IsServer || prefab == null) return null;

            Pose outlet = OutletPose;
            NetworkObject item = prefab.InstantiateAndSpawn(NetworkManager, position: outlet.position, rotation: outlet.rotation);
            if (item != null) PlayTubeEffectsRpc();
            return item;
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void PlayTubeEffectsRpc()
        {
            if (TubeAudio != null) TubeAudio.Play();
            if (TubeParticle != null) TubeParticle.Play();
        }
    }
}
