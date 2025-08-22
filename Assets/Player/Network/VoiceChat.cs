using Steam;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.Voice
{
    public class VoiceChat : NetworkBehaviour
    {
        [SerializeField]
        private AudioSource source;

        [Header("Keybinds")]

        private MemoryStream output;
        private MemoryStream stream;

        private int optimalRate;
        private int clipBufferSize;
        private float[] clipBuffer;
        private Queue<VoiceCommand> voiceQueue = new();

        private int playbackBuffer;
        private int dataPosition;
        private int dataReceived;

        private void Start()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            optimalRate = (int)SteamUser.OptimalSampleRate;
            clipBufferSize = optimalRate * 5;
            clipBuffer = new float[clipBufferSize];
            stream = new MemoryStream();
            output = new MemoryStream();

            // Create the AudioClip with the specified settings
            source.clip = AudioClip.Create("VoiceData", clipBufferSize, 1, optimalRate, true, OnAudioRead, null);
            source.loop = true;
            source.Play();

            Movement.instance._controls.Gameplay.Voice.performed += ChangeVoiceRecordState;
        }
        private void ChangeVoiceRecordState(InputAction.CallbackContext obj)
        {
            SteamUser.VoiceRecord = !SteamUser.VoiceRecord;
        }
        private void Update()
        {
            ListenForVoice();
        }
        private void ListenForVoice()
        {
            if (!Application.isFocused || !SteamClient.IsValid) return;
            if (SteamUser.HasVoiceData)
            {
                Debug.LogError("HasVoiceData");
                int compressedWritten = SteamUser.ReadVoiceData(stream);
                stream.Position = 0;
                VoiceCommand voice = new()
                {
                    VoiceBytes = stream.GetBuffer(),
                    Compressed = compressedWritten,
                    UserId = OwnerClientId
                };
                voiceQueue.Enqueue(voice);
                ProcessVoice();
            }
        }

        private void ProcessVoice()
        {
            var voice = voiceQueue.Dequeue();
            CmdVoiceServerRpc(voice.VoiceBytes, voice.Compressed, voice.UserId);
        }

        [ServerRpc]
        private void CmdVoiceServerRpc(byte[] compressed, int bytesWritten, ulong userId)
        {
            VoiceDataClientRpc(compressed, bytesWritten, userId);
        }

        [ClientRpc]
        private void VoiceDataClientRpc(byte[] compressed, int bytesWritten, ulong senderId)
        {
            //Debug.Log(senderId + "is Sendler ID and " + OwnerClientId + "is Owner client ID");
            if (senderId == OwnerClientId) return;

            // Clear the input stream
            output.SetLength(0);

            // Decompress the voice data
            int uncompressedWritten = SteamUser.DecompressVoice(new MemoryStream(compressed), bytesWritten, output);

            // Reset the output stream position
            output.Position = 0;

            // Get the uncompressed data buffer
            byte[] outputBuffer = output.GetBuffer();

            // Write the decompressed voice data to the AudioClip buffer
            WriteToClip(outputBuffer, uncompressedWritten);
        }

        private void OnAudioRead(float[] data)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                // Start with silence
                data[i] = 0;

                // If there is data to play
                if (playbackBuffer > 0)
                {
                    // Set the current data position
                    dataPosition = (dataPosition + 1) % clipBufferSize;

                    // Set the audio data
                    data[i] = clipBuffer[dataPosition];

                    // Decrease the playback buffer
                    playbackBuffer--;
                }
            }
        }

        private void WriteToClip(byte[] uncompressed, int size)
        {
            for (int i = 0; i < size; i += 2)
            {
                // Convert the short data to float
                float converted = (short)(uncompressed[i] | uncompressed[i + 1] << 8) / 32767.0f;

                // Write the converted data to the clip buffer
                clipBuffer[dataReceived] = converted;

                // Move to the next position in the clip buffer
                dataReceived = (dataReceived + 1) % clipBufferSize;

                // Increase the playback buffer
                playbackBuffer++;
            }
        }
    }

    public class VoiceCommand
    {
        public byte[] VoiceBytes;
        public int Compressed;
        public ulong UserId;
    }
}