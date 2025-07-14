using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Menu
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string _sceneToLoad;

        public void LoadScene()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(_sceneToLoad, LoadSceneMode.Single);
            }
            else
            {
                Debug.Log("Waiting for server to load scene...");
            }
        }
    }
}
