using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Scenes
{
    public class SceneLoader : MonoBehaviour
    {
        [FormerlySerializedAs("_sceneToLoad")] [SerializeField] private string sceneToLoad;
        [FormerlySerializedAs("_isNetwork")] [SerializeField] private bool isNetwork;

        public void LoadScene()
        {
            if (isNetwork) LoadOnlineScene();
            else LoadLocalScene();
        }


        private void LoadOnlineScene()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                SceneManager.LoadScene(sceneToLoad);
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
