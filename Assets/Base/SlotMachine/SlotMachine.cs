using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Base.SlotMachine
{
    public class SlotMachine : Terminal
    {
        [Header("Slot Settings")]
        public int spinCost = 100;
        public float spinLength = 1.5f;
        public int fullSpins = 5;
        public float sectorAngle = 30f; 

        [Header("References")]
        public DropTable table;
        public Transform spinObject;
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public List<NetworkObject> itemsToDrop;

        private readonly NetworkVariable<bool> isSpinning = new(false);

        private void Start()
        {
            if (table == null) table = GetComponent<DropTable>();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SpinRpc()
        {
            if (isSpinning.Value) return;

            if (GameManager.Instance.teamMoney.Value < spinCost)
            {
                Debug.Log("---SlotMachine: Not enough money.");
                return;
            }

            Debug.Log("---SlotMachine: Spin.");
            GameManager.Instance.teamMoney.Value -= spinCost;
            isSpinning.Value = true;

            int dropID = table.ChooseDrop();

            SpinAnimationRpc(dropID);
        }

        [Rpc(SendTo.Everyone)]
        private void SpinAnimationRpc(int dropID)
        {
            StartCoroutine(SpinRoutine(dropID));
        }

        private IEnumerator SpinRoutine(int dropID)
        {
            float elapsedTime = 0f;

            // 1. Отримуємо поточний початковий кут Y
            float startAngleY = spinObject.localEulerAngles.y;

            // 2. Приводимо початковий кут до діапазону 0..360
            float currentNormalizedY = startAngleY % 360f;
            if (currentNormalizedY < 0) currentNormalizedY += 360f;

            // 3. Обчислюємо точний абсолютний кут для сектора dropID
            // (Залежно від напрямку обертання вашої моделі: 360 - sectorAngle * dropID)
            float targetSectorAngle = (360f - (sectorAngle * dropID)) % 360f;
            if (targetSectorAngle < 0) targetSectorAngle += 360f;

            // 4. Обчислюємо різницю (скільки градусів треба докрутити вперед від поточного стану)
            float forwardDelta = (targetSectorAngle - currentNormalizedY + 360f) % 360f;

            // 5. Загальний кінцевий кут = поточний кут + повні оберти + докручування до точного сектора
            float totalTargetAngleY = startAngleY + (360f * fullSpins) + forwardDelta;

            while (elapsedTime < spinLength)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / spinLength);
                float curveValue = movementCurve.Evaluate(progress);

                float currentAngleY = Mathf.Lerp(startAngleY, totalTargetAngleY, curveValue);

                spinObject.localRotation = Quaternion.Euler(0f, currentAngleY, 0f);

                yield return null;
            }

            spinObject.localRotation = Quaternion.Euler(0f, totalTargetAngleY % 360f, 0f);

            if (IsServer)
            {
                table.ExecuteAction(dropID);
                isSpinning.Value = false;
            }
        }

        public void GiveCoins(int amount)
        {
            if (!IsServer) return;
            Debug.Log($"---SlotMachine: Won {amount} coins.");
            GameManager.Instance.teamMoney.Value += amount;
        }

        public void SpawnRandomItem()
        {
            if (!IsServer) return;

            if (itemsToDrop == null || itemsToDrop.Count == 0) return;

            int index = Random.Range(0, itemsToDrop.Count);
            NetworkObject prefab = itemsToDrop[index];

            Vector3 spawnPoint = GameManager.Instance.spawnPoint;

            NetworkObject spawnedItem = Instantiate(prefab, spawnPoint, Quaternion.identity);
            spawnedItem.Spawn();

            Debug.Log($"---SlotMachine: Won random item - {spawnedItem.name}!");
        }

        [Rpc(SendTo.Everyone)]
        public void TurnIntoMonsterRpc()
        {
            DeactivateRpc();
            Debug.Log("---SlotMachine: Main prize - MIMIC!");
        }
    }
}