using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "NetworkSaveables", menuName = "Not/NetworkSaveables")]
public class NetworkSaveables : ScriptableObject
{
    [Header("Усі префаби (Предмети та Меблі)")]
    public List<NetworkObject> prefabs;

    [Header("Системні префаби")]
    public GameObject cubeItemPrefab;

    public NetworkObject GetPrefab(int id)
    {
        if (id >= 0 && id < prefabs.Count)
            return prefabs[id];

        Debug.LogError($"[NetworkSaveables] Префаб з ID {id} не знайдено!");
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (prefabs == null) return;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null) continue;

            var saveable = prefabs[i].GetComponent<INetworkSaveable>();

            if (saveable != null && saveable.PrefabId != i)
            {
                saveable.SetPrefabId(i);
                EditorUtility.SetDirty(prefabs[i]);
                Debug.Log($"[NetworkSaveables] Оновлено ID для {prefabs[i].name} на {i}");
            }
            else if (saveable == null)
            {
                Debug.LogWarning($"[NetworkSaveables] Об'єкт {prefabs[i].name} не має компонента, що реалізує INetworkSaveable!");
            }
        }
    }
#endif
}