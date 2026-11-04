using System.Collections;
using FileSystem.Scripts;
using Steam;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tutorial
{
    public sealed class TutorialSession : MonoBehaviour
    {
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private GameObject[] authoringReferences;
        public static bool Requested { get; private set; }
        private bool bootstrapping;

        private void Awake()
        {
            foreach (GameObject reference in authoringReferences) reference.SetActive(false);
        }

        public static void Open()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;
            Requested = true;
            if (GameManager.Instance != null) GameManager.Instance.currentSave = null;
            SceneManager.LoadScene("Tutorial");
        }

        private IEnumerator Start()
        {
            Requested = true;
            if (GameFileHandler.Instance == null || SteamManager.Instance == null ||
                NetworkManager.Singleton == null || GameManager.Instance == null)
            {
                bootstrapping = true;
                SceneManager.LoadScene("SteamBoot");
                yield break;
            }
            GameManager.Instance.currentSave = null;
            GameManager.Instance.spawnPoint = playerSpawn.position;
            // Register authored scene objects before starting the local NGO session.
            yield return null;
            SteamManager.Instance.StartSingleplayer();
            if (!NetworkManager.Singleton.IsHost)
                Exit("could not start tutorial");
        }

        public static void Exit(string notification = null)
        {
            Requested = false;
            Time.timeScale = 1f;
            if (SteamManager.Instance != null)
            {
                if (!string.IsNullOrEmpty(notification)) SteamManager.Instance.NotifyOnMenu(notification);
                SteamManager.Instance.Disconnect();
            }
            SceneManager.LoadScene("SteamBoot");
        }

        private void OnDestroy()
        {
            if (!bootstrapping) Requested = false;
        }
    }
}
