using Unity.Netcode;
using UnityEngine;

namespace Game.Casino
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MissionWarningAudio : NetworkBehaviour
    {
        [SerializeField] private AudioSource warningSource;
        [SerializeField, Min(0f)] private float warningSeconds = 30f;

        private readonly NetworkVariable<bool> warningActive = new(false,
            writePerm: NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            warningActive.OnValueChanged += OnWarningChanged;
            ApplyWarning(warningActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            warningActive.OnValueChanged -= OnWarningChanged;
            if (warningSource != null) warningSource.Stop();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            // Only the server decrements GameManager's missionDuration.
            GameManager mission = GameManager.Instance;
            bool shouldWarn = mission != null && mission.IsSpawned &&
                mission.hasStartedMission.Value && mission.missionDuration > 0f &&
                mission.missionDuration <= warningSeconds;
            if (warningActive.Value != shouldWarn) warningActive.Value = shouldWarn;
        }

        private void OnWarningChanged(bool previous, bool current)
        {
            ApplyWarning(current);
        }

        private void ApplyWarning(bool active)
        {
            if (!IsClient || warningSource == null) return;

            if (active) warningSource.Play();
            else warningSource.Stop();
        }
    }
}
