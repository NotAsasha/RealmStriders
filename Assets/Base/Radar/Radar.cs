using System.Collections;
using Base.BaseUpgrader;
using Player.Movement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Base.Radar
{
    public class Radar : Terminal, IPowerConsumer
    {
        [Header("Settings")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private Vector2 border = new Vector2(5f, 5f);

        [SerializeField] private float beamLifetime = 7f;
        [SerializeField] private float shotCost = 80;

        [Header("References")]
        public Camera radarCamera;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField, Range(1, 60)] private int radarFrameRate = 30;
        [SerializeField] private LayerMask visibilityOccluders = Physics.DefaultRaycastLayers;
        [SerializeField] private Transform crosshair;
        [SerializeField] private Transform screen;
        [SerializeField] private NetworkObject beamPrefab;
        [SerializeField] private float mapSizeMultiplier = 25.6f;

        [Header("Strike Audio Feedback")]
        [SerializeField] private AudioSource beamAlarmSource;
        [SerializeField] private AudioClip strikeAlarmClip;

        public readonly NetworkVariable<Vector2> syncedCrosshairPos = new(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private InputAction control;
        private NetworkObject beam;
        private PowerGrid powerGrid;
        private bool isPowered = true;
        private float beamEndTime;
        private Camera viewerCamera;
        private readonly Plane[] viewPlanes = new Plane[6];
        private double nextRadarFrame;
        private double nextOcclusionCheck;
        private bool screenUnoccluded;

        private bool IsTerminalBought => BaseManager.Instance != null &&
            (BaseManager.Instance.baseUpgrades.Value & (int)BaseUpgrades.IsTerminalBought) != 0;

        public int PowerDemand => !IsTerminalBought ? 0 : (isPowered ? 4 : 2);
        public int Priority => 100;
        public bool IsPowered => isPowered;

        private void Awake()
        {
            if (radarCamera != null) radarCamera.enabled = false;
            if (crosshair != null)
            {
                var netTransform = crosshair.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (netTransform != null)
                {
                    Destroy(netTransform);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            powerGrid = BaseManager.Instance != null ? BaseManager.Instance.PowerGrid : null;
            powerGrid?.RegisterConsumer(this);

            if (radarCamera != null)
            {
                radarCamera.transform.position = new Vector3(0f, 100f, 0f);
                radarCamera.transform.eulerAngles = new Vector3(90f, 0f, 0f);
            }

            syncedCrosshairPos.OnValueChanged += OnCrosshairPosChanged;
            if (crosshair != null)
            {
                crosshair.localPosition = new Vector3(syncedCrosshairPos.Value.x, 0f, syncedCrosshairPos.Value.y);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (radarCamera != null) radarCamera.enabled = false;
            viewerCamera = null;
            syncedCrosshairPos.OnValueChanged -= OnCrosshairPosChanged;
            powerGrid?.UnregisterConsumer(this);
            base.OnNetworkDespawn();
        }

        private void OnDisable()
        {
            if (radarCamera != null) radarCamera.enabled = false;
            nextRadarFrame = 0;
            nextOcclusionCheck = 0;
        }

        private void LateUpdate()
        {
            if (radarCamera == null) return;
            bool render = false;
            double now = Time.unscaledTimeAsDouble;
            if (IsSpawned && IsClient && IsScreenVisible(now))
            {
                if (now >= nextRadarFrame)
                {
                    render = true;
                    nextRadarFrame = now + 1.0 / Mathf.Clamp(radarFrameRate, 1, 60);
                }
            }
            else
            {
                nextRadarFrame = 0;
                nextOcclusionCheck = 0;
            }
            if (radarCamera.enabled != render) radarCamera.enabled = render;
        }

        private bool IsScreenVisible(double now)
        {
            if (screenRenderer == null || !screenRenderer.enabled ||
                !screenRenderer.gameObject.activeInHierarchy) return false;
            if (viewerCamera == null && PlayerMovement.Instance != null)
                viewerCamera = PlayerMovement.Instance.playerCamera;
            if (viewerCamera == null || !viewerCamera.isActiveAndEnabled ||
                (viewerCamera.cullingMask & (1 << screenRenderer.gameObject.layer)) == 0) return false;

            Bounds bounds = screenRenderer.bounds;
            Vector3 eye = viewerCamera.transform.position;
            // The authored screen is a Plane: its front normal is local +Y.
            if (Vector3.Dot(screenRenderer.transform.up, eye - bounds.center) <= 0f) return false;
            GeometryUtility.CalculateFrustumPlanes(viewerCamera, viewPlanes);
            if (!GeometryUtility.TestPlanesAABB(viewPlanes, bounds)) return false;

            if (now >= nextOcclusionCheck)
            {
                nextOcclusionCheck = now + 0.1;
                Bounds localBounds = screenRenderer.localBounds;
                Vector3 offset = localBounds.extents * 0.8f;
                Transform screenTransform = screenRenderer.transform;
                screenUnoccluded = HasScreenSightline(eye, bounds.center) ||
                    HasScreenSightline(eye, screenTransform.TransformPoint(localBounds.center + offset)) ||
                    HasScreenSightline(eye, screenTransform.TransformPoint(localBounds.center - offset)) ||
                    HasScreenSightline(eye, screenTransform.TransformPoint(localBounds.center + new Vector3(offset.x, -offset.y, offset.z))) ||
                    HasScreenSightline(eye, screenTransform.TransformPoint(localBounds.center + new Vector3(-offset.x, offset.y, -offset.z)));
            }
            return screenUnoccluded;
        }

        private bool HasScreenSightline(Vector3 eye, Vector3 point)
        {
            Vector3 direction = point - eye;
            float distance = direction.magnitude;
            if (distance <= 0.01f) return true;
            if (!Physics.Raycast(eye, direction / distance, out RaycastHit hit,
                distance, visibilityOccluders, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.IsChildOf(transform);
        }

        private void OnCrosshairPosChanged(Vector2 oldPos, Vector2 newPos)
        {
            if (IsLocalPlayerControlling()) return;

            if (crosshair != null)
            {
                crosshair.localPosition = new Vector3(newPos.x, 0f, newPos.y);
            }
        }

        private bool IsLocalPlayerControlling()
        {
            return IsTaken() && NetworkManager.Singleton != null &&
                   currentInteractingClientId.Value == NetworkManager.Singleton.LocalClientId;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
        public void SpawnBeamServerRpc()
        {
            // Called by the physical radar button after its server-side interaction check.
            if (!IsServer || !IsSpawned || !IsTerminalBought ||
                (BaseManager.Instance.baseUpgrades.Value & (int)BaseUpgrades.IsBeamBought) == 0 ||
                !isPowered || powerGrid == null || powerGrid.IsGridOverloaded.Value || beamPrefab == null) return;

            if (beam != null && beam.IsSpawned) beam.Despawn();

            powerGrid.ConsumeInstantChargeServer(shotCost);

            Vector2 targetPos = syncedCrosshairPos.Value;
            Vector3 spawnPos = new(targetPos.x * mapSizeMultiplier, 100f, targetPos.y * mapSizeMultiplier);

            beam = Instantiate(beamPrefab, spawnPos, Quaternion.identity);
            beam.Spawn();
            beamEndTime = Time.time + beamLifetime;

            OnBeamFiredClientRpc();
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void OnBeamFiredClientRpc()
        {
            if (beamAlarmSource != null && strikeAlarmClip != null)
            {
                beamAlarmSource.PlayOneShot(strikeAlarmClip);
            }
            else if (strikeAlarmClip != null)
            {
                AudioSource.PlayClipAtPoint(strikeAlarmClip, transform.position);
            }

            if (crosshair != null)
            {
                StartCoroutine(FlashCrosshairRoutine());
            }
        }

        private IEnumerator FlashCrosshairRoutine()
        {
            if (crosshair == null) yield break;
            var mr = crosshair.GetComponent<MeshRenderer>();
            if (mr == null) yield break;

            var mpb = new MaterialPropertyBlock();
            int colorId = Shader.PropertyToID("_BaseColor");
            int emissionId = Shader.PropertyToID("_EmissionColor");

            float elapsed = 0f;
            float duration = 3.5f;
            while (elapsed < duration && crosshair != null)
            {
                elapsed += Time.deltaTime;
                float blink = Mathf.PingPong(elapsed * 7f, 1f);
                Color flashColor = Color.Lerp(Color.yellow, new Color(1f, 0.18f, 0.02f), blink);

                mr.GetPropertyBlock(mpb);
                mpb.SetColor(colorId, flashColor);
                mpb.SetColor(emissionId, flashColor * 2.5f);
                mr.SetPropertyBlock(mpb);
                yield return null;
            }

            if (crosshair != null && mr != null)
            {
                mr.SetPropertyBlock(null);
            }
        }

        private void Update()
        {
            if (IsServer && beam != null && Time.time >= beamEndTime)
            {
                if (beam.IsSpawned) beam.Despawn();
                beam = null;
            }

            if (!IsLocalPlayerControlling()) return;
            if (!isPowered) return;

            if (control == null && PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                control = PlayerMovement.Instance.controls.UI.Navigate;
            }
            if (control == null) return;

            Vector2 input = control.ReadValue<Vector2>();
            if (input.sqrMagnitude > 0.001f && crosshair != null)
            {
                Vector3 move = moveSpeed * Time.deltaTime * new Vector3(input.x, 0f, input.y);
                Vector3 newLocalPos = crosshair.localPosition + move;

                newLocalPos.x = Mathf.Clamp(newLocalPos.x, -border.x, border.x);
                newLocalPos.z = Mathf.Clamp(newLocalPos.z, -border.y, border.y);
                newLocalPos.y = 0f;

                crosshair.localPosition = newLocalPos;

                UpdateCrosshairServerRpc(new Vector2(newLocalPos.x, newLocalPos.z));
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpdateCrosshairServerRpc(Vector2 pos, RpcParams rpc = default)
        {
            if (!IsOperatedByServer(rpc.Receive.SenderClientId) || !IsTerminalBought || !isPowered ||
                !float.IsFinite(pos.x) || !float.IsFinite(pos.y) ||
                Mathf.Abs(pos.x) > border.x || Mathf.Abs(pos.y) > border.y) return;

            syncedCrosshairPos.Value = pos;
            if (crosshair != null)
            {
                crosshair.localPosition = new Vector3(pos.x, 0f, pos.y);
            }
        }

        public void OnPowerStateChanged(bool powered)
        {
            isPowered = powered;
        }
    }
}
