using UnityEngine;

public interface INetworkSaveable
{
    int PrefabId { get; }
    void SetPrefabId(int id);
    string GetInfo();
    void ApplyInfo(string info);
}
