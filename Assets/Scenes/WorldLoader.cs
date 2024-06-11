using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class WorldLoader : MonoBehaviour
{
    public string worldToLoad;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            UnloadWorld(worldToLoad);
            LoadWorld(worldToLoad);
        }
    }

    public void LoadWorld(string sceneToLoad)
    {
        SceneManager.LoadScene(worldToLoad, LoadSceneMode.Additive);
    }
    public void UnloadWorld(string sceneToLoad)
    {
        if (SceneManager.loadedSceneCount < 2) { return; }
        Scene sceneToUnload = SceneManager.GetSceneAt(SceneManager.loadedSceneCount - 1);
        SceneManager.UnloadSceneAsync(sceneToUnload);
    }
}
