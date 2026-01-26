using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Unity.Netcode;
using System.Threading;

public class SlotMachine : Terminal
{
    // Has a chance to turn into a monster
    // Has a chance to hit jackpot -- 1000shag
    // Барабан такий ж як у вулику, анімація  - зупиняється на конкретному rotation
    public int spinCost;
    public DropTable table;

    private CasinoMonster monster;

    public List<NetworkObject> itemsToDrop;

    private void Start()
    {
        monster = GetComponent<CasinoMonster>();
        table = GetComponent<DropTable>();
    }

    [ServerRpc]
    public void SpinServerRpc()
    {
        if (GameManager.instance.teamMoney.Value < spinCost)
        {
            Debug.Log("---SlotMachine: Not enough money.");
            return;
        }
        Debug.Log("---SlotMachine: Spinned.");
        GameManager.instance.teamMoney.Value -= spinCost;
        table.ExecuteAction(table.ChooseDrop());
    }

    public void GiveCoins(int amount)
    {
        Debug.Log($"---SlotMachine: Won {amount} coins.");
        GameManager.instance.teamMoney.Value += amount;
    }

    public void SpawnRandomItem()
    {
        int index = Random.Range(0, itemsToDrop.Count);
        itemsToDrop[index].InstantiateAndSpawn(NetworkManager.Singleton, 0, false, false, false, GameManager.instance.spawnPoint);
        Debug.Log($"---SlotMachine: Won random item - {itemsToDrop[index].name}!");

    }

    public void TurnIntoMonster()
    {
        //StopInteraction();
        monster.WakeUpClientRpc();
        Debug.Log("---SlotMachine: Main prize!");
    }
}
