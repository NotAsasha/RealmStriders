using Player.Equipment;
using System.Drawing;
using Unity.Netcode;
using UnityEngine;
namespace Base
{
    public class PackedFurnitureItem : Item, INetworkSaveable
    {
        [SerializeField] private LayerMask layersToPlaceOn;

        public NetworkVariable<int> storedPrefabID = new(writePerm: NetworkVariableWritePermission.Server);

        [SerializeField] private GameObject previewPrefab;
        private GameObject currentPreview;

        #region Unity Lifecycle

        public void Start()
        {
            if (IsOwner)
            {
                previewPrefab = NetworkItemsHandler.Instance.database.GetPrefab(storedPrefabID.Value).gameObject;
                currentPreview = Instantiate(previewPrefab, transform);
                currentPreview.SetActive(false);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (currentPreview != null) Destroy(currentPreview);
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned ||!isCurrentlyHeld) return;

            //only local
            HandlePreview();
        }

        #endregion

        #region Saving

        public override string GetInfo()
        {
            return storedPrefabID.Value.ToString();
        }

        public override void ApplyInfo(string info)
        {
            if (!int.TryParse(info, out var id)) Debug.LogError("---PackedFurnitureItem: Tried to load non-int value.");
            storedPrefabID.Value = id;
        }

        #endregion

        #region Item Specific

        override protected void ExecuteItemAction(GameObject player)
        {
            // if (currentPreview.GetComponent<Collider>()) --- somehow check if out-of-bounds
            PlaceServerRpc(currentPreview.transform.position, currentPreview.transform.rotation);
        }

        [Rpc(SendTo.Server)]
        private void PlaceServerRpc(Vector3 position, Quaternion rotation)
        {
            NetworkObject furniturePrefab = NetworkItemsHandler.Instance.database.GetPrefab(storedPrefabID.Value);

            NetworkObject spawnedFurniture = Instantiate(furniturePrefab, position, rotation);
            spawnedFurniture.Spawn();
            spawnedFurniture.Register();

            NetworkObject.Despawn(true);
        }

        override public void Drop()
        {
            currentPreview.SetActive(false);
            base.Drop();
        }

        #endregion

        private void HandlePreview()
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            if (Physics.Raycast(ray, out RaycastHit hit, 5f, layersToPlaceOn))
            {
                currentPreview.SetActive(true);
                currentPreview.transform.position = hit.point; // Тут можна додати логіку прив'язки до сітки (Grid Snapping)
                currentPreview.transform.rotation = hit.transform.rotation;

                // Обертання: можна додати поворот на коліщатко миші
                currentPreview.transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y - 90f, 0f);
            }
            else
            {
                currentPreview.SetActive(false);
            }
        }
    }
}