using Base;
using Player.Equipment;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.Movement
{
    public enum SpectatorTargetType { Player, Terminal }

    [Serializable]
    public class SpectatorTarget
    {
        public Transform Transform;
        public SpectatorTargetType Type;
        public string DisplayName;
    }

    public class SpectatorController : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float followSmoothness = 10f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float distanceFromPlayer = 3f;

        private Transform camTransform;
        private List<SpectatorTarget> targets = new List<SpectatorTarget>();
        private int currentIndex = 0;
        private bool isSpectating = false;

        // Для орбітальної камери навколо гравців
        private float orbitX, orbitY;

        public void StartSpectating(Transform cameraTransform)
        {
            camTransform = cameraTransform;
            camTransform.parent = null; // Відв'язуємо від регдолу!
            isSpectating = true;
            this.enabled = true;

            RebuildTargetList();
            SwitchTarget(0);
        }

        public void StopSpectating()
        {
            isSpectating = false;
            this.enabled = false;
        }

        private void RebuildTargetList()
        {
            targets.Clear();

            // 1. Збираємо живих гравців через чистий список Netcode
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == NetworkManager.Singleton.LocalClientId) continue; // Себе пропускаємо

                if (client.PlayerObject != null)
                {
                    if (client.PlayerObject.TryGetComponent<Human>(out var human) && !human.isDead.Value)
                    {
                        targets.Add(new SpectatorTarget
                        {
                            Transform = client.PlayerObject.transform,
                            Type = SpectatorTargetType.Player,
                            DisplayName = $"Гравець: {client.ClientId}"
                        });
                    }
                }
            }

            // 2. Збираємо всі термінали та камери через твій NetworkItemsHandler та IInteractable
            if (NetworkItemsHandler.Instance != null && NetworkItemsHandler.Instance.activeSaveables != null)
            {
                int terminalCounter = 1;
                foreach (var netObj in NetworkItemsHandler.Instance.activeSaveables)
                {
                    Debug.LogError($"не Знайшовсє {netObj.name}");
                    // Захист від "знищених", але ще не прибраних з пам'яті C++ об'єктів у HashSet
                    if (netObj == null) continue;

                    // Перевіряємо, чи реалізує об'єкт твій інтерфейс взаємодії
                    if (netObj.TryGetComponent<Terminal>(out var interactable))
                    {
                        Transform camPoint = interactable.GetCameraPoint();

                        // Якщо в об'єкта є точка для камери — додаємо її у список спостереження!
                        if (camPoint != null)
                        {
                            targets.Add(new SpectatorTarget
                            {
                                Transform = camPoint,
                                Type = SpectatorTargetType.Terminal,
                                DisplayName = $"Термінал / Камера #{terminalCounter++}"
                            });
                            Debug.LogError($"Знайшовсє {netObj.name}");
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (!isSpectating || targets.Count == 0) return;

            // Перемикання цілей на стрілочки або кліки миші
            // (Краще прив'язати до твоїх Action Maps в Input System)
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
            {
                NextTarget();
            }
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            {
                PrevTarget();
            }
        }

        private void LateUpdate()
        {
            if (!isSpectating || targets.Count == 0) return;

            var currentTarget = targets[currentIndex];

            // Якщо ціль зникла (гравець вийшов або меблі запакували в кубик)
            if (currentTarget.Transform == null)
            {
                RebuildTargetList();
                return;
            }

            if (currentTarget.Type == SpectatorTargetType.Terminal)
            {
                // Жорстка прив'язка до монітора термінала
                camTransform.position = Vector3.Lerp(camTransform.position, currentTarget.Transform.position, Time.deltaTime * followSmoothness);
                camTransform.rotation = Quaternion.Slerp(camTransform.rotation, currentTarget.Transform.rotation, Time.deltaTime * followSmoothness);
            }
            else if (currentTarget.Type == SpectatorTargetType.Player)
            {
                // Орбітальна камера навколо живого гравця (3-я особа)
                Vector2 lookInput = Mouse.current.delta.ReadValue();
                orbitX += lookInput.x * mouseSensitivity * 0.1f;
                orbitY -= lookInput.y * mouseSensitivity * 0.1f;
                orbitY = Mathf.Clamp(orbitY, -20f, 80f);

                Quaternion rotation = Quaternion.Euler(orbitY, orbitX, 0);
                Vector3 targetPosition = currentTarget.Transform.position + Vector3.up * 1.5f; // Рівень плечей/голови
                Vector3 position = targetPosition - (rotation * Vector3.forward * distanceFromPlayer);

                camTransform.rotation = Quaternion.Slerp(camTransform.rotation, rotation, Time.deltaTime * followSmoothness);
                camTransform.position = Vector3.Lerp(camTransform.position, position, Time.deltaTime * followSmoothness);
            }
        }

        private void SwitchTarget(int index)
        {
            if (targets.Count == 0) return;
            currentIndex = (index + targets.Count) % targets.Count;

            // TODO: Викликати UIManager.Instance.ShowSpectatorUI(targets[currentIndex].DisplayName);
            Debug.Log($"Спостерігаємо за: {targets[currentIndex].DisplayName}");
            Debug.Log($"скіку: {targets.Count}");

        }

        private void NextTarget() => SwitchTarget(currentIndex + 1);
        private void PrevTarget() => SwitchTarget(currentIndex - 1);
    }
}