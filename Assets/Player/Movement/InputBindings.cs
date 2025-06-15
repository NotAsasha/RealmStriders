using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;
using TMPro;
using FileSystem;
namespace Player
{
    public class InputBindings : MonoBehaviour
    {
        public Controls _controls;
        public ActionToBind actionToBind;
        public TMP_Text text;
        private SettingsFile settingsFile;
        private GameFileHandler _fileHandler;
        private RebindingOperation rebindingOperation;
        private InputAction inputAction;
        private bool isRebinding = false;
        public enum ActionToBind
        {
            Movement = 0,
            Voice = 1,
            Jump = 2,

        }
        private void Start()
        {
            _controls = new();
            _fileHandler = GameFileHandler.Instance;
            settingsFile = (SettingsFile)_fileHandler.SearchForFileByName("Settings");
            Load();
            CheckWhatToRebind();
            text.text = inputAction.GetBindingDisplayString(0);
        }
        public void Load()
        {
            settingsFile.Load(false);
            string rebinds = settingsFile.save.rebinds;
            if (!string.IsNullOrEmpty(rebinds))
            {
                try { _controls.LoadBindingOverridesFromJson(rebinds); }
                catch { Debug.LogWarning("---InputBindings: Unable to rewrite bindings"); }
                Debug.Log("---InputBindings: Bindings loaded!");
                
            }
        }
        public void Save()
        {
            settingsFile.save.rebinds = _controls.SaveBindingOverridesAsJson();
            settingsFile.Save(false);
            Debug.Log("---InputBindings: Bindings saved!");
        }
        public void CallRebind()
        {
            Load();
            CheckWhatToRebind();
            StartRebinding(inputAction);
        }
        private void CheckWhatToRebind()
        {
            switch (actionToBind)
            {
                case ActionToBind.Movement:
                    inputAction = _controls.Gameplay.Movement; break;
                case ActionToBind.Jump:
                    inputAction = _controls.Gameplay.Jump; break;
                case ActionToBind.Voice:
                    inputAction = _controls.Gameplay.Voice; break;
            }
        }
        public void StartRebinding(InputAction rebindingAction)
        {
            if (isRebinding) return;
            isRebinding = true;

            rebindingAction.Disable();
            rebindingOperation = rebindingAction.PerformInteractiveRebinding()
                .WithControlsExcluding("Mouse")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => RebindComplete(rebindingAction))
                .Start();
        }
        private void RebindComplete(InputAction rebindingAction)
        {

            rebindingOperation.Dispose();
            isRebinding = false;
            rebindingAction.Enable();
            text.text = rebindingAction.GetBindingDisplayString(0);
            Save();
        }
        public void Update()
        {
            if (isRebinding && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                rebindingOperation?.Cancel();
            }
        }
    }
}