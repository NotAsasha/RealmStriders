using FileSystem.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Player.UI
{
    public class InputBindings : MonoBehaviour
    {
        [SerializeField] private InputActionReference actionToBind;
        [SerializeField] private TMP_Text buttonText;
        [SerializeField] private string waitingForInputText = "> ... <";

        private SettingsFile settingsFile;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
        private bool isInitialized = false;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            settingsFile = (SettingsFile)GameFileHandler.Instance.SearchForFileByName("Settings");
            isInitialized = true;
        }

        private void OnEnable()
        {
            if (actionToBind != null && actionToBind.action != null)
            {
                RefreshText();
            }
        }

        private void OnDisable()
        {
            CancelRebind();
        }

        private void OnDestroy()
        {
            rebindingOperation?.Dispose();
        }

        private void RefreshText()
        {
            buttonText.text = actionToBind.action.GetBindingDisplayString(0);
        }

        private void UpdateMemorySave()
        {
            if (settingsFile != null && actionToBind.action.actionMap.asset != null)
            {
                settingsFile.save.rebinds = actionToBind.action.actionMap.asset.SaveBindingOverridesAsJson();
            }
        }

        public void StartRebinding(bool ignoreMouse = true)
        {
            if (rebindingOperation != null && rebindingOperation.started) return;

            buttonText.text = waitingForInputText;
            actionToBind.action.Disable();

            rebindingOperation = actionToBind.action.PerformInteractiveRebinding()
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => RebindComplete())
                .OnCancel(operation => RebindCancelled());

            if (ignoreMouse)
            {
                rebindingOperation.WithControlsExcluding("Mouse");
            }

            rebindingOperation.WithCancelingThrough("<Keyboard>/escape");

            rebindingOperation.Start();
        }

        private void RebindComplete()
        {
            CleanUpRebind();
            RefreshText();
            UpdateMemorySave();
        }

        private void RebindCancelled()
        {
            CleanUpRebind();
            RefreshText();
        }

        private void CancelRebind()
        {
            rebindingOperation?.Cancel();
        }

        private void CleanUpRebind()
        {
            rebindingOperation?.Dispose();
            rebindingOperation = null;
            actionToBind.action.Enable();
        }

        public void Update()
        {
            if (rebindingOperation != null && rebindingOperation.started)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    CancelRebind();
                }
            }
        }
    }
}