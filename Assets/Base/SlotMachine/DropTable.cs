using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct EventChance
{
    public UnityEvent onInteract;
    public float dropChance;
    public float luckCoeff; // Based on events nature: -1 bad; 0 neutral; 1 good;
}

public class DropTable : MonoBehaviour
{
    // Local luck:
    // -1 more bad events;
    // 0 normal;
    // 1 more good events;
    public float luckFactor = 0f; 

    public List<EventChance> drops;

    public void ExecuteAction(int index)
    {
        drops[index].onInteract.Invoke();
    }

    public int ChooseDrop()
    {
        //Calculate drops weights
        int n = drops.Count;
        float[] chances = new float[n];
        float sumOfChances = 0f;
        for (int i = 0; i < n; ++i)
        {
            float chance = drops[i].dropChance + drops[i].luckCoeff * luckFactor;

            if (chance < 0) continue;
            chances[i] = chance;
            sumOfChances += chance;
        }
        //Choose random drop
        float dropPosition = UnityEngine.Random.value * sumOfChances;
        for (int i = 0; i < n; ++i)
        {
            dropPosition -= chances[i];
            if (dropPosition <= 0) return i;
        }

        Debug.Log($"---DropTable: Out Of bounds error");
        //default (error)
        return -1;
    }

}
