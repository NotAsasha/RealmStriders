using System.Collections;
using System.Collections.Generic;
using Player.Equipment;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Base.Crafting
{
    public sealed class Crafter : NetworkBehaviour
    {
        [Header("Recipes")]
        [SerializeField] private CraftRecipe[] recipes = System.Array.Empty<CraftRecipe>();
        [SerializeField, Min(0.1f)] private float craftDuration = 3f;

        [Header("Output")]
        [SerializeField] private Transform outputSpawnPoint;

        [Header("Presentation")]
        [SerializeField] private Renderer leftIndicator;
        [SerializeField] private Renderer rightIndicator;
        [SerializeField] private Color idleLightColor = Color.red;
        [SerializeField] private Color validLightColor = Color.green;
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField] private Vector3 leftDoorClosedPosition = new(-1f, 1f, 0f);
        [SerializeField] private Vector3 rightDoorClosedPosition = new(1f, 1f, 0f);
        [SerializeField] private float doorAnimationDuration = 0.2f;
        [SerializeField] private AudioSource craftNoiseAudio;
        [SerializeField] private AudioSource itemDetectedAudio;
        [SerializeField] private AudioSource tubeAudio;
        [SerializeField] private ParticleSystem tubeParticle;
        [SerializeField] private TMP_Text recipePreviewText;
        [SerializeField] private TMP_Text recipeStatusText;
        [SerializeField, Min(0.05f)] private float socketValidationInterval = 0.1f;

        private readonly NetworkVariable<bool> leftInputValid = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> rightInputValid = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> leftInputOccupied = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> rightInputOccupied = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> previewRecipeIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> isCrafting = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly Item[] socketItems = new Item[2];
        private Coroutine craftRoutine;
        private Coroutine doorRoutine;
        private Coroutine socketValidationRoutine;
        private Vector3 leftDoorOpenPosition;
        private Vector3 rightDoorOpenPosition;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            leftDoorOpenPosition = leftDoor != null ? leftDoor.localPosition : Vector3.zero;
            rightDoorOpenPosition = rightDoor != null ? rightDoor.localPosition : Vector3.zero;
            leftInputValid.OnValueChanged += OnLeftIndicatorChanged;
            rightInputValid.OnValueChanged += OnRightIndicatorChanged;
            leftInputOccupied.OnValueChanged += OnInputStateChanged;
            rightInputOccupied.OnValueChanged += OnInputStateChanged;
            previewRecipeIndex.OnValueChanged += OnRecipePreviewChanged;
            isCrafting.OnValueChanged += OnCraftingChanged;
            RefreshPresentation();
            RefreshRecipePreview();
            StartDoorAnimation(isCrafting.Value);
        }

        public override void OnNetworkDespawn()
        {
            leftInputValid.OnValueChanged -= OnLeftIndicatorChanged;
            rightInputValid.OnValueChanged -= OnRightIndicatorChanged;
            leftInputOccupied.OnValueChanged -= OnInputStateChanged;
            rightInputOccupied.OnValueChanged -= OnInputStateChanged;
            previewRecipeIndex.OnValueChanged -= OnRecipePreviewChanged;
            isCrafting.OnValueChanged -= OnCraftingChanged;
            if (craftRoutine != null)
                StopCoroutine(craftRoutine);
            if (socketValidationRoutine != null)
                StopCoroutine(socketValidationRoutine);
            base.OnNetworkDespawn();
        }

        public void RegisterItem(int socketIndex, Item item)
        {
            if (!IsServer || isCrafting.Value || !IsValidSocket(socketIndex) || item == null ||
                !item.IsSpawned || item.transform.parent != null)
                return;

            if (socketItems[socketIndex] == item)
                return;

            socketItems[socketIndex] = item;
            SetInputOccupied(socketIndex, true);
            EnsureSocketValidation();
            RefreshInputIndicators();
            if (IsValidIngredient(item))
                PlayItemDetectedSoundRpc();
        }

        public void UnregisterItem(int socketIndex, Item item)
        {
            if (!IsServer || !IsValidSocket(socketIndex) || socketItems[socketIndex] != item)
                return;

            ClearSocket(socketIndex);
        }

        private void EnsureSocketValidation()
        {
            if (!IsServer || socketValidationRoutine != null)
                return;

            socketValidationRoutine = StartCoroutine(ValidateSocketItemsRoutine());
        }

        private void StopSocketValidationIfEmpty()
        {
            if (socketItems[0] != null || socketItems[1] != null ||
                leftInputOccupied.Value || rightInputOccupied.Value)
                return;

            if (socketValidationRoutine != null)
            {
                StopCoroutine(socketValidationRoutine);
                socketValidationRoutine = null;
            }
        }

        private IEnumerator ValidateSocketItemsRoutine()
        {
            WaitForSeconds wait = new(Mathf.Max(0.05f, socketValidationInterval));
            while (IsServer && !isCrafting.Value)
            {
                ValidateSocketItems();
                if (socketItems[0] == null && socketItems[1] == null &&
                    !leftInputOccupied.Value && !rightInputOccupied.Value)
                    break;

                yield return wait;
            }

            socketValidationRoutine = null;
        }

        private void ValidateSocketItems()
        {
            for (int socketIndex = 0; socketIndex < socketItems.Length; socketIndex++)
            {
                Item item = socketItems[socketIndex];
                if (item == null || !item.IsSpawned || item.transform.parent != null)
                    ClearSocket(socketIndex);
            }
        }

        private void ClearSocket(int socketIndex)
        {
            if (!IsValidSocket(socketIndex) ||
                (socketItems[socketIndex] == null && !IsSocketOccupied(socketIndex)))
                return;

            socketItems[socketIndex] = null;
            SetInputOccupied(socketIndex, false);
            RefreshInputIndicators();
            StopSocketValidationIfEmpty();
        }

        private bool IsSocketOccupied(int socketIndex)
        {
            return socketIndex == 0 ? leftInputOccupied.Value : rightInputOccupied.Value;
        }

        public void TryStartCraft()
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                TryStartCraftServer();
                return;
            }

            TryStartCraftServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TryStartCraftServerRpc()
        {
            TryStartCraftServer();
        }

        private void TryStartCraftServer()
        {
            if (!IsServer || isCrafting.Value || craftRoutine != null ||
                socketItems[0] == null || socketItems[1] == null)
                return;

            CraftRecipe recipe = FindRecipe(socketItems[0], socketItems[1]);
            if (recipe == null || recipe.Output == null)
                return;

            craftRoutine = StartCoroutine(CraftRoutine(recipe));
        }

        private IEnumerator CraftRoutine(CraftRecipe recipe)
        {
            isCrafting.Value = true;
            SetDoorStateRpc(true);

            DespawnInput(0);
            DespawnInput(1);
            socketItems[0] = null;
            socketItems[1] = null;
            leftInputOccupied.Value = false;
            rightInputOccupied.Value = false;
            previewRecipeIndex.Value = -1;
            RefreshInputIndicators();

            yield return new WaitForSeconds(craftDuration);

            if (SpawnOutputServer(recipe.Output))
                PlayTubeSpawnPresentationRpc();

            SetDoorStateRpc(false);
            isCrafting.Value = false;
            craftRoutine = null;
        }

        private void SetInputOccupied(int socketIndex, bool occupied)
        {
            if (socketIndex == 0)
                leftInputOccupied.Value = occupied;
            else if (socketIndex == 1)
                rightInputOccupied.Value = occupied;

            RefreshRecipeState();
        }

        private void RefreshRecipeState()
        {
            if (!IsServer)
                return;

            CraftRecipe recipe = leftInputOccupied.Value && rightInputOccupied.Value
                ? FindRecipe(socketItems[0], socketItems[1])
                : null;
            previewRecipeIndex.Value = recipe == null ? -1 : System.Array.IndexOf(recipes, recipe);
        }

        private bool SpawnOutputServer(Item outputPrefab)
        {
            if (!IsServer || NetworkManager.Singleton == null || outputPrefab == null)
            {
                Debug.LogError("[Crafter] Cannot spawn craft output: server, NetworkManager, or output prefab is missing.");
                return false;
            }

            NetworkObject networkPrefab = outputPrefab.GetComponent<NetworkObject>();
            if (networkPrefab == null)
            {
                Debug.LogError($"[Crafter] Output prefab '{outputPrefab.name}' has no NetworkObject component.");
                return false;
            }

            Transform spawnPoint = outputSpawnPoint != null ? outputSpawnPoint : transform;
            networkPrefab.InstantiateAndSpawn(
                NetworkManager.Singleton, 0, false, false, false,
                spawnPoint.position, spawnPoint.rotation);
            return true;
        }

        private void DespawnInput(int index)
        {
            Item item = socketItems[index];
            if (item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned)
                item.NetworkObject.Despawn(true);
        }

        private CraftRecipe FindRecipe(Item first, Item second)
        {
            foreach (CraftRecipe recipe in recipes)
            {
                if (recipe != null && recipe.Matches(first, second))
                    return recipe;
            }

            return null;
        }

        private void RefreshInputIndicators()
        {
            leftInputValid.Value = IsValidIngredient(socketItems[0]);
            rightInputValid.Value = IsValidIngredient(socketItems[1]);
        }

        private bool IsValidIngredient(Item item)
        {
            if (item == null)
                return false;

            foreach (CraftRecipe recipe in recipes)
            {
                if (recipe != null && recipe.Accepts(item))
                    return true;
            }

            return false;
        }

        private void OnLeftIndicatorChanged(bool _, bool __) => RefreshPresentation();
        private void OnRightIndicatorChanged(bool _, bool __) => RefreshPresentation();
        private void OnInputStateChanged(bool _, bool __) => RefreshRecipePreview();
        private void OnRecipePreviewChanged(int _, int __) => RefreshRecipePreview();
        private void OnCraftingChanged(bool _, bool __)
        {
            RefreshPresentation();
            RefreshRecipePreview();
        }

        private void RefreshPresentation()
        {
            SetIndicator(leftIndicator, leftInputValid.Value);
            SetIndicator(rightIndicator, rightInputValid.Value);
            if (isCrafting.Value)
                craftNoiseAudio?.Play();
            else if (craftNoiseAudio != null && craftNoiseAudio.isPlaying)
                craftNoiseAudio.Stop();
        }

        private void RefreshRecipePreview()
        {
            if (recipePreviewText == null && recipeStatusText == null)
                return;

            bool bothSlotsOccupied = leftInputOccupied.Value && rightInputOccupied.Value;
            CraftRecipe recipe = previewRecipeIndex.Value >= 0 &&
                previewRecipeIndex.Value < recipes.Length
                ? recipes[previewRecipeIndex.Value]
                : null;

            if (recipe != null)
            {
                if (recipePreviewText != null)
                    recipePreviewText.text = recipe.GetPreviewText();
                if (recipeStatusText != null)
                    recipeStatusText.text = isCrafting.Value ? "CRAFTING..." : "READY TO CRAFT";
                return;
            }

            if (recipePreviewText != null)
                recipePreviewText.text = bothSlotsOccupied ? "NO RECIPE" : "INSERT TWO ITEMS";
            if (recipeStatusText != null)
                recipeStatusText.text = bothSlotsOccupied ? "ERROR" : "WAITING";
        }

        private void SetIndicator(Renderer indicator, bool isValid)
        {
            if (indicator == null)
                return;

            Color color = isValid ? validLightColor : idleLightColor;
            Material material = indicator.material;
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void SetDoorStateRpc(bool closed)
        {
            StartDoorAnimation(closed);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void PlayTubeSpawnPresentationRpc()
        {
            if (tubeAudio != null)
                tubeAudio.Play();
            if (tubeParticle != null)
                tubeParticle.Play();
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void PlayItemDetectedSoundRpc()
        {
            itemDetectedAudio?.Play();
        }

        private static bool IsValidSocket(int socketIndex) => socketIndex >= 0 && socketIndex < 2;

        private void StartDoorAnimation(bool closed)
        {
            if (doorRoutine != null)
                StopCoroutine(doorRoutine);
            doorRoutine = StartCoroutine(AnimateDoors(closed));
        }

        private IEnumerator AnimateDoors(bool closed)
        {
            Vector3 leftStart = leftDoor != null ? leftDoor.localPosition : Vector3.zero;
            Vector3 rightStart = rightDoor != null ? rightDoor.localPosition : Vector3.zero;
            Vector3 leftTarget = closed ? leftDoorClosedPosition : leftDoorOpenPosition;
            Vector3 rightTarget = closed ? rightDoorClosedPosition : rightDoorOpenPosition;
            float duration = Mathf.Max(0.01f, doorAnimationDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (leftDoor != null) leftDoor.localPosition = Vector3.Lerp(leftStart, leftTarget, t);
                if (rightDoor != null) rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, t);
                yield return null;
            }

            if (leftDoor != null) leftDoor.localPosition = leftTarget;
            if (rightDoor != null) rightDoor.localPosition = rightTarget;
            doorRoutine = null;
        }
    }
}
