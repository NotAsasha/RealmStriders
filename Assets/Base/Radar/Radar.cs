using JetBrains.Annotations;
using Player;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
public class Radar : Terminal
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Transform crosshair;
    [SerializeField] private Transform screen;
    [SerializeField] private Vector2 border = new Vector2(5f, 5f);
    [SerializeField] private Camera radarCamera;

    [SerializeField] private NetworkObject beamPrefab;
    [SerializeField] private float mapSizeMultiplier = 25.6f;

    private int playerID;
    InputAction control;
    override public void OnNetworkSpawn() {
        playerID = (int)Movement.instance.GetComponent<NetworkObject>().OwnerClientId;
        control = Movement.instance._controls.UI.Navigate;
    }

    NetworkObject beam;
    [ServerRpc(RequireOwnership = false)]
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
        Vector3 moveDir = (crosshair.right * input.x + crosshair.up * input.y).normalized;

        // Movement scaled by speed and deltaTime
        Vector3 move = moveDir * moveSpeed * Time.deltaTime;

        // Compute new position
        Vector3 newLocalPos = crosshair.localPosition + move;

        // Clamp position within minimap bounds
        newLocalPos.x = Mathf.Clamp(newLocalPos.x, -border.x, border.x);
        newLocalPos.z = Mathf.Clamp(newLocalPos.z, -border.y, border.y);
        newLocalPos.y = 0f;

        crosshair.localPosition = newLocalPos;
    }

}
