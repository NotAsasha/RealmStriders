using System;
using UnityEngine;

namespace Player.Network
{
    public class VoiceAudioBuffer
    {
        private readonly float[] buffer;
        private readonly int bufferSize;
        private int writePosition;
        private int readPosition;
        private int bufferedSamples;

        private readonly int prebufferThreshold;
        private bool isBuffering = true;

        // Базове підсилення чутливості мікрофона Steam
        private const float BASE_VOICE_GAIN = 2.5f;

        private readonly object lockObj = new();

        public VoiceAudioBuffer(int sampleRate, float bufferSeconds = 2.5f, float prebufferSeconds = 0.08f)
        {
            bufferSize = (int)(sampleRate * bufferSeconds);
            buffer = new float[bufferSize];
            prebufferThreshold = (int)(sampleRate * prebufferSeconds);
        }

        /// <summary>
        /// Запис даних з урахуванням індивідуальної гучності гравця
        /// </summary>
        public void WriteData(byte[] uncompressedData, int size, float userVolumeMultiplier = 1.0f)
        {
            // Якщо гравець заглушений (Volume = 0), просто не витрачаємо ресурси
            if (userVolumeMultiplier <= 0.001f) return;

            int sampleCount = size / 2;
            if (sampleCount <= 0) return;

            float finalGain = BASE_VOICE_GAIN * userVolumeMultiplier;

            lock (lockObj)
            {
                for (int i = 0; i < size; i += 2)
                {
                    short sample = (short)(uncompressedData[i] | (uncompressedData[i + 1] << 8));

                    // Застосовуємо загальне підсилення та персональний повзунок гравця
                    float sampleFloat = (sample / 32768.0f) * finalGain;

                    // Захист від аудіокліпінгу (перевантаження)
                    if (sampleFloat > 1.0f) sampleFloat = 1.0f;
                    else if (sampleFloat < -1.0f) sampleFloat = -1.0f;

                    buffer[writePosition] = sampleFloat;
                    writePosition = (writePosition + 1) % bufferSize;
                }

                bufferedSamples += sampleCount;

                if (bufferedSamples > bufferSize)
                {
                    readPosition = (writePosition - (bufferSize / 2) + bufferSize) % bufferSize;
                    bufferedSamples = bufferSize / 2;
                }

                if (isBuffering && bufferedSamples >= prebufferThreshold)
                {
                    isBuffering = false;
                }
            }
        }

        public void ReadData(float[] output)
        {
            lock (lockObj)
            {
                if (isBuffering || bufferedSamples <= 0)
                {
                    Array.Clear(output, 0, output.Length);
                    isBuffering = true;
                    return;
                }

                for (int i = 0; i < output.Length; i++)
                {
                    if (bufferedSamples > 0)
                    {
                        output[i] = buffer[readPosition];
                        readPosition = (readPosition + 1) % bufferSize;
                        bufferedSamples--;
                    }
                    else
                    {
                        output[i] = 0f;
                        isBuffering = true;
                    }
                }
            }
        }
    }
}