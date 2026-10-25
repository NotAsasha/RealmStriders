using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Steam
{
    public class BootstrapManager : MonoBehaviour
    {
        [Header("Next scene")]
        public string sceneToLoad = "Menu";

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(LoadSceneAsync());
        }

        private IEnumerator LoadSceneAsync()
        {
            yield return new WaitForSeconds(1f);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad);  

            while (!asyncLoad.isDone)
            { 
                yield return null; 
            }
        }
    }
}
