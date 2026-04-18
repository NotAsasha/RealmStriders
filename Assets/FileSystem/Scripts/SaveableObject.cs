using Unity.Netcode;

public class SaveableObject : NetworkBehaviour
{
    public int prefabID;

    public override void OnNetworkSpawn()
    {
        this.NetworkObject.Register();
    }

    public override void OnNetworkDespawn()
    {
        this.NetworkObject.UnRegister();
    }
}