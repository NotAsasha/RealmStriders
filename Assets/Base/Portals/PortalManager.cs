using Base.BaseUpgrader;
using Player;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Portals
{
    public class PortalManager : NetworkBehaviour, IPowerConsumer {

        [Header("Cameras & Materials")]
        [SerializeField] private Camera cameraA;
        [SerializeField] private Camera cameraB;

        [SerializeField] private Material cameraMatA;
        [SerializeField] private Material cameraMatB;

        [Header("Hierarchy & Renderers")]
        [FormerlySerializedAs("PortalParent")] [SerializeField] GameObject portalParent;
        [SerializeField] private MeshRenderer[] portalRenderers;

        [Header("CRT Animation Settings")]
        [SerializeField, Range(0.2f, 2.0f)] private float openDuration = 0.8f;
        [SerializeField, Range(0.2f, 2.0f)] private float closeDuration = 0.5f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip portalOpenSound;
        [SerializeField] private AudioClip portalCloseSound;

        public static PortalManager Instance;

        public bool isForward = true;

        public event Action<Human, bool> OnTeleport;

        private PowerGrid powerGrid;
        private bool isPowerGridRegistered;
        private bool isPortalOpen;
        private bool monsterPassBlocked;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int ProgressPropertyId = Shader.PropertyToID("_Progress");
        private Coroutine transitionRoutine;
        private float currentProgress = 0f;

        public bool IsPortalOpen => isPortalOpen;
        public float CurrentProgress => currentProgress;
        public int PowerDemand => isPortalOpen ? 5 : 0;
        public int Priority => 30;
        public bool IsPowered => isPortalOpen;

        /// <summary>True only on the authority that validates enemy portal transitions.</summary>
        public bool IsMonsterPassBlockedServer => IsServer && monsterPassBlocked;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            propertyBlock = new MaterialPropertyBlock();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            CacheRenderers();
        }

        void Start () {
            CacheRenderers();
            ChangeState(false, instant: true);

            if (cameraA != null)
            {
                if (cameraA.targetTexture != null)
                {
                    cameraA.targetTexture.Release();
                }
                cameraA.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
                if (cameraMatA != null) cameraMatA.mainTexture = cameraA.targetTexture;
                cameraA.gameObject.SetActive(true);
            }

            if (cameraB != null)
            {
                if (cameraB.targetTexture != null)
                {
                    cameraB.targetTexture.Release();
                }
                cameraB.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
                if (cameraMatB != null) cameraMatB.mainTexture = cameraB.targetTexture;
                cameraB.gameObject.SetActive(true);
            }

            isForward = true;
        }

        private void OnDisable()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            SetProgress(0f);
        }

        public override void OnNetworkSpawn()
        {
            TryRegisterPowerGrid();
        }

        public override void OnNetworkDespawn()
        {
            if (isPowerGridRegistered)
            {
                powerGrid.UnregisterConsumer(this);
                isPowerGridRegistered = false;
            }
        }

        private void Update()
        {
            TryRegisterPowerGrid();
        }

        private void TryRegisterPowerGrid()
        {
            if (isPowerGridRegistered || BaseManager.Instance == null) return;

            PowerGrid basePowerGrid = BaseManager.Instance.PowerGrid;
            if (basePowerGrid == null || !basePowerGrid.IsSpawned) return;

            powerGrid = basePowerGrid;
            powerGrid.RegisterConsumer(this);
            isPowerGridRegistered = true;
        }
	
        public void ChangeState(bool isStarted, bool instant = false)
        {
            if (instant)
            {
                if (transitionRoutine != null)
                {
                    StopCoroutine(transitionRoutine);
                    transitionRoutine = null;
                }

                isPortalOpen = isStarted;
                SetProgress(isStarted ? 0f : 1f);
                portalParent?.SetActive(isStarted);
                return;
            }

            if (isStarted)
            {
                OpenPortal();
            }
            else
            {
                ClosePortal();
            }
        }

        public void OpenPortal(Action onComplete = null)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            if (audioSource != null && portalOpenSound != null)
            {
                audioSource.PlayOneShot(portalOpenSound);
            }

            transitionRoutine = StartCoroutine(AnimatePortalRoutine(0f, openDuration, () =>
            {
                isPortalOpen = true;
                onComplete?.Invoke();
            }));
        }

        public void ClosePortal(Action onComplete = null)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            isPortalOpen = false;

            if (audioSource != null && portalCloseSound != null)
            {
                audioSource.PlayOneShot(portalCloseSound);
            }

            transitionRoutine = StartCoroutine(AnimatePortalRoutine(1f, closeDuration, onComplete));
        }

        private IEnumerator AnimatePortalRoutine(float targetProgress, float duration, Action onComplete)
        {
            CacheRenderers();

            // When opening, ensure portal hierarchy is active so renderers draw the animation
            if (targetProgress < 0.5f)
            {
                portalParent?.SetActive(true);
            }

            float startProgress = currentProgress;
            float elapsed = 0f;

            if (duration > 0.001f)
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float progress = Mathf.Lerp(startProgress, targetProgress, t);
                    SetProgress(progress);
                    yield return null;
                }
            }

            SetProgress(targetProgress);

            // When closing has finished completely, disable portal hierarchy
            if (targetProgress >= 0.999f)
            {
                portalParent?.SetActive(false);
            }

            transitionRoutine = null;
            onComplete?.Invoke();
        }

        private void SetProgress(float value)
        {
            currentProgress = Mathf.Clamp01(value);

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (portalRenderers == null || portalRenderers.Length == 0)
            {
                CacheRenderers();
            }

            if (portalRenderers != null && portalRenderers.Length > 0)
            {
                for (int i = 0; i < portalRenderers.Length; i++)
                {
                    if (portalRenderers[i] != null)
                    {
                        portalRenderers[i].GetPropertyBlock(propertyBlock);
                        propertyBlock.SetFloat(ProgressPropertyId, currentProgress);
                        portalRenderers[i].SetPropertyBlock(propertyBlock);
                    }
                }
            }
            else
            {
                if (cameraMatA != null) cameraMatA.SetFloat(ProgressPropertyId, currentProgress);
                if (cameraMatB != null) cameraMatB.SetFloat(ProgressPropertyId, currentProgress);
            }
        }

        private void CacheRenderers()
        {
            if ((portalRenderers == null || portalRenderers.Length == 0) && portalParent != null)
            {
                portalRenderers = portalParent.GetComponentsInChildren<MeshRenderer>(true);
            }
        }

        public void SwitchCameras()
        {
            // Logic moved to PortalCamera.cs for better performance (visibility-based)
            isForward = !isForward;
        }

        public void CallOnTeleport(Human player)
        {
            if (!isPortalOpen) return;
            OnTeleport?.Invoke(player, isForward);
        }

        /// <summary>
        /// Changes the server-only rule used by PortalTeleporter for enemies.
        /// Players are intentionally not affected by this flag.
        /// </summary>
        public void SetMonsterPassBlockedServer(bool blocked)
        {
            if (!IsServer) return;

            monsterPassBlocked = blocked;
        }

        public void OnPowerStateChanged(bool powered)
        {
            //isPowered = powered;
            if (!powered)
            {
                //ChangeState(false);
            }
        }
    }
}
