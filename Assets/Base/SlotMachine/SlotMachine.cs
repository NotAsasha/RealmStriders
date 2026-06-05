using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Base.SlotMachine
{
    public class SlotMachine : Terminal
    {
        // Has a chance to turn into a monster
        // Has a chance to hit jackpot -- 1000shag
        // Барабан такий ж як у вулику, анімація  - зупиняється на конкретному rotation
        public int spinCost;
        public DropTable table;

        public Transform spinObject;
        public AnimationCurve movementCurve;

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

            Debug.Log("---SlotMachine: Spin.");
            GameManager.Instance.teamMoney.Value -= spinCost;
            int dropID = table.ChooseDrop();

            SpinClientRpc(dropID);
            table.ExecuteAction(dropID);
        }


        [ClientRpc]
        public void SpinClientRpc(int dropID)
        {
            StartCoroutine(SpinAnimation(dropID));
        }

        // TERRIBLE, to fix TODO
        private bool isSpinning = false;
        private IEnumerator SpinAnimation(int dropID)
        {
            endPos = 1800 - 30 * dropID;
            isSpinning = true;

            yield return new WaitForSeconds(spinLenght);
        }
        private float time = 0;
        private float endPos;


        public void Update()
        {
            if (!isSpinning) return;

            
            var startPos = spinObject.localEulerAngles;
            
            var endPos2 = spinObject.localEulerAngles;
            endPos2.y = endPos;

            spinObject.gameObject.transform.localEulerAngles = Vector3.Lerp(startPos, endPos2, movementCurve.Evaluate(time / 5f));

            time += Time.deltaTime;
            if (time > 5f)
            {
                startPos.y %= 360;
                time = 0;
                isSpinning = false;
            }
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
            DeactivateRpc();
            Debug.Log("---SlotMachine: Main prize!");
        }
    }
}
