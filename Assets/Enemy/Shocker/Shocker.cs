using System.Collections;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Enemy.Shocker
{
    /// <summary>
    /// Shocker - an enemy that slowly pursues entities, then stops to charge and release
    /// a powerful AOE electric burst that damages and briefly freezes all nearby entities.
    /// </summary>
    public class Shocker : Enemy
    {
        [Header("Shocker - Pursuit")]
        [SerializeField] private float dischargeRange = 6f;
        [SerializeField] private float chargeUpDuration = 2f;
        [SerializeField] private float dischargeCooldown = 4f;

        [Header("Shocker - Damage")]
        [SerializeField] private float aoeShockDamage = 0.4f;
        [SerializeField] private float freezeDuration = 3f;

        [Header("Shocker - VFX")]
        [SerializeField] private ParticleSystem chargeVFX;
        [SerializeField] private ParticleSystem dischargeVFX;
        [SerializeField] private Light electricLight;
        [SerializeField] private float lightIntensityMax = 8f;
        [SerializeField] private ShockerEnergyField energyField;

        [Header("Shocker - Detection")]
        // Assign the same layer mask used in EntityDetector (e.g. "Entity" or "Player")
        [SerializeField] private LayerMask entityLayer;

        // Reusable buffer — avoids per-frame allocs
        private readonly Collider[] _hitBuffer = new Collider[32];

        private bool _isCharging;
        private bool _isCoolingDown;

        // Audio hooks — subscribe in ShockerSounds
        public event System.Action OnChargeStarted;
        public event System.Action OnDischarged;

        // Server-side network var so clients can mirror the VFX
        private readonly NetworkVariable<bool> _netIsCharging = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        protected override void Awake()
        {
            base.Awake();
            // defaultSpeed is intentionally low for Shocker — configure it in the Inspector
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _netIsCharging.OnValueChanged += OnChargingStateChanged;

            // Mirror the current value immediately on late-joining clients
            OnChargingStateChanged(false, _netIsCharging.Value);
        }

        public override void OnNetworkDespawn()
        {
            _netIsCharging.OnValueChanged -= OnChargingStateChanged;
            base.OnNetworkDespawn();
        }

        // --------------- Think override ---------------

        protected override void Think()
        {
            if (_isCharging || _isCoolingDown) return;

            // If we have a frozen target to hunt, chase it aggressively
            if (_huntTarget != null)
            {
                HuntFrozenTarget();
                return;
            }

            // Find the closest visible entity to decide if we should discharge
            var target = vision != null ? vision.EntityInSight() : null;

            if (target != null)
            {
                float distSq = (target.transform.position - transform.position).sqrMagnitude;
                if (distSq <= dischargeRange * dischargeRange)
                {
                    // Stop and begin charge-up
                    if (agent != null && agent.isOnNavMesh) agent.ResetPath();
                    StartCoroutine(ChargeAndDischarge());
                    return;
                }
            }

            // Otherwise, use the standard slow pursuit
            base.Think();
        }

        // The entity we are actively hunting after a discharge
        private Entity _huntTarget;

        private void HuntFrozenTarget()
        {
            // Stop hunting if target died, was captured, or thawed out
            if (_huntTarget.isDead.Value
                || _huntTarget.IsBeingCaptured
                || !_huntTarget.IsEffectActive(EffectType.Freeze))
            {
                EndHunt();
                return;
            }

            // Chase the frozen target at full speed
            Vector3 targetPos = _huntTarget.transform.position;
            if (agent != null && agent.isOnNavMesh)
            {
                if ((agent.destination - targetPos).sqrMagnitude > 0.25f)
                    agent.SetDestination(targetPos);

                if (!IsEffectActive(EffectType.Freeze))
                    agent.speed = chaseSpeed;
            }

            enemyState = EnemyState.IsChasingPlayer;
        }

        private void EndHunt()
        {
            _huntTarget = null;
            StartCoroutine(HuntCooldown());
        }

        private IEnumerator HuntCooldown()
        {
            _isCoolingDown = true;
            if (agent != null && !IsEffectActive(EffectType.Freeze))
                agent.speed = defaultSpeed;
            yield return new WaitForSeconds(dischargeCooldown);
            _isCoolingDown = false;
        }

        // --------------- Charge → Discharge sequence (Server only) ---------------

        private IEnumerator ChargeAndDischarge()
        {
            _isCharging = true;
            _netIsCharging.Value = true;
            enemyState = EnemyState.IsChasingPlayer;
            // Notify all clients: start charge VFX + charge sound
            ChargeStartedClientRpc();

            yield return new WaitForSeconds(chargeUpDuration);

            if (!isDead.Value)
            {
                // Apply damage and freeze to every entity in range; returns closest frozen target
                _huntTarget = ExecuteDischarge();
            }

            _isCharging = false;
            _netIsCharging.Value = false;

            // If no target was hit, apply cooldown immediately; otherwise hunt begins via Think()
            if (_huntTarget == null)
                StartCoroutine(HuntCooldown());
        }

        // Returns the closest entity that was hit and frozen — used as the hunt target.
        private Entity ExecuteDischarge()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                dischargeRange,
                _hitBuffer,
                entityLayer,
                QueryTriggerInteraction.Collide);

            Debug.Log($"---Shocker: ExecuteDischarge, overlap found {count} colliders");

            Entity closestTarget = null;
            float  closestDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuffer[i];
                if (col == null) continue;

                var entity = col.GetComponentInParent<Entity>();
                if (entity == null || entity == this || entity.isDead.Value) continue;

                ShockEntity(entity);

                // Track the closest hit entity to hunt after discharge
                float dSq = (entity.transform.position - transform.position).sqrMagnitude;
                if (dSq < closestDistSq)
                {
                    closestDistSq = dSq;
                    closestTarget = entity;
                }
            }

            // Trigger VFX + audio on all clients
            DischargeClientRpc();
            return closestTarget;
        }

        private void ShockEntity(Entity entity)
        {
            float dist = Vector3.Distance(transform.position, entity.transform.position);
            // Damage scales from full at center to half at edge
            float damageFalloff = Mathf.Lerp(1f, 0.5f, dist / dischargeRange);
            float dmg = aoeShockDamage * damageFalloff;

            Debug.Log($"---Shocker: Shocking {entity.name} for {dmg:F2} dmg, freeze {freezeDuration}s");
            entity.AddHealth(-dmg);
            entity.ApplyEffect(EffectType.Freeze, freezeDuration);
        }

        // --------------- Client RPCs for VFX + Audio ---------------

        // Called when charge begins — notifies all clients to play VFX and charge sound
        [ClientRpc]
        private void ChargeStartedClientRpc()
        {
            // ShockerSounds listens to the event; fire it locally on every client
            OnChargeStarted?.Invoke();
        }

        // Called when discharge fires — notifies all clients to play VFX and discharge sound
        [ClientRpc]
        private void DischargeClientRpc()
        {
            PlayDischargeVFX();
            OnDischarged?.Invoke();
        }

        private void OnChargingStateChanged(bool oldVal, bool newVal)
        {
            if (newVal) PlayChargeVFX();
            else StopChargeVFX();
        }

        private void PlayChargeVFX()
        {
            if (chargeVFX != null && !chargeVFX.isPlaying) chargeVFX.Play();
            if (electricLight != null)
            {
                electricLight.enabled = true;
                StartCoroutine(PulseLight());
            }
        }

        private void StopChargeVFX()
        {
            if (chargeVFX != null) chargeVFX.Stop();
            StopCoroutine(PulseLight());
            if (electricLight != null) electricLight.enabled = false;
        }

        private void PlayDischargeVFX()
        {
            if (dischargeVFX != null) dischargeVFX.Play();
            energyField?.PlayDischarge();
            if (electricLight != null)
            {
                electricLight.intensity = lightIntensityMax;
                StartCoroutine(FadeLight());
            }
        }

        private IEnumerator PulseLight()
        {
            while (_netIsCharging.Value)
            {
                if (electricLight != null)
                {
                    float t = Mathf.PingPong(Time.time * 3f, 1f);
                    electricLight.intensity = Mathf.Lerp(0.5f, lightIntensityMax * 0.6f, t);
                }
                yield return null;
            }
        }

        private IEnumerator FadeLight()
        {
            float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (electricLight != null)
                    electricLight.intensity = Mathf.Lerp(lightIntensityMax, 0f, elapsed / duration);
                yield return null;
            }
            if (electricLight != null) electricLight.enabled = false;
        }

        // --------------- Gizmos ---------------

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, dischargeRange);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, dischargeRange);
        }
    }
}
