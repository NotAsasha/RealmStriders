using System;
using System.IO;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Player.Movement;
using Player.Equipment;
using Enemy;

namespace Player.Network
{
    public class VoiceChat : NetworkBehaviour
    {
        [Header("Audio Settings")]
        [Tooltip("3D AudioSource на голові персонажа (живий голос гравця поруч)")]
        [SerializeField] private AudioSource proximityAudioSource;

        [Header("Enemy Hearing")]
        [SerializeField] private SoundProducer voiceSoundProducer;
        [SerializeField, Range(0f, 1f)] private float voiceNoiseThreshold = 0.04f;

        private MemoryStream voiceInputStream;
        private MemoryStream voiceOutputStream;
        private MemoryStream voiceAnalysisStream;

        private int sampleRate;
        private VoiceAudioBuffer proximityBuffer;

        public override void OnNetworkSpawn()
        {
            if (SteamClient.IsValid)
            {
                try
                {
                    sampleRate = (int)SteamUser.OptimalSampleRate;
                }
                catch
                {
                    sampleRate = 24000;
                }
            }
            else
            {
                sampleRate = 24000;
            }

            if (sampleRate <= 0) sampleRate = 24000;

            voiceInputStream = new MemoryStream();
            voiceOutputStream = new MemoryStream();
            voiceAnalysisStream = new MemoryStream();

            if (!IsOwner)
            {
                // Створюємо буфер із пре-буферизацією ~80 мс
                proximityBuffer = new VoiceAudioBuffer(sampleRate, bufferSeconds: 2.5f, prebufferSeconds: 0.08f);
                SetupProximityAudio();
            }
            else
            {
                BindInput();
                // Реєструємо маппінг SteamId ↔ NGO ClientId на всіх клієнтах
                ulong steamId = 0;
                if (SteamClient.IsValid)
                {
                    try { steamId = (ulong)SteamClient.SteamId; } catch { }
                }
                RegisterSteamIdServerRpc(steamId);
            }
        }

        [Rpc(SendTo.Server)]
        private void RegisterSteamIdServerRpc(ulong steamId, RpcParams rpcParams = default)
        {
            BroadcastSteamIdClientRpc(rpcParams.Receive.SenderClientId, steamId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastSteamIdClientRpc(ulong clientId, ulong steamId)
        {
            VoiceVolumeManager.RegisterPlayer(clientId, steamId);
        }

        private void SetupProximityAudio()
        {
            if (proximityAudioSource != null)
            {
                proximityAudioSource.clip = AudioClip.Create("Voice_Proximity", sampleRate, 1, sampleRate, true, OnAudioRead);
                proximityAudioSource.loop = true;
                proximityAudioSource.spatialBlend = 1.0f;
                proximityAudioSource.rolloffMode = AudioRolloffMode.Linear;
                proximityAudioSource.minDistance = 2f;
                proximityAudioSource.maxDistance = 18f;
                proximityAudioSource.Play();
            }
        }

        private void BindInput()
        {
            if (PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                var voiceAction = PlayerMovement.Instance.controls.Gameplay.Voice;
                voiceAction.started += OnVoiceStarted;
                voiceAction.canceled += OnVoiceCanceled;
            }
        }

        private void OnVoiceStarted(InputAction.CallbackContext ctx)
        {
            if (SteamClient.IsValid)
            {
                try { SteamUser.VoiceRecord = true; } catch { }
            }
        }

        private void OnVoiceCanceled(InputAction.CallbackContext ctx)
        {
            if (SteamClient.IsValid)
            {
                try { SteamUser.VoiceRecord = false; } catch { }
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            ListenForVoice();
        }

        private void ListenForVoice()
        {
            if (!Application.isFocused || !SteamClient.IsValid) return;

            // Вичитуємо всі накопичені аудіо-пакети зі Steam
            while (SteamUser.HasVoiceData)
            {
                voiceInputStream.SetLength(0);
                int compressedBytesWritten = SteamUser.ReadVoiceData(voiceInputStream);

                if (compressedBytesWritten > 0)
                {
                    byte[] dataToSend = new byte[compressedBytesWritten];
                    Array.Copy(voiceInputStream.GetBuffer(), dataToSend, compressedBytesWritten);

                    float voiceLevel = GetVoiceLevel(compressedBytesWritten);
                    bool isTransmittingRadio = WalkieTalkie.IsLocalPlayerHoldingActiveRadio();
                    SendVoiceServerRpc(dataToSend, compressedBytesWritten, isTransmittingRadio, voiceLevel);
                }
            }
        }

        private float GetVoiceLevel(int compressedBytes)
        {
            if (!SteamClient.IsValid) return 0f;

            voiceInputStream.Position = 0;
            voiceAnalysisStream.SetLength(0);
            voiceAnalysisStream.Position = 0;

            int uncompressedBytes = SteamUser.DecompressVoice(
                voiceInputStream,
                compressedBytes,
                voiceAnalysisStream);

            if (uncompressedBytes < 2) return 0f;

            byte[] rawBuffer = voiceAnalysisStream.GetBuffer();
            int sampleCount = uncompressedBytes / 2;
            double sumSquares = 0d;

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(rawBuffer[i * 2] | (rawBuffer[i * 2 + 1] << 8));
                float normalizedSample = sample / 32768f;
                sumSquares += normalizedSample * normalizedSample;
            }

            return Mathf.Clamp01(Mathf.Sqrt((float)(sumSquares / sampleCount)));
        }

        // Прибрали Delivery = RpcDelivery.Unreliable! Тепер Netcode сам фрагментує будь-які розміри без OverflowException
        [Rpc(SendTo.Server)]
        private void SendVoiceServerRpc(
            byte[] compressedData,
            int length,
            bool isRadio,
            float voiceLevel,
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (voiceSoundProducer != null && voiceLevel >= voiceNoiseThreshold)
            {
                voiceSoundProducer.RefreshContinuousNoise();
            }

            if (isRadio)
            {
                WalkieTalkie.GetTransmittingRadio(senderClientId)?.RefreshNoiseFromVoice();
            }

            ReceiveVoiceClientRpc(compressedData, length, isRadio, senderClientId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ReceiveVoiceClientRpc(byte[] compressedData, int length, bool isRadio, ulong senderClientId)
        {
            if (!SteamClient.IsValid) return;

            bool isMyOwnVoice = (senderClientId == NetworkManager.Singleton.LocalClientId);

            // Отримуємо налаштовану користувачем гучність для цього конкретного гравця
            float playerVolume = VoiceVolumeManager.GetVolume(senderClientId);

            // Якщо гравець вимкнув звук цього учасника на 0% — пропускаємо декомпресію (економія CPU)
            if (!isMyOwnVoice && playerVolume <= 0.001f) return;

            voiceOutputStream.SetLength(0);

            using (var memoryStream = new MemoryStream(compressedData, 0, length))
            {
                int uncompressedWritten = SteamUser.DecompressVoice(memoryStream, length, voiceOutputStream);

                if (uncompressedWritten <= 0) return;

                byte[] rawBuffer = voiceOutputStream.GetBuffer();

                // 1. Просторовий голос від тіла гравця
                if (!isMyOwnVoice && proximityBuffer != null)
                {
                    proximityBuffer.WriteData(rawBuffer, uncompressedWritten, playerVolume);
                }

                // 2. Трансляція через рації
                if (isRadio)
                {
                    foreach (var radio in WalkieTalkie.AllRadios)
                    {
                        if (radio == null || !radio.isOn.Value) continue;

                        // Пропускаємо лише ту конкретну рацію, яку тримає сам відправник,
                        // щоб не відтворювати луну власного голосу назад у себе.
                        // Не можна використовувати radio.IsOwner — він вказує на NetworkObject-власника
                        // (сервер/хост), а не на гравця, що тримає рацію, через що хост
                        // некоректно пропускав би запис у всі рації.
                        if (isMyOwnVoice && radio.isCurrentlyHeld && radio.IsOwner)
                        {
                            // IsOwner тут означає: ця рація спаунена локально (тобто ми — власник NetworkObject).
                            // Для хоста IsOwner завжди true на його об'єктах, тому додаткова перевірка:
                            // пропускаємо тільки якщо рація reально в руках і ми — локальний відправник.
                            ulong localId = NetworkManager.Singleton.LocalClientId;
                            if (senderClientId == localId)
                                continue;
                        }

                        radio.WriteVoiceData(rawBuffer, uncompressedWritten, playerVolume);
                    }
                }
            }
        }

        private void OnAudioRead(float[] data)
        {
            if (proximityBuffer != null)
            {
                proximityBuffer.ReadData(data);
            }
            else
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && PlayerMovement.Instance != null && PlayerMovement.Instance.controls != null)
            {
                var voiceAction = PlayerMovement.Instance.controls.Gameplay.Voice;
                voiceAction.started -= OnVoiceStarted;
                voiceAction.canceled -= OnVoiceCanceled;
            }

            VoiceVolumeManager.UnregisterPlayer(OwnerClientId);

            if (SteamClient.IsValid)
            {
                try { SteamUser.VoiceRecord = false; } catch { }
            }
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            voiceInputStream?.Dispose();
            voiceOutputStream?.Dispose();
            voiceAnalysisStream?.Dispose();
        }
    }
}