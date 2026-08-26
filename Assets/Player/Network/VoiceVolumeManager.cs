using System;
using System.Collections.Generic;
using UnityEngine;

namespace Player.Network
{
    public static class VoiceVolumeManager
    {
        // Словник: ClientId гравця -> Множник гучності (від 0.0f до 2.0f, де 1.0f = 100%)
        private static readonly Dictionary<ulong, float> playerVolumes = new();

        // Подія зміни гучності (на випадок, якщо треба оновити UI)
        public static event Action<ulong, float> OnVolumeChanged;

        public const float DEFAULT_VOLUME = 5.0f;
        public const float MAX_VOLUME = 10.0f; // Можна підсилювати до 200% для тихих мікрофонів

        /// <summary>
        /// Встановити гучність для конкретного гравця за його ClientId
        /// </summary>
        public static void SetVolume(ulong clientId, float volume)
        {
            volume = Mathf.Clamp(volume, 0.0f, MAX_VOLUME);
            playerVolumes[clientId] = volume;

            OnVolumeChanged?.Invoke(clientId, volume);
        }

        /// <summary>
        /// Отримати поточний множник гучності гравця (за замовчуванням 1.0f)
        /// </summary>
        public static float GetVolume(ulong clientId)
        {
            if (playerVolumes.TryGetValue(clientId, out float volume))
            {
                return volume;
            }
            return DEFAULT_VOLUME;
        }

        /// <summary>
        /// Скинути всі налаштування гучності (наприклад, при виході з лобі)
        /// </summary>
        public static void ResetAll()
        {
            playerVolumes.Clear();
        }
    }
}