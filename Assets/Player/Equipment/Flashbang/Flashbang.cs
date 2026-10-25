using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Enemy;

namespace Player.Equipment.Flashbang
{
    public class Flashbang : Item, IChargeable
    {
        [SerializeField] private float effectRadius = 30f;
        [SerializeField] private LayerMask entityLayer;
        [SerializeField] private ParticleSystem emit;
        [SerializeField] private AudioSource audioS;
        [SerializeField] private SoundProducer soundProducer;
        [SerializeField] private Renderer indicator;
        [Tooltip("Sphere mesh with radius 0.5 using the Subwoofer zone material.")]
        [SerializeField] private MeshRenderer fieldPrefab;
        [SerializeField, ColorUsage(true, true)] private Color fieldColor = new(0.4f, 0.8f, 1f, 0.5f);
        [SerializeField, Min(0.01f)] private float expandDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;

        private const int UseCost = 50;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        public readonly NetworkVariable<int> chargePercent = new(100,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly HashSet<Enemy.Enemy> affectedEnemies = new();
        private MaterialPropertyBlock indicatorProperties;
        private MaterialPropertyBlock fieldProperties;
        private MeshRenderer activeField;
        private Coroutine fieldAnimation;
        private float nextUseTime;

        public int CurrentCharge => chargePercent.Value;
        public int MaxCharge => 100;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            chargePercent.OnValueChanged += UpdateIndicator;
            UpdateIndicator(0, CurrentCharge);
        }

        public override void OnNetworkDespawn()
        {
            chargePercent.OnValueChanged -= UpdateIndicator;
            ClearField();
            base.OnNetworkDespawn();
        }

        void IChargeable.ModifyCharge(int amount)
        {
            if (!IsServer) return;
            chargePercent.Value = Mathf.Clamp(CurrentCharge + amount, 0, MaxCharge);
        }

        private void UpdateIndicator(int previous, int current)
        {
            if (indicator == null) return;
            indicatorProperties ??= new MaterialPropertyBlock();
            Color color = current >= MaxCharge ? Color.green : current >= UseCost ? Color.yellow : Color.red;
            indicator.GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor(BaseColorId, color);
            indicatorProperties.SetColor(ColorId, color);
            indicatorProperties.SetColor(EmissionColorId, color);
            indicator.SetPropertyBlock(indicatorProperties);
        }

        protected override void ExecuteItemAction(GameObject player)
        {
            if (!IsClient || !IsSpawned || CurrentCharge < UseCost) return;
            ExplodeServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ExplodeServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || !IsHeldByClient(rpcParams.Receive.SenderClientId) || !isActiveAndEnabled ||
                CurrentCharge < UseCost || Time.time < nextUseTime) return;

            nextUseTime = Time.time + expandDuration + fadeDuration;
            ((IChargeable)this).ModifyCharge(-UseCost);
            UpdateRemoteHeldPose();
            Vector3 origin = transform.position;
            soundProducer?.EmitSoundServerRpc();

            affectedEnemies.Clear();
            Collider[] hits = Physics.OverlapSphere(origin, effectRadius, entityLayer, QueryTriggerInteraction.Collide);
            foreach (Collider collider in hits)
            {
                var enemy = collider.GetComponentInParent<Enemy.Enemy>();
                if (enemy != null && !enemy.isDead.Value && affectedEnemies.Add(enemy))
                {
                    enemy.Run(origin);
                }
            }

            affectedEnemies.Clear();
            ExplodeClientRpc(origin);
        }

        [ClientRpc]
        private void ExplodeClientRpc(Vector3 origin)
        {
            if (emit != null) emit.Play();
            if (audioS != null) audioS.Play();
            ClearField();
            if (fieldPrefab != null && isActiveAndEnabled)
                fieldAnimation = StartCoroutine(PlayField(origin));
        }

        private IEnumerator PlayField(Vector3 origin)
        {
            // Keep the pulse in world space so item movement cannot change its radius or center.
            activeField = Instantiate(fieldPrefab, origin, Quaternion.identity);
            activeField.gameObject.SetActive(true);
            activeField.enabled = true;
            fieldProperties ??= new MaterialPropertyBlock();
            float expansion = Mathf.Max(0.01f, expandDuration);
            float fade = Mathf.Max(0.01f, fadeDuration);
            float elapsed = 0f;
            while (elapsed < expansion + fade)
            {
                float progress = Mathf.Clamp01(elapsed / expansion);
                activeField.transform.localScale = Vector3.one * (2f * effectRadius * Mathf.SmoothStep(0f, 1f, progress));
                Color fadedColor = fieldColor;
                fadedColor.a *= 1f - Mathf.Clamp01((elapsed - expansion) / fade);
                fieldProperties.SetColor(BaseColorId, fadedColor);
                activeField.SetPropertyBlock(fieldProperties);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Destroy(activeField.gameObject);
            activeField = null;
            fieldAnimation = null;
        }

        private void OnDisable() => ClearField();

        private void ClearField()
        {
            if (fieldAnimation != null) StopCoroutine(fieldAnimation);
            fieldAnimation = null;
            if (activeField != null) Destroy(activeField.gameObject);
            activeField = null;
        }
    }
}
