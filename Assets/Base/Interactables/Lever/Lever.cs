using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Base.Interactables
{
    public class Lever : NetworkBehaviour
    {
        [Header("Hold Settings")]
        [SerializeField] private float holdDuration = 1.0f;
        [SerializeField] private float returnDuration = 0.25f;
        [SerializeField] private float cooldown = 5.0f;

        [Header("References")]
        [SerializeField] private Animator anim;
        [SerializeField] private AudioSource source;
        [SerializeField] private Terminal terminal;
        [SerializeField] private LeverHoldButton holdButton;

        private bool isReady = true;
        private bool isHolding;
        private float currentHoldProgress;
        private bool isTargetingStartMission;
        private bool wasLocallyHeld;

        private Coroutine cooldownRoutine;
        private Coroutine fallbackResetRoutine;

        private void Awake()
        {
            if (anim == null) anim = GetComponent<Animator>();
            if (source == null) source = GetComponent<AudioSource>();
            if (terminal == null) terminal = GetComponent<Terminal>();
        }

        private void Start()
        {
            if (GameManager.Instance != null && anim != null)
            {
                SyncInitialState(GameManager.Instance.hasStartedMission.Value);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged += OnMissionStateChanged;
                SyncInitialState(GameManager.Instance.hasStartedMission.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged -= OnMissionStateChanged;
            }

            StopHold();
        }

        private void Update()
        {
            // Cancel hold if the player leaves terminal mode or is no longer interacting
            if (isHolding && terminal != null)
            {
                bool isInteracting = terminal.isTaken.Value
                    && NetworkManager.Singleton != null
                    && terminal.currentInteractingClientId.Value == NetworkManager.Singleton.LocalClientId;

                if (!isInteracting)
                {
                    StopHold();
                }
            }

            if (isHolding)
            {
                float duration = Mathf.Max(0.01f, holdDuration);
                currentHoldProgress += Time.deltaTime / duration;

                if (currentHoldProgress >= 1.0f)
                {
                    currentHoldProgress = 1.0f;
                    CompleteHold();
                }
                else
                {
                    UpdateAnimation(currentHoldProgress);
                }
            }
            else if (currentHoldProgress > 0f)
            {
                float duration = Mathf.Max(0.01f, returnDuration);
                currentHoldProgress -= Time.deltaTime / duration;

                if (currentHoldProgress <= 0f)
                {
                    currentHoldProgress = 0f;
                    ResetToCurrentIdle();
                }
                else
                {
                    UpdateAnimation(currentHoldProgress);
                }
            }
        }

        public void StartHold()
        {
            if (!isReady || GameManager.Instance == null) return;

            bool isMissionActive = GameManager.Instance.hasStartedMission.Value;
            if (!isMissionActive && string.IsNullOrEmpty(GameManager.Instance.missionName))
            {
                return;
            }

            isTargetingStartMission = !isMissionActive;
            isHolding = true;

            if (source != null && source.clip != null)
            {
                float duration = Mathf.Max(0.01f, holdDuration);
                source.pitch = source.clip.length / duration;

                float targetStartTime = Mathf.Clamp(currentHoldProgress * (source.clip.length - 0.05f), 0f, source.clip.length);
                source.time = targetStartTime;

                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
        }

        public void StopHold()
        {
            if (!isHolding) return;
            isHolding = false;

            if (source != null && source.isPlaying)
            {
                source.Stop();
                source.pitch = 1f;
            }
        }

        private void CompleteHold()
        {
            isHolding = false;
            wasLocallyHeld = true;

            UpdateAnimation(1.0f);

            SwitchMissionState();

            if (fallbackResetRoutine != null)
            {
                StopCoroutine(fallbackResetRoutine);
            }
            fallbackResetRoutine = StartCoroutine(FallbackResetCheck());
        }

        public void SwitchMissionState()
        {
            if (!isReady || GameManager.Instance == null) return;

            if (!GameManager.Instance.hasStartedMission.Value)
            {
                GameManager.Instance.StartMissionServerRpc();
            }
            else
            {
                GameManager.Instance.StopMissionServerRpc();
            }

            if (cooldownRoutine != null)
            {
                StopCoroutine(cooldownRoutine);
            }
            cooldownRoutine = StartCoroutine(Cooldown());
        }

        private IEnumerator Cooldown()
        {
            isReady = false;
            yield return new WaitForSeconds(cooldown);
            isReady = true;
            cooldownRoutine = null;
        }

        private IEnumerator FallbackResetCheck()
        {
            yield return new WaitForSeconds(1.5f);
            wasLocallyHeld = false;
            currentHoldProgress = 0f;
            ResetToCurrentIdle();
            fallbackResetRoutine = null;
        }

        private void UpdateAnimation(float progress)
        {
            if (anim == null) return;

            string clipName = isTargetingStartMission ? "LeverDown" : "LevelUp";
            anim.speed = 0f;
            anim.Play(clipName, 0, Mathf.Clamp01(progress));
            anim.Update(0);
        }

        private void ResetToCurrentIdle()
        {
            if (anim == null) return;

            anim.speed = 1f;
            bool isMissionActive = GameManager.Instance != null && GameManager.Instance.hasStartedMission.Value;
            anim.SetBool("IsOn", isMissionActive);
            anim.Play(isMissionActive ? "LeverDown" : "LevelUp", 0, 1.0f);
            anim.Update(0);
        }

        private void SyncInitialState(bool isMissionActive)
        {
            if (anim == null) return;

            anim.speed = 1f;
            anim.SetBool("IsOn", isMissionActive);
            anim.Play(isMissionActive ? "LeverDown" : "LevelUp", 0, 1.0f);
            anim.Update(0);
        }

        private void OnMissionStateChanged(bool oldValue, bool newValue)
        {
            if (fallbackResetRoutine != null)
            {
                StopCoroutine(fallbackResetRoutine);
                fallbackResetRoutine = null;
            }

            currentHoldProgress = 0f;
            isHolding = false;

            if (anim != null)
            {
                anim.speed = 1f;
                anim.SetBool("IsOn", newValue);

                if (wasLocallyHeld)
                {
                    anim.Play(newValue ? "LeverDown" : "LevelUp", 0, 1.0f);
                    anim.Update(0);
                }
            }

            if (!wasLocallyHeld && source != null)
            {
                source.pitch = 1f;
                source.time = 0f;
                source.Play();
            }

            wasLocallyHeld = false;
        }
    }
}
