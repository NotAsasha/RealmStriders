using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkItemsHandler : NetworkBehaviour
{
    public NetworkSaveables database;
    public HashSet<NetworkObject> activeSaveables = new();

    public static NetworkItemsHandler Instance;

    private void Awake() => Instance = this;


    public void Start()
    {
        if (!IsServer) return;
        GameManager.Instance.currentSave.LoadGameSave();
    }
    public List<ObjectEntry> GetSaveablesInfo()
    {
        List<ObjectEntry> data = new List<ObjectEntry>();

        foreach (var netObj in activeSaveables)
        {
            if (netObj == null) continue;

            var item = netObj.GetComponent<INetworkSaveable>();
            if (item == null) continue;

            data.Add(new ObjectEntry
            {
                prefabID = item.PrefabId,
                position = netObj.transform.position,
                rotation = netObj.transform.rotation,
                otherInfo = item.GetInfo()

            });
        }

        return data;
    }

    public void LoadSaveables(List<ObjectEntry> objects)
    {
        if (!IsServer) return;

        foreach (var obj in objects)
        {
            var prefab = database.GetPrefab(obj.prefabID);
            var instance = Instantiate(prefab, obj.position, obj.rotation);
            instance.GetComponent<INetworkSaveable>().ApplyInfo(obj.otherInfo);

            instance.Spawn();
            //instance.Register();
        }
    }
}

[System.Serializable]
public class ObjectEntry
{
    public int prefabID;
    public Vector3 position;
    public Quaternion rotation;
    public string otherInfo = "";
}

public static class NetworkObjectExtension
{
    public static void Register(this NetworkObject obj)
    {
        if (NetworkItemsHandler.Instance == null)
        {
            Debug.LogWarning($"---NetworkObjectExtension: Trying to Register {obj.name}, but no NetworkItemsHandler.Instance exists.");
        }
        NetworkItemsHandler.Instance.activeSaveables.Add(obj);
    }

    public static void UnRegister(this NetworkObject obj)
    {
        if (NetworkItemsHandler.Instance == null)
        {
            Debug.LogWarning($"---NetworkObjectExtension: Trying to UnRegister {obj.name}, but no NetworkItemsHandler.Instance exists.");
        }
        NetworkItemsHandler.Instance.activeSaveables.Remove(obj);
    }
}