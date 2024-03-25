using UnityEngine;
using UnityEngine.SceneManagement;

namespace Menu
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string _sceneToLoad;

        public void LoadScene()
        {
            SceneManager.LoadScene(_sceneToLoad);
        }

    }
}
