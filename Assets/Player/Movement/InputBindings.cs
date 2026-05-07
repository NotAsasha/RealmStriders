using FileSystem.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;

namespace Player.Movement
{
    public class InputBindings : MonoBehaviour
    {
        public InputActionReference actionToBind;
        public TMP_Text text;

        private SettingsFile settingsFile;
        private RebindingOperation rebindingOperation;
        private InputAction inputAction;

        private bool isRebinding = false;

        private void Start()
        {
            settingsFile = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");

            Load();
            inputAction = actionToBind.action;
            RefreshText();
        }

        private void OnEnable()
        {
            if (inputAction != null) RefreshText();
        }

        private void RefreshText()
        {
            text.text = inputAction.GetBindingDisplayString(0);
        }

        public void Load()
        {
            settingsFile.Load(false);
            string rebinds = settingsFile.save.rebinds;

            if (string.IsNullOrEmpty(rebinds)) return;

            try { actionToBind.action.actionMap.LoadBindingOverridesFromJson(rebinds);}
            catch { Debug.LogWarning("---InputBindings: Unable to rewrite bindings"); }

            Debug.Log("---InputBindings: Bindings loaded!");
        }

        public void Save()
        {
            settingsFile.save.rebinds = actionToBind.action.actionMap.SaveBindingOverridesAsJson();
            settingsFile.Save(false);
            Debug.Log("---InputBindings: Bindings saved!");
        }

        public void StartRebinding(bool ignoreMouse = true)
        {
            if (isRebinding) return;
            isRebinding = true;

            inputAction.Disable();
            rebindingOperation = inputAction.PerformInteractiveRebinding()
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => RebindComplete());

            if (ignoreMouse) rebindingOperation = rebindingOperation.WithControlsExcluding("Mouse");
            rebindingOperation = rebindingOperation.Start();
        }

        private void RebindComplete()
        {
            rebindingOperation.Dispose();
            isRebinding = false;
            inputAction.Enable();

            RefreshText();
            Save();
        }

        public void Update()
        {
            if (isRebinding && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                rebindingOperation?.Cancel();
                isRebinding = false;
            }
        }
    }
}