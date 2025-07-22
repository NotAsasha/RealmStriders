using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using System.Collections;

public class BootstrapManager : MonoBehaviour
{
    [Header("Наступна сцена")]
    public string sceneToLoad = "Lobby";

    private void Start()
    {
        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        // (Опціонально) Затримка — на випадок, якщо треба трохи почекати
        yield return new WaitForSeconds(1f);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad);  

        while (!asyncLoad.isDone)
        { 
            // (Опціонально) Можна оновлювати progress-бар: asyncLoad.progress
            yield return null; 
        }
    }
} 
