using System.Collections;
using System.Collections.Generic;
using Base.Shop;
using Base.SellPoint;
using Enemy;
using Player;
using Player.Equipment;
using Player.Equipment.LeafBlower;
using Player.Equipment.Taser;
using Player.Equipment.WaterGun;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Base.RatingScreen;
using FileSystem.Scripts;
using TMPro;

namespace Game.Tutorial
{
    public sealed class TutorialDirector : NetworkBehaviour
    {
        [SerializeField] private TutorialOverlay overlay;
        [SerializeField] private RatingScreen difficultyScreen;
        [SerializeField] private GameObject purchaseSign;
        [SerializeField] private GameObject dropSign;
        [SerializeField] private GameObject comboSign;
        [SerializeField] private TMP_Text dropBinding;
        [SerializeField] private TutorialDummy dummyPrefab;
        [SerializeField] private LeafBlower blowerPrefab;
        [SerializeField] private WaterGun waterPrefab;
        [SerializeField] private Taser taserPrefab;
        [SerializeField] private Item cagePrefab;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform targetSpawn;
        [SerializeField] private Transform itemSpawn;
        [SerializeField] private GameObject saleRoot;
        [SerializeField] private NetworkObject[] saleObjects;
        [SerializeField] private SellPoint sale;
        [SerializeField] private GameObject shopRoot;
        [SerializeField] private GameObject chargerRoot;
        [SerializeField] private NetworkObject[] chargerObjects;
        [SerializeField] private NetworkObject[] shopObjects;
        [SerializeField] private Shop shop;
        [SerializeField] private Base.Terminal shopTerminal;
        [SerializeField, Min(0.2f)] private float transitionDuration = 1.2f;
        [SerializeField, Min(0f)] private float stageCompletionDelay = 3.3f;
        [SerializeField, Range(2, 5)] private int difficultStars = 3;
        [SerializeField] private int practiceCredits = 500;

        public readonly NetworkVariable<int> Stage = new(-1);
        private readonly List<NetworkObject> spawned = new();
        private TutorialDummy target;
        private LeafBlower blower;
        private NetworkObject capturedCage;
        private Human human;
        private PlayerMovement movement;
        private bool changing;
        private bool boughtWater;
        private bool boughtTaser;
        private bool usedCombo;
        private bool pendingCompletion;
        private float completedAt;
        private int shownDifficulty = -1;
        private Coroutine comboRetry;

        public bool IsChanging => changing;
        public TutorialDummy Target => target;
        public LeafBlower Blower => blower;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            sale.SoldServer += OnSold;
            shop.PurchasedServer += OnPurchased;
            SettingsFile.OnSettingsChanged += RefreshDropBinding;
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (NetworkManager.LocalClient?.PlayerObject == null)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    TutorialSession.Exit("could not start tutorial");
                    yield break;
                }
                yield return null;
            }
            var player = NetworkManager.LocalClient.PlayerObject;
            human = player.GetComponent<Human>();
            movement = player.GetComponent<PlayerMovement>();
            GameManager.Instance.teamRating.Value = 4;
            GameManager.Instance.teamMoney.Value = practiceCredits;
            human.ApplyEffect(EffectType.Invincible, float.MaxValue);
            StartCoroutine(ChangeStage(0, false));
        }

        public override void OnNetworkDespawn()
        {
            StopAllCoroutines();
            sale.SoldServer -= OnSold;
            shop.PurchasedServer -= OnPurchased;
            SettingsFile.OnSettingsChanged -= RefreshDropBinding;
            if (target != null) target.CapturedServer -= OnCaptured;
            spawned.Clear();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || movement == null || changing) return;
            if (pendingCompletion && Time.unscaledTime >= completedAt && !movement.isPaused)
            {
                pendingCompletion = false;
                StartCoroutine(ChangeStage(Stage.Value + 1, true));
                return;
            }
            if (target != null && blower != null)
            {
                int original = Mathf.RoundToInt(target.dangerLevel);
                int effective = Mathf.Max(0, original - target.GetWeakStacks());
                if (Stage.Value == 4 && target.GetWeakStacks() > 0) usedCombo = true;
                if (shownDifficulty != effective)
                {
                    difficultyScreen.ShowDifficulty(effective);
                    shownDifficulty = effective;
                }
            }
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame && !movement.isPaused)
                Retry();
        }

        public void Retry()
        {
            if (!IsServer || changing || Stage.Value < 0 || movement == null || movement.isPaused) return;
            StartCoroutine(ChangeStage(Stage.Value >= 5 ? 0 : Stage.Value, false));
        }

        public void Exit() => TutorialSession.Exit();

        private void Complete()
        {
            if (pendingCompletion || changing) return;
            pendingCompletion = true;
            completedAt = Time.unscaledTime + stageCompletionDelay;
        }

        private void OnCaptured(Entity entity, NetworkObject cage)
        {
            if (!IsServer || changing || entity != target) return;
            spawned.Add(cage);
            capturedCage = cage;
            difficultyScreen.gameObject.SetActive(false);
            if (Stage.Value == 0 || Stage.Value == 3 || (Stage.Value == 4 && usedCombo)) Complete();
            else if (Stage.Value == 4) comboRetry = StartCoroutine(RepeatCombo());
        }

        private IEnumerator RepeatCombo()
        {
            yield return new WaitForSecondsRealtime(stageCompletionDelay);
            comboRetry = null;
            if (!changing) StartCoroutine(ChangeStage(4, false));
        }

        private void OnSold(Item item)
        {
            if (IsServer && !changing && Stage.Value == 1 && item.NetworkObject == capturedCage) Complete();
        }

        private void OnPurchased(NetworkObject item)
        {
            if (!IsServer) return;
            spawned.Add(item);
            if (Stage.Value != 2 || changing) return;
            boughtWater |= item.TryGetComponent<WaterGun>(out _);
            boughtTaser |= item.TryGetComponent<Taser>(out _);
            if (boughtWater && boughtTaser) Complete();
        }

        private IEnumerator ChangeStage(int next, bool advancing)
        {
            changing = true;
            pendingCompletion = false;
            if (comboRetry != null)
            {
                StopCoroutine(comboRetry);
                comboRetry = null;
            }
            if (movement.isInInteraction && CameraMovement.Instance != null)
                CameraMovement.Instance.StopInteraction();
            shopTerminal.StopInteraction();
            movement.isInInteraction = false;
            movement.controls.Gameplay.Disable();
            movement.controls.System.Disable();
            movement.controls.UI.Disable();
            if (blower != null && blower.IsSpawned) blower.TurnOffServer();
            string label = next >= 5 ? "TRAINING COMPLETE" : $"EJECT  /  LOAD TAPE {next + 1:00}";
            overlay.PlayTransitionAudio();
            yield return AnimateTransition(0f, 1f, label);

            NetworkObject keep = advancing && next == 1 ? capturedCage : null;
            Cleanup(keep);
            SetStageObjects(saleRoot, saleObjects, next == 1);
            SetStageObjects(shopRoot, shopObjects, next == 2);
            SetStageObjects(chargerRoot, chargerObjects, next == 4);
            Stage.Value = next;
            ShowWorldPrompts(next);
            if (next >= 5)
            {
                overlay.CarryTransitionToMenu();
                TutorialSession.Exit("tutorial completed");
                yield break;
            }
            human.Teleport(playerSpawn.position, playerSpawn.rotation);
            usedCombo = boughtWater = boughtTaser = false;
            if (next == 0 || next == 3 || next == 4)
            {
                blower = Spawn(blowerPrefab, itemSpawn.position);
                target = Spawn(dummyPrefab, targetSpawn.position);
                target.transform.rotation = targetSpawn.rotation;
                target.dangerLevel = next == 0 ? 1 : difficultStars;
                target.CapturedServer += OnCaptured;
                if (next == 4)
                {
                    Spawn(waterPrefab, itemSpawn.position + Vector3.forward * 1.2f);
                    Spawn(taserPrefab, itemSpawn.position - Vector3.forward * 1.2f);
                }
            }
            else if (next == 1)
            {
                if (keep == null) capturedCage = Spawn(cagePrefab, itemSpawn.position).NetworkObject;
                else
                {
                    if (keep.TryGetComponent<Item>(out var item) && item.transform.parent != null &&
                        item.transform.parent.TryGetComponent<Inventory>(out var inventory))
                        inventory.ForgetItemServer(item);
                    keep.TryRemoveParent();
                    keep.transform.position = itemSpawn.position;
                }
            }
            else if (next == 2)
            {
                GameManager.Instance.teamMoney.Value = practiceCredits;
                GameManager.Instance.teamRating.Value = 4;
            }
            if (target != null)
            {
                shownDifficulty = Mathf.RoundToInt(target.dangerLevel);
                difficultyScreen.ShowDifficulty(shownDifficulty);
            }
            yield return AnimateTransition(1f, 0f, label);
            changing = false;
            movement.controls.System.Enable();
            movement.SwitchToGameplayControls();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private T Spawn<T>(T prefab, Vector3 position) where T : NetworkBehaviour
        {
            T instance = Instantiate(prefab, position, prefab.transform.rotation);
            instance.NetworkObject.Spawn();
            spawned.Add(instance.NetworkObject);
            return instance;
        }

        private void Cleanup(NetworkObject keep)
        {
            if (target != null) target.CapturedServer -= OnCaptured;
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                var item = spawned[i];
                if (item == keep && item != null) continue;
                if (item != null && item.IsSpawned) item.Despawn(true);
                spawned.RemoveAt(i);
            }
            target = null;
            blower = null;
            capturedCage = keep;
        }

        private static void SetStageObjects(GameObject root, NetworkObject[] objects, bool visible)
        {
            if (visible)
            {
                root.SetActive(true);
                foreach (var obj in objects)
                {
                    obj.gameObject.SetActive(true);
                    if (!obj.IsSpawned) obj.Spawn();
                }
            }
            else
            {
                foreach (var obj in objects)
                {
                    if (obj.IsSpawned) obj.Despawn(false);
                    // NGO can detach scene NetworkObjects from non-networked stage parents.
                    obj.gameObject.SetActive(false);
                }
                root.SetActive(false);
            }
        }

        private IEnumerator AnimateTransition(float from, float to, string label)
        {
            float duration = transitionDuration * 0.5f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                overlay.SetTransition(Mathf.Lerp(from, to, t / duration), Time.unscaledTime, label);
                yield return null;
            }
            overlay.SetTransition(to, Time.unscaledTime, label);
        }

        private void ShowWorldPrompts(int stage)
        {
            difficultyScreen.gameObject.SetActive(stage == 0 || stage == 3 || stage == 4);
            purchaseSign.SetActive(stage == 2);
            dropSign.SetActive(stage == 3);
            comboSign.SetActive(stage == 4);
            shownDifficulty = -1;
            RefreshDropBinding();
        }

        private void RefreshDropBinding()
        {
            if (movement == null || movement.controls == null) return;
            dropBinding.text = "Drop Item: " + movement.controls.Gameplay.Drop.GetBindingDisplayString();
        }
    }
}
