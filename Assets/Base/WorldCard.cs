using Unity.Netcode;
using UnityEngine;
using TMPro;

using NetString = Unity.Collections.FixedString64Bytes;
public class WorldCard : NetworkBehaviour
{
    [SerializeField] TMP_Text missionNameUI;
    [SerializeField] TMP_Text enemiesCountUI;
    [SerializeField] TMP_Text avarageDangerUI;

    public NetworkVariable<NetString> missionName = new("World1");
    public NetworkVariable<int> enemiesCount = new(1);
    public NetworkVariable<float> avarageDanger = new(1);

    public WorldChooser parent;

    public override void OnNetworkSpawn()
    {
        missionName.OnValueChanged +=
            (NetString oldV, NetString newV) => missionNameUI.text = newV.ToString();

        enemiesCount.OnValueChanged +=
            (int oldV, int newV) => enemiesCountUI.text = "Enemy number: " + newV.ToString();

        avarageDanger.OnValueChanged +=
            (float oldV, float newV) => avarageDangerUI.text = "Approximate danger: " + newV.ToString();

        missionNameUI.text = missionName.Value.ToString();
        enemiesCountUI.text = "Enemy number: " + enemiesCount.Value.ToString();
        avarageDangerUI.text = "Approximate danger: " + avarageDanger.Value.ToString();
    }
    public void SetMission()
    {
        SetMissionServerRpc(missionName.Value, enemiesCount.Value, avarageDanger.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetMissionServerRpc(NetString missionName, int enemiesCount, float avarageDanger)
    {
        if (GameManager.instance.hasStartedMission.Value) return;
        Debug.Log("ChangeGlobalMissionServerRpc");

        GameManager.instance.missionName = missionName.ToString();
        GameManager.instance.enemiesCount = enemiesCount;
        GameManager.instance.avarageDanger = avarageDanger;
    }
}
