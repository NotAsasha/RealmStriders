using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace Player.Network
{
    public static class VoiceVolumeManager
    {
        // Словник: ClientId гравця -> Множник гучності (від 0.0f до 10.0f, де 5.0f = 100%)
        private static readonly Dictionary<ulong, float> playerVolumes = new();

        // Маппінг SteamId <-> NGO ClientId, заповнюється при OnNetworkSpawn кожного VoiceChat
        private static readonly Dictionary<SteamId, ulong> steamToClient = new();
        private static readonly Dictionary<ulong, SteamId> clientToSteam = new();

        // Подія зміни гучності (на випадок, якщо треба оновити UI)
        public static event Action<ulong, float> OnVolumeChanged;

        // Подія відключення гравця
        public static event Action<ulong, SteamId> OnPlayerUnregistered;

        public const float DEFAULT_VOLUME = 5.0f;
        public const float MAX_VOLUME = 10.0f;

        /// <summary>
        /// Реєструє зв'язок між NGO ClientId та SteamId гравця.
        /// Викликати з VoiceChat при OnNetworkSpawn.
        /// </summary>
        public static void RegisterPlayer(ulong clientId, SteamId steamId)
        {
            steamToClient[steamId] = clientId;
            clientToSteam[clientId] = steamId;
        }

        /// <summary>
        /// Знімає реєстрацію гравця при відключенні.
        /// </summary>
        public static void UnregisterPlayer(ulong clientId)
        {
            if (clientToSteam.TryGetValue(clientId, out SteamId steamId))
            {
                steamToClient.Remove(steamId);
                clientToSteam.Remove(clientId);
                playerVolumes.Remove(clientId);
                OnPlayerUnregistered?.Invoke(clientId, steamId);
            }
            else
            {
                clientToSteam.Remove(clientId);
                playerVolumes.Remove(clientId);
            }
        }

        /// <summary>
        /// Отримати SteamId гравця за NGO ClientId.
        /// </summary>
        public static bool TryGetSteamId(ulong clientId, out SteamId steamId)
        {
            return clientToSteam.TryGetValue(clientId, out steamId);
        }

        /// <summary>
        /// Встановити гучність через NGO ClientId.
        /// </summary>
        public static void SetVolume(ulong clientId, float volume)
        {
            volume = Mathf.Clamp(volume, 0.0f, MAX_VOLUME);
            playerVolumes[clientId] = volume;
            OnVolumeChanged?.Invoke(clientId, volume);
        }

        /// <summary>
        /// Встановити гучність через SteamId (зручно для PlayerCard).
        /// </summary>
        public static void SetVolumeBySteamId(SteamId steamId, float volume)
        {
            if (steamToClient.TryGetValue(steamId, out ulong clientId))
                SetVolume(clientId, volume);
        }

        /// <summary>
        /// Отримати поточний множник гучності гравця (за замовчуванням DEFAULT_VOLUME).
        /// </summary>
        public static float GetVolume(ulong clientId)
        {
            return playerVolumes.TryGetValue(clientId, out float volume) ? volume : DEFAULT_VOLUME;
        }

        /// <summary>
        /// Отримати поточний множник гучності гравця за SteamId.
        /// </summary>
        public static float GetVolumeBySteamId(SteamId steamId)
        {
            return steamToClient.TryGetValue(steamId, out ulong clientId) ? GetVolume(clientId) : DEFAULT_VOLUME;
        }

        /// <summary>
        /// Перевіряє, чи зареєстрований гравець з таким SteamId.
        /// </summary>
        public static bool IsRegistered(SteamId steamId) => steamToClient.ContainsKey(steamId);

        /// <summary>
        /// Скинути всі налаштування гучності (наприклад, при виході з лобі).
        /// </summary>
        public static void ResetAll()
        {
            playerVolumes.Clear();
            steamToClient.Clear();
            clientToSteam.Clear();
        }
    }
}