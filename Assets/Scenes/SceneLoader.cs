using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Menu
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string _sceneToLoad;
        [SerializeField] private bool _isNetwork;

        public void LoadScene()
        {
            if (_isNetwork) LoadOnlineScene();
            else LoadLocalScene();
        }


        private void LoadOnlineScene()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                SceneManager.LoadScene(_sceneToLoad);
                NetworkManager.Singleton.SceneManager.LoadScene(_sceneToLoad, LoadSceneMode.Single);
            }
            else
            {
                Debug.Log("Waiting for server to load scene...");
            }
        }

        private void LoadLocalScene()
        {
            SceneManager.LoadScene(_sceneToLoad);
        }
    }
}
