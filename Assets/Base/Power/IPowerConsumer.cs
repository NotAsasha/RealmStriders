using UnityEngine;

public interface IPowerConsumer
{
    int PowerDemand { get; }
    int Priority { get; }
    bool IsPowered { get; }
    void OnPowerStateChanged(bool isPowered);
}