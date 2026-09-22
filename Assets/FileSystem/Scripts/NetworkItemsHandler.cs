using System;
using System.Collections.Generic;
using Player.Equipment;
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
            if (!IsWithinBaseRange(netObj.transform.position)) continue;

            var item = netObj.GetComponent<INetworkSaveable>();
            if (item == null) continue;

            data.Add(new ObjectEntry
            {
                prefabID = item.PrefabId,
                isSceneObject = netObj.IsSceneObject == true,
                position = netObj.transform.position,
                rotation = netObj.transform.rotation,
                otherInfo = item.GetInfo(),
                hasCharge = item is IChargeable,
                charge = item is IChargeable chargeable ? chargeable.CurrentCharge : 0

            });
        }

        return data;
    }

    public void LoadSaveables(List<ObjectEntry> objects, bool legacyFormat = false)
    {
        if (!IsServer) return;

        HashSet<NetworkObject> restoredSceneObjects = new();
        foreach (var obj in objects)
        {
            if (obj.isSceneObject || legacyFormat)
            {
                NetworkObject sceneObject = FindSceneObject(obj.prefabID, restoredSceneObjects);
                if (sceneObject != null)
                {
                    RestoreSaveable(sceneObject, obj);
                    restoredSceneObjects.Add(sceneObject);
                    continue;
                }

                if (!legacyFormat) continue;
            }

            var prefab = database.GetPrefab(obj.prefabID);
            if (prefab == null) continue;

            var instance = Instantiate(prefab, obj.position, obj.rotation);
            instance.Spawn();
            RestoreSaveable(instance, obj);
        }
    }

    private void RestoreSaveable(NetworkObject netObj, ObjectEntry entry)
    {
        netObj.transform.SetPositionAndRotation(entry.position, entry.rotation);

        var saveable = netObj.GetComponent<INetworkSaveable>();
        saveable.ApplyInfo(entry.otherInfo);

        if (entry.hasCharge && saveable is IChargeable chargeable)
        {
            chargeable.ModifyCharge(entry.charge - chargeable.CurrentCharge);
        }
    }

    private bool IsWithinBaseRange(Vector3 position)
    {
        if (GameManager.Instance == null) return true;

        Vector3 baseCenter = GameManager.Instance.spawnPoint;
        float baseRadius = GameManager.Instance.baseRadius;
        return (position - baseCenter).sqrMagnitude <= baseRadius * baseRadius;
    }

    private NetworkObject FindSceneObject(int prefabID, HashSet<NetworkObject> restoredSceneObjects)
    {
        foreach (var netObj in activeSaveables)
        {
            if (netObj == null || netObj.IsSceneObject != true || restoredSceneObjects.Contains(netObj)) continue;

            var saveable = netObj.GetComponent<INetworkSaveable>();
            if (saveable != null && saveable.PrefabId == prefabID)
            {
                return netObj;
            }
        }

        return null;
    }
}

[System.Serializable]
public class ObjectEntry
{
    public int prefabID;
    public bool isSceneObject;
    public bool hasCharge;
    public int charge;
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
            return;
        }
        NetworkItemsHandler.Instance.activeSaveables.Add(obj);
    }

    public static void UnRegister(this NetworkObject obj)
    {
        if (NetworkItemsHandler.Instance == null)
        {
            Debug.LogWarning($"---NetworkObjectExtension: Trying to UnRegister {obj.name}, but no NetworkItemsHandler.Instance exists.");
            return;
        }
        NetworkItemsHandler.Instance.activeSaveables.Remove(obj);
    }
}