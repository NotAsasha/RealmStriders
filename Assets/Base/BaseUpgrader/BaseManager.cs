using System;
using Unity.Netcode;
using UnityEngine;

namespace Base.BaseUpgrader
{
    [Flags]
    public enum BaseUpgrades : int
    {
        None = 0,         // 00000000
        IsTerminalBought = 1 << 0,    // 00000001 (1)
        IsDetectionBought = 1 << 1,    // 00000010 (2)
        IsBeamBought = 1 << 2,    // 00000100 (4)
        IsCasinoBought = 1 << 3,// 00001000 (8)
        IsShieldBought = 1 << 4, // 00010000 (16)
        IsChargerBought = 1 << 5, // 00100000 (32)
    }

    public class BaseManager : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkObject radarTerminal;
        [SerializeField] private NetworkObject radarButton;
        [SerializeField] private NetworkObject shieldButton;
        [SerializeField] private GameObject casinoWall;
        [SerializeField] private NetworkObject charger;

        [Header("Power Grid")]
        [SerializeField] private PowerGrid powerGrid;

        [Header("Camera & Layers")]
        [Tooltip("Layer for detecting enemies.")]
        [SerializeField] private LayerMask radarOnlyLayer; 
        private Camera radarCamera;

        // Server-only write: тільки сервер змінює стан апгрейдів
        public readonly NetworkVariable<int> baseUpgrades = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public static BaseManager Instance;

        public PowerGrid PowerGrid => powerGrid;

        #region Unity Lifecycle

        private void Awake()
        {
            Instance = this;
            if (!powerGrid)
            {
                powerGrid = GetComponent<PowerGrid>();
            }

            // NGO вимагає, щоб GameObjects з NetworkObject/NetworkBehaviour були активними при завантаженні сцени,
            // інакше NGO виключає їх зі спавну та синхронізації (NetworkBehaviour.IsSpawned залишається false,
            // що викликає NullReferenceException при виклику RPC).
            // Тому ми тримаємо самі GameObjects активними, а видимість та взаємодію керуємо через
            // увімкнення/вимкнення Renderers, Colliders, Canvases та Lights.
            if (radarTerminal) radarTerminal.gameObject.SetActive(true);
            if (radarButton) radarButton.gameObject.SetActive(true);
            if (shieldButton) shieldButton.gameObject.SetActive(true);
            if (charger) charger.gameObject.SetActive(true);

            // Початково приховуємо візуал та колайдери до перевірки збереження/покупки
            SetUpgradeActive(radarTerminal, false);
            SetUpgradeActive(radarButton, false);
            SetUpgradeActive(shieldButton, false);
            SetUpgradeActive(charger, false);
        }

        public override void OnNetworkSpawn()
        {
            if (!radarTerminal)
            {
                Debug.LogError("[Base] radarTerminal is not assigned.", this);
                return;
            }
            if (!radarButton)
            {
                Debug.LogWarning("[Base] radarButton is not assigned.", this);
            }
            if (!shieldButton)
            {
                Debug.LogWarning("[Base] shieldButton is not assigned.", this);
            }
            if (!charger)
            {
                Debug.LogWarning("[Base] charger is not assigned.", this);
            }

            if (!powerGrid)
            {
                Debug.LogWarning("[Base] powerGrid is not assigned or found on this object.", this);
            }

            var radar = radarTerminal.GetComponent<Radar.Radar>();
            if (!radar)
            {
                Debug.LogError("[Base] Radar component not found on radarTerminal.", radarTerminal);
                return;
            }
            radarCamera = radar.radarCamera;
            if (!radarCamera && IsClient)
            {
                Debug.LogError("[Base] radarCamera is missing on Radar.", radar);
            }

            baseUpgrades.OnValueChanged += OnTerminalBoughtChanged;
            baseUpgrades.OnValueChanged += OnDetectionBoughtChanged;
            baseUpgrades.OnValueChanged += OnBeamBoughtChanged;
            baseUpgrades.OnValueChanged += OnCasinoBoughtChanged;
            baseUpgrades.OnValueChanged += OnShieldBoughtChanged;
            baseUpgrades.OnValueChanged += OnChargerBoughtChanged;

            // Застосовуємо поточний стан (важливо для пізно підключених клієнтів)
            OnTerminalBoughtChanged(0, baseUpgrades.Value);
            OnDetectionBoughtChanged(0, baseUpgrades.Value);
            OnBeamBoughtChanged(0, baseUpgrades.Value);
            OnCasinoBoughtChanged(0, baseUpgrades.Value);
            OnShieldBoughtChanged(0, baseUpgrades.Value);
            OnChargerBoughtChanged(0, baseUpgrades.Value);
        }

        public override void OnNetworkDespawn()
        {
            baseUpgrades.OnValueChanged -= OnTerminalBoughtChanged;
            baseUpgrades.OnValueChanged -= OnDetectionBoughtChanged;
            baseUpgrades.OnValueChanged -= OnBeamBoughtChanged;
            baseUpgrades.OnValueChanged -= OnCasinoBoughtChanged;
            baseUpgrades.OnValueChanged -= OnShieldBoughtChanged;
            baseUpgrades.OnValueChanged -= OnChargerBoughtChanged;
        }

        #endregion

        #region --Handlers--

        private void SetUpgradeActive(NetworkObject netObj, bool active)
        {
            if (netObj == null) return;

            // Не вимикаємо сам GameObject, щоб не ламати життєвий цикл NetworkBehaviour у NGO.
            // Замість цього вимикаємо візуалізацію та колізії/інтерактивність.
            foreach (var r in netObj.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = active;
            }
            foreach (var c in netObj.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = active;
            }
            foreach (var canvas in netObj.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = active;
            }
            foreach (var light in netObj.GetComponentsInChildren<Light>(true))
            {
                light.enabled = active;
            }
        }

        private void OnTerminalBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsTerminalBought) != 0;
            SetUpgradeActive(radarTerminal, isBought);
        }

        private void OnBeamBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsBeamBought) != 0;
            SetUpgradeActive(radarButton, isBought);
        }

        private void OnDetectionBoughtChanged(int _, int current)
        {
            if (!IsClient || !radarCamera) return;
            int mask = radarOnlyLayer.value;

            bool isBought = (current & (int)BaseUpgrades.IsDetectionBought) != 0;

            if (isBought)
                radarCamera.cullingMask |= mask;
            else
                radarCamera.cullingMask &= ~mask;
        }

        private void OnCasinoBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsCasinoBought) != 0;
            // Звичайний GameObject (не NetworkObject) — SetActive на всіх OK
            if (casinoWall) casinoWall.SetActive(!isBought);
        }

        private void OnShieldBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsShieldBought) != 0;
            SetUpgradeActive(shieldButton, isBought);
        }

        private void OnChargerBoughtChanged(int _, int current)
        {
            bool isBought = (current & (int)BaseUpgrades.IsChargerBought) != 0;
            SetUpgradeActive(charger, isBought);
        }

        #endregion
    }
}
