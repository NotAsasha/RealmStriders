using System.Collections.Generic;
using Player.Equipment;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Base
{
    public class PackedFurnitureItem : Item, INetworkSaveable
    {
        private const float PlacementReach = 5f;
        [SerializeField] private LayerMask layersToPlaceOn;
        public NetworkVariable<int> storedPrefabID = new(-1, writePerm: NetworkVariableWritePermission.Server);

        private struct PreviewPart
        {
            public Mesh mesh;
            public Matrix4x4 relativeTransform;
            public RenderParams[] materials;
        }

        private readonly List<PreviewPart> previewParts = new();
        private int previewId = -1;

        public override string GetInfo() => storedPrefabID.Value.ToString();

        public override void ApplyInfo(string info)
        {
            if (IsServer && int.TryParse(info, out int id) &&
                NetworkItemsHandler.Instance.database.TryGetTerminalPrefab(id, out _))
                storedPrefabID.Value = id;
        }

        private bool TryGetPlacement(Vector3 origin, Vector3 forward, out RaycastHit hit)
        {
            return Physics.Raycast(origin, forward, out hit, PlacementReach, layersToPlaceOn,
                QueryTriggerInteraction.Ignore);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!IsSpawned || !IsClient || !IsHeldByClient(NetworkManager.LocalClientId) ||
                CameraMovement.Instance == null) return;
            Transform aim = CameraMovement.Instance.transform;
            if (!TryGetPlacement(aim.position, aim.forward, out RaycastHit hit)) return;
            if (previewId != storedPrefabID.Value) CachePreview();
            var pose = Matrix4x4.TRS(hit.point, Quaternion.Euler(0f, transform.eulerAngles.y - 90f, 0f), Vector3.one);
            foreach (var part in previewParts)
            {
                var matrix = pose * part.relativeTransform;
                for (int i = 0; i < part.materials.Length; i++)
                    if (part.materials[i].material != null)
                        Graphics.RenderMesh(part.materials[i], part.mesh, i, matrix);
            }
        }

        // Drawing the prefab's meshes avoids spawning a second set of terminals, colliders and audio sources.
        private void CachePreview()
        {
            previewParts.Clear();
            previewId = storedPrefabID.Value;
            if (!NetworkItemsHandler.Instance.database.TryGetTerminalPrefab(previewId, out var prefab)) return;
            Matrix4x4 relativeToRoot = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var renderer) ||
                    !renderer.enabled) continue;
                var materials = renderer.sharedMaterials;
                int count = Mathf.Min(filter.sharedMesh.subMeshCount, materials.Length);
                var parameters = new RenderParams[count];
                for (int i = 0; i < count; i++)
                    parameters[i] = new RenderParams(materials[i]) { layer = renderer.gameObject.layer };
                previewParts.Add(new PreviewPart
                {
                    mesh = filter.sharedMesh,
                    relativeTransform = relativeToRoot * filter.transform.localToWorldMatrix,
                    materials = parameters
                });
            }
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (CameraMovement.Instance == null) return;
            Transform aim = CameraMovement.Instance.transform;
            if (TryGetPlacement(aim.position, aim.forward, out _))
                PlaceServerRpc(aim.position, aim.forward, transform.eulerAngles.y - 90f);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceServerRpc(Vector3 origin, Vector3 forward, float yaw, RpcParams rpcParams = default)
        {
            if (!TryGetUsableHolderServer(rpcParams.Receive.SenderClientId, out var holder) ||
                !IsValidAim(holder, origin, forward, layersToPlaceOn) || !float.IsFinite(yaw) ||
                !TryGetPlacement(origin, forward, out RaycastHit hit) ||
                !NetworkItemsHandler.Instance.database.TryGetTerminalPrefab(storedPrefabID.Value, out var prefab)) return;
            var furniture = Instantiate(prefab, hit.point, Quaternion.Euler(0f, yaw, 0f));
            Scene surfaceScene = hit.collider.gameObject.scene;
            if (surfaceScene.IsValid() && surfaceScene.isLoaded)
                SceneManager.MoveGameObjectToScene(furniture.gameObject, surfaceScene);
            furniture.Spawn();
            NetworkObject.Despawn(true);
        }
    }
}
