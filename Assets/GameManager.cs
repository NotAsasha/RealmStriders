using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameManager instance = null;
    private void Start()
    {
        if (instance != null) Destroy(instance);
        instance = this;
    }
}
