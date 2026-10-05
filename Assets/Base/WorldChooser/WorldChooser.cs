using Enemy;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Base.WorldChooser
{
    public struct MissionData : INetworkSerializable, System.IEquatable<MissionData>
    {
        public FixedString64Bytes missionName;
        // Pre-rolled danger tier for each enemy (1-5). Max 8 enemies (FixedList32Bytes<int> = 8x4 bytes).
        public FixedList32Bytes<int> dangerLevels;

        public int EnemiesCount => dangerLevels.Length;

        public float AverageDanger
        {
            get
            {
                if (dangerLevels.Length == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < dangerLevels.Length; i++) sum += dangerLevels[i];
                return sum / dangerLevels.Length;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref missionName);

            // FixedList32Bytes<int> has no built-in SerializeValue overload — serialize manually.
            int length = dangerLevels.Length;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                dangerLevels = new FixedList32Bytes<int>();
                for (int i = 0; i < length; i++)
                {
                    int val = 0;
                    serializer.SerializeValue(ref val);
                    dangerLevels.Add(val);
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    int val = dangerLevels[i];
                    serializer.SerializeValue(ref val);
                }
            }
        }

        public bool Equals(MissionData other)
        {
            if (!missionName.Equals(other.missionName)) return false;
            if (dangerLevels.Length != other.dangerLevels.Length) return false;
            for (int i = 0; i < dangerLevels.Length; i++)
                if (dangerLevels[i] != other.dangerLevels[i]) return false;
            return true;
        }
    }

    public class WorldChooser : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int missionNumber = 3;
        [SerializeField] private string[] availableWorlds;

        [Header("UI References")]
        [SerializeField] private Transform cardParent;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TMP_Text currentMissionText;
        [SerializeField] private AudioSource audioSource;

        private NetworkList<MissionData> availableMissions;

        private readonly NetworkVariable<FixedString64Bytes> selectedMissionName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            availableMissions = new NetworkList<MissionData>();
            if (audioSource == null) TryGetComponent(out audioSource);
        }

        public override void OnNetworkSpawn()
        {
            availableMissions.OnListChanged += OnMissionsListChanged;
            selectedMissionName.OnValueChanged += OnSelectedMissionChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged += ReactToMissionState;
                ReactToMissionState(false, GameManager.Instance.hasStartedMission.Value);
            }

            if (IsServer)
            {
                // a bit scary.. TODO
                GameManager.Instance.teamRating.OnValueChanged += (int _, int __) => GenerateMissions(missionNumber);
                GenerateMissions(missionNumber);
            }

            UpdateSelectedMissionUI(selectedMissionName.Value);
            RebuildCardsUI();
        }

        public override void OnNetworkDespawn()
        {
            availableMissions.OnListChanged -= OnMissionsListChanged;
            selectedMissionName.OnValueChanged -= OnSelectedMissionChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasStartedMission.OnValueChanged -= ReactToMissionState;
            }
        }

        private void OnMissionsListChanged(NetworkListEvent<MissionData> changeEvent)
        {
            RebuildCardsUI();
        }

        private void OnSelectedMissionChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            UpdateSelectedMissionUI(newValue);
            if (audioSource != null && !newValue.IsEmpty)
            {
                audioSource.Play();
            }
        }

        private void ReactToMissionState(bool oldState, bool isStarted)
        {
            cardParent.gameObject.SetActive(!isStarted);

            if (IsServer && oldState && !isStarted)
            {
                selectedMissionName.Value = default;
                GenerateMissions(missionNumber);
            }

            if (!isStarted)
            {
                RebuildCardsUI();
            }
        }

        private void UpdateSelectedMissionUI(FixedString64Bytes missionName)
        {
            if (missionName.IsEmpty)
            {
                currentMissionText.text = "NONE";
                currentMissionText.color = Color.red;
            }
            else
            {
                currentMissionText.text = missionName.ToString();
                currentMissionText.color = Color.green;
            }
        }

        private void RebuildCardsUI()
        {
            ClearUI();

            for (int i = 0; i < availableMissions.Count; i++)
            {
                var mission = availableMissions[i];
                var cardObj = Instantiate(cardPrefab, cardParent);
                if (cardObj.TryGetComponent<WorldCard>(out var card))
                {
                    card.Setup(this, mission.missionName, mission.EnemiesCount, mission.AverageDanger);
                }
            }
        }

        private void ClearUI()
        {
            for (int i = cardParent.childCount - 1; i >= 0; i--)
            {
                Destroy(cardParent.GetChild(i).gameObject);
            }
        }

        private void GenerateMissions(int capacity)
        {
            if (availableWorlds == null || availableWorlds.Length == 0)
            {
                Debug.LogError("[WorldChooser] availableWorlds is empty!");
                return;
            }

            availableMissions.Clear();

            int currentRating = GameManager.Instance != null ? GameManager.Instance.teamRating.Value : 1;

            for (int i = 0; i < capacity; i++)
            {
                var missionName = new FixedString64Bytes(availableWorlds[Random.Range(0, availableWorlds.Length)]);

                int difficultyStep = Mathf.Max(1, currentRating + i - 1);
                int enemiesCount = EnemySpawner.RandomEnemiesNumber(difficultyStep);
                var rolledDangers = EnemySpawner.CalculateDangers(difficultyStep, enemiesCount);

                var dangerLevels = new FixedList32Bytes<int>();
                foreach (int d in rolledDangers) dangerLevels.Add(d);

                availableMissions.Add(new MissionData
                {
                    missionName = missionName,
                    dangerLevels = dangerLevels,
                });
            }
        }

        // Client sends only the mission name — the server looks up dangerLevels from
        // its own authoritative availableMissions list, avoiding ILPP serialization issues
        // with FixedList32Bytes<int> as an RPC parameter.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetMissionServerRpc(FixedString64Bytes missionName)
        {
            if (GameManager.Instance == null || GameManager.Instance.hasStartedMission.Value) return;

            for (int i = 0; i < availableMissions.Count; i++)
            {
                if (!availableMissions[i].missionName.Equals(missionName)) continue;

                GameManager.Instance.missionName = missionName.ToString();
                GameManager.Instance.dangerLevels = availableMissions[i].dangerLevels;
                selectedMissionName.Value = missionName;
                return;
            }

            Debug.LogWarning($"[WorldChooser] Mission '{missionName}' not found in availableMissions.");
        }
    }
}