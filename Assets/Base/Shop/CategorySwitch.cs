using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class CategorySwitch : MonoBehaviour
{
    public GameObject[] categories;

    public void SwitchToItem(int index)
    {
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].SetActive(i == index);
        }
    }
}
