using Base;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "NetworkSaveables", menuName = "Not/NetworkSaveables")]
public class NetworkSaveables : ScriptableObject
{
    [Header("Усі префаби (Предмети та Меблі)")]
    public List<NetworkObject> prefabs;

    [Header("Системні префаби")]
    public GameObject cubeItemPrefab;

    private Dictionary<int, NetworkObject> prefabsById;

    public NetworkObject GetPrefab(int id)
    {
        if (TryGetPrefab(id, out NetworkObject prefab))
            return prefab;

        Debug.LogError($"[NetworkSaveables] No unique saveable prefab has ID {id}.", this);
        return null;
    }

    public bool TryGetPrefab(int id, out NetworkObject prefab)
    {
        prefabsById ??= BuildLookup();
        return prefabsById.TryGetValue(id, out prefab) && prefab != null;
    }

    public bool TryGetTerminalPrefab(int id, out NetworkObject prefab) =>
        TryGetPrefab(id, out prefab) && prefab.TryGetComponent<Terminal>(out _);

    private Dictionary<int, NetworkObject> BuildLookup()
    {
        Dictionary<int, NetworkObject> lookup = new();
        if (prefabs == null) return lookup;

        foreach (NetworkObject prefab in prefabs)
        {
            if (prefab == null) continue;
            INetworkSaveable saveable = prefab.GetComponent<INetworkSaveable>();
            if (saveable == null || saveable.PrefabId < 0) continue;

            int id = saveable.PrefabId;
            if (!lookup.TryAdd(id, prefab))
                lookup[id] = null;
        }
        return lookup;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        prefabsById = null;
        if (prefabs == null) return;

        HashSet<int> ids = new();
        foreach (NetworkObject prefab in prefabs)
        {
            if (prefab == null) continue;

            INetworkSaveable saveable = prefab.GetComponent<INetworkSaveable>();
            if (saveable == null || saveable.PrefabId < 0)
                Debug.LogWarning($"[NetworkSaveables] Assign a saveable ID to {prefab.name}.", prefab);
            else if (!ids.Add(saveable.PrefabId))
                Debug.LogWarning($"[NetworkSaveables] Duplicate saveable ID {saveable.PrefabId} on {prefab.name}.", prefab);
        }
    }
#endif
}
