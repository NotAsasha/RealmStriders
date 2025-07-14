using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
public class WorldLoader : MonoBehaviour
{
    public string worldToLoad;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (SceneManager.loadedSceneCount > 1) { UnloadWorld(SceneManager.GetSceneAt(SceneManager.loadedSceneCount - 1).name); }
            LoadWorld(worldToLoad);
        }
    }

    public void LoadWorld(string sceneToLoad)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Additive);
        }
        else
        {
            Debug.Log("Waiting for server to load scene...");
        }
    }
    public void UnloadWorld(string sceneToLoad)
    {
        Scene sceneToUnload = SceneManager.GetSceneByName(sceneToLoad);
        NetworkManager.Singleton.SceneManager.UnloadScene(sceneToUnload);
    }
}
