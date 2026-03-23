using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Base.Console
{
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

        private void HandleLog(string logString, string stackTrace, LogType type) {
            if (!IsServer) return;
            string firstLine = logString.Split('\n')[0];
            AddLogClientRpc(firstLine);
        }

        [ClientRpc] 
        private void AddLogClientRpc(string logString) {
            parent.transform.position = parentStartPosition;
            logText.text = logString;
        
            Instantiate(logText.gameObject, parent);
            if (parent.transform.childCount >= maxLogs) 
                Destroy(parent.transform.GetChild(0).gameObject);
        }
    }
}
