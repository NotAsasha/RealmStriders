using System.Collections;
using Enemy;
using Enemy.Casino;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Base.Alarm
{
    /// <summary>
    /// Server-authoritative alarm system for the player base.
    /// Triggers when an active enemy enters the base radius or when CasinoMonster wakes up inside it.
    /// If the intruder is not killed within <see cref="alarmDuration"/> seconds, all living entities
    /// in the base are killed and the active mission (if any) is ended.
    /// </summary>
    public class BaseAlarmManager : NetworkBehaviour
    {
        [Header("Alarm Settings")]
        [SerializeField, Min(1f)] private float alarmDuration = 30f;
        [Tooltip("How often (in seconds) the server polls enemy positions against the base radius.")]
        [SerializeField, Min(0.1f)] private float checkInterval = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource alarmAudioSource;
        [SerializeField] private AudioClip alarmLoopClip;
        [SerializeField] private AudioClip alarmEndClip;
        [Tooltip("Pitch at the start of the alarm (full time remaining).")]
        [SerializeField] private float pitchMin = 1f;
        [Tooltip("Pitch when the timer is about to expire (zero time remaining).")]
        [SerializeField] private float pitchMax = 1.6f;

        [Header("Visual")]
        [SerializeField] private Light[] alarmLights;
        [SerializeField] private float lightFlashRate = 2f;

        // Replicated so all clients can drive UI/VFX without server RPC per-frame.
        public readonly NetworkVariable<bool> isAlarmActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        public readonly NetworkVariable<float> alarmTimeRemaining = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public static BaseAlarmManager Instance { get; private set; }

        // Server-only state
        private Coroutine _alarmCoroutine;
        private Coroutine _checkCoroutine;

        // Client-only state
        private Coroutine _flashCoroutine;

        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            isAlarmActive.OnValueChanged += OnAlarmActiveChanged;

            // Apply current state for late-joining clients
            OnAlarmActiveChanged(false, isAlarmActive.Value);

            if (IsServer)
            {
                _checkCoroutine = StartCoroutine(PeriodicBaseCheck());
            }
        }

        public override void OnNetworkDespawn()
        {
            isAlarmActive.OnValueChanged -= OnAlarmActiveChanged;

            if (_alarmCoroutine != null) StopCoroutine(_alarmCoroutine);
            if (_checkCoroutine != null) StopCoroutine(_checkCoroutine);
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void Update()
        {
            // Pitch is updated every frame on the client using the replicated alarmTimeRemaining —
            // no extra RPCs needed; the NetworkVariable already keeps all clients in sync.
            if (!isAlarmActive.Value || alarmAudioSource == null || !alarmAudioSource.isPlaying) return;

            // t goes from 0 (full time) → 1 (time expired): pitch rises as danger increases.
            float t = 1f - Mathf.Clamp01(alarmTimeRemaining.Value / alarmDuration);
            alarmAudioSource.pitch = Mathf.Lerp(pitchMin, pitchMax, t);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Called by CasinoMonster when it wakes up. Server-only entry point.
        /// </summary>
        public void NotifyCasinoMonsterWoke(CasinoMonster monster)
        {
            if (!IsServer) return;
            if (IsEnemyInBase(monster))
            {
                TriggerAlarm();
            }
        }

        /// <summary>
        /// Called when an intruder is confirmed dead. Cancels the alarm if no other intruders remain.
        /// </summary>
        public void NotifyEnemyDied(Enemy.Enemy enemy)
        {
            if (!IsServer || !isAlarmActive.Value) return;
            if (!HasActiveIntruderInBase())
            {
                CancelAlarm();
            }
        }

        #endregion

        #region Server Logic

        // Polls all active enemies against the base radius at a low frequency to avoid performance cost.
        private IEnumerator PeriodicBaseCheck()
        {
            var wait = new WaitForSeconds(checkInterval);
            while (true)
            {
                yield return wait;

                if (!isAlarmActive.Value && HasActiveIntruderInBase())
                {
                    TriggerAlarm();
                }
            }
        }

        private void TriggerAlarm()
        {
            if (isAlarmActive.Value) return;

            Debug.Log("[BaseAlarm] Alarm triggered!");
            isAlarmActive.Value = true;
            alarmTimeRemaining.Value = alarmDuration;
            _alarmCoroutine = StartCoroutine(AlarmCountdown());
        }

        private void CancelAlarm()
        {
            if (!isAlarmActive.Value) return;

            Debug.Log("[BaseAlarm] Alarm cancelled – intruder eliminated.");
            isAlarmActive.Value = false;
            alarmTimeRemaining.Value = 0f;

            if (_alarmCoroutine != null)
            {
                StopCoroutine(_alarmCoroutine);
                _alarmCoroutine = null;
            }

            PlayEndSoundClientRpc();
        }

        private IEnumerator AlarmCountdown()
        {
            while (alarmTimeRemaining.Value > 0f)
            {
                yield return new WaitForSeconds(1f);
                alarmTimeRemaining.Value -= 1f;

                // Re-check: if intruder died mid-countdown the periodic check will call CancelAlarm;
                // here we just count down and let the periodic check do the work.
            }

            // Timer expired — intruder survived
            if (isAlarmActive.Value)
            {
                Debug.Log("[BaseAlarm] Timer expired! Executing alarm consequence.");
                ExecuteAlarmConsequence();
            }

            _alarmCoroutine = null;
        }

        private void ExecuteAlarmConsequence()
        {
            KillAllInBase();

            isAlarmActive.Value = false;
            alarmTimeRemaining.Value = 0f;

            // End the mission if one is active
            if (GameManager.Instance != null && GameManager.Instance.hasStartedMission.Value)
            {
                GameManager.Instance.StopMissionServerRpc();
            }
        }

        // Reused buffer for Physics.OverlapSphereNonAlloc — avoids per-frame heap allocation.
        private readonly Collider[] _overlapBuffer = new Collider[64];

        private void KillAllInBase()
        {
            if (GameManager.Instance == null) return;

            float radius = GameManager.Instance.baseRadius;
            Vector3 center = GameManager.Instance.spawnPoint;

            // Use OverlapSphere to find ALL enemies in the base radius — including static scene
            // enemies (e.g. CasinoMonster) that are never registered in activeEnemies.
            int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<Enemy.Enemy>();
                if (enemy == null || enemy.isDead.Value) continue;
                enemy.isDead.Value = true;
            }

            // Kill all players inside the base
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var human = client.PlayerObject.GetComponent<Human>();
                if (human == null || human.isDead.Value) continue;
                if (Vector3.Distance(human.transform.position, center) <= radius)
                {
                    human.isDead.Value = true;
                }
            }
        }

        private bool HasActiveIntruderInBase()
        {
            if (GameManager.Instance == null) return false;

            float radius = GameManager.Instance.baseRadius;
            Vector3 center = GameManager.Instance.spawnPoint;

            int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<Enemy.Enemy>();
                if (IsActiveIntruder(enemy)) return true;
            }
            return false;
        }

        private bool IsEnemyInBase(CasinoMonster monster)
        {
            if (GameManager.Instance == null) return false;
            float radius = GameManager.Instance.baseRadius;
            Vector3 center = GameManager.Instance.spawnPoint;
            return IsActiveIntruder(monster) &&
                   Vector3.Distance(monster.transform.position, center) <= radius;
        }

        /// <summary>
        /// Returns true if the enemy is alive, not asleep (static), and not being captured.
        /// </summary>
        private static bool IsActiveIntruder(Enemy.Enemy enemy)
        {
            if (enemy == null || enemy.isDead.Value) return false;
            // Ignore sleeping (static) enemies – e.g. an inactive CasinoMonster in the base
            if (enemy.IsEffectActive(EffectType.Asleep)) return false;
            return true;
        }

        #endregion

        #region Client Effects (RPCs / NetworkVariable callbacks)

        private void OnAlarmActiveChanged(bool _, bool isActive)
        {
            if (isActive)
            {
                StartAlarmEffects();
            }
            else
            {
                StopAlarmEffects();
            }
        }

        private void StartAlarmEffects()
        {
            if (alarmAudioSource != null && alarmLoopClip != null)
            {
                alarmAudioSource.clip = alarmLoopClip;
                alarmAudioSource.loop = true;
                alarmAudioSource.Play();
            }

            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            if (alarmLights != null && alarmLights.Length > 0)
            {
                _flashCoroutine = StartCoroutine(FlashLights());
            }
        }

        private void StopAlarmEffects()
        {
            if (alarmAudioSource != null)
            {
                alarmAudioSource.Stop();
                alarmAudioSource.pitch = 1f;
            }

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            SetLights(false);
        }

        [ClientRpc]
        private void PlayEndSoundClientRpc()
        {
            if (alarmAudioSource != null && alarmEndClip != null)
            {
                alarmAudioSource.PlayOneShot(alarmEndClip);
            }
        }

        private IEnumerator FlashLights()
        {
            float halfPeriod = 1f / (lightFlashRate * 2f);
            var wait = new WaitForSeconds(halfPeriod);
            while (true)
            {
                SetLights(true);
                yield return wait;
                SetLights(false);
                yield return wait;
            }
        }

        private void SetLights(bool on)
        {
            foreach (var light in alarmLights)
            {
                if (light != null) light.enabled = on;
            }
        }

        #endregion
    }
}
