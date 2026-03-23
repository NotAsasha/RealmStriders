using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Base.SlotMachine
{
    public class SlotMachine : Terminal
    {
        // Has a chance to turn into a monster
        // Has a chance to hit jackpot -- 1000shag
        // Барабан такий ж як у вулику, анімація  - зупиняється на конкретному rotation
        public int spinCost;
        public DropTable table;

        public List<NetworkObject> itemsToDrop;

        //Spin animation lenght in seconds.
        float spinLenght = 1.5f;

        private void Start()
        {
            table = GetComponent<DropTable>();
        }

        [ServerRpc]
        public void SpinServerRpc()
        {
            if (GameManager.Instance.teamMoney.Value < spinCost)
            {
                Debug.Log("---SlotMachine: Not enough money.");
                return;
            }

            SpinClientRpc();
        }

        [ClientRpc]
        public void SpinClientRpc()
        {
            StartCoroutine(SpinAnimation());

            if (IsServer)
            {
                Debug.Log("---SlotMachine: Spinned.");
                GameManager.Instance.teamMoney.Value -= spinCost;
                table.ExecuteAction(table.ChooseDrop());
            }
        }

        private IEnumerator SpinAnimation()
        {
            yield return new WaitForSeconds(spinLenght);
        }

   

        public void GiveCoins(int amount)
        {
            Debug.Log($"---SlotMachine: Won {amount} coins.");
            GameManager.Instance.teamMoney.Value += amount;
        }

        public void SpawnRandomItem()
        {
            int index = Random.Range(0, itemsToDrop.Count);
            itemsToDrop[index].InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, GameManager.Instance.spawnPoint);
            Debug.Log($"---SlotMachine: Won random item - {itemsToDrop[index].name}!");

        }
        [ClientRpc]
        public void TurnIntoMonsterClientRpc()
        {
            if (playerCameraComponent != null) playerCameraComponent.StopInteraction();
            Debug.Log("---SlotMachine: Main prize!");
        }
    }
}
