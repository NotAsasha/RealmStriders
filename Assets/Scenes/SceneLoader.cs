using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scenes
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string sceneToLoad;
        [SerializeField] private bool isNetwork;

        public void LoadScene()
        {
            if (isNetwork) LoadOnlineScene();
            else LoadLocalScene();
        }

        private void LoadOnlineScene()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            }
            else
            {
                Debug.Log("Waiting for server to load scene...");
            }
        }

        private void LoadLocalScene()
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
