using System;
using System.IO;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Player.Movement;
using Player.Equipment;

namespace Player.Network
{
    public class VoiceChat : NetworkBehaviour
    {
        [Header("Audio Settings")]
        [Tooltip("3D AudioSource на голові персонажа (живий голос гравця поруч)")]
        [SerializeField] private AudioSource proximityAudioSource;

        private MemoryStream voiceInputStream;
        private MemoryStream voiceOutputStream;

        private int sampleRate;
        private VoiceAudioBuffer proximityBuffer;

        public override void OnNetworkSpawn()
        {
            sampleRate = (int)SteamUser.OptimalSampleRate;
            if (sampleRate <= 0) sampleRate = 24000;

            voiceInputStream = new MemoryStream();
            voiceOutputStream = new MemoryStream();

            if (!IsOwner)
            {
                // Створюємо буфер із пре-буферизацією ~80 мс
                proximityBuffer = new VoiceAudioBuffer(sampleRate, bufferSeconds: 2.5f, prebufferSeconds: 0.08f);
                SetupProximityAudio();
            }
            else
            {
                BindInput();
            }
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

        private void OnVoiceStarted(InputAction.CallbackContext ctx) => SteamUser.VoiceRecord = true;
        private void OnVoiceCanceled(InputAction.CallbackContext ctx) => SteamUser.VoiceRecord = false;

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

                    bool isTransmittingRadio = WalkieTalkie.IsLocalPlayerHoldingActiveRadio();
                    SendVoiceServerRpc(dataToSend, compressedBytesWritten, isTransmittingRadio);
                }
            }
        }

        // Прибрали Delivery = RpcDelivery.Unreliable! Тепер Netcode сам фрагментує будь-які розміри без OverflowException
        [Rpc(SendTo.Server)]
        private void SendVoiceServerRpc(byte[] compressedData, int length, bool isRadio, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            ReceiveVoiceClientRpc(compressedData, length, isRadio, senderClientId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ReceiveVoiceClientRpc(byte[] compressedData, int length, bool isRadio, ulong senderClientId)
        {
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
                        if (radio != null && radio.isOn.Value)
                        {
                            if (isMyOwnVoice && radio.IsOwner && radio.isCurrentlyHeld)
                            {
                                continue;
                            }

                            // Передаємо персональну гучність того, хто говорить у рацію
                            radio.WriteVoiceData(rawBuffer, uncompressedWritten, playerVolume);
                        }
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

            SteamUser.VoiceRecord = false;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            voiceInputStream?.Dispose();
            voiceOutputStream?.Dispose();
        }
    }
}