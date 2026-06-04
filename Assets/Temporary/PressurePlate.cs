using FileSystem.Scripts;
using Player.Equipment;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Temporary
{
    public class PressurePlate : NetworkBehaviour, ICollidable
    {
        [FormerlySerializedAs("_saveFileName")] [SerializeField] private string saveFileName;
        [FormerlySerializedAs("_pressurePlate")] [SerializeField] private GameObject pressurePlate;
        [FormerlySerializedAs("_pressesCounter")] [SerializeField] private TMP_Text pressesCounter;
        [FormerlySerializedAs("_normalColor")] [SerializeField] private Color normalColor;
        [FormerlySerializedAs("_pressesColor")] [SerializeField] private Color pressesColor;
        private GameFileHandler fileHandler;
        private TestGameFile testGameFile;

        private void Awake()
        {
            fileHandler = GameFileHandler.Instance;
            testGameFile = (TestGameFile)fileHandler.SearchForFileByName(saveFileName);
            pressesCounter.text = "Button press count: " + testGameFile.buttonPresses;
        }

        public void OnColliderEnter(GameObject collider)
        {
            CallButtonPressServerRpc();
        }

        [ClientRpc]
        private void UiUpdateClientRPC(int pressesAmount)
        {
            pressesCounter.text = "Button press count: " + pressesAmount;
        }
        [ServerRpc]
        public void CallButtonPressServerRpc()
        {
            testGameFile.AddButtonClick();
            testGameFile.Save();
            UiUpdateClientRPC(testGameFile.buttonPresses);
            //  _pressesCounter.text = "Button press count: " + testGameFile.buttonPresses;
        }
    }
}
