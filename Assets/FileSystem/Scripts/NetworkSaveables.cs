using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "NetworkSaveables", menuName = "Not/NetworkSaveables")]
public class NetworkSaveables : ScriptableObject
{
    public List<NetworkObject> prefabs;

    public NetworkObject GetPrefab(int id) => prefabs[id];

#if UNITY_EDITOR
    // Викликається при зміні списку в інспекторі
    private void OnValidate()
    {
        if (prefabs == null) return;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null) continue;

            var item = prefabs[i].GetComponent<Player.Equipment.Item>();
            if (item != null && item.PrefabId != i)
            {
                item.SetPrefabId(i);
                // Позначаємо префаб як "брудний", щоб Unity зберегла зміни в файлі
                EditorUtility.SetDirty(prefabs[i]);
            }
        }
    }
#endif
}