using System;
using System.Collections.Generic;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using Player.Network;

namespace Player.Equipment
{
    [RequireComponent(typeof(AudioSource))]
    public class WalkieTalkie : Item
    {
        public static readonly HashSet<WalkieTalkie> AllRadios = new();

        [Header("Walkie-Talkie Settings")]
        public NetworkVariable<bool> isOn = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Header("Audio Components")]
        [SerializeField] private AudioSource speakerSource;
        [SerializeField] private AudioClip turnOnClip;
        [SerializeField] private AudioClip turnOffClip;

        [Header("Visual Feedback")]
        [SerializeField] private Light powerLed;
        [SerializeField] private GameObject screenEmissiveObject;

        private int sampleRate;
        private VoiceAudioBuffer radioBuffer;

        #region Unity & Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            AllRadios.Add(this);

            InitAudioBuffer();

            isOn.OnValueChanged += OnPowerStateChanged;
            UpdateVisualsAndAudioState(isOn.Value, playSound: false);
        }

        public override void OnNetworkDespawn()
        {
            AllRadios.Remove(this);
            isOn.OnValueChanged -= OnPowerStateChanged;
            base.OnNetworkDespawn();
        }

        private void InitAudioBuffer()
        {
            sampleRate = (int)SteamUser.OptimalSampleRate;
            if (sampleRate <= 0) sampleRate = 24000;

            radioBuffer = new VoiceAudioBuffer(sampleRate, bufferSeconds: 2.5f, prebufferSeconds: 0.08f);

            if (speakerSource == null) speakerSource = GetComponent<AudioSource>();

            speakerSource.clip = AudioClip.Create("RadioSpeakerClip", sampleRate, 1, sampleRate, true, OnAudioRead);
            speakerSource.loop = true;
            speakerSource.spatialBlend = 1.0f; // 3D просторовий звук
            speakerSource.Play();
        }

        #endregion

        #region Item Actions

        protected override void ExecuteItemAction(GameObject player)
        {
            TogglePowerServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TogglePowerServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        private void OnPowerStateChanged(bool previousValue, bool newValue)
        {
            UpdateVisualsAndAudioState(newValue, playSound: true);
        }

        private void UpdateVisualsAndAudioState(bool active, bool playSound)
        {
            if (powerLed != null) powerLed.enabled = active;
            if (screenEmissiveObject != null) screenEmissiveObject.SetActive(active);

            if (playSound && audioSource != null)
            {
                AudioClip clipToPlay = active ? turnOnClip : turnOffClip;
                if (clipToPlay != null) audioSource.PlayOneShot(clipToPlay);
            }
        }

        #endregion

        #region Audio Streaming

        public void WriteVoiceData(byte[] uncompressedData, int size, float volumeMultiplier = 1.0f)
        {
            if (!isOn.Value || radioBuffer == null) return;
            radioBuffer.WriteData(uncompressedData, size, volumeMultiplier);
        }

        private void OnAudioRead(float[] data)
        {
            if (radioBuffer != null && isOn.Value)
            {
                radioBuffer.ReadData(data);
            }
            else
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        #endregion

        #region Helpers & Save

        public bool CanTransmit() => isCurrentlyHeld && isOn.Value;

        public static bool IsLocalPlayerHoldingActiveRadio()
        {
            foreach (var radio in AllRadios)
            {
                if (radio.IsOwner && radio.CanTransmit())
                    return true;
            }
            return false;
        }

        public override string GetInfo() => isOn.Value.ToString();

        public override void ApplyInfo(string info)
        {
            if (bool.TryParse(info, out bool loadedState) && IsServer)
            {
                isOn.Value = loadedState;
            }
        }

        #endregion
    }
}