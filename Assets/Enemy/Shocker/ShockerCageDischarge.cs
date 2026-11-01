using System.Collections;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Enemy.Shocker
{
    public sealed class ShockerCageDischarge : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 6f;
        [SerializeField] private Vector2 interval = new(4f, 9f);
        [SerializeField, Min(0.1f)] private float freezeDuration = 2f;
        [SerializeField, Min(0.1f)] private float wetWeaknessDuration = 6f;
        [SerializeField] private Transform dischargeOrigin;
        [SerializeField] private LineRenderer bolt;
        [SerializeField] private AudioSource dischargeSound;
        [SerializeField, Min(0.01f)] private float boltDuration = 0.2f;

        private Coroutine dischargeRoutine;
        private Coroutine boltRoutine;
        private readonly Vector3[] boltPoints = new Vector3[9];

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            bolt.enabled = false;
            if (IsServer) dischargeRoutine = StartCoroutine(DischargeLoop());
        }

        public override void OnNetworkDespawn()
        {
            if (dischargeRoutine != null) StopCoroutine(dischargeRoutine);
            if (boltRoutine != null) StopCoroutine(boltRoutine);
            dischargeRoutine = null;
            boltRoutine = null;
            bolt.enabled = false;
            dischargeSound.Stop();
            base.OnNetworkDespawn();
        }

        private IEnumerator DischargeLoop()
        {
            while (IsSpawned && IsServer)
            {
                float minimum = Mathf.Max(0.1f, interval.x);
                yield return new WaitForSeconds(Random.Range(minimum, Mathf.Max(minimum, interval.y)));
                if (!IsSpawned || !IsServer) yield break;

                // Scan only on discharge, not every frame. Each entity gets one chance,
                // regardless of how many colliders its model has.
                Entity target = SelectTarget(FindObjectsByType<Entity>(FindObjectsSortMode.None));
                if (target == null) continue;

                target.ApplyEffect(EffectType.Freeze, freezeDuration);
                if (target.IsEffectActive(EffectType.Water))
                    target.ApplyWeakStack(1, wetWeaknessDuration, slot: 3);
                PlayDischargeRpc(target.transform.position + Vector3.up * 0.5f);
            }
        }

        private Entity SelectTarget(Entity[] entities)
        {
            Entity selected = null;
            int candidates = 0;
            Vector3 origin = dischargeOrigin.position;
            foreach (Entity entity in entities)
            {
                if (!entity.IsSpawned || !entity.isActiveAndEnabled || entity.isDead.Value ||
                    entity.IsBeingCaptured || entity.IsEffectActive(EffectType.Invincible) ||
                    (entity.transform.position - origin).sqrMagnitude > radius * radius) continue;
                if (Random.Range(0, ++candidates) == 0) selected = entity;
            }
            return selected;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayDischargeRpc(Vector3 targetPosition)
        {
            if (boltRoutine != null) StopCoroutine(boltRoutine);
            dischargeSound.Play();
            boltRoutine = StartCoroutine(ShowBolt(targetPosition));
        }

        private IEnumerator ShowBolt(Vector3 targetPosition)
        {
            bolt.enabled = true;
            float endTime = Time.time + boltDuration;
            while (Time.time < endTime)
            {
                Vector3 origin = dischargeOrigin.position;
                for (int i = 0; i < boltPoints.Length; i++)
                {
                    float t = i / (float)(boltPoints.Length - 1);
                    boltPoints[i] = Vector3.Lerp(origin, targetPosition, t);
                    if (i > 0 && i < boltPoints.Length - 1)
                        boltPoints[i] += Random.insideUnitSphere * 0.13f;
                }
                bolt.SetPositions(boltPoints);
                yield return null;
            }
            bolt.enabled = false;
            boltRoutine = null;
        }
    }
}
