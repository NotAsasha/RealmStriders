using TMPro;
using Unity.Netcode;
using UnityEngine;
public class Console : NetworkBehaviour
{
    [SerializeField] int maxLogs = 10;

    [SerializeField] private Transform parent;
    [SerializeField] private TextMeshProUGUI logText;

    private Vector3 parentStartPosition;
    private void OnEnable() {
        parentStartPosition = parent.transform.position;
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable() {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string _logString, string _stackTrace, LogType _type) {
        if (!IsServer) return;
        string firstLine = _logString.Split('\n')[0];
        AddLogClientRpc(firstLine);
    }

    [ClientRpc] 
    private void AddLogClientRpc(string _logString) {
        parent.transform.position = parentStartPosition;
        logText.text = _logString;
        
        Instantiate(logText.gameObject, parent);
        if (parent.transform.childCount >= maxLogs) 
            Destroy(parent.transform.GetChild(0).gameObject);
    }
}
