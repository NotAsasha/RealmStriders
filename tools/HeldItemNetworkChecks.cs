using System;
using System.Reflection;
using System.Threading.Tasks;
using Player;
using Player.Equipment;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run Prepare in Edit Mode, enter Play Mode, Run, stop Play Mode, then Restore.
// Uses UDP loopback and the production Inventory/Item RPCs; Steam transport is not involved.
public static class HeldItemNetworkChecks
{
    private const string Folder = "Assets/Temporary/HeldItemNetworkChecks";
    private const string ScenePath = Folder + "/Check.unity";
    private const string PlayerPath = Folder + "/Inventory.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Prepare()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Restore previous check first.");
        SessionState.SetString("HeldItems.PreviousPlayScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary", "HeldItemNetworkChecks");
        Scene previous = SceneManager.GetActiveScene();
        Scene check = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var player = new GameObject("InventoryCheckPlayer");
            SceneManager.MoveGameObjectToScene(player, check);
            player.AddComponent<NetworkObject>();
            player.AddComponent<Inventory>();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            UnityEngine.Object.DestroyImmediate(player);
            EditorSceneManager.SaveScene(check, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(check, true);
            SceneManager.SetActiveScene(previous);
        }
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        return "Prepared isolated Play Mode scene; existing scenes left untouched.";
    }

    public static string Restore()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        string previous = SessionState.GetString("HeldItems.PreviousPlayScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        AssetDatabase.DeleteAsset(Folder);
        SessionState.EraseString("HeldItems.PreviousPlayScene");
        return "Restored Play Mode start scene and removed temporary test assets.";
    }

    public static async Task<string> Run()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Enter the prepared Play Mode scene first.");
        NetworkManager host = null, client = null, late = null;
        try
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Equipment/Lantern/Lantern.prefab");
            GameObject radioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Equipment/WalkieTalkie/WalkieTalkie.prefab");
            GameObject blowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Equipment/LeafBlower/LeafBlower.prefab");
            GameObject subwooferPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Equipment/Subwoofer/Subwoofer.prefab");
            host = CreateManager("CheckHost", player, lantern, radioPrefab, blowerPrefab, subwooferPrefab);
            host.SetSingleton();
            Require(host.StartHost(), "Host start failed");
            await Until(() => host.LocalClient.PlayerObject != null, "host player spawn");
            Inventory inventory = host.LocalClient.PlayerObject.GetComponent<Inventory>();
            Item light = Spawn(host, lantern);
            var radio = (WalkieTalkie)Spawn(host, radioPrefab);
            Require(inventory.AddItem(light.gameObject, 0), "Host pickup request rejected locally");
            Require(inventory.AddItem(radio.gameObject, 1), "Second pickup request rejected locally");
            await Until(() => inventory.GetItem(1) == radio, "host slots");
            Invoke(inventory, "SelectSlotRpc", 1, default(RpcParams));
            await Until(() => inventory.GetActiveItem() == radio, "host selection");
            radio.isOn.Value = true;
            radio.RefreshTransmissionServer();

            var blower = Spawn(host, blowerPrefab);
            var blowerOn = (NetworkVariable<bool>)blower.GetType().GetField("isOn", Private).GetValue(blower);
            blowerOn.Value = true;
            var subwoofer = (Player.Equipment.Subwoofer.Subwoofer)Spawn(host, subwooferPrefab);
            subwoofer.isOn.Value = true;
            client = CreateManager("CheckClient", player, lantern, radioPrefab, blowerPrefab, subwooferPrefab);
            Require(client.StartClient(), "Client start failed");
            await Until(() => client.IsConnectedClient && client.LocalClient.PlayerObject != null,
                "client join", radio.RefreshTransmissionServer);
            ulong playerId = inventory.NetworkObjectId;
            Inventory remote = await InventoryOn(client, playerId);
            await Until(() => remote.GetItem(0) != null && remote.GetItem(1) != null && remote.GetActiveItem() is WalkieTalkie,
                "late join slots", radio.RefreshTransmissionServer);
            CheckInventory(remote);

            late = CreateManager("CheckLateClient", player, lantern, radioPrefab, blowerPrefab, subwooferPrefab);
            Require(late.StartClient(), "Late client start failed");
            await Until(() => late.IsConnectedClient && late.LocalClient.PlayerObject != null,
                "second late join", radio.RefreshTransmissionServer);
            Inventory lateRemote = await InventoryOn(late, playerId);
            await Until(() => lateRemote.GetActiveItem() is WalkieTalkie r && r.IsHoldActionActive,
                "radio transmission snapshot", radio.RefreshTransmissionServer);
            CheckInventory(lateRemote);
            await Until(() => late.SpawnManager.SpawnedObjects.ContainsKey(blower.NetworkObjectId) &&
                late.SpawnManager.SpawnedObjects.ContainsKey(subwoofer.NetworkObjectId), "powered equipment spawn");
            Item remoteBlower = late.SpawnManager.SpawnedObjects[blower.NetworkObjectId].GetComponent<Item>();
            var blowerParticle = (ParticleSystem)remoteBlower.GetType().GetField("particle", Private).GetValue(remoteBlower);
            Require(blowerParticle.isPlaying, "Late-join blower particles did not start");
            Item remoteSubwoofer = late.SpawnManager.SpawnedObjects[subwoofer.NetworkObjectId].GetComponent<Item>();
            var bubble = (GameObject)remoteSubwoofer.GetType().GetField("effectBubble", Private).GetValue(remoteSubwoofer);
            Require(bubble.activeSelf, "Late-join subwoofer effects did not start");

            // A client cannot change another player's active slot or release their item.
            Invoke(remote, "SelectSlotRpc", 0, default(RpcParams));
            remote.DropItemServerRpc(new NetworkObjectReference(remote.GetItem(1).NetworkObject), Vector3.up, Vector3.forward);
            await Task.Delay(250);
            Require(inventory.GetActiveItem() == radio && radio.isCurrentlyHeld, "Foreign inventory request accepted");

            Invoke(inventory, "SelectSlotRpc", 0, default(RpcParams));
            await Until(() => lateRemote.GetActiveItem() != null && lateRemote.GetActiveItem().NetworkObjectId == light.NetworkObjectId,
                "slot switch replication");
            Require(!radio.isOn.Value && !radio.IsHoldActionActive, "Unequipped radio still transmits");
            Invoke(inventory, "DropEverything");
            await Until(() => remote.GetItem(0) == null && remote.GetItem(1) == null &&
                lateRemote.GetItem(0) == null && lateRemote.GetItem(1) == null, "death drop replication");
            Require(!light.isCurrentlyHeld && !radio.isCurrentlyHeld && light.GetComponent<Collider>().enabled,
                "Dropped item did not restore physics");

            Inventory clientInventory = client.LocalClient.PlayerObject.GetComponent<Inventory>();
            Item clientLight = client.SpawnManager.SpawnedObjects[light.NetworkObjectId].GetComponent<Item>();
            Require(clientInventory.AddItem(clientLight.gameObject, 0), "Client pickup request rejected locally");
            Inventory serverClientInventory = host.ConnectedClients[client.LocalClientId].PlayerObject.GetComponent<Inventory>();
            await Until(() => serverClientInventory.GetActiveItem() == light, "client-authority request on server");
            Invoke(clientInventory, "SelectSlotRpc", 3, default(RpcParams));
            await Until(() => !serverClientInventory.isHoldingItem.Value, "empty slot selection");
            Invoke(clientInventory, "SelectSlotRpc", 0, default(RpcParams));
            await Until(() => clientInventory.GetActiveItem() == clientLight, "client selection acknowledgement");
            clientInventory.DropItemServerRpc(new NetworkObjectReference(clientLight.NetworkObject), Vector3.up, Vector3.forward);
            await Until(() => !light.isCurrentlyHeld && serverClientInventory.GetItem(0) == null, "client drop");
            Require(inventory.AddItem(radio.gameObject, 0), "Radio repickup request failed");
            await Until(() => inventory.GetActiveItem() == radio, "radio repickup");
            radio.isOn.Value = true;
            radio.RefreshTransmissionServer();
            inventory.DropItemServerRpc(new NetworkObjectReference(radio.NetworkObject), Vector3.up, Vector3.forward);
            await Until(() => !radio.isCurrentlyHeld, "powered radio drop");
            Require(radio.isOn.Value && !radio.IsHoldActionActive, "Normal drop changed power or kept transmission pose");
            inventory.AddItem(radio.gameObject, 0);
            await Until(() => inventory.GetActiveItem() == radio, "powered radio repickup");
            radio.NetworkObject.Despawn(true);
            await Until(() => inventory.GetItem(0) == null && remote.GetItem(0) == null && lateRemote.GetItem(0) == null,
                "despawn clears slots");
            return "PASS: host + 2 clients; late-join slots/visibility/parenting; live radio snapshot; foreign requests rejected; slot switching; death drops; client pickup/drop; despawn cleanup.";
        }
        finally
        {
            if (late != null) late.Shutdown();
            if (client != null) client.Shutdown();
            if (host != null) host.Shutdown();
            await Task.Delay(200);
            if (late != null) UnityEngine.Object.Destroy(late.gameObject);
            if (client != null) UnityEngine.Object.Destroy(client.gameObject);
            if (host != null) UnityEngine.Object.Destroy(host.gameObject);
        }
    }

    private static NetworkManager CreateManager(string name, GameObject player, params GameObject[] items)
    {
        var root = new GameObject(name);
        var transport = root.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 17843);
        var manager = root.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport, PlayerPrefab = player, EnableSceneManagement = false,
            ForceSamePrefabs = true, TickRate = 30
        };
        foreach (GameObject item in items) manager.AddNetworkPrefab(item);
        return manager;
    }

    private static Item Spawn(NetworkManager manager, GameObject prefab)
    {
        GameObject root = UnityEngine.Object.Instantiate(prefab, Vector3.up, Quaternion.identity);
        NetworkObject obj = root.GetComponent<NetworkObject>();
        typeof(NetworkObject).GetField("NetworkManagerOwner", Private | BindingFlags.Public).SetValue(obj, manager);
        root.GetComponent<Rigidbody>().useGravity = false;
        obj.Spawn();
        return root.GetComponent<Item>();
    }

    private static async Task<Inventory> InventoryOn(NetworkManager manager, ulong id)
    {
        await Until(() => manager.SpawnManager.SpawnedObjects.ContainsKey(id), "remote player spawn");
        return manager.SpawnManager.SpawnedObjects[id].GetComponent<Inventory>();
    }

    private static void CheckInventory(Inventory inventory)
    {
        Require(inventory.isHoldingItem.Value, "Holding state missing");
        Item stored = inventory.GetItem(0), active = inventory.GetItem(1);
        Require(stored.transform.parent == inventory.transform && active.transform.parent == inventory.transform,
            "Late-join parenting missing");
        Require(stored.gameObject.activeSelf, "Stored network root disabled");
        foreach (Renderer renderer in stored.GetComponentsInChildren<Renderer>(true))
            Require(renderer.forceRenderingOff, "Late-join stored mesh visible");
        foreach (Renderer renderer in active.GetComponentsInChildren<Renderer>(true))
            Require(!renderer.forceRenderingOff, "Late-join selected mesh hidden");
        Require(!active.GetComponent<Collider>().enabled, "Held collider enabled");
    }

    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private static async Task Until(Func<bool> condition, string step, Action tick = null)
    {
        double deadline = EditorApplication.timeSinceStartup + 8;
        while (!condition())
        {
            tick?.Invoke();
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(step);
            await Task.Delay(20);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
