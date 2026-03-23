using System.Collections;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Base.Radar
{
    public class Radar : Terminal
    {
        [Header("Settings")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private Vector2 border = new Vector2(5f, 5f);

        [Header("References")]
        public Camera radarCamera;
        [SerializeField] private Transform crosshair;
        [SerializeField] private Transform screen;
        [SerializeField] private NetworkObject beamPrefab;
        [SerializeField] private float mapSizeMultiplier = 25.6f;

        private int playerID;
        InputAction control;
        NetworkObject beam;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            radarCamera.transform.position = new Vector3(0f, 100f, 0f);
            radarCamera.transform.eulerAngles = new Vector3(90f, 180, 0f);
            playerID = (int)PlayerMovement.Instance.GetComponent<NetworkObject>().OwnerClientId;
            control = PlayerMovement.Instance.controls.UI.Navigate;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SpawnBeamServerRpc()
        {
            Vector3 spawnPos = new(crosshair.localPosition.x * mapSizeMultiplier, 100f, crosshair.localPosition.z * mapSizeMultiplier);

            beam = Instantiate(beamPrefab, spawnPos, Quaternion.identity);
            beam.Spawn();
            StartCoroutine(Despawner());
        }

        private IEnumerator Despawner()
        {
            yield return new WaitForSeconds(7f);
            beam.Despawn();
        }

        void Update()
        {
            if (!IsTaken() || playerID != ownerID) return;

            Vector2 input = control.ReadValue<Vector2>();

            // Рух у локальній площині екрана
            Vector3 move = moveSpeed * Time.deltaTime * new Vector3(input.x, 0f, input.y);

            // Оновлення позиції
            Vector3 newLocalPos = crosshair.localPosition + move;

            // Обмеження межами екрана
            newLocalPos.x = Mathf.Clamp(newLocalPos.x, -border.x, border.x);
            newLocalPos.z = Mathf.Clamp(newLocalPos.z, -border.y, border.y);
            newLocalPos.y = 0f;

            crosshair.localPosition = newLocalPos;
        }
    }
}