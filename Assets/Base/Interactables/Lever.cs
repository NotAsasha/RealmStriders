using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Base.Interactables
{
    public class Lever : NetworkBehaviour
    {
        [SerializeField] private float cooldown = 0f;

        private bool isReady = true;

        public void SwitchMissionState()
        {
            if (!isReady) return;
            if (!GameManager.Instance.hasStartedMission.Value)
                GameManager.Instance.StartMissionServerRpc();
            else
                GameManager.Instance.StopMissionServerRpc();
            StartCoroutine(Cooldown());
        }

        private IEnumerator Cooldown()
        {
            isReady = false;
            yield return new WaitForSeconds(cooldown);
            isReady = true;
        }

        // Tut bug, new players will not have the color updated on join, NetworkVariables synchronize after the OnNetworkSpawn call
        // wrodi fixed
        protected override void OnNetworkPostSpawn()
        {
            GameManager.Instance.hasStartedMission.OnValueChanged += OnMissionStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (!GameManager.Instance) return;
            GameManager.Instance.hasStartedMission.OnValueChanged -= OnMissionStateChanged;
        }

        private void OnMissionStateChanged(bool oldValue, bool newValue)
        {
            //Debug.LogError($"OnMissionStateChanged, newValue = {newValue}");
            //GetComponent<Renderer>().material.color = newValue ? Color.gray : Color.white; 
        }
    }
}
