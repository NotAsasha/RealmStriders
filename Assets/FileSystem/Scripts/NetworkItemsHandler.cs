using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Base;
using FileSystem.Scripts;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

public class NetworkItemsHandler : NetworkBehaviour
{
    public NetworkSaveables database;
    public HashSet<NetworkObject> activeSaveables = new();

    public static NetworkItemsHandler Instance;
    public bool IsWorldRestored { get; private set; }

    private readonly Dictionary<uint, SceneFurnitureEntry> removedSceneFurniture = new();
    private readonly List<ObjectEntry> unresolvedSceneObjects = new();
    private bool hasRestoredItems;
    private Coroutine restoreRoutine;

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            restoreRoutine = StartCoroutine(RestoreWorld());
    }

    public override void OnNetworkDespawn()
    {
        if (restoreRoutine != null)
            StopCoroutine(restoreRoutine);
        restoreRoutine = null;
        IsWorldRestored = false;
        hasRestoredItems = false;
        removedSceneFurniture.Clear();
        unresolvedSceneObjects.Clear();
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    private IEnumerator RestoreWorld()
    {
        // Other in-scene objects register during their network spawn callbacks.
        yield return null;
        restoreRoutine = null;
        SaveFile saveFile = GameManager.Instance != null ? GameManager.Instance.currentSave : null;
        IsWorldRestored = saveFile == null || saveFile.LoadGameSave();
        if (!IsWorldRestored)
        {
            Debug.LogError("[NetworkItemsHandler] The host save could not be restored.", this);
            Steam.SteamManager.Instance?.ReturnToMenuWithError(
                "Could not restore this save. Choose another slot or check the save files.");
        }
    }

    public List<ObjectEntry> GetSaveablesInfo()
    {
        List<ObjectEntry> data = new();
        if (!IsServer) return data;
        if (database == null)
            throw new InvalidOperationException("The saveable prefab registry is missing.");

        HashSet<uint> savedSceneHashes = new();
        foreach (var netObj in activeSaveables)
        {
            if (netObj == null || !netObj.IsSpawned) continue;
            if (!IsWithinBaseRange(netObj.transform.position)) continue;

            var item = netObj.GetComponent<INetworkSaveable>();
            if (item == null || item.PrefabId < 0) continue;

            ObjectEntry entry = new()
            {
                prefabID = item.PrefabId,
                isSceneObject = netObj.IsSceneObject == true,
                position = netObj.transform.position,
                rotation = netObj.transform.rotation,
                otherInfo = item.GetInfo(),
                hasCharge = item is IChargeable,
                charge = item is IChargeable chargeable ? chargeable.CurrentCharge : 0
            };
            if (entry.isSceneObject)
                entry.sceneObjectHash = netObj.PrefabIdHash;

            if (!database.TryGetPrefab(entry.prefabID, out NetworkObject prefab) ||
                prefab.GetComponent<INetworkSaveable>().GetType() != item.GetType())
                continue;

            if (!HasValidPrefabInfo(prefab, entry.otherInfo))
                throw new InvalidOperationException($"Furniture {netObj.name} has an invalid packed prefab ID.");

            data.Add(entry);
            if (entry.isSceneObject)
                savedSceneHashes.Add(entry.sceneObjectHash);
        }

        foreach (ObjectEntry entry in unresolvedSceneObjects)
        {
            if (!savedSceneHashes.Contains(entry.sceneObjectHash) && !removedSceneFurniture.ContainsKey(entry.sceneObjectHash))
                data.Add(entry);
        }

        return data;
    }

    public List<SceneFurnitureEntry> GetRemovedSceneFurniture() => new(removedSceneFurniture.Values);

    public void RecordPackedSceneFurniture(NetworkObject obj)
    {
        if (!IsServer || obj == null || !obj.IsSpawned || obj.IsSceneObject != true ||
            obj.PrefabIdHash == 0 || !obj.TryGetComponent(out Terminal terminal))
            return;

        removedSceneFurniture[obj.PrefabIdHash] = new SceneFurnitureEntry
        {
            sceneObjectHash = obj.PrefabIdHash,
            prefabID = terminal.PrefabId
        };
    }

    public bool ValidateSavedPrefabs(List<ObjectEntry> objects, List<SceneFurnitureEntry> removedFurniture)
    {
        if (database == null) return false;
        foreach (ObjectEntry entry in objects)
        {
            if (!database.TryGetPrefab(entry.prefabID, out NetworkObject prefab) ||
                !HasValidPrefabInfo(prefab, entry.otherInfo))
                return false;
        }
        if (removedFurniture != null)
        {
            foreach (SceneFurnitureEntry removed in removedFurniture)
            {
                if (!database.TryGetTerminalPrefab(removed.prefabID, out _))
                    return false;
            }
        }
        return true;
    }

    private bool HasValidPrefabInfo(NetworkObject prefab, string info) =>
        !prefab.TryGetComponent<PackedFurnitureItem>(out _) ||
        (int.TryParse(info, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packedId) &&
         database.TryGetTerminalPrefab(packedId, out _));

    public bool LoadSaveables(List<ObjectEntry> objects, List<SceneFurnitureEntry> removedFurniture, int formatVersion)
    {
        if (!IsServer) return false;
        if (hasRestoredItems) return true;
        if (!ValidateSavedPrefabs(objects, removedFurniture)) return false;

        HashSet<NetworkObject> restoredSceneObjects = new();
        List<ObjectEntry> unresolved = new();
        List<(NetworkObject Target, ObjectEntry Entry, bool Spawn)> plan = new();
        foreach (ObjectEntry entry in objects)
        {
            if (entry.isSceneObject || formatVersion == 0)
            {
                NetworkObject sceneObject = FindSceneObject(entry.prefabID, entry.sceneObjectHash, entry.position,
                    restoredSceneObjects);
                if (sceneObject != null)
                {
                    plan.Add((sceneObject, entry, false));
                    restoredSceneObjects.Add(sceneObject);
                    continue;
                }

                if (formatVersion > 0)
                {
                    Debug.LogWarning($"[NetworkItemsHandler] Saved scene object {entry.sceneObjectHash} " +
                        $"(prefab ID {entry.prefabID}) was not found.", this);
                    if (entry.sceneObjectHash != 0)
                        unresolved.Add(entry);
                    continue;
                }
            }

            database.TryGetPrefab(entry.prefabID, out NetworkObject prefab);
            plan.Add((prefab, entry, true));
        }

        List<NetworkObject> spawned = new();
        try
        {
            foreach (var (target, entry, spawn) in plan)
            {
                if (!spawn) continue;
                NetworkObject instance = Instantiate(target, entry.position, entry.rotation.normalized);
                spawned.Add(instance);
                instance.Spawn();
                RestoreSaveable(instance, entry);
            }

            foreach (var (target, entry, spawn) in plan)
            {
                if (!spawn)
                    RestoreSaveable(target, entry);
            }
        }
        catch (Exception exception)
        {
            foreach (NetworkObject instance in spawned)
            {
                if (instance.IsSpawned)
                    instance.Despawn(true);
                else
                    Destroy(instance.gameObject);
            }
            Debug.LogException(exception, this);
            return false;
        }

        // Apply explicit removals only after all replacement items have restored.
        if (removedFurniture != null)
        {
            foreach (SceneFurnitureEntry removed in removedFurniture)
            {
                NetworkObject sceneObject = FindSceneObject(removed.prefabID, removed.sceneObjectHash, default);
                if (sceneObject != null && sceneObject.TryGetComponent<Terminal>(out _))
                    sceneObject.Despawn(true);
                removedSceneFurniture[removed.sceneObjectHash] = removed;
            }
        }

        unresolvedSceneObjects.AddRange(unresolved);
        hasRestoredItems = true;
        return true;
    }

    private void RestoreSaveable(NetworkObject netObj, ObjectEntry entry)
    {
        netObj.transform.SetPositionAndRotation(entry.position, entry.rotation.normalized);

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

    private NetworkObject FindSceneObject(int prefabID, uint sceneObjectHash, Vector3 position,
        HashSet<NetworkObject> restoredSceneObjects = null)
    {
        NetworkObject nearest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (var netObj in activeSaveables)
        {
            if (netObj == null || !netObj.IsSpawned || netObj.IsSceneObject != true ||
                (restoredSceneObjects != null && restoredSceneObjects.Contains(netObj)))
                continue;

            var saveable = netObj.GetComponent<INetworkSaveable>();
            if (saveable == null || saveable.PrefabId != prefabID)
                continue;
            if (sceneObjectHash != 0)
            {
                if (netObj.PrefabIdHash == sceneObjectHash)
                    return netObj;
                continue;
            }

            float distance = (netObj.transform.position - position).sqrMagnitude;
            if (nearest == null || distance < closestDistance ||
                (distance == closestDistance && netObj.PrefabIdHash < nearest.PrefabIdHash))
            {
                nearest = netObj;
                closestDistance = distance;
            }
        }

        return nearest;
    }
}

[System.Serializable]
public class ObjectEntry
{
    public int prefabID;
    public bool isSceneObject;
    public uint sceneObjectHash;
    public bool hasCharge;
    public int charge;
    public Vector3 position;
    public Quaternion rotation;
    public string otherInfo = "";
}

[Serializable]
public struct SceneFurnitureEntry
{
    public int prefabID;
    public uint sceneObjectHash;
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
