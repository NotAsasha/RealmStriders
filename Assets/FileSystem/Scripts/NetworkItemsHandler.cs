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

            var item = netObj.GetComponent<Player.Equipment.Item>();
            if (item == null) continue;

            data.Add(new ObjectEntry
            {
                prefabID = item.PrefabId,
                position = netObj.transform.position,
                rotation = netObj.transform.rotation
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

            instance.Spawn();
            instance.Register();
        }
    }
}

[System.Serializable]
public class ObjectEntry
{
    public int prefabID;
    public Vector3 position;
    public Quaternion rotation;
}

public static class NetworkObjectExtension
{
    public static void Register(this NetworkObject obj)
    {
        NetworkItemsHandler.Instance.activeSaveables.Add(obj);
    }

    public static void UnRegister(this NetworkObject obj)
    {
        NetworkItemsHandler.Instance.activeSaveables.Remove(obj);
    }
}