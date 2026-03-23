using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Steam
{
    public class ToggleOnLobbyJoin : MonoBehaviour
    {
        [FormerlySerializedAs("ToggleState")] public bool toggleState = false;
        void Start()
        {
            NetworkManager.Singleton.OnClientStarted += Toggle;

        }

        void Toggle()
        {
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            NetworkManager.Singleton.OnClientStarted -= Toggle;
        }
    }
}
