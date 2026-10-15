using Enemy;
using Player.Equipment;
using Unity.Netcode;
using UnityEngine;

namespace Enemy.LureMan
{
    /// <summary>
    /// Предмет-клітка (LureCage) на основі EnemyCage, яка випадає з LureMonster та функціонує як музична скринька.
    /// Коли ввімкнена (isOn == true): плавно розганяється, обертається та відтворює музику LureMonster.
    /// Коли вимкнена (isOn == false): плавно сповільнюється до повної зупинки та стає статичною.
    /// Автоматично вимикається після завершення мелодії або після 57 секунд.
    /// </summary>
    public class LureCage : EnemyCage
    {
        [Header("Music Box State")]
        [Tooltip("Початковий стан скриньки при спавні")]
        [SerializeField] private bool defaultOn = false;

        public NetworkVariable<bool> isOn = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Header("Auto Shutoff")]
        [Tooltip("Чи автоматично вимикати скриньку після завершення мелодії.")]
        [SerializeField] private bool autoShutoff = true;

        [Tooltip("Резервна тривалість у секундах, якщо AudioClip не призначений або його тривалість не визначена.")]
        [SerializeField] private float fallbackDuration = 57f;

        [Header("Procedural Spin Settings")]
        [Tooltip("Трансформ для процедурного обертання (наприклад, балерина, циліндр або внутрішній механізм). Якщо не призначено, обертається весь об'єкт.")]
        [SerializeField] private Transform spinTransform;

        [Tooltip("Локальна вісь обертання.")]
        [SerializeField] private Vector3 spinAxis = Vector3.up;

        [Tooltip("Максимальна швидкість обертання (градусів за секунду).")]
        [SerializeField] private float maxSpinSpeed = 90f;

        [Tooltip("Прискорення при розгоні (градусів/сек²). Забезпечує плавний старт.")]
        [SerializeField] private float acceleration = 60f;

        [Tooltip("Уповільнення при зупинці (градусів/сек²). Забезпечує плавне гальмування.")]
        [SerializeField] private float deceleration = 40f;

        [Header("Music Box Audio")]
        [Tooltip("AudioSource для програвання музики скриньки.")]
        [SerializeField] private AudioSource musicAudioSource;

        [Tooltip("Аудіокліп музичної скриньки (якщо не налаштований безпосередньо в AudioSource).")]
        [SerializeField] private AudioClip musicClip;

        [Tooltip("Максимальна гучність відтворення музики.")]
        [SerializeField] [Range(0f, 1f)] private float maxVolume = 1f;

        [Tooltip("Чи плавно змінювати гучність пропорційно швидкості обертання.")]
        [SerializeField] private bool fadeVolumeWithSpin = true;

        [Tooltip("Чи модулювати pitch (висоту тону) залежно від швидкості обертання (ефект механічного сповільнення заводної скриньки).")]
        [SerializeField] private bool modulatePitchWithSpin = true;

        [Tooltip("Мінімальна висота тону (pitch) при сповільненні.")]
        [SerializeField] [Range(0.1f, 1f)] private float minPitch = 0.4f;

        [Tooltip("Нормальна висота тону (pitch) на повній швидкості.")]
        [SerializeField] [Range(0.5f, 2f)] private float normalPitch = 1.0f;

        [Tooltip("Призупиняти відтворення (Pause) при ручному вимкненні замість повної зупинки (Stop), щоб мелодія продовжувалась з того ж місця після повторного ввімкнення.")]
        [SerializeField] private bool pauseOnStop = true;

        [Header("Monster Lure (Optional)")]
        [Tooltip("Якщо призначено SoundProducer, чи приманювати монстрів звуком під час роботи скриньки на землі.")]
        [SerializeField] private bool lureEnemiesWhenActive = true;

        [SerializeField] private SoundProducer soundProducer;

        private float currentSpinSpeed = 0f;
        private float playbackTimer = 0f;
        private bool hasFinishedMelody = false;

        #region Unity Lifecycle

        private void Start()
        {
            if (musicAudioSource == null)
            {
                musicAudioSource = GetComponent<AudioSource>();
            }

            if (soundProducer == null)
            {
                soundProducer = GetComponent<SoundProducer>();
            }

            if (musicAudioSource != null)
            {
                if (musicClip != null)
                {
                    musicAudioSource.clip = musicClip;
                }
                musicAudioSource.loop = !autoShutoff;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                isOn.Value = defaultOn;
            }

            isOn.OnValueChanged += OnIsOnChanged;

            // Синхронізація стану для нових/late-join гравців
            ApplyInitialState(isOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            isOn.OnValueChanged -= OnIsOnChanged;

            if (IsServer && soundProducer != null)
            {
                soundProducer.StopContinuousNoise();
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            UpdateSpinAndAudio();

            // Авто-вимкнення після завершення мелодії (або 57 сек)
            if (autoShutoff && isOn.Value)
            {
                playbackTimer += Time.deltaTime;
                float duration = GetMelodyDuration();

                bool audioEndedNaturally = musicAudioSource != null && musicAudioSource.clip != null &&
                                           !musicAudioSource.isPlaying && playbackTimer > 1f;

                if (playbackTimer >= duration || audioEndedNaturally)
                {
                    if (IsServer)
                    {
                        playbackTimer = 0f;
                        hasFinishedMelody = true;
                        isOn.Value = false;
                    }
                    else if (!hasFinishedMelody)
                    {
                        hasFinishedMelody = true;
                        NotifyMelodyEndedServerRpc();
                    }
                }
            }

            // Серверна логіка приманювання монстрів (якщо підключено SoundProducer)
            if (IsServer && lureEnemiesWhenActive && soundProducer != null)
            {
                if (isOn.Value && currentSpinSpeed > 0.001f && !isCurrentlyHeld)
                {
                    soundProducer.StartContinuousNoise();
                }
                else
                {
                    soundProducer.StopContinuousNoise();
                }
            }
        }

        #endregion

        #region Rotation and Audio

        public float GetMelodyDuration()
        {
            if (musicClip != null && musicClip.length > 0f)
            {
                return musicClip.length;
            }

            if (musicAudioSource != null && musicAudioSource.clip != null && musicAudioSource.clip.length > 0f)
            {
                return musicAudioSource.clip.length;
            }

            return fallbackDuration;
        }

        private void UpdateSpinAndAudio()
        {
            float targetSpeed = isOn.Value ? maxSpinSpeed : 0f;
            float rate = targetSpeed > currentSpinSpeed ? acceleration : deceleration;

            currentSpinSpeed = Mathf.MoveTowards(
                currentSpinSpeed,
                targetSpeed,
                Mathf.Max(rate, 0.01f) * Time.deltaTime
            );

            // Процедурне обертання
            if (Mathf.Abs(currentSpinSpeed) > 0.001f)
            {
                Transform targetTransform = spinTransform != null ? spinTransform : transform;
                targetTransform.Rotate(spinAxis, currentSpinSpeed * Time.deltaTime, Space.Self);
            }

            // Оновлення параметрів звуку
            UpdateAudioPlayback();
        }

        private void UpdateAudioPlayback()
        {
            if (musicAudioSource == null) return;

            float speedRatio = maxSpinSpeed > 0.001f
                ? Mathf.Clamp01(Mathf.Abs(currentSpinSpeed) / maxSpinSpeed)
                : (isOn.Value ? 1f : 0f);

            bool isSpinning = Mathf.Abs(currentSpinSpeed) > 0.001f;

            if (isOn.Value && isSpinning)
            {
                if (!musicAudioSource.isPlaying && !hasFinishedMelody)
                {
                    musicAudioSource.Play();
                }

                if (modulatePitchWithSpin)
                {
                    musicAudioSource.pitch = Mathf.Lerp(minPitch, normalPitch, speedRatio);
                }
                else
                {
                    musicAudioSource.pitch = normalPitch;
                }

                if (fadeVolumeWithSpin)
                {
                    musicAudioSource.volume = Mathf.Lerp(0f, maxVolume, speedRatio);
                }
                else
                {
                    musicAudioSource.volume = maxVolume;
                }
            }
            else
            {
                if (musicAudioSource.isPlaying)
                {
                    if (fadeVolumeWithSpin && isSpinning)
                    {
                        musicAudioSource.volume = Mathf.Lerp(0f, maxVolume, speedRatio);
                        if (modulatePitchWithSpin)
                        {
                            musicAudioSource.pitch = Mathf.Lerp(minPitch, normalPitch, speedRatio);
                        }
                    }
                    else
                    {
                        if (pauseOnStop && !hasFinishedMelody)
                        {
                            musicAudioSource.Pause();
                        }
                        else
                        {
                            musicAudioSource.Stop();
                        }
                    }
                }
            }
        }

        private void ApplyInitialState(bool active)
        {
            currentSpinSpeed = active ? maxSpinSpeed : 0f;
            UpdateAudioPlayback();
        }

        private void OnIsOnChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                if (hasFinishedMelody)
                {
                    playbackTimer = 0f;
                    hasFinishedMelody = false;
                    if (musicAudioSource != null)
                    {
                        musicAudioSource.Stop();
                        musicAudioSource.time = 0f;
                    }
                }

                if (musicAudioSource != null && !musicAudioSource.isPlaying)
                {
                    if (musicAudioSource.clip != null || musicClip != null)
                    {
                        musicAudioSource.Play();
                    }
                }
            }
            else
            {
                if (hasFinishedMelody || !pauseOnStop)
                {
                    playbackTimer = 0f;
                    if (musicAudioSource != null)
                    {
                        musicAudioSource.Stop();
                        musicAudioSource.time = 0f;
                    }
                }
            }
        }

        #endregion

        #region Item Interaction

        /// <summary>
        /// Викликається гравцем при натисканні ЛКМ (Use), коли предмет тримають у руках.
        /// </summary>
        protected override void ExecuteItemAction(GameObject player)
        {
            ToggleStateServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetStateServerRpc(bool state)
        {
            isOn.Value = state;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void NotifyMelodyEndedServerRpc()
        {
            if (isOn.Value)
            {
                playbackTimer = 0f;
                hasFinishedMelody = true;
                isOn.Value = false;
            }
        }

        #endregion

        #region Save System

        public override string GetInfo()
        {
            return isOn.Value ? "1" : "0";
        }

        public override void ApplyInfo(string info)
        {
            if (IsServer && int.TryParse(info, out int val))
            {
                isOn.Value = val == 1;
            }
        }

        #endregion
    }
}
